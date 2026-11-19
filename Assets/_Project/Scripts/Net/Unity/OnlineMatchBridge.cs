using System;
using System.Collections.Generic;
using Mejnun.Core;
using Mejnun.Cosmetics;
using Mejnun.Game;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Mejnun.Net.Unity
{
    public sealed class OnlineMatchBridge : MonoBehaviour, ICommandRelay
    {
        private MatchFlow matchFlow;
        private OnlineSessionLaunch launch;
        private HostSession hostSession;
        private ClientSession clientSession;
        private NgoNetTransport transport;
        private CloudSaveSnapshotStore snapshotStore;
        private int localSeat = -1;
        private bool started;
        private VisualElement disconnectPrompt;
        private string pendingDisconnectId;
        private SessionRecord migrationRecord;

        public bool IsRemoteAuthority => started;
        public bool ApplyCommandsLocally => clientSession != null;
        public event Action<string> DisconnectRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!OnlineLaunch.HasPending)
            {
                return;
            }

            var flow = FindAnyObjectByType<MatchFlow>();
            if (flow != null && flow.GetComponent<OnlineMatchBridge>() == null)
            {
                flow.gameObject.AddComponent<OnlineMatchBridge>();
            }
        }

        private void Update()
        {
            if (!started)
            {
                StartWhenReady();
            }

            hostSession?.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (hostSession != null)
            {
                hostSession.StateChanged -= OnHostStateChanged;
            }

            if (transport != null)
            {
                transport.PlayerDisconnected -= OnPlayerDisconnected;
            }

            if (clientSession != null)
            {
                clientSession.StateChanged -= OnClientStateChanged;
                clientSession.StatusChanged -= OnClientStatusChanged;
            }

            if (launch != null)
            {
                launch.Session.SessionMigrated -= OnSessionMigrated;
                launch.Service.MigrationHandler.DataRestored -= OnMigrationDataRestored;
            }

            if (matchFlow != null && matchFlow.Controller != null)
            {
                matchFlow.Controller.SetCommandRelay(null);
                matchFlow.SetOnlineAuthority(false, -1);
            }

            disconnectPrompt?.RemoveFromHierarchy();

            if (launch != null)
            {
                LeaveSession(launch.Service);
            }
        }

        public bool CanControlSeat(int seat)
        {
            return started && seat == localSeat;
        }

        public bool Submit(IReadOnlyList<GameCommand> commands)
        {
            if (!CanControlSeat(matchFlow == null ? -1 : matchFlow.CurrentSeat))
            {
                return false;
            }

            if (hostSession != null)
            {
                return hostSession.SubmitCommands(launch.AuthenticationId, commands);
            }

            if (clientSession != null)
            {
                clientSession.Submit(commands);
                return true;
            }

            return false;
        }

        public bool ResolveDisconnect(string authenticationId, DisconnectChoice choice)
        {
            if (hostSession == null || !hostSession.ResolveDisconnect(authenticationId, choice))
            {
                return false;
            }

            UpdateSeatStatus(authenticationId);
            if (choice == DisconnectChoice.SaveAndPause)
            {
                matchFlow.SetOnlinePaused(true);
                ShowPausedPrompt();
                return true;
            }

            if (string.Equals(pendingDisconnectId, authenticationId, StringComparison.Ordinal))
            {
                disconnectPrompt?.RemoveFromHierarchy();
                disconnectPrompt = null;
                pendingDisconnectId = null;
            }

            return true;
        }

        public bool ResumePausedMatch()
        {
            if (hostSession == null || !hostSession.IsPaused)
            {
                return false;
            }

            hostSession.Resume();
            matchFlow.SetOnlinePaused(false);
            disconnectPrompt?.RemoveFromHierarchy();
            disconnectPrompt = null;
            pendingDisconnectId = null;
            return true;
        }

        private void StartWhenReady()
        {
            if (launch == null)
            {
                matchFlow = FindAnyObjectByType<MatchFlow>();
                if (matchFlow == null || matchFlow.ActiveRequest == null ||
                    matchFlow.ActiveRequest.Mode != MatchMode.Online || !OnlineLaunch.TryTake(out launch))
                {
                    return;
                }

                transport = GetComponent<NgoNetTransport>();
                if (transport == null)
                {
                    transport = gameObject.AddComponent<NgoNetTransport>();
                }

                transport.PlayerDisconnected += OnPlayerDisconnected;
                launch.Session.SessionMigrated += OnSessionMigrated;
                launch.Service.MigrationHandler.DataRestored += OnMigrationDataRestored;
                localSeat = launch.IsHost ? 0 : -1;
                matchFlow.SetOnlineAuthority(true, localSeat);
                matchFlow.Controller.SetCommandRelay(this);
                if (!launch.IsHost)
                {
                    clientSession = new ClientSession();
                    clientSession.StateChanged += OnClientStateChanged;
                    clientSession.StatusChanged += OnClientStatusChanged;
                }

                if (!matchFlow.StartMatch(matchFlow.ActiveRequest))
                {
                    throw new InvalidOperationException("Unable to initialize the online match.");
                }
            }

            if (started || matchFlow.IsInitializingRound || matchFlow.Game == null)
            {
                return;
            }

            if (launch.IsHost)
            {
                StartHost();
            }
            else
            {
                StartClient();
            }

            started = true;
        }

        private void StartHost()
        {
            snapshotStore = new CloudSaveSnapshotStore();
            hostSession = new HostSession(matchFlow.Game, snapshotStore);
            launch.Service.MigrationHandler.AuthoritativeSession = hostSession;
            hostSession.StateChanged += OnHostStateChanged;
            transport.ConfigureHost(NetworkManager.Singleton, launch.Session, hostSession,
                launch.AuthenticationId, launch.DisplayName);
            if (hostSession.Game.State.Phase == GamePhase.ChoosingDealer)
            {
                hostSession.SubmitCommands(launch.AuthenticationId,
                    new[] { GameCommand.ChooseRandomDealer() });
            }
        }

        private void StartClient()
        {
            transport.ConfigureClient(NetworkManager.Singleton, launch.Session, clientSession,
                launch.AuthenticationId, launch.DisplayName);
        }

        private void OnHostStateChanged(MejnunGame game)
        {
            matchFlow.ApplyOnlineGame(game);
        }

        private void OnClientStateChanged(MejnunGame game)
        {
            localSeat = clientSession.LocalSeat;
            matchFlow.SetOnlineAuthority(true, localSeat);
            matchFlow.ApplyOnlineGame(game);
        }

        private void OnClientStatusChanged(NetMessage message)
        {
            if (message is SeatStatusMessage status)
            {
                matchFlow.SetOnlineSeatStatus(status.Seat, status.DisplayName, status.IsBot);
            }
            else if (message is PausedMessage paused)
            {
                matchFlow.SetOnlinePaused(paused.Paused);
            }
        }

        private void OnPlayerDisconnected(string authenticationId)
        {
            hostSession?.Disconnect(authenticationId, DisconnectChoice.Wait);
            UpdateSeatStatus(authenticationId);
            ShowDisconnectPrompt(authenticationId);
            DisconnectRequested?.Invoke(authenticationId);
        }

        private void OnMigrationDataRestored(SessionRecord record)
        {
            migrationRecord = record;
        }

        private void OnSessionMigrated()
        {
            if (launch.Session.IsHost)
            {
                RestoreMigratedHost();
            }
            else
            {
                transport?.PrepareForHostMigration();
            }
        }

        private void RestoreMigratedHost()
        {
            if (migrationRecord == null)
            {
                return;
            }

            if (hostSession != null)
            {
                hostSession.StateChanged -= OnHostStateChanged;
            }

            if (clientSession != null)
            {
                clientSession.StateChanged -= OnClientStateChanged;
                clientSession.StatusChanged -= OnClientStatusChanged;
                clientSession = null;
            }

            snapshotStore = snapshotStore ?? new CloudSaveSnapshotStore();
            hostSession = HostSession.FromRecord(migrationRecord, snapshotStore);
            localSeat = -1;
            for (var seat = 0; seat < hostSession.Game.Setup.SeatCount; seat++)
            {
                var status = hostSession.GetSeat(seat);
                if (string.Equals(status.AuthenticationId, launch.AuthenticationId, StringComparison.Ordinal))
                {
                    localSeat = seat;
                }
                else if (status.Connected && !string.IsNullOrEmpty(status.AuthenticationId))
                {
                    hostSession.Disconnect(status.AuthenticationId, DisconnectChoice.Wait);
                }
            }

            if (localSeat < 0)
            {
                return;
            }

            launch.Service.MigrationHandler.AuthoritativeSession = hostSession;
            hostSession.StateChanged += OnHostStateChanged;
            matchFlow.SetOnlineAuthority(true, localSeat);
            matchFlow.Controller.SetCommandRelay(this);
            matchFlow.ApplyOnlineGame(hostSession.Game);
            transport.ConfigureHost(NetworkManager.Singleton, launch.Session, hostSession,
                launch.AuthenticationId, launch.DisplayName);
        }

        private void UpdateSeatStatus(string authenticationId)
        {
            if (hostSession == null || matchFlow == null)
            {
                return;
            }

            for (var seat = 0; seat < hostSession.Game.Setup.SeatCount; seat++)
            {
                var status = hostSession.GetSeat(seat);
                if (string.Equals(status.AuthenticationId, authenticationId, StringComparison.Ordinal))
                {
                    matchFlow.SetOnlineSeatStatus(seat, status.DisplayName, status.IsBot);
                    return;
                }
            }
        }

        private void ShowDisconnectPrompt(string authenticationId)
        {
            var document = FindAnyObjectByType<UIDocument>();
            if (document == null || string.IsNullOrEmpty(authenticationId))
            {
                return;
            }

            disconnectPrompt?.RemoveFromHierarchy();
            pendingDisconnectId = authenticationId;
            disconnectPrompt = new VisualElement();
            disconnectPrompt.style.position = Position.Absolute;
            disconnectPrompt.style.left = 0;
            disconnectPrompt.style.right = 0;
            disconnectPrompt.style.top = 0;
            disconnectPrompt.style.bottom = 0;
            disconnectPrompt.style.alignItems = Align.Center;
            disconnectPrompt.style.justifyContent = Justify.Center;
            disconnectPrompt.style.backgroundColor = new Color(0, 0, 0, 0.72f);

            var card = new VisualElement();
            card.style.width = 360;
            card.style.paddingLeft = 24;
            card.style.paddingRight = 24;
            card.style.paddingTop = 22;
            card.style.paddingBottom = 22;
            card.style.backgroundColor = new Color(0.10f, 0.12f, 0.16f, 1f);
            card.style.borderTopLeftRadius = 12;
            card.style.borderTopRightRadius = 12;
            card.style.borderBottomLeftRadius = 12;
            card.style.borderBottomRightRadius = 12;
            card.Add(new Label("A player disconnected"));
            card.Add(new Label("Wait for them, let a Medium bot take over, or save and pause."));
            card.Add(CreateDisconnectButton("Wait / auto-pass", () => ResolveDisconnect(authenticationId,
                DisconnectChoice.Wait)));
            card.Add(CreateDisconnectButton("Let AI take over", () => ResolveDisconnect(authenticationId,
                DisconnectChoice.TakeOverWithBot)));
            card.Add(CreateDisconnectButton("Save and pause", () => ResolveDisconnect(authenticationId,
                DisconnectChoice.SaveAndPause)));
            disconnectPrompt.Add(card);
            document.rootVisualElement.Add(disconnectPrompt);
        }

        private void ShowPausedPrompt()
        {
            var document = FindAnyObjectByType<UIDocument>();
            if (document == null)
            {
                return;
            }

            disconnectPrompt?.RemoveFromHierarchy();
            disconnectPrompt = new VisualElement();
            disconnectPrompt.style.position = Position.Absolute;
            disconnectPrompt.style.left = 0;
            disconnectPrompt.style.right = 0;
            disconnectPrompt.style.top = 0;
            disconnectPrompt.style.bottom = 0;
            disconnectPrompt.style.alignItems = Align.Center;
            disconnectPrompt.style.justifyContent = Justify.Center;
            disconnectPrompt.style.backgroundColor = new Color(0, 0, 0, 0.72f);
            var card = new VisualElement();
            card.style.width = 360;
            card.style.paddingLeft = 24;
            card.style.paddingRight = 24;
            card.style.paddingTop = 22;
            card.style.paddingBottom = 22;
            card.style.backgroundColor = new Color(0.10f, 0.12f, 0.16f, 1f);
            card.Add(new Label("Match saved and paused"));
            card.Add(new Label("The host can resume this session when everyone is ready."));
            card.Add(CreateDisconnectButton("Resume match", () => ResumePausedMatch()));
            disconnectPrompt.Add(card);
            document.rootVisualElement.Add(disconnectPrompt);
        }

        private static Button CreateDisconnectButton(string label, Action clicked)
        {
            var button = new Button(clicked) { text = label };
            button.style.marginTop = 10;
            button.style.height = 42;
            return button;
        }

        private static async void LeaveSession(UgsOnlineService service)
        {
            try
            {
                await service.LeaveAsync();
            }
            catch (Exception)
            {
                // Session teardown is best-effort while leaving the match scene.
            }
        }
    }
}