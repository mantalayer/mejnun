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