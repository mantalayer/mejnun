using System;
using UnityEngine;

namespace Mejnun.Game
{
    public static class CrashReportingProbe
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RunDevelopmentProbe()
        {
#if DEVELOPMENT_BUILD
            var args = Environment.GetCommandLineArgs();
            if (Contains(args, "-mejnunTestException"))
            {
                try
                {
                    throw new Exception("Mejnun Cloud Diagnostics test exception.");
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            if (Contains(args, "-mejnunTestCrash"))
                UnityEngine.Diagnostics.Utils.ForceCrash(UnityEngine.Diagnostics.ForcedCrashCategory.AccessViolation);
#endif
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Mejnun/Diagnostics/Send Test Exception")]
        private static void SendTestException()
        {
            Debug.LogException(new Exception("Mejnun Cloud Diagnostics test exception."));
        }
#endif

        private static bool Contains(string[] args, string value)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, value, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}