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
        private readonly VisualElement frontendRoot;
        private readonly CosmeticCatalog cosmeticCatalog;
        private readonly ProgressionService progression;
        private readonly Button resumeButton;
        private readonly ProgressBar levelProgress;

        public MainMenuScreen(VisualElement documentRoot, CosmeticCatalog catalog, Action<MatchMode> playAction,
            Action resumeAction, Action openGallery, Action openSettings)
        {
            root = documentRoot.Q<VisualElement>("main-menu-screen");
            frontendRoot = documentRoot.Q<VisualElement>("frontend-root");
            cosmeticCatalog = catalog;
            play = playAction;
            resume = resumeAction;
            gallery = openGallery;
            settings = openSettings;
            progression = ProgressionService.Instance;
            resumeButton = root.Q<Button>("resume-match-button");
            levelProgress = root.Q<ProgressBar>("level-progress");

            root.Q<Button>("play-solo-button").clicked += () => play?.Invoke(MatchMode.Solo);
            root.Q<Button>("play-pass-and-play-button").clicked += () => play?.Invoke(MatchMode.PassAndPlay);
            resumeButton.clicked += () => resume?.Invoke();
            root.Q<Button>("online-button").SetEnabled(false);
            root.Q<Button>("gallery-button").clicked += () => gallery?.Invoke();
            root.Q<Button>("settings-button").clicked += () => settings?.Invoke();
            var quitButton = root.Q<Button>("quit-button");
            quitButton.style.display = Application.isMobilePlatform ? DisplayStyle.None : DisplayStyle.Flex;
            quitButton.clicked += Application.Quit;
            progression.Changed += Refresh;
            Refresh();
        }

        public void SetVisible(bool visible)
        {
            root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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