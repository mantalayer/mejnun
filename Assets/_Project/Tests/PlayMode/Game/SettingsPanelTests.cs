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
        }
    }
}