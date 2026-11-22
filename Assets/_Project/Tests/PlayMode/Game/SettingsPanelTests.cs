using System.Collections.Generic;
using Mejnun.Audio;
using Mejnun.Game;
using Mejnun.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.Game.Tests
{
    public sealed class SettingsPanelTests
    {
        [TestCase(400f, true)]
        [TestCase(SettingsPanel.CompactWidthThreshold, true)]
        [TestCase(600f, false)]
        [TestCase(0f, false)]
        [TestCase(float.NaN, false)]
        public void IsCompactWidthReflectsAvailableWidth(float width, bool expected)
        {
            Assert.That(SettingsPanel.IsCompactWidth(width), Is.EqualTo(expected));
        }

        [Test]
        public void SettingsChangesStayInSyncAcrossPanelInstances()
        {
            var originalVolume = GameSettings.VolumeMaster;
            var firstRoot = CreateVolumePanel(out var firstSlider);
            var secondRoot = CreateVolumePanel(out var secondSlider);
            var firstPanel = new SettingsPanel();
            var secondPanel = new SettingsPanel();

            try
            {
                firstPanel.Initialize(firstRoot);
                secondPanel.Initialize(secondRoot);
                GameSettings.VolumeMaster = 0.37f;

                Assert.That(GameSettings.VolumeMaster, Is.EqualTo(0.37f).Within(0.001f));
                Assert.That(firstSlider.value, Is.EqualTo(0.37f).Within(0.001f));
                Assert.That(secondSlider.value, Is.EqualTo(0.37f).Within(0.001f));
            }
            finally
            {
                firstPanel.Dispose();
                secondPanel.Dispose();
                GameSettings.VolumeMaster = originalVolume;
            }
        }

        [Test]
        public void HideOpponentHandsDefaultsToMobilePlatform()
        {
            var hadSetting = PlayerPrefs.HasKey(GameSettings.HideOpponentHandsKey);
            var previousValue = PlayerPrefs.GetInt(GameSettings.HideOpponentHandsKey,
                DeviceProfile.IsMobilePlatform ? 1 : 0);
            try
            {
                PlayerPrefs.DeleteKey(GameSettings.HideOpponentHandsKey);
                Assert.That(GameSettings.HideOpponentHands, Is.EqualTo(DeviceProfile.IsMobilePlatform));
            }
            finally
            {
                if (hadSetting)
                {
                    PlayerPrefs.SetInt(GameSettings.HideOpponentHandsKey, previousValue);
                }
                else
                {
                    PlayerPrefs.DeleteKey(GameSettings.HideOpponentHandsKey);
                }

                PlayerPrefs.Save();
            }
        }

        [Test]
        public void HideOpponentHandsToggleIsBoundToSetting()
        {
            var hadSetting = PlayerPrefs.HasKey(GameSettings.HideOpponentHandsKey);
            var previousValue = PlayerPrefs.GetInt(GameSettings.HideOpponentHandsKey,
                DeviceProfile.IsMobilePlatform ? 1 : 0);
            var root = new VisualElement();
            var toggle = new Toggle { name = "hide-opponent-hands-toggle" };
            root.Add(toggle);
            var document = AttachToRuntimePanel(root);
            var panel = new SettingsPanel();

            try
            {
                panel.Initialize(root);
                Assert.That(toggle.value, Is.EqualTo(GameSettings.HideOpponentHands));

                toggle.value = !toggle.value;

                Assert.That(GameSettings.HideOpponentHands, Is.EqualTo(toggle.value));
            }
            finally
            {
                panel.Dispose();
                DestroyRuntimePanel(document);
                if (hadSetting)
                {
                    PlayerPrefs.SetInt(GameSettings.HideOpponentHandsKey, previousValue);
                }
                else
                {
                    PlayerPrefs.DeleteKey(GameSettings.HideOpponentHandsKey);
                }

                PlayerPrefs.Save();
            }
        }

        [Test]
        public void EdgePanWidthSliderIsBoundAndSettingIsClamped()
        {
            var hadSetting = PlayerPrefs.HasKey(GameSettings.EdgePanWidthKey);
            var previousValue = PlayerPrefs.GetFloat(GameSettings.EdgePanWidthKey,
                GameSettings.DefaultEdgePanWidth);
            var root = new VisualElement();
            var slider = new Slider { name = "edge-pan-width-slider", lowValue = 0f, highValue = 0.25f };
            root.Add(slider);
            var document = AttachToRuntimePanel(root);
            var panel = new SettingsPanel();

            try
            {
                GameSettings.EdgePanWidth = 0.1f;
                panel.Initialize(root);
                Assert.That(slider.value, Is.EqualTo(0.1f).Within(0.001f));

                slider.value = 0.2f;
                Assert.That(GameSettings.EdgePanWidth, Is.EqualTo(0.2f).Within(0.001f));

                GameSettings.EdgePanWidth = -0.1f;
                Assert.That(GameSettings.EdgePanWidth, Is.EqualTo(0f));
                GameSettings.EdgePanWidth = 0.5f;
                Assert.That(GameSettings.EdgePanWidth, Is.EqualTo(0.25f));
            }
            finally
            {
                panel.Dispose();
                DestroyRuntimePanel(document);
                if (hadSetting)
                {
                    PlayerPrefs.SetFloat(GameSettings.EdgePanWidthKey, previousValue);
                }
                else
                {
                    PlayerPrefs.DeleteKey(GameSettings.EdgePanWidthKey);
                }

                PlayerPrefs.Save();
            }
        }

        [Test]
        public void EdgeScrollWidthSliderIsBoundAndSettingIsClamped()
        {
            var hadSetting = PlayerPrefs.HasKey(GameSettings.EdgeScrollWidthKey);
            var previousValue = PlayerPrefs.GetFloat(GameSettings.EdgeScrollWidthKey,
                GameSettings.DefaultEdgeScrollWidth);
            var root = new VisualElement();
            var slider = new Slider { name = "edge-scroll-width-slider", lowValue = 0f, highValue = 0.25f };
            root.Add(slider);
            var document = AttachToRuntimePanel(root);
            var panel = new SettingsPanel();

            try
            {
                GameSettings.EdgeScrollWidth = 0.1f;
                panel.Initialize(root);
                Assert.That(slider.value, Is.EqualTo(0.1f).Within(0.001f));

                slider.value = 0.2f;
                Assert.That(GameSettings.EdgeScrollWidth, Is.EqualTo(0.2f).Within(0.001f));

                GameSettings.EdgeScrollWidth = -0.1f;
                Assert.That(GameSettings.EdgeScrollWidth, Is.EqualTo(0f));
                GameSettings.EdgeScrollWidth = 0.5f;
                Assert.That(GameSettings.EdgeScrollWidth, Is.EqualTo(0.25f));
            }
            finally
            {
                panel.Dispose();
                DestroyRuntimePanel(document);
                RestoreFloatSetting(GameSettings.EdgeScrollWidthKey, hadSetting, previousValue);
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void LongPressSliderUsesScriptableDefaultAndClampsSetting()
        {
            var previousDefaults = SettingsDefaults.Active;
            var defaults = ScriptableObject.CreateInstance<SettingsDefaults>();
            defaults.LongPressSeconds = 0.22f;
            var hadSetting = PlayerPrefs.HasKey(GameSettings.LongPressSecondsKey);
            var previousValue = PlayerPrefs.GetFloat(GameSettings.LongPressSecondsKey, 0.35f);
            var root = new VisualElement();
            var slider = new Slider { name = "long-press-seconds-slider", lowValue = 0.1f, highValue = 0.8f };
            root.Add(slider);
            var document = AttachToRuntimePanel(root);
            var panel = new SettingsPanel();

            try
            {
                SettingsDefaults.Active = defaults;
                PlayerPrefs.DeleteKey(GameSettings.LongPressSecondsKey);
                panel.Initialize(root);
                Assert.That(slider.value, Is.EqualTo(0.22f).Within(0.001f));

                slider.value = 0.25f;
                Assert.That(GameSettings.LongPressSeconds, Is.EqualTo(0.25f).Within(0.001f));

                GameSettings.LongPressSeconds = 0f;
                Assert.That(GameSettings.LongPressSeconds, Is.EqualTo(0.1f));
                GameSettings.LongPressSeconds = 1f;
                Assert.That(GameSettings.LongPressSeconds, Is.EqualTo(0.8f));
            }
            finally
            {
                panel.Dispose();
                DestroyRuntimePanel(document);
                RestoreFloatSetting(GameSettings.LongPressSecondsKey, hadSetting, previousValue);
                SettingsDefaults.Active = previousDefaults;
                Object.DestroyImmediate(defaults);
            }
        }

        [Test]
        public void PanStartSpeedSliderUsesScriptableDefaultAndClampsSetting()
        {
            var previousDefaults = SettingsDefaults.Active;
            var defaults = ScriptableObject.CreateInstance<SettingsDefaults>();
            defaults.TouchPanStartSpeed = 230f;
            var hadSetting = PlayerPrefs.HasKey(GameSettings.TouchPanStartSpeedKey);
            var previousValue = PlayerPrefs.GetFloat(GameSettings.TouchPanStartSpeedKey, 150f);
            var root = new VisualElement();
            var slider = new Slider { name = "pan-start-speed-slider", lowValue = 50f, highValue = 400f };
            root.Add(slider);
            var document = AttachToRuntimePanel(root);
            var panel = new SettingsPanel();

            try
            {
                SettingsDefaults.Active = defaults;
                PlayerPrefs.DeleteKey(GameSettings.TouchPanStartSpeedKey);
                panel.Initialize(root);
                Assert.That(slider.value, Is.EqualTo(230f).Within(0.001f));

                slider.value = 325f;
                Assert.That(GameSettings.TouchPanStartSpeed, Is.EqualTo(325f).Within(0.001f));

                GameSettings.TouchPanStartSpeed = 0f;
                Assert.That(GameSettings.TouchPanStartSpeed, Is.EqualTo(50f));
                GameSettings.TouchPanStartSpeed = 500f;
                Assert.That(GameSettings.TouchPanStartSpeed, Is.EqualTo(400f));
            }
            finally
            {
                panel.Dispose();
                DestroyRuntimePanel(document);
                RestoreFloatSetting(GameSettings.TouchPanStartSpeedKey, hadSetting, previousValue);
                SettingsDefaults.Active = previousDefaults;
                Object.DestroyImmediate(defaults);
            }
        }

        [Test]
        public void ResetButtonRestoresScriptableDefaultsAndKeepsPersonalSettings()
        {
            const string languageKey = "settings.language";
            const string nicknameKey = "settings.online.nickname";
            const string tutorialKey = "settings.tutorial.completed";
            var previousDefaults = SettingsDefaults.Active;
            var defaults = ScriptableObject.CreateInstance<SettingsDefaults>();
            defaults.ConfirmRevert = false;
            defaults.EdgePanWidthMobile = 0.17f;
            defaults.EdgePanWidthDesktop = 0.17f;
            defaults.EdgeScrollWidthMobile = 0.12f;
            defaults.EdgeScrollWidthDesktop = 0.12f;
            defaults.LongPressSeconds = 0.23f;
            defaults.TouchPanStartSpeed = 220f;
            var hadConfirmRevert = PlayerPrefs.HasKey("settings.confirm.revert");
            var previousConfirmRevert = PlayerPrefs.GetInt("settings.confirm.revert");
            var hadEdgePan = PlayerPrefs.HasKey(GameSettings.EdgePanWidthKey);
            var previousEdgePan = PlayerPrefs.GetFloat(GameSettings.EdgePanWidthKey, 0.08f);
            var hadEdgeScroll = PlayerPrefs.HasKey(GameSettings.EdgeScrollWidthKey);
            var previousEdgeScroll = PlayerPrefs.GetFloat(GameSettings.EdgeScrollWidthKey, 0.08f);
            var hadLongPress = PlayerPrefs.HasKey(GameSettings.LongPressSecondsKey);
            var previousLongPress = PlayerPrefs.GetFloat(GameSettings.LongPressSecondsKey, 0.35f);
            var hadPanStartSpeed = PlayerPrefs.HasKey(GameSettings.TouchPanStartSpeedKey);
            var previousPanStartSpeed = PlayerPrefs.GetFloat(GameSettings.TouchPanStartSpeedKey, 150f);
            var hadLanguage = PlayerPrefs.HasKey(languageKey);
            var previousLanguage = PlayerPrefs.GetString(languageKey);
            var hadNickname = PlayerPrefs.HasKey(nicknameKey);
            var previousNickname = PlayerPrefs.GetString(nicknameKey);
            var hadTutorial = PlayerPrefs.HasKey(tutorialKey);
            var previousTutorial = PlayerPrefs.GetInt(tutorialKey);
            var root = new VisualElement();
            var toggle = new Toggle { name = "confirm-revert-toggle" };
            var slider = new Slider { name = "long-press-seconds-slider", lowValue = 0.1f, highValue = 0.8f };
            var panStartSpeedSlider = new Slider
                { name = "pan-start-speed-slider", lowValue = 50f, highValue = 400f };
            var resetButton = new Button { name = "settings-reset-button" };
            root.Add(toggle);
            root.Add(slider);
            root.Add(panStartSpeedSlider);
            root.Add(resetButton);
            var document = AttachToRuntimePanel(root);
            var panel = new SettingsPanel();

            try
            {
                SettingsDefaults.Active = defaults;
                panel.Initialize(root);
                GameSettings.ConfirmRevert = true;
                GameSettings.EdgePanWidth = 0.2f;
                GameSettings.EdgeScrollWidth = 0.22f;
                GameSettings.LongPressSeconds = 0.3f;
                GameSettings.TouchPanStartSpeed = 300f;
                GameSettings.Language = "pt-BR";
                GameSettings.OnlineNickname = "Player";
                GameSettings.TutorialCompleted = true;

                panel.ResetToDefaults();

                Assert.That(GameSettings.ConfirmRevert, Is.False);
                Assert.That(GameSettings.EdgePanWidth, Is.EqualTo(0.17f).Within(0.001f));
                Assert.That(GameSettings.EdgeScrollWidth, Is.EqualTo(0.12f).Within(0.001f));
                Assert.That(GameSettings.EdgePanWidth, Is.Not.EqualTo(GameSettings.EdgeScrollWidth));
                Assert.That(GameSettings.LongPressSeconds, Is.EqualTo(0.23f).Within(0.001f));
                Assert.That(GameSettings.TouchPanStartSpeed, Is.EqualTo(220f).Within(0.001f));
                Assert.That(toggle.value, Is.False);
                Assert.That(slider.value, Is.EqualTo(0.23f).Within(0.001f));
                Assert.That(panStartSpeedSlider.value, Is.EqualTo(220f).Within(0.001f));
                Assert.That(GameSettings.Language, Is.EqualTo("pt-BR"));
                Assert.That(GameSettings.OnlineNickname, Is.EqualTo("Player"));
                Assert.That(GameSettings.TutorialCompleted, Is.True);
            }
            finally
            {
                panel.Dispose();
                DestroyRuntimePanel(document);
                RestoreIntSetting("settings.confirm.revert", hadConfirmRevert, previousConfirmRevert);
                RestoreFloatSetting(GameSettings.EdgePanWidthKey, hadEdgePan, previousEdgePan);
                RestoreFloatSetting(GameSettings.EdgeScrollWidthKey, hadEdgeScroll, previousEdgeScroll);
                RestoreFloatSetting(GameSettings.LongPressSecondsKey, hadLongPress, previousLongPress);
                RestoreFloatSetting(GameSettings.TouchPanStartSpeedKey, hadPanStartSpeed, previousPanStartSpeed);
                RestoreStringSetting(languageKey, hadLanguage, previousLanguage);
                RestoreStringSetting(nicknameKey, hadNickname, previousNickname);
                RestoreIntSetting(tutorialKey, hadTutorial, previousTutorial);
                PlayerPrefs.Save();
                SettingsDefaults.Active = previousDefaults;
                Object.DestroyImmediate(defaults);
            }
        }

        [TestCase("rules")]
        [TestCase("credits")]
        public void ShowingInformationPageHidesOthersAndSelectsItsTab(string selectedPage)
        {
            var root = new VisualElement();
            var gameplayTab = new Button { name = "settings-tab-gameplay" };
            var rulesTab = new Button { name = "settings-tab-rules" };
            var creditsTab = new Button { name = "settings-tab-credits" };
            var gameplayPage = new VisualElement { name = "settings-page-gameplay" };
            var rulesPage = new VisualElement { name = "settings-page-rules" };
            var creditsPage = new VisualElement { name = "settings-page-credits" };
            root.Add(gameplayTab);
            root.Add(rulesTab);
            root.Add(creditsTab);
            root.Add(gameplayPage);
            root.Add(rulesPage);
            root.Add(creditsPage);
            var panel = new SettingsPanel();

            try
            {
                panel.Initialize(root);
                panel.ShowPage(selectedPage);

                Assert.That(root.Q<VisualElement>("settings-page-" + selectedPage).style.display.value,
                    Is.EqualTo(DisplayStyle.Flex));
                Assert.That(gameplayPage.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(rulesPage.style.display.value,
                    Is.EqualTo(selectedPage == "rules" ? DisplayStyle.Flex : DisplayStyle.None));
                Assert.That(creditsPage.style.display.value,
                    Is.EqualTo(selectedPage == "credits" ? DisplayStyle.Flex : DisplayStyle.None));
                Assert.That(root.Q<Button>("settings-tab-" + selectedPage).ClassListContains("selected-tab"),
                    Is.True);
            }
            finally
            {
                panel.Dispose();
            }
        }

        [Test]
        public void UiScaleClampsToSupportedRange()
        {
            var originalScale = GameSettings.UiScale;
            try
            {
                GameSettings.UiScale = 0f;
                Assert.That(GameSettings.UiScale, Is.EqualTo(0.8f));

                GameSettings.UiScale = 3f;
                Assert.That(GameSettings.UiScale, Is.EqualTo(3f));
            }
            finally
            {
                GameSettings.UiScale = originalScale;
            }
        }

        [Test]
        public void AudioVolumesMapFromZeroToOneIntoWwiseRtpcs()
        {
            var originalMaster = GameSettings.VolumeMaster;
            var originalMusic = GameSettings.VolumeMusic;
            var originalSfx = GameSettings.VolumeSfx;
            var originalVoice = GameSettings.VolumeVoice;
            var gameObject = new GameObject("Audio Director Volume Test");
            var director = gameObject.AddComponent<AudioDirector>();
            var audioService = new RecordingAudioService();

            try
            {
                director.ConfigureAudioService(audioService);
                GameSettings.VolumeMaster = 0.25f;
                GameSettings.VolumeMusic = 0.5f;
                GameSettings.VolumeSfx = 0.75f;
                GameSettings.VolumeVoice = 1f;

                Assert.That(audioService.Values[AudioIds.VolumeMaster], Is.EqualTo(25f));
                Assert.That(audioService.Values[AudioIds.VolumeMusic], Is.EqualTo(50f));
                Assert.That(audioService.Values[AudioIds.VolumeSfx], Is.EqualTo(75f));
                Assert.That(audioService.Values[AudioIds.VolumeVoice], Is.EqualTo(100f));
            }
            finally
            {
                GameSettings.VolumeMaster = originalMaster;
                GameSettings.VolumeMusic = originalMusic;
                GameSettings.VolumeSfx = originalSfx;
                GameSettings.VolumeVoice = originalVoice;
                Object.DestroyImmediate(gameObject);
            }
        }

        private static VisualElement CreateVolumePanel(out Slider slider)
        {
            var root = new VisualElement();
            slider = new Slider { name = "volume-master-slider" };
            root.Add(slider);
            return root;
        }

        private static UIDocument AttachToRuntimePanel(VisualElement root)
        {
            var gameObject = new GameObject("Settings Panel Test Document");
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            document.rootVisualElement.Add(root);
            return document;
        }

        private static void DestroyRuntimePanel(UIDocument document)
        {
            var panelSettings = document.panelSettings;
            Object.DestroyImmediate(document.gameObject);
            Object.DestroyImmediate(panelSettings);
        }

        private static void RestoreFloatSetting(string key, bool hadSetting, float value)
        {
            if (hadSetting)
            {
                PlayerPrefs.SetFloat(key, value);
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }
        }

        private static void RestoreIntSetting(string key, bool hadSetting, int value)
        {
            if (hadSetting)
            {
                PlayerPrefs.SetInt(key, value);
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }
        }

        private static void RestoreStringSetting(string key, bool hadSetting, string value)
        {
            if (hadSetting)
            {
                PlayerPrefs.SetString(key, value);
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }
        }

        private sealed class RecordingAudioService : IAudioService
        {
            public readonly Dictionary<string, float> Values = new Dictionary<string, float>();

            public void Post(string eventName)
            {
            }

            public void SetState(string stateGroup, string state)
            {
            }

            public void SetSwitch(string switchGroup, string switchValue)
            {
            }

            public void SetRtpc(string rtpcName, float value)
            {
                Values[rtpcName] = value;
            }

            public bool LoadBank(string bankName)
            {
                return true;
            }

            public bool IsInitialized => true;

            public string LoadBankResult(string bankName)
            {
                return "AK_Success";
            }
        }
    }
}