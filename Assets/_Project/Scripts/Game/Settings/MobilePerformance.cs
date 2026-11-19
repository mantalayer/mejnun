using UnityEngine;

namespace Mejnun.Game
{
    public static class MobilePerformance
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureMobileFrameRate()
        {
            if (!Application.isMobilePlatform)
            {
                return;
            }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Mathf.Max(60,
                Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value));
        }
    }
}