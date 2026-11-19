using System;
using System.Collections.Generic;
using Mejnun.Cosmetics;
using Mejnun.Game;
using Mejnun.UI;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public enum FrontEndScreen
    {
        MainMenu,
        Setup,
        Gallery,
        Settings
    }

    public sealed class FrontEndFlow : IDisposable
    {
        private readonly Stack<FrontEndScreen> backStack = new Stack<FrontEndScreen>();
        private readonly MainMenuScreen mainMenu;
        private readonly SetupScreen setup;
        private readonly TablePickerScreen tablePicker;
        private readonly GalleryScreen gallery;
        private readonly SettingsPanel settings;
        private MatchRequest pendingRequest;

        public FrontEndFlow(VisualElement root, CosmeticCatalog catalog, DeckSkinLoader loader)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            mainMenu = new MainMenuScreen(root, catalog, OpenSetup, ResumeMatch, () => OpenGallery(), OpenSettings,
                StartTutorial);
            tablePicker = new TablePickerScreen(root, catalog, ProgressionService.Instance, OpenGallery, loader);
            setup = new SetupScreen(root, OnSetupNext, Back, MatchMode.Solo);
            setup.SetCanStart(() => tablePicker.CanStart);
            gallery = new GalleryScreen(root, catalog, ProgressionService.Instance, loader);
            settings = new SettingsPanel();
            settings.Initialize(root.Q<VisualElement>("settings-panel"));
            settings.Closed += Back;
            root.Q<Button>("gallery-back-button").clicked += Back;

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
            if (screen == FrontEndScreen.Settings)
            {
                settings.Show();
            }
            else
            {
                settings.Hide();
            }
        }

    }
}