using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mejnun.AI;
using Mejnun.Core;
using Mejnun.Cosmetics;
using Mejnun.Game;
using Mejnun.Localization;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        private global::Unity.Services.Multiplayer.ISession subscribedSession;
        private int localSeat = -1;
        private bool started;
        private SessionRecord migrationRecord;
        private bool[] lastSeatConnections = Array.Empty<bool>();
        private bool[] lastSeatReadiness = Array.Empty<bool>();
        private bool[] hasSeatStatus = Array.Empty<bool>();
        private bool hostRecoveryInProgress;
        private bool hostRecoveryFailed;
        private bool restoringHost;
        private bool hostNetworkRecoveryInProgress;
        private bool migrationUploadInProgress;
        private bool migrationUploadQueued;
        private bool sessionRefreshInProgress;
        private bool sessionMembershipLost;
        private bool rejoinInProgress;
        private bool matchWentLive;
        private float hostPromotionElapsed;
        private Task migrationUploadTask;
        private double nextMigrationUploadCheckAt;
        private readonly MigrationUploadThrottle migrationUploadThrottle = new MigrationUploadThrottle();
        private string lastKnownHostAuthenticationId;
        private string recoveringHostAuthenticationId;
        private readonly Dictionary<string, int> seatsByAuthenticationId = new Dictionary<string, int>();

        public bool IsRemoteAuthority => started;
        public bool ApplyCommandsLocally => clientSession != null;
        public event Action<string> DisconnectRequested;
        public event Action<OnlineRosterViewModel> RosterChanged;
        public OnlineRosterViewModel Roster { get; private set; }
        public bool HostRecoveryInProgress => hostRecoveryInProgress;
        public bool HostRecoveryFailed => hostRecoveryFailed;
        public bool IsRestoringHost => restoringHost || hostNetworkRecoveryInProgress;
        public bool LocalPendingHandBack => GetLocalSeatState()?.PendingHandBack ?? false;
        public bool HasOnlineMatch => started && launch != null;
        public bool IsCurrentSessionHost => launch != null && launch.Session != null && launch.Session.IsHost;
        public bool IsNetworkConnected => NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;
        public bool IsTransportHost => transport != null && transport.IsHost;
        public bool IsHostNetworkOperational => IsCurrentSessionHost && IsTransportHost &&
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening &&
            launch.Session.Network.State == global::Unity.Services.Multiplayer.NetworkState.Started;
        public bool MembershipLost => sessionMembershipLost;
        public bool HasOtherSessionMembers => launch?.Session != null && launch.Session.PlayerCount > 1;
        public bool IsAwaitingWelcome => clientSession != null && clientSession.LocalSeat < 0;
        public string SessionId => launch == null ? UgsOnlineService.LastSessionId :
            string.IsNullOrWhiteSpace(launch.Service.SessionId) ? UgsOnlineService.LastSessionId :
            launch.Service.SessionId;
        public string SessionCode => matchFlow != null && !string.IsNullOrWhiteSpace(matchFlow.OnlineSessionCode)
            ? matchFlow.OnlineSessionCode
            : launch == null ? UgsOnlineService.LastSessionCode : launch.SessionCode;
        public UgsOnlineService OnlineService => launch?.Service;
        public string AuthenticationId => launch?.AuthenticationId ?? string.Empty;
        public string DisplayName => launch?.DisplayName ?? string.Empty;

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
            if (migrationUploadQueued && !migrationUploadInProgress &&
                Time.realtimeSinceStartupAsDouble >= nextMigrationUploadCheckAt)
            {
                migrationUploadQueued = false;
                QueueMigrationUpload();
            }

            if (started && launch?.Session != null && launch.Session.IsHost && hostSession == null &&
                !IsRestoringHost && !hostRecoveryFailed)
            {
                hostPromotionElapsed += Time.unscaledDeltaTime;
                if (hostPromotionElapsed >= 12)
                {
                    hostPromotionElapsed = 0;
                    RecoverPromotedHostAsync();
                }
            }
            else
            {
                hostPromotionElapsed = 0;
            }
        }

        private void OnDestroy()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            UnsubscribeHostSession();

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

            if (subscribedSession != null)
            {
                subscribedSession.SessionMigrated -= OnSessionMigrated;
                subscribedSession.SessionHostChanged -= OnSessionHostChanged;
                subscribedSession.RemovedFromSession -= OnRemovedFromSession;
                subscribedSession.Network.MigrationFailed -= OnMigrationFailed;
                subscribedSession = null;
            }

            if (launch != null)
            {
                launch.Service.MigrationHandler.DataRestored -= OnMigrationDataRestored;
            }

            if (matchFlow != null)
            {
                matchFlow.OnlineRematchRequested -= OnOnlineRematchRequested;
                if (matchFlow.Controller != null)
                {
                    matchFlow.Controller.SetCommandRelay(null);
                    matchFlow.SetOnlineAuthority(false, -1);
                    matchFlow.OnlineSessionCode = null;
                }
            }

            if (launch != null)
            {
                LeaveSession(launch.Service);
            }
        }

        public bool CanControlSeat(int seat)
        {
            if (!started || seat != localSeat || matchFlow == null ||
                !string.IsNullOrEmpty(matchFlow.OnlineWaitingMessage) ||
                hostSession != null && hostSession.IsPaused ||
                clientSession != null && clientSession.IsPaused)
            {
                return false;
            }

            return !(GetLocalSeatState()?.PendingHandBack ?? false);
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

            return true;
        }

        public bool KeepWaiting(int seat)
        {
            if (hostSession == null || seat < 0 || seat >= hostSession.Roster.SeatCount)
            {
                return false;
            }

            var status = hostSession.Roster.Get(seat);
            return status.Presence == SeatPresence.Disconnected &&
                   hostSession.ResolveDisconnect(status.AuthenticationId, DisconnectChoice.Wait);
        }

        public bool LetAiPlay(int seat)
        {
            if (hostSession == null || seat < 0 || seat >= hostSession.Roster.SeatCount ||
                hostSession.Roster.Get(seat).Presence != SeatPresence.Disconnected)
            {
                return false;
            }

            hostSession.SetBotControl(seat, BotDifficulty.Medium);
            return true;
        }

        public bool SaveAndPauseMatch()
        {
            if (hostSession == null)
            {
                return false;
            }

            hostSession.SetPauseReason(PauseReason.SavedByHost);
            return true;
        }

        public bool ResumePausedMatch()
        {
            if (hostSession == null || !hostSession.IsPaused)
            {
                return false;
            }

            hostSession.Resume();
            return true;
        }

        public void NotifyApplicationPaused()
        {
            transport?.SendAway();
        }

        public void RejoinActiveClientSeat()
        {
            transport?.RejoinClientSeat();
        }

        public void ResumeHostAway()
        {
            if (hostSession != null && hostSession.PauseReason == PauseReason.HostAway)
            {
                hostSession.Resume();
            }
        }

        public void PrepareForReconnect(bool shutdownHostNetwork = false)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening &&
                (shutdownHostNetwork || !IsCurrentSessionHost))
            {
                NetworkManager.Singleton.Shutdown();
            }

            transport?.PrepareForHostMigration();
        }

        public async void RefreshSessionAsync()
        {
            if (sessionRefreshInProgress || launch?.Session == null)
            {
                return;
            }

            sessionRefreshInProgress = true;
            try
            {
                await launch.Session.RefreshAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Refreshing the online session during host recovery failed: " + exception.Message);
            }
            finally
            {
                sessionRefreshInProgress = false;
            }
        }

        public void BeginRejoin()
        {
            rejoinInProgress = true;
        }

        public void EndRejoin()
        {
            rejoinInProgress = false;
        }

        public bool ConfigureAfterReconnect()
        {
            if (launch == null || launch.Session == null || transport == null || matchFlow == null)
            {
                return false;
            }

            SubscribeSessionEvents();
            transport.PrepareForHostMigration();
            if (launch.Session.IsHost)
            {
                if (hostSession == null)
                {
                    RecoverPromotedHostAsync();
                    return true;
                }

                EnsureHostNetworkAfterReconnectAsync();
                return true;
            }

            UnsubscribeHostSession();
            hostSession = null;
            if (clientSession != null)
            {
                clientSession.StateChanged -= OnClientStateChanged;
                clientSession.StatusChanged -= OnClientStatusChanged;
            }

            clientSession = new ClientSession();
            clientSession.StateChanged += OnClientStateChanged;
            clientSession.StatusChanged += OnClientStatusChanged;
            localSeat = -1;
            matchFlow.OnlineIsHost = false;
            matchFlow.SetOnlineAuthority(true, localSeat);
            matchFlow.Controller.SetCommandRelay(this);
            matchFlow.SetOnlinePaused(true);
            transport.ConfigureClient(NetworkManager.Singleton, launch.Session, clientSession,
                launch.AuthenticationId, launch.DisplayName);
            hostRecoveryFailed = false;
            sessionMembershipLost = false;
            UpdateClientReadiness();
            PublishRosterChanged();
            return true;
        }

        public void FailHostRecovery()
        {
            SetHostRecoveryFailure("The host recovery timed out.");
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

                matchFlow.OnlineRematchRequested -= OnOnlineRematchRequested;
                matchFlow.OnlineRematchRequested += OnOnlineRematchRequested;

                transport = GetComponent<NgoNetTransport>();
                if (transport == null)
                {
                    transport = gameObject.AddComponent<NgoNetTransport>();
                }

                transport.PlayerDisconnected += OnPlayerDisconnected;
                transport.HostDisconnected += OnHostDisconnected;
                SubscribeSessionEvents();
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
                PublishRosterChanged();
            }

            if (started || matchFlow.IsInitializingRound || matchFlow.Game == null)
            {
                return;
            }

            if (launch.JoinedStartedMatch && launch.IsHost)
            {
                started = true;
                hostRecoveryInProgress = true;
                matchFlow.SetOnlinePaused(true);
                UgsOnlineService.MarkUnfinishedMatch();
                EnsureReconnectController();
                RecoverPromotedHostAsync();
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
            UgsOnlineService.MarkUnfinishedMatch();
            EnsureReconnectController();
        }

        private void EnsureReconnectController()
        {
            if (GetComponent<OnlineReconnectController>() == null)
            {
                gameObject.AddComponent<OnlineReconnectController>();
            }
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
                PublishRosterChanged();
            }
        }

        private void StartHost()
        {
            snapshotStore = new CloudSaveSnapshotStore();
            hostSession = new HostSession(matchFlow.Game, snapshotStore);
            launch.Service.MigrationHandler.AuthoritativeSession = hostSession;
            hostSession.StateChanged += OnHostStateChanged;
            hostSession.SeatStatusChanged += OnHostSeatStatusChanged;
            hostSession.RematchStatusChanged += OnHostRematchStatusChanged;
            hostSession.PauseReasonChanged += OnHostPauseReasonChanged;
            hostSession.SeatExpired += OnSeatExpired;
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

        private void SubscribeSessionEvents()
        {
            if (launch == null || launch.Session == null || subscribedSession == launch.Session)
            {
                return;
            }

            if (subscribedSession != null)
            {
                subscribedSession.SessionMigrated -= OnSessionMigrated;
                subscribedSession.SessionHostChanged -= OnSessionHostChanged;
                subscribedSession.RemovedFromSession -= OnRemovedFromSession;
                subscribedSession.Network.MigrationFailed -= OnMigrationFailed;
            }

            subscribedSession = launch.Session;
            subscribedSession.SessionMigrated += OnSessionMigrated;
            subscribedSession.SessionHostChanged += OnSessionHostChanged;
            subscribedSession.RemovedFromSession += OnRemovedFromSession;
            subscribedSession.Network.MigrationFailed += OnMigrationFailed;
            if (rejoinInProgress || string.IsNullOrWhiteSpace(lastKnownHostAuthenticationId))
            {
                lastKnownHostAuthenticationId = subscribedSession.Host;
            }
        }

        private void UnsubscribeHostSession()
        {
            if (hostSession == null)
            {
                return;
            }

            hostSession.StateChanged -= OnHostStateChanged;
            hostSession.SeatStatusChanged -= OnHostSeatStatusChanged;
            hostSession.RematchStatusChanged -= OnHostRematchStatusChanged;
            hostSession.PauseReasonChanged -= OnHostPauseReasonChanged;
            hostSession.SeatExpired -= OnSeatExpired;
        }

        private void OnHostStateChanged(MejnunGame game)
        {
            if (game.State.Phase == GamePhase.Finished)
            {
                UgsOnlineService.ClearUnfinishedMatch();
            }
            else if (matchFlow.Game != null && matchFlow.Game.State.Phase == GamePhase.Finished)
            {
                UgsOnlineService.MarkUnfinishedMatch();
            }

            matchFlow.ApplyOnlineGame(game);
            UpdateHostReadiness();
            PublishRosterChanged();
            QueueMigrationUpload();
        }

        private void OnClientStateChanged(MejnunGame game)
        {
            if (game.State.Phase == GamePhase.Finished)
            {
                UgsOnlineService.ClearUnfinishedMatch();
            }
            else if (matchFlow.Game != null && matchFlow.Game.State.Phase == GamePhase.Finished)
            {
                UgsOnlineService.MarkUnfinishedMatch();
            }

            localSeat = clientSession.LocalSeat;
            matchFlow.SetOnlineAuthority(true, localSeat);
            matchFlow.ApplyOnlineGame(game);
            UpdateClientReadiness();
            PublishRosterChanged();
        }

        private void OnClientStatusChanged(NetMessage message)
        {
            if (message is SeatStatusMessage status)
            {
                HandleSeatStatus(status);
                UpdateClientReadiness();
                PublishRosterChanged();
            }
            else if (message is PausedMessage paused)
            {
                hostRecoveryInProgress = false;
                hostRecoveryFailed = false;
                sessionMembershipLost = false;
                recoveringHostAuthenticationId = null;
                matchFlow.SetOnlinePaused(paused.Paused);
                PostPauseNotice(paused.Paused);
                PublishRosterChanged();
            }
            else if (message is RematchStatusMessage)
            {
                UpdateClientReadiness();
            }
            else if (message is RejectedMessage rejected)
            {
                switch (rejected.Code)
                {
                    case JoinRejectReason.NoFreeSeat:
                        matchFlow.PostOnlineNotice(Loc.Get("online.status.no_free_seats"));
                        break;
                    case JoinRejectReason.NotSessionMember:
                        matchFlow.PostOnlineNotice(Loc.Get("online.status.match_not_found"));
                        break;
                    default:
                        matchFlow.PostOnlineNotice(Loc.Get("online.status.join_failed"));
                        break;
                }
            }
        }

        private void OnPlayerDisconnected(string authenticationId)
        {
            hostSession?.Disconnect(authenticationId, DisconnectChoice.Wait);
            DisconnectRequested?.Invoke(authenticationId);
        }

        private void OnHostDisconnected()
        {
            if (rejoinInProgress)
            {
                return;
            }

            hostRecoveryInProgress = true;
            hostRecoveryFailed = false;
            if (string.IsNullOrWhiteSpace(recoveringHostAuthenticationId))
            {
                recoveringHostAuthenticationId = lastKnownHostAuthenticationId;
            }

            matchFlow?.SetOnlinePaused(true);
            matchFlow?.PostOnlineNotice(Loc.Get("net.notice.host_disconnected"));
            PublishRosterChanged();
        }

        private void OnRemovedFromSession()
        {
            if (rejoinInProgress)
            {
                return;
            }

            sessionMembershipLost = true;
            PublishRosterChanged();
        }

        private void OnHostSeatStatusChanged(SeatStatusMessage status)
        {
            HandleSeatStatus(status);
            UpdateHostReadiness();
            PublishRosterChanged();
            QueueMigrationUpload();
        }

        private void OnHostRematchStatusChanged(RematchStatusMessage status)
        {
            UpdateHostReadiness();
        }

        private void OnOnlineRematchRequested()
        {
            if (hostSession != null)
            {
                hostSession.RequestRematch(launch.AuthenticationId);
            }
            else
            {
                clientSession?.SubmitRematch();
            }
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
            var isConnected = status.Connected || status.PendingHandBack;
            if (!string.IsNullOrEmpty(status.AuthenticationId))
            {
                seatsByAuthenticationId[status.AuthenticationId] = status.Seat;
            }

            var statusDisplayName = string.IsNullOrWhiteSpace(status.DisplayName)
                ? matchFlow.GetPlayerName(status.Seat)
                : status.DisplayName;
            matchFlow.SetOnlineSeatStatus(status.Seat, statusDisplayName, status.IsBot);
            var displayName = matchFlow.GetPlayerName(status.Seat);

            var isLocalClientJoining = !wasKnown && isConnected && status.Seat == localSeat &&
                                       launch != null && !launch.IsHost;
            if (isLocalClientJoining || (wasKnown && !wasConnected && isConnected))
            {
                matchFlow.PostOnlineNotice(Loc.Format("net.notice.player_joined", displayName));
            }
            else if (wasKnown && wasConnected && !isConnected)
            {
                matchFlow.PostOnlineNotice(Loc.Format("net.notice.player_disconnected", displayName));
            }

            lastSeatConnections[status.Seat] = isConnected;
            lastSeatReadiness[status.Seat] = status.Connected || status.IsBot;
            hasSeatStatus[status.Seat] = true;
        }

        private void UpdateHostReadiness()
        {
            if (hostSession == null)
            {
                return;
            }

            if (hostSession.IsRematchPending)
            {
                matchFlow.SetOnlineWaiting(FormatRematchWaitingMessage(hostSession.RematchReadyCount,
                    hostSession.Game.Setup.SeatCount));
                return;
            }

            if (hostSession.Game.State.Phase != GamePhase.ChoosingDealer)
            {
                matchFlow.SetOnlineWaiting(string.Empty);
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
                var status = hostSession.GetSeat(seat);
                if (status.Connected || status.IsBot)
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

            if (clientSession.LocalSeat < 0)
            {
                matchFlow.SetOnlineWaiting(Loc.Get("net.waiting.rejoining"));
                return;
            }

            if (clientSession.IsRematchPending)
            {
                var status = clientSession.RematchStatus;
                matchFlow.SetOnlineWaiting(FormatRematchWaitingMessage(status.ReadySeats, status.SeatCount));
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
                if (lastSeatReadiness[seat])
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

        private SeatState GetLocalSeatState()
        {
            if (localSeat < 0)
            {
                return null;
            }

            if (hostSession != null && localSeat < hostSession.Roster.SeatCount)
            {
                return hostSession.Roster.Get(localSeat);
            }

            if (clientSession != null && localSeat < clientSession.SeatStatuses.Count)
            {
                var status = clientSession.SeatStatuses[localSeat];
                if (status != null)
                {
                    return new SeatState(status.Seat, status.AuthenticationId, status.DisplayName, status.Presence,
                        BotDifficulty.Medium, status.PendingHandBack, status.GraceRemaining);
                }
            }

            return null;
        }

        private void PublishRosterChanged()
        {
            if (matchFlow == null)
            {
                return;
            }

            var seatCount = matchFlow.SeatCount;
            var states = new SeatState[seatCount];
            for (var seat = 0; seat < seatCount; seat++)
            {
                if (hostSession != null && seat < hostSession.Roster.SeatCount)
                {
                    states[seat] = WithDisplayNameFallback(hostSession.Roster.Get(seat));
                }
                else if (clientSession != null && seat < clientSession.SeatStatuses.Count &&
                         clientSession.SeatStatuses[seat] != null)
                {
                    var status = clientSession.SeatStatuses[seat];
                    states[seat] = WithDisplayNameFallback(new SeatState(status.Seat, status.AuthenticationId,
                        status.DisplayName, status.Presence, BotDifficulty.Medium, status.PendingHandBack,
                        status.GraceRemaining));
                }
                else
                {
                    states[seat] = new SeatState(seat, string.Empty, string.Empty, SeatPresence.Empty,
                        BotDifficulty.Medium, false, 0);
                }
            }

            if ((hostRecoveryInProgress || hostRecoveryFailed) && launch != null && !launch.IsHost &&
                !string.IsNullOrWhiteSpace(recoveringHostAuthenticationId) &&
                seatsByAuthenticationId.TryGetValue(recoveringHostAuthenticationId, out var recoveringHostSeat) &&
                recoveringHostSeat >= 0 && recoveringHostSeat < states.Length &&
                states[recoveringHostSeat].Presence == SeatPresence.Connected)
            {
                var previousHost = states[recoveringHostSeat];
                states[recoveringHostSeat] = new SeatState(previousHost.Seat, previousHost.AuthenticationId,
                    previousHost.DisplayName, SeatPresence.Disconnected, previousHost.Difficulty,
                    previousHost.PendingHandBack, previousHost.GraceRemaining);
            }

            var hostSeat = launch != null && launch.Session != null &&
                           seatsByAuthenticationId.TryGetValue(launch.Session.Host, out var seatIndex)
                ? seatIndex
                : launch != null && launch.IsHost ? localSeat : -1;
            var pauseReason = hostSession != null
                ? hostSession.PauseReason
                : clientSession != null ? clientSession.PauseReason : PauseReason.None;
            if (pauseReason == PauseReason.None && hostRecoveryInProgress)
            {
                pauseReason = PauseReason.HostAway;
            }

            var roomCode = !string.IsNullOrWhiteSpace(matchFlow.OnlineSessionCode)
                ? matchFlow.OnlineSessionCode
                : launch == null ? string.Empty : launch.SessionCode;
            Roster = new OnlineRosterViewModel(roomCode, states, hostSeat, localSeat,
                launch != null && launch.IsHost, pauseReason, hostRecoveryInProgress, hostRecoveryFailed);
            RosterChanged?.Invoke(Roster);
        }

        private SeatState WithDisplayNameFallback(SeatState state)
        {
            if (!string.IsNullOrWhiteSpace(state.DisplayName) || matchFlow == null)
            {
                return state;
            }

            return new SeatState(state.Seat, state.AuthenticationId, matchFlow.GetPlayerName(state.Seat),
                state.Presence, state.Difficulty, state.PendingHandBack, state.GraceRemaining);
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

        private static string FormatRematchWaitingMessage(int readySeats, int seatCount)
        {
            return Loc.Format("net.waiting.rematch", readySeats, seatCount);
        }

        // The waiting message is built from the current language, so rebuild it when the language changes.
        private void OnLanguageChanged()
        {
            if (matchFlow != null && !string.IsNullOrEmpty(matchFlow.OnlineWaitingMessage))
            {
                if (hostSession != null)
                {
                    if (hostSession.IsRematchPending)
                    {
                        matchFlow.SetOnlineWaiting(FormatRematchWaitingMessage(hostSession.RematchReadyCount,
                            hostSession.Game.Setup.SeatCount));
                    }
                    else if (hostSession.Game.State.Phase == GamePhase.ChoosingDealer)
                    {
                        matchFlow.SetOnlineWaiting(GetWaitingMessage());
                    }
                }
                else
                {
                    UpdateClientReadiness();
                }
            }

        }

        private void InitializeSeatStatusTracking(int seatCount)
        {
            lastSeatConnections = new bool[seatCount];
            lastSeatReadiness = new bool[seatCount];
            hasSeatStatus = new bool[seatCount];
        }

        private void EnsureSeatStatusTracking(int seatCount)
        {
            if (lastSeatConnections.Length != seatCount || lastSeatReadiness.Length != seatCount ||
                hasSeatStatus.Length != seatCount)
            {
                InitializeSeatStatusTracking(seatCount);
            }
        }

        private void OnMigrationDataRestored(SessionRecord record)
        {
            if (record != null)
            {
                migrationRecord = record;
            }
        }

        private void OnSessionMigrated()
        {
            hostRecoveryInProgress = true;
            hostRecoveryFailed = false;
            matchFlow?.SetOnlinePaused(true);
            transport?.PrepareForHostMigration();
            if (launch != null && launch.Session != null && launch.Session.IsHost)
            {
                RecoverPromotedHostAsync();
            }
            else if (hostSession != null || transport != null && transport.IsHost)
            {
                hostSession?.SetPauseReason(PauseReason.HostAway);
                ConfigureAfterReconnect();
            }
            else
            {
                if (matchFlow != null)
                {
                    matchFlow.OnlineIsHost = false;
                }

                PublishRosterChanged();
            }
        }

        private void OnMigrationFailed(global::Unity.Services.Multiplayer.SessionError error)
        {
            Debug.LogWarning("Online session network migration failed: " + error);
            if (launch?.Session != null && launch.Session.IsHost)
            {
                hostRecoveryInProgress = true;
                hostRecoveryFailed = false;
                matchFlow?.SetOnlinePaused(true);
                RecoverPromotedHostAsync();
            }
        }

        private void OnSessionHostChanged(string authenticationId)
        {
            if (rejoinInProgress || launch == null || matchFlow == null ||
                string.IsNullOrWhiteSpace(authenticationId))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(lastKnownHostAuthenticationId) &&
                !string.Equals(lastKnownHostAuthenticationId, authenticationId, StringComparison.Ordinal))
            {
                recoveringHostAuthenticationId = lastKnownHostAuthenticationId;
            }

            lastKnownHostAuthenticationId = authenticationId;
            matchFlow.OnlineIsHost = launch.Session != null && launch.Session.IsHost;
            if (matchFlow.OnlineIsHost)
            {
                hostRecoveryInProgress = true;
                hostRecoveryFailed = false;
                hostPromotionElapsed = 0;
                matchFlow.SetOnlinePaused(true);
            }
            else if (!string.IsNullOrWhiteSpace(recoveringHostAuthenticationId))
            {
                hostRecoveryInProgress = true;
                hostRecoveryFailed = false;
                matchFlow.SetOnlinePaused(true);
            }

            var displayName = launch.AuthenticationId == authenticationId
                ? launch.DisplayName
                : seatsByAuthenticationId.TryGetValue(authenticationId, out var seat)
                    ? matchFlow.GetPlayerName(seat)
                    : null;
            matchFlow.PostOnlineNotice(string.IsNullOrWhiteSpace(displayName)
                ? Loc.Get("net.notice.unknown_player_is_host")
                : Loc.Format("net.notice.player_is_host", displayName));
            PublishRosterChanged();
        }

        private async void RecoverPromotedHostAsync()
        {
            if (restoringHost || launch == null || matchFlow == null || transport == null || launch.Session == null ||
                !launch.Session.IsHost)
            {
                return;
            }

            restoringHost = true;
            hostRecoveryInProgress = true;
            hostRecoveryFailed = false;
            matchFlow.SetOnlinePaused(true);
            try
            {
                SessionRecord record = null;
                for (var attempt = 0; record == null && attempt < 5; attempt++)
                {
                    try
                    {
                        record = await launch.Service.DownloadMigrationRecordAsync();
                        if (record != null)
                        {
                            migrationRecord = record;
                            break;
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("Downloading online migration data failed: " + exception.Message);
                    }

                    if (record == null && attempt < 4)
                    {
                        await Task.Delay(1000);
                    }
                }

                record = record ?? migrationRecord ?? launch.Service.MigrationHandler.RestoredRecord;

                if (record == null)
                {
                    SetHostRecoveryFailure("No migration record was available for the promoted host.");
                    return;
                }

                await launch.Service.EnsureHostRelayNetworkAsync();
                RestoreMigratedHost(record);
            }
            catch (Exception exception)
            {
                SetHostRecoveryFailure("The promoted host could not restore the match: " + exception.Message);
            }
            finally
            {
                restoringHost = false;
            }
        }

        private async void EnsureHostNetworkAfterReconnectAsync()
        {
            if (hostNetworkRecoveryInProgress || launch?.Session == null || !launch.Session.IsHost ||
                hostSession == null)
            {
                return;
            }

            hostNetworkRecoveryInProgress = true;
            hostRecoveryInProgress = true;
            matchFlow.SetOnlinePaused(true);
            try
            {
                await launch.Service.EnsureHostRelayNetworkAsync();
                localSeat = hostSession.PrepareForPromotion(launch.AuthenticationId);
                if (localSeat < 0)
                {
                    throw new InvalidOperationException("The session host does not have a seat in the match.");
                }

                matchFlow.OnlineIsHost = true;
                matchFlow.SetOnlineAuthority(true, localSeat);
                matchFlow.Controller.SetCommandRelay(this);
                transport.ConfigureHost(NetworkManager.Singleton, launch.Session, hostSession,
                    launch.AuthenticationId, launch.DisplayName);
                ResumeHostAway();
                hostRecoveryInProgress = false;
                hostRecoveryFailed = false;
                sessionMembershipLost = false;
                matchFlow.SetOnlinePaused(hostSession.IsPaused);
                PublishRosterChanged();
                QueueMigrationUpload();
            }
            catch (Exception exception)
            {
                SetHostRecoveryFailure("The session host could not restore its relay network: " + exception.Message);
            }
            finally
            {
                hostNetworkRecoveryInProgress = false;
            }
        }

        private void RestoreMigratedHost(SessionRecord record)
        {
            if (hostSession != null)
            {
                UnsubscribeHostSession();
            }

            if (clientSession != null)
            {
                clientSession.StateChanged -= OnClientStateChanged;
                clientSession.StatusChanged -= OnClientStatusChanged;
                clientSession = null;
            }

            snapshotStore = snapshotStore ?? new CloudSaveSnapshotStore();
            hostSession = HostSession.FromRecord(record, snapshotStore);
            hostSession.StateChanged += OnHostStateChanged;
            hostSession.SeatStatusChanged += OnHostSeatStatusChanged;
            hostSession.RematchStatusChanged += OnHostRematchStatusChanged;
            hostSession.PauseReasonChanged += OnHostPauseReasonChanged;
            hostSession.SeatExpired += OnSeatExpired;
            localSeat = hostSession.PrepareForPromotion(launch.AuthenticationId);
            if (localSeat < 0)
            {
                hostSession = null;
                SetHostRecoveryFailure("The promoted host does not have a seat in the migration record.");
                return;
            }

            launch.Service.MigrationHandler.AuthoritativeSession = hostSession;
            matchFlow.OnlineIsHost = true;
            matchFlow.SetOnlineAuthority(true, localSeat);
            matchFlow.Controller.SetCommandRelay(this);
            matchFlow.ApplyOnlineGame(hostSession.Game);
            transport.ConfigureHost(NetworkManager.Singleton, launch.Session, hostSession,
                launch.AuthenticationId, launch.DisplayName);
            hostRecoveryInProgress = false;
            hostRecoveryFailed = false;
            sessionMembershipLost = false;
            matchFlow.SetOnlinePaused(hostSession.IsPaused);
            PublishRosterChanged();
            QueueMigrationUpload();
            Debug.Log("Online host migration restored from the session record.");
        }

        public async Task FlushMigrationRecordAsync()
        {
            if (hostSession == null || launch?.Session == null || !launch.Session.IsHost)
            {
                return;
            }

            while (migrationUploadInProgress && migrationUploadTask != null)
            {
                var activeUpload = migrationUploadTask;
                await activeUpload;
            }

            migrationUploadQueued = false;
            nextMigrationUploadCheckAt = 0;
            var data = SessionRecordCodec.Write(hostSession.CreateRecord());
            try
            {
                await launch.Service.UploadMigrationRecordAsync(data);
                migrationUploadThrottle.OnUploadSucceeded(data, Time.realtimeSinceStartupAsDouble);
            }
            catch
            {
                migrationUploadThrottle.OnUploadFailed(Time.realtimeSinceStartupAsDouble);
                migrationUploadQueued = true;
                nextMigrationUploadCheckAt = migrationUploadThrottle.NextUploadAt;
                throw;
            }
        }

        private void QueueMigrationUpload()
        {
            if (hostSession == null || launch?.Session == null || !launch.Session.IsHost || restoringHost ||
                hostNetworkRecoveryInProgress)
            {
                return;
            }

            byte[] data;
            try
            {
                data = SessionRecordCodec.Write(hostSession.CreateRecord());
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Preparing online migration data failed: " + exception.Message);
                return;
            }

            var now = Time.realtimeSinceStartupAsDouble;
            if (migrationUploadInProgress)
            {
                migrationUploadQueued = true;
                return;
            }

            if (migrationUploadThrottle.IsUnchanged(data))
            {
                migrationUploadQueued = false;
                return;
            }

            if (now < migrationUploadThrottle.NextUploadAt)
            {
                migrationUploadQueued = true;
                nextMigrationUploadCheckAt = migrationUploadThrottle.NextUploadAt;
                return;
            }

            if (!migrationUploadThrottle.ShouldUpload(data, now))
            {
                return;
            }

            migrationUploadTask = UploadMigrationDataAsync(data);
        }

        private async Task UploadMigrationDataAsync(byte[] data)
        {
            migrationUploadInProgress = true;
            try
            {
                await launch.Service.UploadMigrationRecordAsync(data);
                migrationUploadThrottle.OnUploadSucceeded(data, Time.realtimeSinceStartupAsDouble);
            }
            catch (Exception exception)
            {
                migrationUploadThrottle.OnUploadFailed(Time.realtimeSinceStartupAsDouble);
                migrationUploadQueued = true;
                nextMigrationUploadCheckAt = migrationUploadThrottle.NextUploadAt;
                Debug.LogWarning("Uploading online migration data failed: " + exception.Message);
            }
            finally
            {
                migrationUploadInProgress = false;
                migrationUploadTask = null;
                if (migrationUploadQueued)
                {
                    nextMigrationUploadCheckAt = Math.Max(nextMigrationUploadCheckAt,
                        Time.realtimeSinceStartupAsDouble + 0.1);
                }
            }
        }

        private void SetHostRecoveryFailure(string reason)
        {
            hostRecoveryInProgress = false;
            hostRecoveryFailed = true;
            matchFlow?.SetOnlinePaused(true);
            Debug.LogWarning("Online host migration recovery failed: " + reason);
            PublishRosterChanged();
        }

        private void OnHostPauseReasonChanged(PauseReason reason)
        {
            var paused = reason != PauseReason.None;
            matchFlow?.SetOnlinePaused(paused);
            PostPauseNotice(paused);
            PublishRosterChanged();
            QueueMigrationUpload();
        }

        private void PostPauseNotice(bool paused)
        {
            if (!paused && !matchWentLive)
            {
                matchWentLive = true;
                return;
            }

            if (paused && !matchWentLive)
            {
                return;
            }

            matchFlow?.PostOnlineNotice(paused
                ? Loc.Get("net.notice.match_paused")
                : Loc.Get("net.notice.match_resumed"));
        }

        private async void OnSeatExpired(int seat)
        {
            if (hostSession == null || launch == null || launch.Session == null || !launch.Session.IsHost ||
                seat < 0 || seat >= hostSession.Roster.SeatCount)
            {
                return;
            }

            var authenticationId = hostSession.Roster.Get(seat).AuthenticationId;
            if (string.IsNullOrWhiteSpace(authenticationId))
            {
                return;
            }

            if (transport != null && transport.HasActivePlayer(authenticationId))
            {
                return;
            }

            try
            {
                await launch.Service.RemovePlayerAsync(authenticationId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Failed to remove a disconnected player from the lobby: " + exception.Message);
            }
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

    public sealed class OnlineRosterViewModel
    {
        private readonly SeatState[] seatStates;

        public OnlineRosterViewModel(string roomCode, SeatState[] seats, int hostSeat, int localSeat,
            bool isHost, PauseReason pauseReason, bool hostRecoveryInProgress, bool hostRecoveryFailed)
        {
            RoomCode = roomCode ?? string.Empty;
            seatStates = seats == null ? throw new ArgumentNullException(nameof(seats)) :
                (SeatState[])seats.Clone();
            HostSeat = hostSeat;
            LocalSeat = localSeat;
            IsHost = isHost;
            PauseReason = pauseReason;
            HostRecoveryInProgress = hostRecoveryInProgress;
            HostRecoveryFailed = hostRecoveryFailed;
        }

        public string RoomCode { get; }
        public IReadOnlyList<SeatState> Seats => Array.AsReadOnly(seatStates);
        public int HostSeat { get; }
        public int LocalSeat { get; }
        public bool IsHost { get; }
        public PauseReason PauseReason { get; }
        public bool IsPaused => PauseReason != Mejnun.Net.PauseReason.None;
        public bool HostRecoveryInProgress { get; }
        public bool HostRecoveryFailed { get; }
        public bool LocalPendingHandBack => LocalSeat >= 0 && LocalSeat < seatStates.Length &&
                                            seatStates[LocalSeat].PendingHandBack;
    }
}