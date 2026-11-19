using System;
using System.Collections.Generic;
using Mejnun.Cosmetics;
using Mejnun.Game;
using Mejnun.Net.Unity;
using Mejnun.UI;
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
        private const string StartedPropertyKey = "mejnun_started";
        private const string PlayerNamePropertyKey = "_player_name";
        private readonly Stack<FrontEndScreen> backStack = new Stack<FrontEndScreen>();
        private readonly MainMenuScreen mainMenu;
        private readonly SetupScreen setup;
        private readonly TablePickerScreen tablePicker;
        private readonly GalleryScreen gallery;
        private readonly SettingsPanel settings;
        private readonly OnlineScreen onlineScreen;
        private readonly UgsOnlineService onlineService = new UgsOnlineService();
        private MatchRequest pendingRequest;
        private ISession pendingOnlineSession;
        private string pendingOnlineName;
        private string pendingOnlineCode;
        private string pendingOnlineHostId;
        private bool onlineStartRequested;
        private bool onlineMatchLaunched;

        public FrontEndFlow(VisualElement root, CosmeticCatalog catalog, DeckSkinLoader loader)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            mainMenu = new MainMenuScreen(root, catalog, OpenSetup, ResumeMatch, () => OpenGallery(), OpenSettings,
                StartTutorial, OpenOnline);
            tablePicker = new TablePickerScreen(root, catalog, ProgressionService.Instance, OpenGallery, loader);
            setup = new SetupScreen(root, OnSetupNext, Back, MatchMode.Solo);
            setup.SetCanStart(() => tablePicker.CanStart);
            gallery = new GalleryScreen(root, catalog, ProgressionService.Instance, loader);
            settings = new SettingsPanel();
            settings.Initialize(root.Q<VisualElement>("settings-panel"));
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

        public void NavigateTo(MenuDestination destination)
        {
            switch (destination.Kind)
            {
                case MenuDestinationKind.Setup:
                    OpenSetup(MatchMode.Solo);
                    break;
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
                    ShowScreen(FrontEndScreen.MainMenu);
                    break;
            }
        }

        public void Back()
        {
            if (CurrentScreen == FrontEndScreen.Online && pendingOnlineSession != null)
            {
                LeaveOnlineLobby();
                return;
            }
            if (tablePicker.IsPopupOpen)
            {
                tablePicker.ClosePopup();
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
            ShowScreen(backStack.Peek());
        }

        public void Dispose()
        {
            mainMenu.Dispose();
            gallery.Dispose();
            settings.Closed -= Back;
            settings.Dispose();
            onlineScreen.Requested -= HandleOnlineRequest;
            onlineScreen.ContinueRequested -= ContinueOnlineMatch;
            UnsubscribeOnlineSession();
        }

        public void OpenSetup(MatchMode mode)
        {
            setup.SetMode(mode);
            Push(FrontEndScreen.Setup);
        }

        private void ResumeMatch()
        {
            SceneFlow.LoadTable(new MatchRequest(resume: true));
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
            onlineScreen.HideLobby();
            onlineScreen.SetStatus(string.Empty);
            Push(FrontEndScreen.Online);
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
                SceneFlow.LoadTable(pendingRequest);
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

        private void ShowScreen(FrontEndScreen screen)
        {
            mainMenu.SetVisible(screen == FrontEndScreen.MainMenu);
            setup.SetVisible(screen == FrontEndScreen.Setup);
            gallery.SetVisible(screen == FrontEndScreen.Gallery);
            onlineScreen.SetVisible(screen == FrontEndScreen.Online);
            if (screen == FrontEndScreen.Settings)
            {
                settings.Show();
            }
            else
            {
                settings.Hide();
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
                        session = await onlineService.CreateSessionAsync(onlineScreen.SeatCount);
                        break;
                    case OnlineAction.JoinByCode:
                        session = await onlineService.JoinByCodeAsync(sessionCode);
                        break;
                    case OnlineAction.QuickMatch:
                        session = await onlineService.QuickMatchAsync(onlineScreen.SeatCount);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(action));
                }

                EnterLobby(session, displayName,
                    action == OnlineAction.JoinByCode ? sessionCode : session.Code);
            }
            catch (Exception exception)
            {
                onlineScreen.SetStatus(exception.Message);
            }
            finally
            {
                if (!onlineStartRequested)
                {
                    onlineScreen.SetBusy(false);
                }
            }
        }

        private void ContinueOnlineMatch()
        {
            if (pendingOnlineSession == null)
            {
                onlineScreen.SetStatus("Join or create a session first.");
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
                        : "Player " + (index + 1);
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
                onlineScreen.SetStatus(exception.Message);
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
            onlineScreen.SetStatus(wasHost ? "The host cancelled the game." : "You left the lobby.");
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
                onlineScreen.SetStatus(exception.Message);
            }
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
            onlineScreen.SetStatus("The host cancelled the game.");
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
            ClearPendingOnlineSession();
            OnlineLaunch.Set(onlineService, session, session.IsHost, displayName, sessionCode, startedEarly);
            var seatCount = Math.Max(2, Math.Min(6, session.MaxPlayers));
            var seats = new SeatConfig[seatCount];
            seats[0] = SeatConfig.Human(displayName);
            for (var seat = 1; seat < seatCount; seat++)
            {
                seats[seat] = SeatConfig.Human("Player " + (seat + 1));
            }

            SceneFlow.LoadTable(new MatchRequest(seats, 30, MatchMode.Online));
        }

    }
}