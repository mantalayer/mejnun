using UnityEngine;

namespace Mejnun.Game
{
    public static class MobilePerformance
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureMobileFrameRate()
        {
            FrameRateGovernor.Initialize();
        }
    }
}