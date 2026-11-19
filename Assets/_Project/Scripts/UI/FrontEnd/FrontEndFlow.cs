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
            onlineScreen = new OnlineScreen(root, Back);
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
                LeavePendingOnlineSession();
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
                        pendingOnlineSession = session;
                        pendingOnlineName = displayName;
                        onlineScreen.SetStatus("Share this code with your opponent.");
                        onlineScreen.ShowSessionCode(session.Code);
                        return;
                    case OnlineAction.JoinByCode:
                        session = await onlineService.JoinByCodeAsync(sessionCode);
                        break;
                    case OnlineAction.QuickMatch:
                        session = await onlineService.QuickMatchAsync(onlineScreen.SeatCount);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(action));
                }

                LaunchOnlineMatch(session, displayName);
            }
            catch (Exception exception)
            {
                onlineScreen.SetStatus(exception.Message);
            }
            finally
            {
                onlineScreen.SetBusy(false);
            }
        }

        private void ContinueOnlineMatch()
        {
            if (pendingOnlineSession == null)
            {
                onlineScreen.SetStatus("Create a session before continuing.");
                return;
            }

            var session = pendingOnlineSession;
            pendingOnlineSession = null;
            LaunchOnlineMatch(session, pendingOnlineName);
            pendingOnlineName = null;
        }

        private void LaunchOnlineMatch(ISession session, string displayName)
        {
            OnlineLaunch.Set(onlineService, session, onlineService.IsHost, displayName);
            var seatCount = Math.Max(2, Math.Min(6, session.MaxPlayers));
            var seats = new SeatConfig[seatCount];
            seats[0] = SeatConfig.Human(displayName);
            for (var seat = 1; seat < seatCount; seat++)
            {
                seats[seat] = SeatConfig.Human("Player " + (seat + 1));
            }

            SceneFlow.LoadTable(new MatchRequest(seats, 30, MatchMode.Online));
        }

        private async void LeavePendingOnlineSession()
        {
            pendingOnlineSession = null;
            pendingOnlineName = null;
            try
            {
                await onlineService.LeaveAsync();
            }
            catch (Exception exception)
            {
                onlineScreen.SetStatus(exception.Message);
            }
        }

    }
}