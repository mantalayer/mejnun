using UnityEngine;

namespace Mejnun.Game
{
    public static class DisplaySettings
    {
        private const int DefaultWindowWidth = 1280;
        private const int DefaultWindowHeight = 720;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (Application.isEditor || Application.isMobilePlatform)
            {
                return;
            }

            GameSettings.Changed -= OnSettingsChanged;
            GameSettings.Changed += OnSettingsChanged;
            ApplyDisplayMode();
            ApplyVSync();
            ApplyFrameRateLimit();
        }

        public static FullScreenMode ResolveFullScreenMode(int displayMode)
        {
            return displayMode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        public static int ResolveTargetFrameRate(int frameRateLimit)
        {
            switch (Mathf.Clamp(frameRateLimit, 0, 3))
            {
                case 0:
                    return 30;
                case 1:
                    return 60;
                case 2:
                    return 120;
                default:
                    return -1;
            }
        }

        private static void OnSettingsChanged(string key)
        {
            if (key == GameSettings.DisplayModeKey)
            {
                ApplyDisplayMode();
            }
            else if (key == GameSettings.VSyncKey)
            {
                ApplyVSync();
            }
            else if (key == GameSettings.FrameRateLimitKey)
            {
                ApplyFrameRateLimit();
            }
        }

        private static void ApplyDisplayMode()
        {
            var resolution = Screen.currentResolution;
            if (resolution.width <= 0 || resolution.height <= 0)
            {
                return;
            }

            var mode = ResolveFullScreenMode(GameSettings.DisplayMode);
            var width = mode == FullScreenMode.Windowed
                ? Mathf.Min(DefaultWindowWidth, resolution.width)
                : resolution.width;
            var height = mode == FullScreenMode.Windowed
                ? Mathf.Min(DefaultWindowHeight, resolution.height)
                : resolution.height;
            Screen.SetResolution(width, height, mode);
        }

        private static void ApplyVSync()
        {
            QualitySettings.vSyncCount = GameSettings.VSync ? 1 : 0;
        }

        private static void ApplyFrameRateLimit()
        {
            Application.targetFrameRate = ResolveTargetFrameRate(GameSettings.FrameRateLimit);
        }
    }
}