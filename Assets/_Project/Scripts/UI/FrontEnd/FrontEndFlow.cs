using System;
using System.Collections.Generic;
using System.Globalization;
using Mejnun.Cosmetics;
using Mejnun.Core;
using Mejnun.Game;
using Mejnun.Localization;
using Mejnun.Net.Unity;
using Mejnun.UI;
using Unity.Services.Lobbies;
using Unity.Services.Multiplayer;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public enum FrontEndScreen
    {
        MainMenu,
        Setup,
        Gallery,
        Settings,
        Online
    }

    public sealed class FrontEndFlow : IDisposable
    {
        private static readonly FrontEndScreen[] Screens =
        {
            FrontEndScreen.MainMenu,
            FrontEndScreen.Setup,
            FrontEndScreen.Gallery,
            FrontEndScreen.Settings,
            FrontEndScreen.Online
        };

        private const string StartedPropertyKey = "mejnun_started";
        private const string PlayerNamePropertyKey = "_player_name";
        private const string PlayerCosmeticsPropertyKey = "mejnun_cosmetics";
        private const string MatchCosmeticsPropertyKey = "mejnun_match_cosmetics";
        private const string TimerPropertyKey = "mejnun_timer";
        private readonly Stack<FrontEndScreen> backStack = new Stack<FrontEndScreen>();
        private readonly MainMenuScreen mainMenu;
        private readonly SetupScreen setup;
        private readonly TablePickerScreen tablePicker;
        private readonly TablePickerScreen onlineTablePicker;
        private readonly GalleryScreen gallery;
        private readonly SettingsPanel settings;
        private readonly OnlineScreen onlineScreen;
        private readonly VisualElement setupRoot;
        private readonly VisualElement galleryRoot;
        private readonly VisualElement settingsRoot;
        private readonly VisualElement onlineRoot;
        private readonly UgsOnlineService onlineService = new UgsOnlineService();
        private FrontEndScreen currentScreen;
        private bool hasCurrentScreen;
        private MatchRequest pendingRequest;
        private ISession pendingOnlineSession;
        private string pendingOnlineName;
        private string pendingOnlineCode;
        private string pendingOnlineHostId;
        private bool pendingJoinedStartedMatch;
        private bool onlineStartRequested;
        private bool onlineMatchLaunched;
        private bool reconnectingUnfinishedMatch;
        private bool suppressScreenTransitions;
        private bool tableTransitionStarted;

        public FrontEndFlow(VisualElement root, CosmeticCatalog catalog, DeckSkinLoader loader)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            setupRoot = root.Q<VisualElement>("setup-screen");
            galleryRoot = root.Q<VisualElement>("gallery-screen");
            settingsRoot = root.Q<VisualElement>("settings-panel");
            onlineRoot = root.Q<VisualElement>("online-screen");
            mainMenu = new MainMenuScreen(root, catalog, OpenSetup, ResumeMatch, () => OpenGallery(), OpenSettings,
                StartTutorial, OpenOnline, loader, ReconnectOnline);
            tablePicker = new TablePickerScreen(root, catalog, ProgressionService.Instance, OpenGallery, loader);
            onlineTablePicker = new TablePickerScreen(root, catalog, ProgressionService.Instance, OpenGallery, loader,
                "online-setup", "online-");
            tablePicker.FeltPickChanged += OnFeltPickChanged;
            onlineTablePicker.FeltPickChanged += OnFeltPickChanged;
            setup = new SetupScreen(root, OnSetupNext, Back, MatchMode.Solo);
            setup.SetCanStart(() => tablePicker.CanStart);
            gallery = new GalleryScreen(root, catalog, ProgressionService.Instance, loader);
            settings = new SettingsPanel();
            settings.Initialize(settingsRoot);
            settings.Closed += Back;
            root.Q<Button>("gallery-back-button").clicked += Back;
            onlineScreen = new OnlineScreen(root, HandleOnlineBack);
            onlineScreen.Requested += HandleOnlineRequest;
            onlineScreen.ContinueRequested += ContinueOnlineMatch;

            backStack.Push(FrontEndScreen.MainMenu);
            ShowScreen(FrontEndScreen.MainMenu);
        }

        public FrontEndScreen CurrentScreen => backStack.Peek();
        public TablePickerScreen Picker => tablePicker;
        public MainMenuScreen MainMenu => mainMenu;
        public MatchRequest PendingRequest => pendingRequest;

        /// <summary>Loads the scripted tutorial match (You vs Layla, stacked deck, untimed).</summary>
        public void StartTutorial()
        {
            LaunchMatch(MejnunTutorial.CreateMatchRequest());
        }

        public void NavigateTo(MenuDestination destination, bool animate = true)
        {
            suppressScreenTransitions = !animate;
            try
            {
                switch (destination.Kind)
                {
                    case MenuDestinationKind.Setup:
                    case MenuDestinationKind.TablePicker:
                        OpenSetup(MatchMode.Solo);
                        break;
                    case MenuDestinationKind.Gallery:
                        OpenGallery(destination.ItemId);
                        break;
                    case MenuDestinationKind.Settings:
                        OpenSettings();
                        break;
                    default:
                        ShowScreen(FrontEndScreen.MainMenu, true);
                        break;
                }
            }
            finally
            {
                suppressScreenTransitions = false;
            }
        }

        public void EnterCurrentScreen()
        {
            var screenRoot = GetScreenRoot(CurrentScreen);
            ScreenTransition.Cancel(screenRoot);
            ScreenTransition.Enter(screenRoot, true);
        }

        public void Back()
        {
            if (CurrentScreen == FrontEndScreen.Online && pendingOnlineSession != null)
            {
                LeaveOnlineLobby();
                return;
            }
            if (tablePicker.IsPopupOpen || onlineTablePicker.IsPopupOpen)
            {
                if (tablePicker.IsPopupOpen)
                {
                    tablePicker.ClosePopup();
                }

                if (onlineTablePicker.IsPopupOpen)
                {
                    onlineTablePicker.ClosePopup();
                }

                return;
            }

            if (backStack.Count <= 1)
            {
                return;
            }

            if (CurrentScreen == FrontEndScreen.Gallery)
            {
                gallery.MarkSeen();
            }

            backStack.Pop();
            ShowScreen(backStack.Peek(), true);
        }

        public void Dispose()
        {
            mainMenu.Dispose();
            gallery.Dispose();
            setup.Dispose();
            tablePicker.Dispose();
            onlineTablePicker.Dispose();
            onlineScreen.Dispose();
            settings.Closed -= Back;
            settings.Dispose();
            onlineScreen.Requested -= HandleOnlineRequest;
            onlineScreen.ContinueRequested -= ContinueOnlineMatch;
            tablePicker.FeltPickChanged -= OnFeltPickChanged;
            onlineTablePicker.FeltPickChanged -= OnFeltPickChanged;
            UnsubscribeOnlineSession();
        }

        public void OpenSetup(MatchMode mode)
        {
            tablePicker.ReloadPicks();
            setup.SetMode(mode);
            Push(FrontEndScreen.Setup);
        }

        private void ResumeMatch()
        {
            BeginTableTransition(new MatchRequest(resume: true));
        }

        public void OpenGallery(string itemId = null)
        {
            if (tablePicker.IsPopupOpen)
            {
                tablePicker.ClosePopup();
            }

            gallery.Show(itemId);
            Push(FrontEndScreen.Gallery);
        }

        public void OpenSettings()
        {
            Push(FrontEndScreen.Settings);
        }

        public void OpenOnline()
        {
            onlineTablePicker.ReloadPicks();
            onlineScreen.HideLobby();
            onlineScreen.RefreshNickname();
            onlineScreen.SetStatus(string.Empty);
            Push(FrontEndScreen.Online);
        }

        private void ReconnectOnline()
        {
            var sessionCode = UgsOnlineService.LastSessionCode;
            if (!UgsOnlineService.HasUnfinishedMatch || string.IsNullOrWhiteSpace(sessionCode))
            {
                UgsOnlineService.ClearUnfinishedMatch();
                mainMenu.RefreshReconnectOption();
                return;
            }

            OpenOnline();
            onlineScreen.SetLocalizedStatus(() => Loc.Format("online.status.reconnecting", sessionCode));
            reconnectingUnfinishedMatch = true;
            HandleOnlineRequest(OnlineAction.JoinByCode, GameSettings.OnlineNickname, sessionCode);
        }

        private void OnSetupNext(MatchRequest request)
        {
            SubmitMatchRequest(request);
            LaunchMatch();
        }

        public void SubmitMatchRequest(MatchRequest request)
        {
            if (request == null || request.Resume)
            {
                throw new ArgumentException("A new match request is required.", nameof(request));
            }

            tablePicker.SetSetupRequest(request);
            pendingRequest = tablePicker.CreateMatchRequest();
            if (CurrentScreen != FrontEndScreen.Setup)
            {
                Push(FrontEndScreen.Setup);
            }

            setup.RefreshCanStart();
        }

        public void LaunchMatch()
        {
            if (pendingRequest != null)
            {
                BeginTableTransition(pendingRequest);
            }
        }

        public void LaunchMatch(MatchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            pendingRequest = request;
            LaunchMatch();
        }

        private void Push(FrontEndScreen screen)
        {
            backStack.Push(screen);
            ShowScreen(screen);
        }

        private void ShowScreen(FrontEndScreen screen, bool backwards = false)
        {
            var previousScreen = currentScreen;
            var hadPreviousScreen = hasCurrentScreen;
            currentScreen = screen;
            hasCurrentScreen = true;

            if (suppressScreenTransitions)
            {
                for (var index = 0; index < Screens.Length; index++)
                {
                    var target = Screens[index];
                    ScreenTransition.Cancel(GetScreenRoot(target));
                    SetScreenVisible(target, target == screen);
                }

                return;
            }

            if (!hadPreviousScreen || previousScreen == screen)
            {
                for (var index = 0; index < Screens.Length; index++)
                {
                    var otherScreen = Screens[index];
                    if (otherScreen == screen)
                    {
                        continue;
                    }

                    ScreenTransition.Cancel(GetScreenRoot(otherScreen));
                    SetScreenVisible(otherScreen, false);
                }

                ScreenTransition.Cancel(GetScreenRoot(screen));
                SetScreenVisible(screen, true);
                return;
            }

            for (var index = 0; index < Screens.Length; index++)
            {
                var otherScreen = Screens[index];
                if (otherScreen == screen || otherScreen == previousScreen)
                {
                    continue;
                }

                ScreenTransition.Cancel(GetScreenRoot(otherScreen));
                SetScreenVisible(otherScreen, false);
            }

            var incomingRoot = GetScreenRoot(screen);
            var outgoingRoot = GetScreenRoot(previousScreen);
            SetScreenVisible(screen, true);
            ScreenTransition.Enter(incomingRoot, backwards);
            ScreenTransition.Exit(outgoingRoot, !backwards, () =>
            {
                if (currentScreen != previousScreen)
                {
                    SetScreenVisible(previousScreen, false);
                }
            });
        }

        private void BeginTableTransition(MatchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (tableTransitionStarted)
            {
                return;
            }

            tableTransitionStarted = true;
            if (tablePicker.IsPopupOpen)
            {
                tablePicker.ClosePopup();
            }

            if (onlineTablePicker.IsPopupOpen)
            {
                onlineTablePicker.ClosePopup();
            }

            var outgoingScreen = CurrentScreen;
            ScreenTransition.Exit(GetScreenRoot(outgoingScreen), true, () =>
            {
                SetScreenVisible(outgoingScreen, false);
                var feltId = ResolveRequestFeltId(request);
                if (!string.IsNullOrWhiteSpace(feltId) &&
                    !string.Equals(feltId, mainMenu.CurrentFeltId, StringComparison.Ordinal))
                {
                    mainMenu.ShowFelt(feltId, true, () => SceneFlow.LoadTable(request));
                    return;
                }

                SceneFlow.LoadTable(request);
            });
        }

        private static string ResolveRequestFeltId(MatchRequest request)
        {
            if (request.Resume)
            {
                if (!SavedGame.TryLoad(out var savedGame))
                {
                    return null;
                }

                try
                {
                    return SnapshotSerializer.Read(savedGame.Snapshot).State.TableFeltId;
                }
                catch
                {
                    return null;
                }
            }

            return request.FeltPool != null && request.FeltPool.Count > 0
                ? request.FeltPool[0]
                : TableFeltAssigner.GreenFelt;
        }

        private void OnFeltPickChanged(string feltId)
        {
            mainMenu.RefreshBackground();
        }

        private VisualElement GetScreenRoot(FrontEndScreen screen)
        {
            switch (screen)
            {
                case FrontEndScreen.MainMenu:
                    return mainMenu.Root;
                case FrontEndScreen.Setup:
                    return setupRoot;
                case FrontEndScreen.Gallery:
                    return galleryRoot;
                case FrontEndScreen.Settings:
                    return settingsRoot;
                case FrontEndScreen.Online:
                    return onlineRoot;
                default:
                    throw new ArgumentOutOfRangeException(nameof(screen), screen, null);
            }
        }

        private void SetScreenVisible(FrontEndScreen screen, bool visible)
        {
            switch (screen)
            {
                case FrontEndScreen.MainMenu:
                    mainMenu.SetVisible(visible);
                    break;
                case FrontEndScreen.Setup:
                    setup.SetVisible(visible);
                    break;
                case FrontEndScreen.Gallery:
                    gallery.SetVisible(visible);
                    break;
                case FrontEndScreen.Settings:
                    if (visible)
                    {
                        settings.Show();
                    }
                    else
                    {
                        settings.Hide();
                    }
                    break;
                case FrontEndScreen.Online:
                    onlineScreen.SetVisible(visible);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(screen), screen, null);
            }
        }

        private async void HandleOnlineRequest(OnlineAction action, string nickname, string sessionCode)
        {
            onlineScreen.SetBusy(true);
            try
            {
                await onlineService.InitializeAsync(nickname);
                var displayName = string.IsNullOrWhiteSpace(nickname)
                    ? onlineService.PlayerName
                    : nickname.Trim();
                ISession session;
                switch (action)
                {
                    case OnlineAction.Create:
                        session = await onlineService.CreateSessionAsync(onlineScreen.SeatCount,
                            playerProperties: CreateOnlinePlayerProperties());
                        break;
                    case OnlineAction.JoinByCode:
                    {
                        var joinCode = sessionCode == null ? string.Empty : sessionCode.Trim();
                        string reconnectSessionId;
                        try
                        {
                            reconnectSessionId = await onlineService.FindRejoinSessionIdAsync(joinCode);
                        }
                        catch (Exception lookupException)
                        {
                            UnityEngine.Debug.LogWarning("Looking up the previous online session failed: " +
                                                         lookupException.Message);
                            reconnectSessionId = string.Empty;
                        }

                        if (!string.IsNullOrEmpty(reconnectSessionId))
                        {
                            try
                            {
                                session = await onlineService.ReconnectAsync(reconnectSessionId);
                                if (session.IsHost && IsSessionStarted(session))
                                {
                                    UnityEngine.Debug.LogWarning(
                                        "The saved session still identifies this player as host; leaving and joining by code to recover as a client.");
                                    session = await onlineService.RejoinByCodeAsync(joinCode,
                                        CreateOnlinePlayerProperties());
                                }
                            }
                            catch (Exception reconnectException)
                            {
                                UnityEngine.Debug.LogWarning(
                                    "Reconnect by saved session failed; falling back to the join code. " +
                                    reconnectException.Message);
                                session = await onlineService.RejoinByCodeAsync(joinCode,
                                    CreateOnlinePlayerProperties());
                            }
                        }
                        else
                        {
                            session = await onlineService.JoinByCodeAsync(joinCode,
                                CreateOnlinePlayerProperties());
                        }

                        break;
                    }
                    case OnlineAction.QuickMatch:
                        session = await onlineService.QuickMatchAsync(onlineScreen.SeatCount,
                            playerProperties: CreateOnlinePlayerProperties());
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(action));
                }

                EnterLobby(session, displayName,
                    action == OnlineAction.JoinByCode ? sessionCode : session.Code);
                reconnectingUnfinishedMatch = false;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("Online " + action + " request failed: " + exception);
                if (reconnectingUnfinishedMatch &&
                    ResolveOnlineErrorKey(exception) == "online.status.match_not_found")
                {
                    UgsOnlineService.ClearUnfinishedMatch();
                    mainMenu.RefreshReconnectOption();
                    onlineScreen.SetLocalizedStatus(() => Loc.Get("online.status.match_ended"));
                }
                else
                {
                    SetOnlineFailureStatus(exception);
                }

                reconnectingUnfinishedMatch = false;
            }
            finally
            {
                if (!onlineStartRequested)
                {
                    onlineScreen.SetBusy(false);
                }

                reconnectingUnfinishedMatch = false;
            }
        }

        private void ContinueOnlineMatch()
        {
            if (pendingOnlineSession == null)
            {
                onlineScreen.SetLocalizedStatus(() => Loc.Get("online.status.join_first"));
                return;
            }

            StartOnlineMatch();
        }

        private void EnterLobby(ISession session, string displayName, string sessionCode)
        {
            pendingOnlineSession = session;
            pendingOnlineName = displayName;
            pendingOnlineCode = string.IsNullOrWhiteSpace(session.Code) ? sessionCode : session.Code;
            pendingOnlineHostId = session.Host;
            pendingJoinedStartedMatch = IsSessionStarted(session);
            onlineStartRequested = false;
            onlineMatchLaunched = false;
            SubscribeOnlineSession(session);

            if (IsSessionStarted(session))
            {
                LaunchOnlineMatch(session, displayName, pendingOnlineCode, IsSessionStartedEarly(session));
                return;
            }

            RefreshOnlineLobby();
            if (session.IsHost && session.PlayerCount >= session.MaxPlayers)
            {
                StartOnlineMatch();
            }
        }

        private void SubscribeOnlineSession(ISession session)
        {
            session.Changed += OnOnlineSessionChanged;
            session.PlayerJoined += OnOnlinePlayerJoined;
            session.PlayerHasLeft += OnOnlinePlayerLeft;
            session.SessionPropertiesChanged += OnOnlineSessionChanged;
            session.Deleted += OnOnlineSessionEnded;
            session.RemovedFromSession += OnOnlineSessionEnded;
            session.SessionHostChanged += OnOnlineSessionHostChanged;
        }

        private void UnsubscribeOnlineSession()
        {
            if (pendingOnlineSession == null)
            {
                return;
            }

            pendingOnlineSession.Changed -= OnOnlineSessionChanged;
            pendingOnlineSession.PlayerJoined -= OnOnlinePlayerJoined;
            pendingOnlineSession.PlayerHasLeft -= OnOnlinePlayerLeft;
            pendingOnlineSession.SessionPropertiesChanged -= OnOnlineSessionChanged;
            pendingOnlineSession.Deleted -= OnOnlineSessionEnded;
            pendingOnlineSession.RemovedFromSession -= OnOnlineSessionEnded;
            pendingOnlineSession.SessionHostChanged -= OnOnlineSessionHostChanged;
        }

        private void OnOnlineSessionChanged()
        {
            var session = pendingOnlineSession;
            if (session == null || onlineMatchLaunched)
            {
                return;
            }

            if (IsSessionStarted(session))
            {
                if (!session.IsHost)
                {
                    LaunchOnlineMatch(session, pendingOnlineName, pendingOnlineCode,
                        IsSessionStartedEarly(session));
                }

                return;
            }

            RefreshOnlineLobby();
            if (session.IsHost && session.PlayerCount >= session.MaxPlayers)
            {
                StartOnlineMatch();
            }
        }

        private void OnOnlinePlayerJoined(string playerId)
        {
            OnOnlineSessionChanged();
        }

        private void OnOnlinePlayerLeft(string playerId)
        {
            if (string.Equals(playerId, pendingOnlineHostId, StringComparison.Ordinal))
            {
                CancelOnlineLobby();
                return;
            }

            OnOnlineSessionChanged();
        }

        private void OnOnlineSessionHostChanged(string authenticationId)
        {
            CancelOnlineLobby();
        }

        private void OnOnlineSessionEnded()
        {
            CancelOnlineLobby();
        }

        private void RefreshOnlineLobby()
        {
            var session = pendingOnlineSession;
            if (session == null || onlineMatchLaunched)
            {
                return;
            }

            var playerNames = new List<string>(session.PlayerCount);
            for (var index = 0; index < session.Players.Count; index++)
            {
                var player = session.Players[index];
                var playerName = player.Properties != null &&
                                 player.Properties.TryGetValue(PlayerNamePropertyKey, out var nameProperty)
                    ? nameProperty.Value
                    : null;
                if (string.IsNullOrWhiteSpace(playerName))
                {
                    playerName = string.Equals(player.Id, onlineService.AuthenticationId, StringComparison.Ordinal)
                        ? pendingOnlineName
                        : Loc.Format("online.lobby.player_default", index + 1);
                }

                playerNames.Add(playerName);
            }

            onlineScreen.ShowLobby(pendingOnlineCode, session.IsHost, playerNames, session.MaxPlayers);
        }

        private static bool IsSessionStarted(ISession session)
        {
            return session.Properties != null && session.Properties.ContainsKey(StartedPropertyKey);
        }

        private static bool IsSessionStartedEarly(ISession session)
        {
            return session.Properties != null &&
                   session.Properties.TryGetValue(StartedPropertyKey, out var property) &&
                   string.Equals(property.Value, "early", StringComparison.Ordinal);
        }

        private async void StartOnlineMatch()
        {
            var session = pendingOnlineSession;
            if (session == null || !session.IsHost || onlineStartRequested || onlineMatchLaunched)
            {
                return;
            }

            onlineStartRequested = true;
            onlineScreen.SetBusy(true);
            var startedEarly = session.PlayerCount < session.MaxPlayers;
            try
            {
                var hostSession = session.AsHost();
                hostSession.SetProperty(TimerPropertyKey, new SessionProperty(
                    onlineScreen.TimerSeconds.ToString(CultureInfo.InvariantCulture), VisibilityPropertyOptions.Member));
                hostSession.SetProperty(MatchCosmeticsPropertyKey,
                    new SessionProperty(SelectRandomPlayerCosmetics(session) ?? string.Empty,
                        VisibilityPropertyOptions.Member));
                hostSession.SetProperty(StartedPropertyKey,
                    new SessionProperty(startedEarly ? "early" : "started"));
                await hostSession.SavePropertiesAsync();
                if (pendingOnlineSession == session)
                {
                    LaunchOnlineMatch(session, pendingOnlineName, pendingOnlineCode, startedEarly);
                }
            }
            catch (Exception exception)
            {
                onlineStartRequested = false;
                onlineScreen.SetBusy(false);
                SetOnlineFailureStatus(exception);
            }
        }

        private void HandleOnlineBack()
        {
            if (CurrentScreen == FrontEndScreen.Online && pendingOnlineSession != null)
            {
                LeaveOnlineLobby();
                return;
            }

            Back();
        }

        private async void LeaveOnlineLobby()
        {
            var session = pendingOnlineSession;
            if (session == null)
            {
                Back();
                return;
            }

            var wasHost = session.IsHost;
            UnsubscribeOnlineSession();
            ClearPendingOnlineSession();
            onlineScreen.HideLobby();
            onlineScreen.SetBusy(false);
            if (wasHost)
            {
                onlineScreen.SetLocalizedStatus(() => Loc.Get("online.status.host_cancelled"));
            }
            else
            {
                onlineScreen.SetLocalizedStatus(() => Loc.Get("online.status.left_lobby"));
            }

            try
            {
                if (wasHost)
                {
                    await onlineService.DeleteSessionAsync();
                }
                else
                {
                    await onlineService.LeaveAsync();
                }
            }
            catch (Exception exception)
            {
                SetOnlineFailureStatus(exception);
            }
        }

        private void SetOnlineFailureStatus(Exception exception)
        {
            switch (ResolveOnlineErrorKey(exception))
            {
                case "online.status.no_free_seats":
                    onlineScreen.SetLocalizedStatus(() => Loc.Get("online.status.no_free_seats"));
                    break;
                case "online.status.match_not_found":
                    onlineScreen.SetLocalizedStatus(() => Loc.Get("online.status.match_not_found"));
                    break;
                case "online.status.no_open_matches":
                    onlineScreen.SetLocalizedStatus(() => Loc.Get("online.status.no_open_matches"));
                    break;
                default:
                    onlineScreen.SetLocalizedStatus(() => Loc.Get("online.status.join_failed"));
                    break;
            }
        }

        private static string ResolveOnlineErrorKey(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is LobbyServiceException lobbyException)
                {
                    switch (lobbyException.Reason)
                    {
                        case LobbyExceptionReason.LobbyFull:
                        case LobbyExceptionReason.LobbyLocked:
                            return "online.status.no_free_seats";
                        case LobbyExceptionReason.LobbyNotFound:
                        case LobbyExceptionReason.InvalidJoinCode:
                            return "online.status.match_not_found";
                        case LobbyExceptionReason.NoOpenLobbies:
                            return "online.status.no_open_matches";
                    }
                }

                if (current is SessionException sessionException)
                {
                    switch (sessionException.Error)
                    {
                        case SessionError.SessionNotFound:
                        case SessionError.SessionDeleted:
                        case SessionError.InvalidSessionIdentifier:
                        case SessionError.NotInLobby:
                            return "online.status.match_not_found";
                    }
                }
            }

            return "online.status.join_failed";
        }

        private async void CancelOnlineLobby()
        {
            if (pendingOnlineSession == null || onlineMatchLaunched)
            {
                return;
            }

            UnsubscribeOnlineSession();
            ClearPendingOnlineSession();
            onlineScreen.HideLobby();
            onlineScreen.SetBusy(false);
            onlineScreen.SetLocalizedStatus(() => Loc.Get("online.status.host_cancelled"));
            try
            {
                await onlineService.LeaveAsync();
            }
            catch
            {
                // The session may already have been deleted or removed.
            }
        }

        private void ClearPendingOnlineSession()
        {
            pendingOnlineSession = null;
            pendingOnlineName = null;
            pendingOnlineCode = null;
            pendingOnlineHostId = null;
            pendingJoinedStartedMatch = false;
            onlineStartRequested = false;
        }

        private void LaunchOnlineMatch(ISession session, string displayName, string sessionCode, bool startedEarly)
        {
            if (onlineMatchLaunched)
            {
                return;
            }

            onlineMatchLaunched = true;
            UnsubscribeOnlineSession();
            var joinedStartedMatch = pendingJoinedStartedMatch;
            ClearPendingOnlineSession();
            OnlineLaunch.Set(onlineService, session, session.IsHost, displayName, sessionCode, startedEarly,
                joinedStartedMatch);
            var seatCount = Math.Max(2, Math.Min(6, session.MaxPlayers));
            var seats = new SeatConfig[seatCount];
            seats[0] = SeatConfig.Human(displayName);
            for (var seat = 1; seat < seatCount; seat++)
            {
                // Placeholder until the network names arrive; MatchFlow gives
                // unnamed seats their default name, so no text is stored here.
                seats[seat] = SeatConfig.Human(string.Empty);
            }

            var timerSeconds = ReadOnlineTimer(session);
            var cosmeticsToken = ReadSessionProperty(session, MatchCosmeticsPropertyKey);
            if (!TablePickerScreen.TryDecodeCosmeticsToken(cosmeticsToken, out var selectedDeckPicks,
                    out var selectedFeltPick))
            {
                selectedDeckPicks = new[] { onlineTablePicker.GetDeckPick(0), onlineTablePicker.GetDeckPick(1) };
                selectedFeltPick = onlineTablePicker.FeltPick;
            }

            var request = new MatchRequest(seats, timerSeconds, MatchMode.Online);
            BeginTableTransition(onlineTablePicker.CreateMatchRequest(request, selectedDeckPicks, selectedFeltPick));
        }

        private Dictionary<string, PlayerProperty> CreateOnlinePlayerProperties()
        {
            var deckPicks = new[] { onlineTablePicker.GetDeckPick(0), onlineTablePicker.GetDeckPick(1) };
            var cosmetics = TablePickerScreen.EncodeCosmeticsToken(deckPicks, onlineTablePicker.FeltPick);
            return new Dictionary<string, PlayerProperty>
            {
                { PlayerCosmeticsPropertyKey, new PlayerProperty(cosmetics, VisibilityPropertyOptions.Member) }
            };
        }

        private static string SelectRandomPlayerCosmetics(ISession session)
        {
            if (session?.Players == null || session.Players.Count == 0)
            {
                return null;
            }

            var choices = new List<string>();
            for (var index = 0; index < session.Players.Count; index++)
            {
                var properties = session.Players[index].Properties;
                if (properties != null && properties.TryGetValue(PlayerCosmeticsPropertyKey, out var property) &&
                    TablePickerScreen.TryDecodeCosmeticsToken(property.Value, out _, out _))
                {
                    choices.Add(property.Value);
                }
            }

            return choices.Count == 0 ? null : choices[new Random().Next(choices.Count)];
        }

        private static int ReadOnlineTimer(ISession session)
        {
            var value = ReadSessionProperty(session, TimerPropertyKey);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                ? Math.Max(0, Math.Min(120, seconds))
                : 0;
        }

        private static string ReadSessionProperty(ISession session, string key)
        {
            return session?.Properties != null && session.Properties.TryGetValue(key, out var property)
                ? property.Value
                : null;
        }

    }
}