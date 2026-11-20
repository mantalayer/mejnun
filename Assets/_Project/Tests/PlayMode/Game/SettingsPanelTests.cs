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

                Assert.That(root.Q<VisualElement>("settings-page-" + selectedPage).style.display,
                    Is.EqualTo(DisplayStyle.Flex));
                Assert.That(gameplayPage.style.display, Is.EqualTo(DisplayStyle.None));
                Assert.That(rulesPage.style.display,
                    Is.EqualTo(selectedPage == "rules" ? DisplayStyle.Flex : DisplayStyle.None));
                Assert.That(creditsPage.style.display,
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