using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization.Plugins.Google;
using UnityEngine;

namespace Mejnun.Localization.Editor
{
    // Project configuration for the translation pipeline (Docs/Localization.md).
    // Lives at Assets/_Project/Localization/LocalizationPipelineSettings.asset.
    public sealed class LocalizationPipelineSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/_Project/Localization/LocalizationPipelineSettings.asset";

        [Serializable]
        public sealed class LocaleColumn
        {
            [Tooltip("Unity locale code, for example 'zh-Hans'.")]
            public string LocaleCode;

            [Tooltip("Language code passed to GOOGLETRANSLATE, for example 'zh-CN'.")]
            public string GoogleLanguageCode;
        }

        [Serializable]
        public sealed class SheetTab
        {
            [Tooltip("String Table collection name, for example 'UI'.")]
            public string Collection;

            [Tooltip("Tab (sheet) name inside the workbook.")]
            public string TabName;
        }

        [Header("Google Sheets")]
        [Tooltip("Unity Localization's Sheets Service Provider asset holding the OAuth desktop client (plan §5.5).")]
        public SheetsServiceProvider SheetsServiceProvider;

        [Tooltip("The workbook ID from its URL: docs.google.com/spreadsheets/d/<ID>/edit")]
        public string SpreadsheetId = string.Empty;

        [Tooltip("Workbook ID used only for importing popup lines and dealer questions.")]
        public string PopupSpreadsheetId = "1kEKMuh_SyPiWCqKREpXMf1fXh6f3QmqY0rXrHE-U9j4";

        public List<SheetTab> Tabs = new()
        {
            new SheetTab { Collection = Loc.UiTable, TabName = Loc.UiTable },
            new SheetTab { Collection = Loc.DealerQuestionsTable, TabName = Loc.DealerQuestionsTable },
        };

        [Tooltip("Target locales and their GOOGLETRANSLATE codes. English is the source and is not listed.")]
        public List<LocaleColumn> TargetLocales = DefaultTargetLocales();

        [Header("Build")]
        [Tooltip("Before every player build: validate, and when anything is missing push new source text, wait for GOOGLETRANSLATE, then pull.")]
        public bool SyncBeforeBuild = true;

        [Tooltip("Off: missing or broken translations are reported as warnings only (English is used at run time).")]
        public bool FailBuildOnIssues;

        [Tooltip("How long to wait for GOOGLETRANSLATE formulas to finish after a push.")]
        [Min(5)]
        public int TranslationWaitSeconds = 120;

        public bool IsSheetsConfigured(out string reason)
        {
            if (SheetsServiceProvider == null)
            {
                reason = "no Sheets Service Provider is assigned";
                return false;
            }

            if (string.IsNullOrEmpty(SheetsServiceProvider.ClientId) || string.IsNullOrEmpty(SheetsServiceProvider.ClientSecret))
            {
                reason = $"'{SheetsServiceProvider.name}' has no OAuth client ID/secret";
                return false;
            }

            if (string.IsNullOrWhiteSpace(SpreadsheetId))
            {
                reason = "no spreadsheet ID is set";
                return false;
            }

            reason = null;
            return true;
        }

        public bool IsPopupSheetConfigured(out string reason)
        {
            if (SheetsServiceProvider == null)
            {
                reason = "no Sheets Service Provider is assigned";
                return false;
            }

            if (string.IsNullOrEmpty(SheetsServiceProvider.ClientId) || string.IsNullOrEmpty(SheetsServiceProvider.ClientSecret))
            {
                reason = $"'{SheetsServiceProvider.name}' has no OAuth client ID/secret";
                return false;
            }

            if (string.IsNullOrWhiteSpace(PopupSpreadsheetId))
            {
                reason = "no popup/questions spreadsheet ID is set";
                return false;
            }

            reason = null;
            return true;
        }

        public string TabFor(string collection)
        {
            foreach (var tab in Tabs)
            {
                if (tab.Collection == collection)
                    return string.IsNullOrWhiteSpace(tab.TabName) ? collection : tab.TabName;
            }

            return null;
        }

        public static List<LocaleColumn> DefaultTargetLocales()
        {
            var list = new List<LocaleColumn>();
            foreach (var (code, google) in LocalizationProjectSetup.TargetLocales)
                list.Add(new LocaleColumn { LocaleCode = code, GoogleLanguageCode = google });
            return list;
        }

        public static LocalizationPipelineSettings Load()
        {
            return AssetDatabase.LoadAssetAtPath<LocalizationPipelineSettings>(AssetPath);
        }

        public static LocalizationPipelineSettings LoadOrCreate()
        {
            var settings = Load();
            if (settings != null)
                return settings;

            settings = CreateInstance<LocalizationPipelineSettings>();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AssetPath)!);
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}
