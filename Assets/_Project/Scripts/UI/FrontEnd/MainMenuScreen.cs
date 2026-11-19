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

        public MainMenuScreen(VisualElement documentRoot, CosmeticCatalog catalog, Action<MatchMode> playAction,
            Action resumeAction, Action openGallery, Action openSettings, Action startTutorial = null,
            Action openOnline = null)
        {
            root = documentRoot.Q<VisualElement>("main-menu-screen");
            frontendRoot = documentRoot.Q<VisualElement>("frontend-root");
            cosmeticCatalog = catalog;
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
            quitButton.style.display = Application.isMobilePlatform ? DisplayStyle.None : DisplayStyle.Flex;
            quitButton.clicked += Application.Quit;
            progression.Changed += Refresh;
            Refresh();
        }

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
        }

        public void Dispose()
        {
            progression.Changed -= Refresh;
        }

        private void Refresh()
        {
            resumeButton.style.display = SavedGame.HasSave ? DisplayStyle.Flex : DisplayStyle.None;
            var profile = progression.Profile;
            var feltId = MenuFeltBackground.ResolveFeltId(profile);
            var felt = cosmeticCatalog == null || cosmeticCatalog.Felts == null
                ? null
                : cosmeticCatalog.Felts.Resolve(feltId) ?? cosmeticCatalog.Felts.Resolve(TableFeltAssigner.GreenFelt);
            MenuFeltBackground.Apply(frontendRoot, felt);
            var progress = ProgressionRules.LevelProgress(profile.Xp);
            levelProgress.value = progress.needed <= 0 ? 0f : 100f * progress.into / progress.needed;
            levelProgress.title = "Level " + profile.Level + " · " + progress.into + " / " + progress.needed + " XP";
        }
    }
}