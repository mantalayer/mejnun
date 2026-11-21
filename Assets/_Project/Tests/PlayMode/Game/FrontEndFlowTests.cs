using System.Collections;
using System;
using System.IO;
using Mejnun.AI;
using Mejnun.Cosmetics;
using Mejnun.Game;
using Mejnun.UI;
using Mejnun.UI.FrontEnd;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Mejnun.Game.Tests
{
    public sealed class FrontEndFlowTests
    {
        private string profilePath;

        [SetUp]
        public void SetUp()
        {
            SceneFlow.ClearPendingState();
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "M6FrontEndTests");
            Directory.CreateDirectory(directory);
            profilePath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
            ProgressionService.ConfigureForTests(new ProfileStore(profilePath));
        }

        [TearDown]
        public void TearDown()
        {
            ProgressionService.ResetForTests();
            DeleteIfExists(profilePath);
            DeleteIfExists(profilePath + ".tmp");
            DeleteIfExists(profilePath + ".bak");
            var directory = Path.GetDirectoryName(profilePath);
            if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
            {
                Directory.Delete(directory);
            }
        }

        [UnityTest]
        public IEnumerator MainMenuBootsWithConditionalResumeAndBackStack()
        {
            SceneManager.LoadScene(SceneFlow.MainMenuScene, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var presenter = Object.FindAnyObjectByType<FrontEndPresenter>();
            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter.Flow.CurrentScreen, Is.EqualTo(FrontEndScreen.MainMenu));
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(Camera.main.isActiveAndEnabled, Is.True);
            var document = presenter.GetComponent<UIDocument>();
            Assert.That(document.panelSettings, Is.Not.Null);
            var root = document.rootVisualElement;
            Assert.That(root.panel, Is.Not.Null);
            var mainMenu = root.Q<VisualElement>(className: "main-menu-screen");
            Assert.That(mainMenu, Is.Not.Null);
            Assert.That(Mathf.RoundToInt(mainMenu.resolvedStyle.maxWidth.value), Is.EqualTo(520));
            Assert.That(root.Q<Button>("play-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("play-solo-button"), Is.Null);
            Assert.That(root.Q<Button>("play-pass-and-play-button"), Is.Null);
            var resumeButton = root.Q<Button>("resume-match-button");
            Assert.That(resumeButton.style.display == DisplayStyle.Flex, Is.EqualTo(SavedGame.HasSave));
            Assert.That(root.Q<Button>("online-button").enabledSelf, Is.True);
            Assert.That(root.Q<VisualElement>("setup-screen").style.display.value,
                Is.EqualTo(DisplayStyle.None));

            presenter.Flow.OpenSetup(MatchMode.Solo);
            Assert.That(presenter.Flow.CurrentScreen, Is.EqualTo(FrontEndScreen.Setup));
            var modeField = root.Q<DropdownField>("mode-field");
            Assert.That(modeField.value, Is.EqualTo(SetupScreen.SoloMode));
            Assert.That(modeField.enabledSelf, Is.True);
            modeField.value = SetupScreen.PassAndPlayMode;
            var botKindField = root.Q<DropdownField>("seat-kind-1");
            botKindField.value = botKindField.choices[1];
            var botStyleField = root.Q<DropdownField>("seat-style-1");
            Assert.That(botStyleField.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(botStyleField.enabledSelf, Is.True);
            presenter.Flow.Back();
            presenter.Flow.OpenSettings();
            Assert.That(presenter.Flow.CurrentScreen, Is.EqualTo(FrontEndScreen.Settings));
            Assert.That(root.Q<VisualElement>("settings-panel").style.display.value,
                Is.EqualTo(DisplayStyle.Flex));
            presenter.Flow.Back();
            Assert.That(presenter.Flow.CurrentScreen, Is.EqualTo(FrontEndScreen.MainMenu));
            presenter.Flow.Back();
            Assert.That(presenter.Flow.CurrentScreen, Is.EqualTo(FrontEndScreen.MainMenu));
        }

        [UnityTest]
        public IEnumerator MatchRequestLaunchesTableAndConsumesRequest()
        {
            SceneManager.LoadScene(SceneFlow.MainMenuScene, LoadSceneMode.Single);
            yield return null;

            var presenter = Object.FindAnyObjectByType<FrontEndPresenter>();
            var request = new MatchRequest(new[]
            {
                SeatConfig.Human("Sam"),
                SeatConfig.Bot("Leyla", BotDifficulty.Medium)
            }, 30, MatchMode.Solo, randomFacePool: new[] { "classic", "classic_ivory" }, seed: 91234UL);
            presenter.Flow.SubmitMatchRequest(request);
            Assert.That(presenter.Flow.CurrentScreen, Is.EqualTo(FrontEndScreen.Setup));
            presenter.Flow.LaunchMatch();
            yield return WaitForScene(SceneFlow.TableScene);

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneFlow.TableScene));
            var matchFlow = Object.FindAnyObjectByType<MatchFlow>();
            Assert.That(matchFlow, Is.Not.Null);
            var frames = 0;
            while (matchFlow.IsInitializingRound && frames++ < 300)
            {
                yield return null;
            }

            Assert.That(matchFlow.IsInitializingRound, Is.False);
            Assert.That(matchFlow.SeatCount, Is.EqualTo(2));
            Assert.That(matchFlow.RoundSeed, Is.EqualTo(91234UL));
            Assert.That(matchFlow.Game.Setup.FacePool, Is.EqualTo(request.RandomFacePool));
            Assert.That(MatchLaunch.TryTake(out _), Is.False);
        }

        [UnityTest]
        public IEnumerator ColdBootPickerUsesSelectedDecksAndFeltAndRecordsMatchRewards()
        {
            SceneManager.LoadScene(SceneFlow.MainMenuScene, LoadSceneMode.Single);
            yield return null;

            var presenter = Object.FindAnyObjectByType<FrontEndPresenter>();
            var flow = presenter.Flow;
            flow.SubmitMatchRequest(new MatchRequest(new[]
            {
                SeatConfig.Human("Sam"),
                SeatConfig.Human("Lee")
            }, mode: MatchMode.PassAndPlay, seed: 71234UL));
            Assert.That(flow.Picker.SelectDeck(0, "classic"), Is.True);
            Assert.That(flow.Picker.SelectDeck(1, "classic_ivory"), Is.True);
            Assert.That(flow.Picker.SelectFelt("burgundy"), Is.True);

            var request = flow.Picker.CreateMatchRequest();
            var profile = ProgressionService.Instance.Profile;
            var startingXp = profile.Xp;
            var startingPlayed = profile.Stats.Played;
            var startingStalemates = profile.Stats.Stalemates;
            flow.LaunchMatch(request);
            yield return WaitForScene(SceneFlow.TableScene);

            var matchFlow = Object.FindAnyObjectByType<MatchFlow>();
            var frames = 0;
            while (matchFlow.IsInitializingRound && frames++ < 300)
            {
                yield return null;
            }

            Assert.That(matchFlow.Game.Setup.DeckPicks, Is.EqualTo(new[] { "classic", "classic_ivory" }));
            Assert.That(matchFlow.ChooseDealer(0).Ok, Is.True);
            CollectionAssert.AreEqual(new[] { "classic", "classic_ivory" }, matchFlow.Game.State.DeckFaces);
            Assert.That(matchFlow.Game.State.TableFeltId, Is.EqualTo("burgundy"));
            Assert.That(ProgressionService.Instance.Profile.LastUsedFelt, Is.EqualTo("burgundy"));
            matchFlow.Game.State.DrawPile.Clear();
            for (var pass = 0; pass < 3 && matchFlow.Game.State.Phase == Mejnun.Core.GamePhase.Playing; pass++)
            {
                var seat = matchFlow.Game.State.CurrentSeat;
                Assert.That(matchFlow.Controller.ApplyCommand(Mejnun.Core.GameCommand.DrawOrPass(seat)).Ok, Is.True);
            }

            Assert.That(matchFlow.Screen, Is.EqualTo(MatchFlowScreen.Results));
            Assert.That(matchFlow.LastProgression.XpGained, Is.EqualTo(41));
            var resultsPresenter = Object.FindAnyObjectByType<MatchFlowPresenter>();
            var resultsDocument = resultsPresenter.GetComponent<UIDocument>();
            Assert.That(resultsDocument.rootVisualElement.Q<Label>("result-xp-gained").text, Is.EqualTo("+41 XP"));
            Assert.That(ProgressionService.Instance.Profile.Xp, Is.EqualTo(startingXp + 41));
            Assert.That(ProgressionService.Instance.Profile.Stats.Played, Is.EqualTo(startingPlayed + 1));
            Assert.That(ProgressionService.Instance.Profile.Stats.Stalemates, Is.EqualTo(startingStalemates + 1));
        }

        [UnityTest]
        public IEnumerator MainMenuBackgroundUsesLastUsedFelt()
        {
            ProgressionService.Instance.SetLastPicks(new[] { "classic", "classic_ivory" }, null);
            ProgressionService.Instance.SetLastUsedFelt("burgundy");
            SceneManager.LoadScene(SceneFlow.MainMenuScene, LoadSceneMode.Single);
            yield return null;

            var presenter = Object.FindAnyObjectByType<FrontEndPresenter>();
            var background = presenter.GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("frontend-root");
            var felt = presenter.Catalog.Felts.Resolve("burgundy");

            Assert.That(background.resolvedStyle.backgroundImage.sprite, Is.EqualTo(felt.Tile));
            Assert.That(background.resolvedStyle.backgroundColor, Is.EqualTo(felt.ClearColor));
        }

        [UnityTest]
        public IEnumerator MainMenuBackgroundPrefersPickedFeltOverLastUsedFelt()
        {
            ProgressionService.Instance.SetLastUsedFelt("burgundy");
            ProgressionService.Instance.SetLastPicks(new[] { "classic", "classic_ivory" }, "blue");
            SceneManager.LoadScene(SceneFlow.MainMenuScene, LoadSceneMode.Single);
            yield return null;

            var presenter = Object.FindAnyObjectByType<FrontEndPresenter>();
            var background = presenter.GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("frontend-root");
            var felt = presenter.Catalog.Felts.Resolve("blue");

            Assert.That(background.resolvedStyle.backgroundImage.sprite, Is.EqualTo(felt.Tile));
            Assert.That(background.resolvedStyle.backgroundColor, Is.EqualTo(felt.ClearColor));
        }

        [UnityTest]
        public IEnumerator RulesParagraphsAlignRightInRightToLeftLocales()
        {
            var english = LocalizationSettings.SelectedLocale;
            SceneManager.LoadScene(SceneFlow.MainMenuScene, LoadSceneMode.Single);
            yield return null;

            var presenter = Object.FindAnyObjectByType<FrontEndPresenter>();
            var root = presenter.GetComponent<UIDocument>().rootVisualElement;
            presenter.Flow.OpenSettings();
            var rulesPage = root.Q<ScrollView>("settings-page-rules");
            Assert.That(rulesPage, Is.Not.Null);
            rulesPage.style.display = DisplayStyle.Flex;
            var body = rulesPage.Q<Label>(className: "settings-body-text");
            var title = rulesPage.Q<Label>(className: "settings-section-title");
            var tooltip = root.Q<Label>("settings-tooltip");
            var scrollContainer = rulesPage.Q(className: "unity-scroll-view__content-and-vertical-scroll-container");
            var tutorialCopy = root.Q<Label>(className: "tutorial-prompt-copy");
            yield return null;

            Assert.That(root.ClassListContains("rtl"), Is.False);
            Assert.That(body.resolvedStyle.unityTextAlign, Is.EqualTo(TextAnchor.UpperLeft));
            Assert.That(scrollContainer.resolvedStyle.flexDirection, Is.EqualTo(FlexDirection.Row));

            try
            {
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("ar");
                yield return WaitForLocale();

                Assert.That(root.ClassListContains("rtl"), Is.True);
                Assert.That(root.languageDirection, Is.EqualTo(LanguageDirection.RTL));
                Assert.That(body.resolvedStyle.unityTextAlign, Is.EqualTo(TextAnchor.UpperRight));
                Assert.That(title.resolvedStyle.unityTextAlign, Is.EqualTo(TextAnchor.UpperRight));
                Assert.That(tooltip.resolvedStyle.unityTextAlign, Is.EqualTo(TextAnchor.UpperRight));
                Assert.That(scrollContainer.resolvedStyle.flexDirection, Is.EqualTo(FlexDirection.RowReverse));
                Assert.That(tutorialCopy.resolvedStyle.unityTextAlign, Is.EqualTo(TextAnchor.MiddleCenter));

                LocalizationSettings.SelectedLocale = english;
                yield return WaitForLocale();
                Assert.That(root.ClassListContains("rtl"), Is.False);
                Assert.That(body.resolvedStyle.unityTextAlign, Is.EqualTo(TextAnchor.UpperLeft));
            }
            finally
            {
                LocalizationSettings.SelectedLocale = english;
            }
        }

        // SelectedLocaleChanged fires only after the new locale's tables have preloaded.
        private static IEnumerator WaitForLocale()
        {
            var frames = 0;
            while (!LocalizationSettings.InitializationOperation.IsDone && frames++ < 600)
            {
                yield return null;
            }

            yield return null;
            yield return null;
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            var frames = 0;
            while (SceneManager.GetActiveScene().name != sceneName && frames++ < 600)
            {
                yield return null;
            }

            yield return null;
        }

        [Test]
        public void SafeAreaUtilityAppliesInsetsAndOrientationClasses()
        {
            var root = new VisualElement();

            SafeAreaUtility.Apply(root, new Rect(100f, 60f, 800f, 420f), 1000, 600);

            Assert.That(root.style.paddingLeft.value.value, Is.EqualTo(100f));
            Assert.That(root.style.paddingRight.value.value, Is.EqualTo(100f));
            Assert.That(root.style.paddingBottom.value.value, Is.EqualTo(60f));
            Assert.That(root.style.paddingTop.value.value, Is.EqualTo(120f));
            Assert.That(root.ClassListContains("landscape-layout"), Is.True);
            Assert.That(root.ClassListContains("portrait-layout"), Is.False);
            SafeAreaUtility.Apply(root, new Rect(100f, 60f, 800f, 420f), 1000, 600, 12f);
            Assert.That(root.style.paddingLeft.value.value, Is.EqualTo(112f));
            Assert.That(root.style.paddingRight.value.value, Is.EqualTo(112f));
            Assert.That(root.style.paddingBottom.value.value, Is.EqualTo(72f));
            Assert.That(root.style.paddingTop.value.value, Is.EqualTo(132f));
            SafeAreaUtility.Apply(root, new Rect(0f, 40f, 400f, 700f), 400, 800);
            Assert.That(root.ClassListContains("portrait-layout"), Is.True);
        }

        [Test]
        public void SafeAreaUtilityUsesLargerBottomInset()
        {
            var root = new VisualElement();
            var safeArea = new Rect(100f, 60f, 800f, 420f);

            SafeAreaUtility.Apply(root, safeArea, 1000, 600, 12f, 200f);
            Assert.That(root.style.paddingBottom.value.value, Is.EqualTo(200f));

            SafeAreaUtility.Apply(root, safeArea, 1000, 600, 12f, 50f);
            Assert.That(root.style.paddingBottom.value.value, Is.EqualTo(72f));
        }

        [Test]
        public void SoftKeyboardInsetConvertsKeyboardHeightToPanelUnits()
        {
            var inset = SoftKeyboardInset.ToPanelUnits(true, new Rect(0f, 0f, 1000f, 400f), 1000, 500f);

            Assert.That(inset, Is.EqualTo(200f));
        }

        [Test]
        public void SoftKeyboardInsetReturnsZeroWhenKeyboardIsHidden()
        {
            var inset = SoftKeyboardInset.ToPanelUnits(false, new Rect(0f, 0f, 1000f, 400f), 1000, 500f);

            Assert.That(inset, Is.Zero);
        }

        [Test]
        public void SoftKeyboardInsetUsesFallbackWhenVisibleAreaIsEmpty()
        {
            var inset = SoftKeyboardInset.ToPanelUnits(true, Rect.zero, 1000, 500f);

            Assert.That(inset, Is.EqualTo(200f));
        }

        [Test]
        public void SoftKeyboardInsetFindsTextFieldFromInnerElement()
        {
            var root = new VisualElement();
            var textField = new TextField();
            var innerElement = new VisualElement();
            textField.Add(innerElement);
            root.Add(textField);

            Assert.That(SoftKeyboardInset.FindTextField(innerElement), Is.SameAs(textField));
        }

        private static void DeleteIfExists(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}