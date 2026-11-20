using System;
using System.Collections.Generic;
using Mejnun.Platform;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Mejnun.Localization
{
    // Startup locale selector registered in LocalizationSettings. Order:
    //   1. the player's explicit choice in Settings (LanguagePreference);
    //   2. the platform's languages through IPlatformServices.Language: on Steam
    //      the game language picked in the Steam client, then the Steam UI
    //      language; on iOS/Android/desktop the OS's ordered language list;
    //   3. otherwise null, so the next selector (English) is used.
    [Serializable]
    public class PlatformLocaleSelector : IStartupLocaleSelector
    {
        public Locale GetStartupLocale(ILocalesProvider availableLocales)
        {
            return Resolve(LanguagePreference.Saved, availableLocales?.Locales);
        }

        public static Locale Resolve(string savedCode, IReadOnlyList<Locale> locales)
        {
            if (locales == null || locales.Count == 0)
                return null;

            var codes = new List<string>(locales.Count);
            foreach (var locale in locales)
                codes.Add(locale.Identifier.Code);

            string code = null;
            if (!string.IsNullOrWhiteSpace(savedCode))
                code = LocaleMatcher.MatchOne(savedCode, codes);

            if (code == null)
            {
                // The startup selector can run before the platform's own
                // BeforeSceneLoad hook; start the store backend first so its
                // language wins over the Null backend's device list.
                if (Application.isPlaying)
                    PlatformServices.EnsureInitialized();
                code = LocaleMatcher.Match(PlatformServices.Language?.PreferredLanguageTags, codes);
            }

            code ??= LocaleMatcher.MatchOne(Loc.ProjectLocaleCode, codes);
            if (code == null)
                return null;

            foreach (var locale in locales)
            {
                if (locale.Identifier.Code == code)
                    return locale;
            }

            return null;
        }
    }
}
