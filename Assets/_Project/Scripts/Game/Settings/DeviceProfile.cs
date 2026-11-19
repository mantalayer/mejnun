using System;
using UnityEngine;

namespace Mejnun.Game
{
    public static class DeviceProfile
    {
        public const string ForceMobileKey = "debug.forceMobile";

        private const float PhoneMaxShortSideInches = 4.5f;
        private const int PhoneMaxShortSidePixels = 1200;
        private const float PhoneMinAspectRatio = 1.9f;
        private static readonly bool ForceMobileFromCommandLine = HasForceMobileArgument();

        public static bool ForceMobile => ForceMobileFromCommandLine || PlayerPrefs.GetInt(ForceMobileKey, 0) != 0;
        public static bool IsMobilePlatform => Application.isMobilePlatform || ForceMobile;
        public static bool IsPhoneLayout => ForceMobile ||
                                            IsPhoneScreen(Application.isMobilePlatform, Screen.width, Screen.height,
                                                Screen.dpi);

        public static void SetForceMobile(bool enabled)
        {
            PlayerPrefs.SetInt(ForceMobileKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static bool IsPhoneScreen(bool isMobilePlatform, int width, int height, float dpi)
        {
            var shortSide = Mathf.Min(width, height);
            var longSide = Mathf.Max(width, height);
            if (shortSide <= 0)
            {
                return false;
            }

            if ((float)longSide / shortSide >= PhoneMinAspectRatio)
            {
                return true;
            }

            if (!isMobilePlatform)
            {
                return false;
            }

            return dpi > 0f ? shortSide / dpi < PhoneMaxShortSideInches : shortSide < PhoneMaxShortSidePixels;
        }

        private static bool HasForceMobileArgument()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], "-force-mobile", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}