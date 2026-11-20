using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Tables;

namespace Mejnun.Localization.Editor
{
    // Adds or updates English source entries from a CSV file
    // (header: table,key,en,comment). This is the scripted way to add strings;
    // the Localization Tables window is the manual way. Only English is
    // written: translations always come from the Google Sheets pull.
    //   Menu:  Mejnun > Localization > Import English Source CSV...
    //   Batch: -executeMethod Mejnun.Localization.Editor.LocalizationSourceImporter.ImportFromCommandLine
    //          -locSource <file-or-folder> [-locSource ...]
    public static class LocalizationSourceImporter
    {
        public struct Row
        {
            public string Table;
            public string Key;
            public string English;
            public string Comment;
        }

        [MenuItem("Mejnun/Localization/Import English Source CSV...", priority = 20)]
        private static void ImportWithDialog()
        {
            string path = EditorUtility.OpenFilePanel("Import English source strings", Application.dataPath, "csv");
            if (string.IsNullOrEmpty(path))
                return;

            int changed = ImportCsv(path);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Localization", $"{changed} entries added or updated from {Path.GetFileName(path)}.", "OK");
        }

        public static void ImportFromCommandLine()
        {
            var sources = new List<string>();
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-locSource")
                    sources.Add(args[i + 1]);
            }

            if (sources.Count == 0)
                throw new ArgumentException("Pass at least one -locSource <csv file or folder>.");

            int changed = 0;
            foreach (string source in sources)
            {
                if (Directory.Exists(source))
                {
                    foreach (string file in Directory.GetFiles(source, "*.csv"))
                        changed += ImportCsv(file);
                }
                else
                {
                    changed += ImportCsv(source);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Localization] Imported English source: {changed} entries added or updated.");
        }

        public static int ImportCsv(string path)
        {
            int changed = 0;
            foreach (var row in ReadCsv(File.ReadAllText(path, Encoding.UTF8)))
            {
                if (Upsert(row.Table, row.Key, row.English, row.Comment))
                    changed++;
            }

            return changed;
        }

        // Returns true when the entry was added or its English text/comment changed.
        public static bool Upsert(string table, string key, string english, string comment = null)
        {
            if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(key))
                throw new ArgumentException($"Table and key are required (table '{table}', key '{key}').");

            var collection = LocalizationEditorSettings.GetStringTableCollection(table);
            if (collection == null)
                throw new InvalidOperationException($"String Table collection '{table}' does not exist. Run Mejnun > Localization > Set Up Project.");

            var englishTable = collection.GetTable(Loc.ProjectLocaleCode) as StringTable;
            if (englishTable == null)
                throw new InvalidOperationException($"Collection '{table}' has no '{Loc.ProjectLocaleCode}' table.");

            bool changed = false;
            var sharedEntry = collection.SharedData.GetEntry(key);
            if (sharedEntry == null)
            {
                sharedEntry = collection.SharedData.AddKey(key);
                changed = true;
            }

            var entry = englishTable.GetEntry(sharedEntry.Id);
            if (entry == null)
            {
                entry = englishTable.AddEntry(sharedEntry.Id, english ?? string.Empty);
                changed = true;
            }
            else if (entry.Value != english)
            {
                entry.Value = english ?? string.Empty;
                changed = true;
            }

            bool smart = LocalizationValidator.IsSmart(english);
            foreach (var stringTable in collection.StringTables)
            {
                var localized = stringTable.GetEntry(sharedEntry.Id);
                if (localized != null && localized.IsSmart != smart)
                {
                    localized.IsSmart = smart;
                    EditorUtility.SetDirty(stringTable);
                    changed = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(comment))
            {
                var existing = sharedEntry.Metadata.GetMetadata<Comment>();
                if (existing == null)
                {
                    sharedEntry.Metadata.AddMetadata(new Comment { CommentText = comment });
                    changed = true;
                }
                else if (existing.CommentText != comment)
                {
                    existing.CommentText = comment;
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(englishTable);
                EditorUtility.SetDirty(collection.SharedData);
            }

            return changed;
        }

        // RFC 4180: comma separated, double quotes around fields containing
        // commas, quotes or newlines, quotes doubled inside quoted fields.
        public static List<Row> ReadCsv(string text)
        {
            var rows = new List<Row>();
            var records = ParseRecords(text);
            if (records.Count == 0)
                return rows;

            var header = records[0];
            int tableIndex = header.IndexOf("table");
            int keyIndex = header.IndexOf("key");
            int englishIndex = header.IndexOf("en");
            int commentIndex = header.IndexOf("comment");
            if (tableIndex < 0 || keyIndex < 0 || englishIndex < 0)
                throw new FormatException("The CSV header must contain table,key,en (and optionally comment).");

            for (int i = 1; i < records.Count; i++)
            {
                var record = records[i];
                if (record.Count == 1 && string.IsNullOrWhiteSpace(record[0]))
                    continue;

                rows.Add(new Row
                {
                    Table = Field(record, tableIndex).Trim(),
                    Key = Field(record, keyIndex).Trim(),
                    English = Field(record, englishIndex),
                    Comment = commentIndex >= 0 ? Field(record, commentIndex) : null,
                });
            }

            return rows;
        }

        private static string Field(List<string> record, int index) => index < record.Count ? record[index] : string.Empty;

        internal static List<List<string>> ParseRecords(string text)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }

                    continue;
                }

                switch (c)
                {
                    case '"':
                        quoted = true;
                        break;
                    case ',':
                        record.Add(field.ToString());
                        field.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        record.Add(field.ToString());
                        field.Clear();
                        records.Add(record);
                        record = new List<string>();
                        break;
                    default:
                        field.Append(c);
                        break;
                }
            }

            if (field.Length > 0 || record.Count > 0)
            {
                record.Add(field.ToString());
                records.Add(record);
            }

            if (records.Count > 0)
            {
                var header = records[0];
                for (int i = 0; i < header.Count; i++)
                    header[i] = header[i].Trim().TrimStart('\uFEFF').ToLowerInvariant();
            }

            return records;
        }
    }
}
