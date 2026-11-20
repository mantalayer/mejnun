using System;
using System.Collections.Generic;
using Mejnun.Core;
using Mejnun.Cosmetics;
using Mejnun.Game;
using Mejnun.Localization;
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
        private bool[] lastSeatConnections = Array.Empty<bool>();
        private bool[] hasSeatStatus = Array.Empty<bool>();
        private bool pausedPromptShown;
        private readonly Dictionary<string, int> seatsByAuthenticationId = new Dictionary<string, int>();

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

        private void Awake()
        {
            Loc.LanguageChanged += OnLanguageChanged;
        }

        private void Update()
        {
            if (!started)
            {
                StartWhenReady();
            }

            RefreshOnlineSessionCode();
            hostSession?.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            if (hostSession != null)
            {
                hostSession.StateChanged -= OnHostStateChanged;
                hostSession.SeatStatusChanged -= OnHostSeatStatusChanged;
            }

            if (transport != null)
            {
                transport.PlayerDisconnected -= OnPlayerDisconnected;
                transport.HostDisconnected -= OnHostDisconnected;
            }

            if (clientSession != null)
            {
                clientSession.StateChanged -= OnClientStateChanged;
                clientSession.StatusChanged -= OnClientStatusChanged;
            }

            if (launch != null)
            {
                launch.Session.SessionMigrated -= OnSessionMigrated;
                launch.Session.SessionHostChanged -= OnSessionHostChanged;
                launch.Service.MigrationHandler.DataRestored -= OnMigrationDataRestored;
            }

            if (matchFlow != null && matchFlow.Controller != null)
            {
                matchFlow.Controller.SetCommandRelay(null);
                matchFlow.SetOnlineAuthority(false, -1);
                matchFlow.OnlineSessionCode = null;
            }

            disconnectPrompt?.RemoveFromHierarchy();

            if (launch != null)
            {
                LeaveSession(launch.Service);
            }
        }

        public bool CanControlSeat(int seat)
        {
            return started && seat == localSeat && matchFlow != null &&
                   string.IsNullOrEmpty(matchFlow.OnlineWaitingMessage);
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
                matchFlow.PostOnlineNotice(Loc.Get("net.notice.match_paused"));
                ShowPausedPrompt();
                return true;
            }

            if (string.Equals(pendingDisconnectId, authenticationId, StringComparison.Ordinal))
            {
                disconnectPrompt?.RemoveFromHierarchy();
                disconnectPrompt = null;
                pendingDisconnectId = null;
                pausedPromptShown = false;
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
            matchFlow.PostOnlineNotice(Loc.Get("net.notice.match_resumed"));
            disconnectPrompt?.RemoveFromHierarchy();
            disconnectPrompt = null;
            pendingDisconnectId = null;
            pausedPromptShown = false;
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
                transport.HostDisconnected += OnHostDisconnected;
                launch.Session.SessionMigrated += OnSessionMigrated;
                launch.Session.SessionHostChanged += OnSessionHostChanged;
                launch.Service.MigrationHandler.DataRestored += OnMigrationDataRestored;
                localSeat = launch.IsHost ? 0 : -1;
                matchFlow.OnlineIsHost = launch.IsHost;
                matchFlow.OnlineSessionCode = !string.IsNullOrWhiteSpace(launch.Session.Code)
                    ? launch.Session.Code
                    : launch.SessionCode;
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

                InitializeSeatStatusTracking(matchFlow.SeatCount);
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

        private void RefreshOnlineSessionCode()
        {
            if (matchFlow == null || launch == null)
            {
                return;
            }

            var sessionCode = launch.Session == null ? null : launch.Session.Code;
            if (string.IsNullOrWhiteSpace(sessionCode))
            {
                sessionCode = launch.SessionCode;
            }

            if (!string.IsNullOrWhiteSpace(sessionCode) && matchFlow.OnlineSessionCode != sessionCode)
            {
                matchFlow.OnlineSessionCode = sessionCode;
            }
        }

        private void StartHost()
        {
            snapshotStore = new CloudSaveSnapshotStore();
            hostSession = new HostSession(matchFlow.Game, snapshotStore);
            launch.Service.MigrationHandler.AuthoritativeSession = hostSession;
            hostSession.StateChanged += OnHostStateChanged;
            hostSession.SeatStatusChanged += OnHostSeatStatusChanged;
            if (hostSession.Game.State.Phase == GamePhase.ChoosingDealer)
            {
                matchFlow.SetOnlineWaiting(GetWaitingMessage());
            }

            transport.ConfigureHost(NetworkManager.Singleton, launch.Session, hostSession,
                launch.AuthenticationId, launch.DisplayName);
        }

        private void StartClient()
        {
            UpdateClientReadiness();
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
            UpdateClientReadiness();
        }

        private void OnClientStatusChanged(NetMessage message)
        {
            if (message is SeatStatusMessage status)
            {
                HandleSeatStatus(status);
                UpdateClientReadiness();
            }
            else if (message is PausedMessage paused)
            {
                matchFlow.SetOnlinePaused(paused.Paused);
                matchFlow.PostOnlineNotice(paused.Paused
                    ? Loc.Get("net.notice.match_paused")
                    : Loc.Get("net.notice.match_resumed"));
            }
        }

        private void OnPlayerDisconnected(string authenticationId)
        {
            hostSession?.Disconnect(authenticationId, DisconnectChoice.Wait);
            UpdateSeatStatus(authenticationId);
            ShowDisconnectPrompt(authenticationId);
            DisconnectRequested?.Invoke(authenticationId);
        }

        private void OnHostDisconnected()
        {
            matchFlow?.PostOnlineNotice(Loc.Get("net.notice.host_disconnected"));
        }

        private void OnHostSeatStatusChanged(SeatStatusMessage status)
        {
            HandleSeatStatus(status);
            UpdateHostReadiness();
        }

        private void HandleSeatStatus(SeatStatusMessage status)
        {
            if (status == null || matchFlow == null || status.Seat < 0 || status.Seat >= matchFlow.SeatCount)
            {
                return;
            }

            EnsureSeatStatusTracking(matchFlow.SeatCount);
            var wasKnown = hasSeatStatus[status.Seat];
            var wasConnected = lastSeatConnections[status.Seat];
            if (!string.IsNullOrEmpty(status.AuthenticationId))
            {
                seatsByAuthenticationId[status.AuthenticationId] = status.Seat;
            }

            matchFlow.SetOnlineSeatStatus(status.Seat, status.DisplayName, status.IsBot);
            var displayName = matchFlow.GetPlayerName(status.Seat);

            var isLocalClientJoining = !wasKnown && status.Connected && status.Seat == localSeat &&
                                       launch != null && !launch.IsHost;
            if (isLocalClientJoining || (wasKnown && !wasConnected && status.Connected))
            {
                matchFlow.PostOnlineNotice(Loc.Format("net.notice.player_joined", displayName));
            }
            else if (wasKnown && wasConnected && !status.Connected)
            {
                matchFlow.PostOnlineNotice(Loc.Format("net.notice.player_disconnected", displayName));
            }

            lastSeatConnections[status.Seat] = status.Connected;
            hasSeatStatus[status.Seat] = true;
        }

        private void UpdateHostReadiness()
        {
            if (hostSession == null || hostSession.Game.State.Phase != GamePhase.ChoosingDealer)
            {
                return;
            }

            var readySeatCount = CountReadySeats();
            var enoughPlayersReady = launch.StartedEarly
                ? readySeatCount >= 2
                : hostSession.AreAllSeatsConnected;
            if (!enoughPlayersReady)
            {
                matchFlow.SetOnlineWaiting(GetWaitingMessage());
                return;
            }

            var wasWaiting = !string.IsNullOrEmpty(matchFlow.OnlineWaitingMessage);
            matchFlow.SetOnlineWaiting(string.Empty);
            if (wasWaiting)
            {
                CancelInvoke(nameof(PostAllPlayersJoinedNotice));
                Invoke(nameof(PostAllPlayersJoinedNotice), 1.2f);
                hostSession.SubmitCommands(launch.AuthenticationId,
                    new[] { GameCommand.ChooseRandomDealer() });
            }
        }

        private int CountReadySeats()
        {
            var readySeats = 0;
            for (var seat = 0; seat < hostSession.Game.Setup.SeatCount; seat++)
            {
                if (hostSession.GetSeat(seat).Connected)
                {
                    readySeats++;
                }
            }

            return readySeats;
        }

        private void UpdateClientReadiness()
        {
            if (launch == null || launch.IsHost || matchFlow == null || matchFlow.Game == null)
            {
                return;
            }

            if (matchFlow.Game.State.Phase != GamePhase.ChoosingDealer)
            {
                matchFlow.SetOnlineWaiting(string.Empty);
                return;
            }

            var readySeats = 0;
            for (var seat = 0; seat < matchFlow.SeatCount; seat++)
            {
                if (lastSeatConnections[seat])
                {
                    readySeats++;
                }
            }

            var requiredSeats = launch.StartedEarly ? 2 : matchFlow.SeatCount;
            if (readySeats >= requiredSeats)
            {
                matchFlow.SetOnlineWaiting(string.Empty);
                return;
            }

            matchFlow.SetOnlineWaiting(FormatWaitingMessage(readySeats, matchFlow.SeatCount,
                matchFlow.OnlineSessionCode));
        }

        private void PostAllPlayersJoinedNotice()
        {
            matchFlow?.PostOnlineNotice(Loc.Get("net.notice.all_players_joined"));
        }

        private string GetWaitingMessage()
        {
            var readySeats = 0;
            for (var seat = 0; seat < hostSession.Game.Setup.SeatCount; seat++)
            {
                var status = hostSession.GetSeat(seat);
                if (status.Connected || status.IsBot)
                {
                    readySeats++;
                }
            }

            var roomCode = matchFlow != null && launch != null && launch.IsHost
                ? matchFlow.OnlineSessionCode
                : null;
            return FormatWaitingMessage(readySeats, hostSession.Game.Setup.SeatCount, roomCode);
        }

        private static string FormatWaitingMessage(int readySeats, int seatCount, string roomCode)
        {
            return string.IsNullOrWhiteSpace(roomCode)
                ? Loc.Format("net.waiting.players", readySeats, seatCount)
                : Loc.Format("net.waiting.players_with_code", readySeats, seatCount, roomCode);
        }

        // The waiting message and the prompts are built from the current language, so rebuild them when it changes.
        private void OnLanguageChanged()
        {
            if (matchFlow != null && !string.IsNullOrEmpty(matchFlow.OnlineWaitingMessage))
            {
                if (hostSession != null)
                {
                    if (hostSession.Game.State.Phase == GamePhase.ChoosingDealer)
                    {
                        matchFlow.SetOnlineWaiting(GetWaitingMessage());
                    }
                }
                else
                {
                    UpdateClientReadiness();
                }
            }

            if (disconnectPrompt == null)
            {
                return;
            }

            if (pausedPromptShown)
            {
                ShowPausedPrompt();
            }
            else if (!string.IsNullOrEmpty(pendingDisconnectId))
            {
                ShowDisconnectPrompt(pendingDisconnectId);
            }
        }

        private void InitializeSeatStatusTracking(int seatCount)
        {
            lastSeatConnections = new bool[seatCount];
            hasSeatStatus = new bool[seatCount];
        }

        private void EnsureSeatStatusTracking(int seatCount)
        {
            if (lastSeatConnections.Length != seatCount || hasSeatStatus.Length != seatCount)
            {
                InitializeSeatStatusTracking(seatCount);
            }
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
                if (matchFlow != null)
                {
                    matchFlow.OnlineIsHost = false;
                }

                transport?.PrepareForHostMigration();
            }
        }

        private void OnSessionHostChanged(string authenticationId)
        {
            if (launch == null || matchFlow == null || string.IsNullOrWhiteSpace(authenticationId))
            {
                return;
            }

            matchFlow.OnlineIsHost = launch.Session != null && launch.Session.IsHost;

            var displayName = launch.AuthenticationId == authenticationId
                ? launch.DisplayName
                : seatsByAuthenticationId.TryGetValue(authenticationId, out var seat)
                    ? matchFlow.GetPlayerName(seat)
                    : null;
            matchFlow.PostOnlineNotice(string.IsNullOrWhiteSpace(displayName)
                ? Loc.Get("net.notice.unknown_player_is_host")
                : Loc.Format("net.notice.player_is_host", displayName));
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
                hostSession.SeatStatusChanged -= OnHostSeatStatusChanged;
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
            matchFlow.OnlineIsHost = true;
            matchFlow.SetOnlineAuthority(true, localSeat);
            matchFlow.Controller.SetCommandRelay(this);
            matchFlow.ApplyOnlineGame(hostSession.Game);
            hostSession.SeatStatusChanged += OnHostSeatStatusChanged;
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
            pausedPromptShown = false;
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
            card.Add(new Label(Loc.Get("net.disconnect.title")));
            card.Add(new Label(Loc.Get("net.disconnect.message")));
            card.Add(CreateDisconnectButton(Loc.Get("net.disconnect.wait"),
                () => ResolveDisconnect(authenticationId, DisconnectChoice.Wait)));
            card.Add(CreateDisconnectButton(Loc.Get("net.disconnect.bot_takeover"),
                () => ResolveDisconnect(authenticationId, DisconnectChoice.TakeOverWithBot)));
            card.Add(CreateDisconnectButton(Loc.Get("net.disconnect.save_and_pause"),
                () => ResolveDisconnect(authenticationId, DisconnectChoice.SaveAndPause)));
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
            pausedPromptShown = true;
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
            card.Add(new Label(Loc.Get("net.paused.title")));
            card.Add(new Label(Loc.Get("net.paused.message")));
            card.Add(CreateDisconnectButton(Loc.Get("net.paused.resume"), () => ResumePausedMatch()));
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