using System.Collections.Generic;
using Mejnun.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public static class UiScaleUtility
    {
        private static readonly Dictionary<PanelSettings, Vector2Int> OriginalResolutions =
            new Dictionary<PanelSettings, Vector2Int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterForApplicationQuit()
        {
            Application.quitting -= RestoreOriginalResolutions;
            Application.quitting += RestoreOriginalResolutions;
        }

        public static void Apply(UIDocument document)
        {
            if (document == null || document.panelSettings == null)
            {
                return;
            }

            var settings = document.panelSettings;
            if (!DeviceProfile.IsPhoneLayout)
            {
                Restore(settings);
                return;
            }

            if (!OriginalResolutions.TryGetValue(settings, out var originalResolution))
            {
                originalResolution = settings.referenceResolution;
                OriginalResolutions.Add(settings, originalResolution);
            }

            var scale = Mathf.Clamp(GameSettings.UiScale, 0.8f, 3f);
            var scaledResolution = new Vector2Int(
                Mathf.Max(1, Mathf.RoundToInt(originalResolution.x / scale)),
                Mathf.Max(1, Mathf.RoundToInt(originalResolution.y / scale)));
            if (settings.referenceResolution != scaledResolution)
            {
                settings.referenceResolution = scaledResolution;
            }
        }

        private static void Restore(PanelSettings settings)
        {
            if (settings != null && OriginalResolutions.TryGetValue(settings, out var resolution))
            {
                settings.referenceResolution = resolution;
            }
        }

        private static void RestoreOriginalResolutions()
        {
            foreach (var original in OriginalResolutions)
            {
                if (original.Key != null)
                {
                    original.Key.referenceResolution = original.Value;
                }
            }

            OriginalResolutions.Clear();
        }
    }
}