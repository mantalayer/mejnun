using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Mejnun.Localization.Editor
{
    public static class HandwrittenTranslationApplier
    {
        public const string CsvPath = "Assets/_Project/Localization/HandwrittenTranslations.csv";

        public sealed class Translation
        {
            public string Table;
            public string Key;
            public string Locale;
            public string Text;
            public string Comment;
        }

        [MenuItem("Mejnun/Localization/Apply Handwritten Translations", priority = 12)]
        private static void ApplyFromMenu()
        {
            int changed = ApplyAll();
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"[Localization] Applied {changed} handwritten translations.");
        }

        public static void ApplyFromCommandLine()
        {
            int changed = ApplyAll();
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"[Localization] Applied {changed} handwritten translations.");
        }

        public static IReadOnlyList<Translation> ReadTranslations()
        {
            if (!File.Exists(CsvPath))
                return Array.Empty<Translation>();

            var records = LocalizationSourceImporter.ParseRecords(File.ReadAllText(CsvPath));
            if (records.Count == 0)
                return Array.Empty<Translation>();

            var header = records[0];
            int tableIndex = header.IndexOf("table");
            int keyIndex = header.IndexOf("key");
            int localeIndex = header.IndexOf("locale");
            int textIndex = header.IndexOf("text");
            int commentIndex = header.IndexOf("comment");
            if (tableIndex < 0 || keyIndex < 0 || localeIndex < 0 || textIndex < 0)
                throw new FormatException("The handwritten translation CSV must contain table,key,locale,text (and optionally comment).");

            var translations = new List<Translation>();
            for (int row = 1; row < records.Count; row++)
            {
                var record = records[row];
                if (record.Count == 1 && string.IsNullOrWhiteSpace(record[0]))
                    continue;

                translations.Add(new Translation
                {
                    Table = Field(record, tableIndex).Trim(),
                    Key = Field(record, keyIndex).Trim(),
                    Locale = Field(record, localeIndex).Trim(),
                    Text = Field(record, textIndex),
                    Comment = commentIndex >= 0 ? Field(record, commentIndex) : null,
                });
            }

            return translations;
        }

        public static IReadOnlyList<Translation> ForTable(string table)
        {
            var translations = new List<Translation>();
            foreach (var translation in ReadTranslations())
            {
                if (translation.Table == table)
                    translations.Add(translation);
            }

            return translations;
        }

        public static int ApplyAll()
        {
            int changed = 0;
            var applied = new HashSet<string>(StringComparer.Ordinal);
            foreach (var translation in ReadTranslations())
            {
                string identity = translation.Table + "\0" + translation.Key + "\0" + translation.Locale;
                if (!applied.Add(identity))
                    throw new FormatException($"Duplicate handwritten translation for {translation.Table}/{translation.Key} [{translation.Locale}].");

                changed += Apply(translation);
            }

            return changed;
        }

        public static int ApplyCollection(string table)
        {
            int changed = 0;
            foreach (var translation in ForTable(table))
                changed += Apply(translation);
            AssetDatabase.SaveAssets();
            return changed;
        }

        private static int Apply(Translation translation)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(translation.Table);
            var localeTable = collection?.GetTable(translation.Locale) as StringTable;
            var shared = collection?.SharedData.GetEntry(translation.Key);
            if (localeTable == null || shared == null)
                throw new InvalidOperationException($"Cannot apply handwritten translation for {translation.Table}/{translation.Key} [{translation.Locale}].");

            var entry = localeTable.GetEntry(shared.Id);
            if (entry == null)
                entry = localeTable.AddEntry(shared.Id, translation.Text);
            else if (entry.Value != translation.Text)
                entry.Value = translation.Text;
            else
                return 0;

            var sourceTable = collection.GetTable(Loc.ProjectLocaleCode) as StringTable;
            entry.IsSmart = LocalizationValidator.IsSmart(sourceTable?.GetEntry(shared.Id)?.Value);
            EditorUtility.SetDirty(localeTable);
            return 1;
        }

        private static string Field(List<string> record, int index) => index >= 0 && index < record.Count ? record[index] : string.Empty;
    }
}