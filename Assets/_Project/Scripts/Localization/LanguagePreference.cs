using UnityEngine;

namespace Mejnun.Localization
{
    // The player's explicit language choice from Settings. Empty means
    // "follow the platform". GameSettings.Language reads and writes the same key.
    public static class LanguagePreference
    {
        public const string PlayerPrefsKey = "settings.language";
        public const string FollowPlatform = "";

        public static string Saved
        {
            get => PlayerPrefs.GetString(PlayerPrefsKey, FollowPlatform);
            set
            {
                PlayerPrefs.SetString(PlayerPrefsKey, value?.Trim() ?? FollowPlatform);
                PlayerPrefs.Save();
            }
        }
    }
}
