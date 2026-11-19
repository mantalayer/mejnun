using System;
using Mejnun.Cosmetics;
using Mejnun.Core;
using Mejnun.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI.FrontEnd
{
    public sealed class MainMenuScreen : IDisposable
    {
        private readonly VisualElement root;
        private readonly Action<MatchMode> play;
        private readonly Action resume;
        private readonly Action gallery;
        private readonly Action settings;
        private readonly Action howToPlay;
        private readonly Action online;
        private readonly VisualElement tutorialPrompt;
        private MatchMode pendingMode;
        private readonly VisualElement frontendRoot;
        private readonly CosmeticCatalog cosmeticCatalog;
        private readonly ProgressionService progression;
        private readonly Button resumeButton;
        private readonly ProgressBar levelProgress;
        private readonly MenuCardCursor cardCursor;
        private readonly FeltWipe feltWipe;
        private string currentFeltId;

        public MainMenuScreen(VisualElement documentRoot, CosmeticCatalog catalog, Action<MatchMode> playAction,
            Action resumeAction, Action openGallery, Action openSettings, Action startTutorial = null,
            Action openOnline = null, DeckSkinLoader loader = null)
        {
            root = documentRoot.Q<VisualElement>("main-menu-screen");
            frontendRoot = documentRoot.Q<VisualElement>("frontend-root");
            cosmeticCatalog = catalog;
            feltWipe = new FeltWipe(frontendRoot);
            play = playAction;
            resume = resumeAction;
            gallery = openGallery;
            settings = openSettings;
            howToPlay = startTutorial;
            online = openOnline;
            progression = ProgressionService.Instance;
            tutorialPrompt = root.Q<VisualElement>("tutorial-prompt");
            resumeButton = root.Q<Button>("resume-match-button");
            levelProgress = root.Q<ProgressBar>("level-progress");

            root.Q<Button>("play-solo-button").clicked += () => RequestPlay(MatchMode.Solo);
            root.Q<Button>("play-pass-and-play-button").clicked += () => RequestPlay(MatchMode.PassAndPlay);
            var howToPlayButton = root.Q<Button>("how-to-play-button");
            if (howToPlayButton != null)
            {
                howToPlayButton.clicked += () => howToPlay?.Invoke();
            }

            if (tutorialPrompt != null)
            {
                tutorialPrompt.Q<Button>("tutorial-prompt-play-button").clicked += AcceptTutorialPrompt;
                tutorialPrompt.Q<Button>("tutorial-prompt-skip-button").clicked += DeclineTutorialPrompt;
            }
            resumeButton.clicked += () => resume?.Invoke();
            root.Q<Button>("online-button").clicked += () => online?.Invoke();
            root.Q<Button>("gallery-button").clicked += () => gallery?.Invoke();
            root.Q<Button>("settings-button").clicked += () => settings?.Invoke();
            var quitButton = root.Q<Button>("quit-button");
            quitButton.style.display = DeviceProfile.IsMobilePlatform ? DisplayStyle.None : DisplayStyle.Flex;
            quitButton.clicked += QuitGame;
            AddVersionLabel();
            progression.Changed += Refresh;
            Refresh();
            cardCursor = new MenuCardCursor(root, tutorialPrompt, cosmeticCatalog, progression, loader);
        }

        public VisualElement Root => root;
        public string CurrentFeltId => currentFeltId;

        public bool IsTutorialPromptVisible => tutorialPrompt != null &&
                                               tutorialPrompt.style.display == DisplayStyle.Flex;

        /// <summary>New players (no XP, no matches played, tutorial never played) are offered the tutorial before starting a match.</summary>
        public void RequestPlay(MatchMode mode)
        {
            if (ShouldOfferTutorial() && tutorialPrompt != null && howToPlay != null)
            {
                pendingMode = mode;
                tutorialPrompt.style.display = DisplayStyle.Flex;
                return;
            }

            play?.Invoke(mode);
        }

        private bool ShouldOfferTutorial()
        {
            var profile = progression.Profile;
            return profile.Xp <= 0 && profile.Stats.Played <= 0 && !GameSettings.TutorialCompleted;
        }

        private void AcceptTutorialPrompt()
        {
            HideTutorialPrompt();
            howToPlay?.Invoke();
        }

        private void DeclineTutorialPrompt()
        {
            HideTutorialPrompt();
            play?.Invoke(pendingMode);
        }

        private void HideTutorialPrompt()
        {
            if (tutorialPrompt != null)
            {
                tutorialPrompt.style.display = DisplayStyle.None;
            }
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void SetVisible(bool visible)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
            {
                HideTutorialPrompt();
            }
            if (visible)
            {
                Refresh();
            }

            cardCursor.SetVisible(visible);
        }

        public void RefreshBackground()
        {
            ShowFelt(MenuFeltBackground.ResolveFeltId(progression.Profile), true);
        }

        public void ShowFelt(string feltId, bool animate, Action completed = null)
        {
            var requestedId = string.IsNullOrWhiteSpace(feltId) ? TableFeltAssigner.GreenFelt : feltId;
            var felt = ResolveFelt(requestedId);
            var resolvedId = felt == null ? requestedId : felt.Id;
            var shouldAnimate = animate && !string.IsNullOrWhiteSpace(currentFeltId);
            if (string.Equals(currentFeltId, resolvedId, StringComparison.Ordinal))
            {
                completed?.Invoke();
                return;
            }

            currentFeltId = resolvedId;
            if (shouldAnimate)
            {
                feltWipe.Play(felt, completed);
            }
            else
            {
                feltWipe.Cancel();
                MenuFeltBackground.Apply(frontendRoot, felt);
                completed?.Invoke();
            }
        }

        private void AddVersionLabel()
        {
            if (root.Q<Label>("version-label") != null)
            {
                return;
            }

            var label = new Label("v" + Application.version) { name = "version-label" };
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.left = 12;
            label.style.top = 8;
            label.style.fontSize = 14;
            label.style.color = new Color(1f, 1f, 1f, 0.35f);
            root.Add(label);
        }

        public void Dispose()
        {
            progression.Changed -= Refresh;
            feltWipe.Cancel();
            cardCursor.Dispose();
        }

        private void Refresh()
        {
            resumeButton.style.display = SavedGame.HasSave ? DisplayStyle.Flex : DisplayStyle.None;
            var profile = progression.Profile;
            RefreshBackground();
            var progress = ProgressionRules.LevelProgress(profile.Xp);
            levelProgress.value = progress.needed <= 0 ? 0f : 100f * progress.into / progress.needed;
            levelProgress.title = "Level " + profile.Level + " · " + progress.into + " / " + progress.needed + " XP";
        }

        private TableFelt ResolveFelt(string feltId)
        {
            return cosmeticCatalog == null || cosmeticCatalog.Felts == null
                ? null
                : cosmeticCatalog.Felts.Resolve(feltId) ??
                  cosmeticCatalog.Felts.Resolve(TableFeltAssigner.GreenFelt);
        }
    }
}