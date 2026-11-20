using System;

namespace Mejnun.Platform
{
    // Steam reports languages as its own "API language codes"
    // (https://partner.steamgames.com/doc/store/localization/languages).
    // This map turns them into BCP-47 tags. It lives outside the Steam
    // assembly so it can be tested without MEJNUN_STEAM.
    public static class SteamLanguageCodes
    {
        public static string ToLanguageTag(string steamApiLanguage)
        {
            if (string.IsNullOrWhiteSpace(steamApiLanguage))
                return null;

            switch (steamApiLanguage.Trim().ToLowerInvariant())
            {
                case "arabic": return "ar";
                case "bulgarian": return "bg";
                case "schinese": return "zh-Hans";
                case "tchinese": return "zh-Hant";
                case "czech": return "cs";
                case "danish": return "da";
                case "dutch": return "nl";
                case "english": return "en";
                case "finnish": return "fi";
                case "french": return "fr";
                case "german": return "de";
                case "greek": return "el";
                case "hungarian": return "hu";
                case "indonesian": return "id";
                case "italian": return "it";
                case "japanese": return "ja";
                case "koreana": return "ko";
                case "norwegian": return "no";
                case "polish": return "pl";
                case "portuguese": return "pt-PT";
                case "brazilian": return "pt-BR";
                case "romanian": return "ro";
                case "russian": return "ru";
                case "spanish": return "es-ES";
                case "latam": return "es-419";
                case "swedish": return "sv";
                case "thai": return "th";
                case "turkish": return "tr";
                case "ukrainian": return "uk";
                case "vietnamese": return "vi";
                default: return null;
            }
        }

        // Steam has no Persian, Armenian, Hindi or Swahili, so those players
        // can only reach their language through the device list or picker.
        public static bool IsSupportedBySteam(string languageTag)
        {
            if (string.IsNullOrEmpty(languageTag))
                return false;

            string baseLanguage = languageTag.Split('-')[0];
            return !string.Equals(baseLanguage, "fa", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(baseLanguage, "hy", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(baseLanguage, "hi", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(baseLanguage, "sw", StringComparison.OrdinalIgnoreCase);
        }
    }
}
