using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.SmartFormat.Core.Extensions;

namespace Mejnun.Localization
{
    // Smart String source for the game's name: "{app.name}" resolves to the
    // UI/app.name entry of the locale being formatted, so the name is written
    // once per language and every other string refers to it. Combine with the
    // "upper" formatter for shouted forms: "{app.name:upper}".
    // Registered in LocalizationSettings > String Database > Smart Format > Sources.
    [Serializable]
    public sealed class AppNameSource : ISource
    {
        public const string GroupSelector = "app";
        public const string NameSelector = "name";
        public const string AppNameKey = "app.name";

        private sealed class AppGroup
        {
            public static readonly AppGroup Instance = new();
        }

        public bool TryEvaluateSelector(ISelectorInfo selectorInfo)
        {
            if (selectorInfo.SelectorIndex == 0 && selectorInfo.SelectorText == GroupSelector)
            {
                selectorInfo.Result = AppGroup.Instance;
                return true;
            }

            if (selectorInfo.CurrentValue is AppGroup && selectorInfo.SelectorText == NameSelector)
            {
                selectorInfo.Result = Resolve(LocaleOf(selectorInfo));
                return true;
            }

            return false;
        }

        public static string Resolve(Locale locale)
        {
            if (!LocalizationSettings.HasSettings)
                return string.Empty;

            return LocalizationSettings.StringDatabase.GetLocalizedString(Loc.UiTable, AppNameKey,
                locale ?? Loc.CurrentLocale, FallbackBehavior.UseProjectSettings);
        }

        private static Locale LocaleOf(ISelectorInfo selectorInfo)
        {
            var table = selectorInfo.FormatDetails?.FormatCache?.Table;
            if (table == null || !LocalizationSettings.HasSettings)
                return null;

            return LocalizationSettings.AvailableLocales.GetLocale(table.LocaleIdentifier);
        }
    }
}
