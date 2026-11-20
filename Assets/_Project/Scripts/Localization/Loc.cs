using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Mejnun.Localization
{
    // The single entry point for player-facing text set from C#. Every string
    // a player can see comes from a String Table entry: UXML binds entries
    // directly, and code calls Loc.Get / Loc.Format with the entry key.
    // Rules and the key naming scheme are in Docs/Localization.md.
    public static class Loc
    {
        public const string UiTable = "UI";
        public const string DealerQuestionsTable = "DealerQuestions";
        public const string ProjectLocaleCode = "en";

        private static bool s_subscribed;
        private static event Action s_languageChanged;

        // Raised after the selected locale changes. Presenters that set text
        // from code re-render their labels from this event.
        public static event Action LanguageChanged
        {
            add
            {
                EnsureSubscribed();
                s_languageChanged += value;
            }
            remove => s_languageChanged -= value;
        }

        public static string Get(string key) => Resolve(UiTable, key, null);

        // Smart String formatting with positional arguments: "{0}",
        // "{0:plural:{} card|{} cards}". Positional placeholders survive
        // machine translation better than named ones.
        public static string Format(string key, params object[] args) => Resolve(UiTable, key, args);

        public static string GetFrom(string table, string key) => Resolve(table, key, null);

        public static string FormatFrom(string table, string key, params object[] args) => Resolve(table, key, args);

        // Keys of every entry in a table, for content tables such as
        // DealerQuestions where code picks entries rather than naming them.
        public static IReadOnlyList<string> KeysOf(string table)
        {
            var keys = new List<string>();
            if (!LocalizationSettings.HasSettings)
                return keys;

            var stringTable = LocalizationSettings.StringDatabase.GetTable(table, CurrentLocale);
            if (stringTable == null)
                return keys;

            foreach (var entry in stringTable.SharedData.Entries)
                keys.Add(entry.Key);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        public static bool Has(string key) => Has(UiTable, key);

        public static bool Has(string table, string key)
        {
            if (string.IsNullOrEmpty(key) || !LocalizationSettings.HasSettings)
                return false;

            var stringTable = LocalizationSettings.StringDatabase.GetTable(table, CurrentLocale);
            return stringTable != null && stringTable.SharedData.Contains(key);
        }

        public static Locale CurrentLocale
        {
            get
            {
                if (!LocalizationSettings.HasSettings)
                    return null;

                // Outside Play Mode the package returns the Editor preview
                // locale, which is often unset; fall back to English so edit
                // mode tests and UI Builder previews show the source text.
                return LocalizationSettings.SelectedLocale ?? LocalizationSettings.ProjectLocale;
            }
        }

        public static string CurrentLocaleCode => CurrentLocale?.Identifier.Code ?? ProjectLocaleCode;

        public static bool IsRightToLeft
        {
            get
            {
                var culture = CurrentLocale?.Identifier.CultureInfo;
                return culture != null && culture.TextInfo.IsRightToLeft;
            }
        }

        public static IReadOnlyList<Locale> AvailableLocales
        {
            get
            {
                if (!LocalizationSettings.HasSettings)
                    return Array.Empty<Locale>();
                return LocalizationSettings.AvailableLocales.Locales;
            }
        }

        public static IReadOnlyList<string> AvailableLocaleCodes
        {
            get
            {
                var codes = new List<string>();
                foreach (var locale in AvailableLocales)
                    codes.Add(locale.Identifier.Code);
                return codes;
            }
        }

        // The locale's own name for itself ("Deutsch", "日本語", "فارسی"), as
        // shown in the language picker.
        public static string NativeName(Locale locale)
        {
            if (locale == null)
                return string.Empty;

            var culture = locale.Identifier.CultureInfo;
            if (locale.Identifier.Code == "zh-Hans")
                culture = CultureInfo.GetCultureInfo("zh-CN");
            else if (locale.Identifier.Code == "zh-Hant")
                culture = CultureInfo.GetCultureInfo("zh-TW");

            if (culture == null)
                return locale.LocaleName;

            string name = culture.NativeName;
            return string.IsNullOrEmpty(name) ? locale.LocaleName : char.ToUpper(name[0], culture) + name.Substring(1);
        }

        // Saves the player's choice and switches to it. An empty code means
        // "follow the platform" (store language, then device languages).
        public static void SetLanguage(string code)
        {
            LanguagePreference.Saved = code;
            if (!LocalizationSettings.HasSettings)
                return;

            var locale = PlatformLocaleSelector.Resolve(code, AvailableLocales);
            if (locale != null && !ReferenceEquals(locale, LocalizationSettings.SelectedLocale))
                LocalizationSettings.SelectedLocale = locale;
        }

        private static string Resolve(string table, string key, object[] args)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (!LocalizationSettings.HasSettings)
                return key;

            try
            {
                return LocalizationSettings.StringDatabase.GetLocalizedString(table, key, CurrentLocale,
                    FallbackBehavior.UseProjectSettings, args ?? Array.Empty<object>());
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Loc] Could not resolve '{table}/{key}': {exception.Message}");
                return key;
            }
        }

        private static void EnsureSubscribed()
        {
            if (s_subscribed || !LocalizationSettings.HasSettings)
                return;

            s_subscribed = true;
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        }

        private static void OnSelectedLocaleChanged(Locale locale)
        {
            s_languageChanged?.Invoke();
        }

        // Domain reload is disabled on Play entry, so drop the previous
        // session's subscribers.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnEnterPlayMode()
        {
            if (s_subscribed && LocalizationSettings.HasSettings)
                LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
            s_subscribed = false;
            s_languageChanged = null;
        }
    }
}
