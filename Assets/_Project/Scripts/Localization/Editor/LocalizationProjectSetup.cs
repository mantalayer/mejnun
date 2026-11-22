using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;

namespace Mejnun.Localization.Editor
{
    // Creates (or repairs) every localization asset the game needs. Safe to
    // re-run: existing assets and entries are kept.
    //   Menu:  Mejnun > Localization > Set Up Project
    //   Batch: -executeMethod Mejnun.Localization.Editor.LocalizationProjectSetup.RunBatch
    public static class LocalizationProjectSetup
    {
        public const string Root = "Assets/_Project/Localization";
        public const string LocalesFolder = Root + "/Locales";
        public const string TablesFolder = Root + "/Tables";
        public const string SettingsPath = Root + "/LocalizationSettings.asset";
        // The game's name lives only in this entry (English source; translators
        // may transliterate). Other strings use "{app.name}" / "{app.name:upper}",
        // and PlayerSettings.productName is synced from it before every build.
        public const string AppNameKey = AppNameSource.AppNameKey;

        // Unity locale code -> GOOGLETRANSLATE language code (plan §7.1).
        public static readonly (string Code, string Google)[] TargetLocales =
        {
            ("ar", "ar"),
            ("fa", "fa"),
            ("hy", "hy"),
            ("es", "es"),
            ("de", "de"),
            ("el", "el"),
            ("it", "it"),
            ("fr", "fr"),
            ("ja", "ja"),
            ("zh-Hans", "zh-CN"),
            ("pt-BR", "pt"),
            ("ko", "ko"),
            ("zh-Hant", "zh-TW"),
            ("vi", "vi"),
            ("nl", "nl"),
            ("pt-PT", "pt-PT"),
            ("ru", "ru"),
            ("hi", "hi"),
            ("sw", "sw"),
            ("th", "th"),
        };

        public static readonly string[] Collections = { Loc.UiTable, Loc.DealerQuestionsTable };

        [MenuItem("Mejnun/Localization/Set Up Project", priority = 0)]
        public static void Run()
        {
            Directory.CreateDirectory(LocalesFolder);
            Directory.CreateDirectory(TablesFolder);

            var settings = EnsureSettings();
            var english = EnsureLocale(Loc.ProjectLocaleCode);
            var locales = new List<Locale> { english };
            foreach (var (code, _) in TargetLocales)
            {
                var locale = EnsureLocale(code);
                if (locale.Metadata.GetMetadata<FallbackLocale>() == null)
                {
                    locale.Metadata.AddMetadata(new FallbackLocale(english));
                    EditorUtility.SetDirty(locale);
                }

                locales.Add(locale);
            }

            LocalizationSettings.ProjectLocale = english;
            ConfigureSelectors(settings);
            ConfigureStringDatabase(settings);

            foreach (string collectionName in Collections)
                EnsureCollection(collectionName, locales);

            if (string.IsNullOrWhiteSpace(EnglishAppName()))
            {
                LocalizationSourceImporter.Upsert(Loc.UiTable, AppNameKey, PlayerSettings.productName,
                    "The game's name, shown in text, on the home screen and in the store. Keep short.");
            }

            SyncProductName();
            ConfigureAppInfo(settings);

            var pipelineSettings = LocalizationPipelineSettings.LoadOrCreate();
            bool pipelineSettingsChanged = false;
            if (pipelineSettings.TargetLocales == null)
            {
                pipelineSettings.TargetLocales = new List<LocalizationPipelineSettings.LocaleColumn>();
                pipelineSettingsChanged = true;
            }

            foreach (var (code, google) in TargetLocales)
            {
                bool exists = false;
                foreach (var target in pipelineSettings.TargetLocales)
                {
                    if (target != null && target.LocaleCode == code)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    pipelineSettings.TargetLocales.Add(new LocalizationPipelineSettings.LocaleColumn
                    {
                        LocaleCode = code,
                        GoogleLanguageCode = google,
                    });
                    pipelineSettingsChanged = true;
                }
            }

            if (pipelineSettingsChanged)
                EditorUtility.SetDirty(pipelineSettings);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Localization] Project set up: {locales.Count} locales, collections {string.Join(", ", Collections)}.");
        }

        public static void RunBatch()
        {
            Run();
        }

        public static string EnglishAppName()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(Loc.UiTable);
            var english = collection?.GetTable(Loc.ProjectLocaleCode) as UnityEngine.Localization.Tables.StringTable;
            return english?.GetEntry(AppNameKey)?.Value?.Trim();
        }

        // PlayerSettings.productName (desktop window/app title, build file
        // names) follows the English app.name so the name is edited in one place.
        public static bool SyncProductName()
        {
            string name = EnglishAppName();
            if (string.IsNullOrEmpty(name) || PlayerSettings.productName == name)
                return false;

            Debug.Log($"[Localization] Product name '{PlayerSettings.productName}' -> '{name}' (from {Loc.UiTable}/{AppNameKey}).");
            PlayerSettings.productName = name;
            AssetDatabase.SaveAssets();
            return true;
        }

        private static LocalizationSettings EnsureSettings()
        {
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
            {
                settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsPath);
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                    settings.name = "LocalizationSettings";
                    AssetDatabase.CreateAsset(settings, SettingsPath);
                }

                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            }

            return settings;
        }

        private static Locale EnsureLocale(string code)
        {
            foreach (var existing in LocalizationEditorSettings.GetLocales())
            {
                if (existing.Identifier.Code == code)
                    return existing;
            }

            var locale = Locale.CreateLocale(new LocaleIdentifier(code));
            locale.name = $"{locale.Identifier.CultureInfo?.EnglishName ?? code} ({code})";
            AssetDatabase.CreateAsset(locale, $"{LocalesFolder}/{locale.name}.asset");
            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }

        private static void ConfigureSelectors(LocalizationSettings settings)
        {
            var selectors = settings.GetStartupLocaleSelectors();
            selectors.RemoveAll(s => s is CommandLineLocaleSelector || s is PlatformLocaleSelector
                                     || s is SystemLocaleSelector || s is PlayerPrefLocaleSelector
                                     || s is SpecificLocaleSelector);

            // -language=xx on the command line, then the player's choice or the
            // platform's languages, then English.
            selectors.Add(new CommandLineLocaleSelector());
            selectors.Add(new PlatformLocaleSelector());
            selectors.Add(new SpecificLocaleSelector { LocaleId = new LocaleIdentifier(Loc.ProjectLocaleCode) });
        }

        private static void ConfigureStringDatabase(LocalizationSettings settings)
        {
            var database = settings.GetStringDatabase();
            database.MissingTranslationState = MissingTranslationBehavior.ShowMissingTranslationMessage;
            database.NoTranslationFoundMessage = "[{key}]";

            // "{app.name}" and "{app.name:upper}" in Smart Strings.
            var smart = database.SmartFormatter;
            if (!smart.SourceExtensions.Exists(source => source is AppNameSource))
                smart.SourceExtensions.Insert(0, new AppNameSource());
            if (!smart.FormatterExtensions.Exists(formatter => formatter is UpperCaseFormatter))
                smart.FormatterExtensions.Insert(0, new UpperCaseFormatter { Names = new[] { "upper" } });
        }

        private static void EnsureCollection(string name, IList<Locale> locales)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(name)
                             ?? LocalizationEditorSettings.CreateStringTableCollection(name, TablesFolder, locales);

            foreach (var locale in locales)
            {
                if (!collection.ContainsTable(locale.Identifier))
                    collection.AddNewTable(locale.Identifier);
            }

            foreach (var table in collection.StringTables)
            {
                if (!LocalizationEditorSettings.GetPreloadTableFlag(table))
                    LocalizationEditorSettings.SetPreloadTableFlag(table, true);
            }
        }

        private static void ConfigureAppInfo(LocalizationSettings settings)
        {
            var metadata = settings.GetMetadata();
            if (metadata.GetMetadata<UnityEngine.Localization.Platform.iOS.AppInfo>() == null)
            {
                metadata.AddMetadata(new UnityEngine.Localization.Platform.iOS.AppInfo
                {
                    DisplayName = new LocalizedString(Loc.UiTable, AppNameKey),
                    ShortName = new LocalizedString(Loc.UiTable, AppNameKey),
                });
            }

            if (metadata.GetMetadata<UnityEngine.Localization.Platform.Android.AppInfo>() == null)
            {
                metadata.AddMetadata(new UnityEngine.Localization.Platform.Android.AppInfo
                {
                    DisplayName = new LocalizedString(Loc.UiTable, AppNameKey),
                });
            }
        }
    }
}
