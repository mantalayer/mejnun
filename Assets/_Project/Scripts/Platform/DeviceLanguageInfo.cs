using System;
using System.Collections.Generic;
using System.Globalization;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace Mejnun.Platform
{
    // Reads the operating system's ordered language list through the native
    // API of each platform. Application.systemLanguage alone is not enough:
    // it has no Persian or Armenian values and cannot tell regional variants
    // (pt-BR vs pt-PT, zh-Hans vs zh-Hant) apart.
    public sealed class DeviceLanguageInfo : ILanguageInfo
    {
        public static readonly DeviceLanguageInfo Instance = new();

        private IReadOnlyList<string> m_cached;

        public IReadOnlyList<string> PreferredLanguageTags => m_cached ??= Read();

#if UNITY_IOS && !UNITY_EDITOR
        // Assets/_Project/Plugins/iOS/MejnunLocale.mm; returns
        // [NSLocale preferredLanguages] joined with commas.
        [DllImport("__Internal")]
        private static extern string MejnunPreferredLanguages();
#endif

        private static IReadOnlyList<string> Read()
        {
            var tags = new List<string>();
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                AddSeparated(tags, MejnunPreferredLanguages());
#elif UNITY_ANDROID && !UNITY_EDITOR
                using (var localeListClass = new AndroidJavaClass("android.os.LocaleList"))
                using (AndroidJavaObject localeList = localeListClass.CallStatic<AndroidJavaObject>("getDefault"))
                {
                    AddSeparated(tags, localeList.Call<string>("toLanguageTags"));
                }
#endif
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Platform] Could not read the device language list: {exception.Message}");
            }

            Add(tags, CultureInfo.CurrentUICulture?.Name);
            Add(tags, FromSystemLanguage(Application.systemLanguage));
            return tags;
        }

        private static void AddSeparated(List<string> tags, string separated)
        {
            if (string.IsNullOrEmpty(separated))
                return;

            foreach (string tag in separated.Split(','))
                Add(tags, tag);
        }

        private static void Add(List<string> tags, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return;

            string normalized = tag.Trim().Replace('_', '-');
            foreach (string existing in tags)
            {
                if (string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            tags.Add(normalized);
        }

        public static string FromSystemLanguage(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.English: return "en";
                case SystemLanguage.Arabic: return "ar";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Greek: return "el";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified: return "zh-Hans";
                case SystemLanguage.ChineseTraditional: return "zh-Hant";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.Vietnamese: return "vi";
                case SystemLanguage.Hindi: return "hi";
                case SystemLanguage.Thai: return "th";
                case SystemLanguage.Turkish: return "tr";
                case SystemLanguage.Hebrew: return "he";
                case SystemLanguage.Dutch: return "nl";
                case SystemLanguage.Polish: return "pl";
                default: return null;
            }
        }
    }
}
