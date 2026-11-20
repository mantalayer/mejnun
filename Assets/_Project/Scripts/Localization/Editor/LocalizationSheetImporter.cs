using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace Mejnun.Localization.Editor
{
    public static class LocalizationSheetImporter
    {
        public const string SnapshotPath = "Assets/_Project/Localization/Source/PopupSheet.csv";
        private const int GoodPopupsGid = 0;
        private const int BadPopupsGid = 1383348199;
        private const int DealerQuestionsGid = 1343378837;

        private sealed class SheetTab
        {
            public string Table;
            public string KeyPrefix;
            public int Gid;
            public string TabName;
            public string Comment;
        }

        public struct SourceLine
        {
            public string Key;
            public string English;
        }

        public struct ExistingLine
        {
            public string Key;
            public string English;
            public bool Retired;
        }

        public struct SheetRow
        {
            public int Row;
            public string English;
            public string Key;
        }

        public sealed class TabPlan
        {
            public readonly List<SourceLine> Lines = new List<SourceLine>();
            public readonly List<string> RetiredKeys = new List<string>();
            public readonly Dictionary<int, string> KeyCells = new Dictionary<int, string>();
        }

        public struct ImportSummary
        {
            public int Added;
            public int Updated;
            public int Retired;
        }

        private static readonly SheetTab[] Tabs =
        {
            new SheetTab
            {
                Table = "UI",
                KeyPrefix = "flavor.good.",
                Gid = GoodPopupsGid,
                TabName = "Good Popups",
                Comment = "Good-move flavor popup shown briefly during a game."
            },
            new SheetTab
            {
                Table = "UI",
                KeyPrefix = "flavor.bad.",
                Gid = BadPopupsGid,
                TabName = "Bad Popups",
                Comment = "Bad-move flavor shown above the localized rule explanation."
            },
            new SheetTab
            {
                Table = Loc.DealerQuestionsTable,
                KeyPrefix = "dq.",
                Gid = DealerQuestionsGid,
                TabName = "Deciding Questions",
                Comment = "Question asked to players before a local game; answers decide who goes first."
            }
        };

        [MenuItem("Mejnun/Localization/Import Popups & Questions Sheet", priority = 21)]
        private static void ImportFromMenu()
        {
            var settings = LocalizationPipelineSettings.LoadOrCreate();
            if (!settings.IsPopupSheetConfigured(out var reason))
            {
                EditorUtility.DisplayDialog("Localization", $"Google Sheets is not configured: {reason}.", "OK");
                Selection.activeObject = settings;
                return;
            }

            var summary = ImportSheet(settings.SheetsServiceProvider.Service, settings.PopupSpreadsheetId,
                GoodPopupsGid, BadPopupsGid, DealerQuestionsGid);
            EditorUtility.DisplayDialog("Localization",
                $"Imported {summary.Added} new and {summary.Updated} updated entries; retired {summary.Retired}. " +
                $"Snapshot saved to {Path.GetFileName(SnapshotPath)}.",
                "OK");
        }

        public static void ImportFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var settings = LocalizationPipelineSettings.LoadOrCreate();
            var spreadsheetId = Argument(args, "-popupSheetId");
            if (string.IsNullOrWhiteSpace(spreadsheetId))
            {
                if (!settings.IsPopupSheetConfigured(out var reason))
                {
                    throw new InvalidOperationException($"Google Sheets is not configured: {reason}.");
                }

                spreadsheetId = settings.PopupSpreadsheetId;
            }
            else if (settings.SheetsServiceProvider == null ||
                     string.IsNullOrEmpty(settings.SheetsServiceProvider.ClientId) ||
                     string.IsNullOrEmpty(settings.SheetsServiceProvider.ClientSecret))
            {
                throw new InvalidOperationException("Google Sheets OAuth credentials are not configured.");
            }

            var summary = ImportSheet(settings.SheetsServiceProvider.Service, spreadsheetId,
                ArgumentInt(args, "-goodPopupsGid", GoodPopupsGid),
                ArgumentInt(args, "-badPopupsGid", BadPopupsGid),
                ArgumentInt(args, "-decidingQuestionsGid", DealerQuestionsGid));
            Debug.Log($"[Localization] Imported popup and question sheet: {summary.Added} added, " +
                      $"{summary.Updated} updated, {summary.Retired} retired.");
        }

        public static ImportSummary ImportSheet(SheetsService service, string spreadsheetId, int goodPopupsGid,
            int badPopupsGid, int dealerQuestionsGid)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (string.IsNullOrWhiteSpace(spreadsheetId))
            {
                throw new ArgumentException("A Google spreadsheet ID is required.", nameof(spreadsheetId));
            }

            var summary = new ImportSummary();
            var snapshot = new StringBuilder("table,key,en,comment\n");
            var spreadsheet = service.Spreadsheets.Get(spreadsheetId).Execute();
            var downloadedValues = new IList<IList<object>>[Tabs.Length];
            var resolvedTabTitles = new string[Tabs.Length];
            for (var index = 0; index < Tabs.Length; index++)
            {
                var tab = Tabs[index];
                var gid = tab.Gid == GoodPopupsGid ? goodPopupsGid :
                    tab.Gid == BadPopupsGid ? badPopupsGid : dealerQuestionsGid;
                var tabTitle = ResolveTabTitle(spreadsheet, spreadsheetId, tab, gid);
                resolvedTabTitles[index] = tabTitle;
                var escapedTabTitle = tabTitle.Replace("'", "''");
                var values = service.Spreadsheets.Values.Get(spreadsheetId, $"'{escapedTabTitle}'!A:B").Execute();
                downloadedValues[index] = values.Values;
            }

            for (var index = 0; index < Tabs.Length; index++)
            {
                var tab = Tabs[index];
                var rows = ReadSheetRows(downloadedValues[index], out var hasHeader, out var addKeyHeader);
                var existing = ReadExistingLines(tab.Table, tab.KeyPrefix);
                var plan = PlanTab(rows, existing, tab.KeyPrefix);
                var existingByKey = existing.ToDictionary(line => line.Key, StringComparer.Ordinal);
                var collection = LocalizationEditorSettings.GetStringTableCollection(tab.Table);
                for (var lineIndex = 0; lineIndex < plan.Lines.Count; lineIndex++)
                {
                    var line = plan.Lines[lineIndex];
                    var hadEntry = existingByKey.TryGetValue(line.Key, out var previous);
                    var changed = LocalizationSourceImporter.Upsert(tab.Table, line.Key, line.English, tab.Comment);
                    SetRetired(collection, line.Key, false);
                    if (!hadEntry)
                    {
                        summary.Added++;
                    }
                    else if (changed || previous.Retired)
                    {
                        summary.Updated++;
                    }

                    AppendCsv(snapshot, tab.Table, line.Key, line.English, tab.Comment);
                }

                for (var retiredIndex = 0; retiredIndex < plan.RetiredKeys.Count; retiredIndex++)
                {
                    if (SetRetired(collection, plan.RetiredKeys[retiredIndex], true))
                        summary.Retired++;
                }

                WriteKeyCells(service, spreadsheetId, resolvedTabTitles[index], plan.KeyCells,
                    hasHeader && addKeyHeader);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(SnapshotPath));
            File.WriteAllText(SnapshotPath, snapshot.ToString(), new UTF8Encoding(false));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return summary;
        }

        public static List<SheetRow> ReadSheetRows(IList<IList<object>> values, out bool hasHeader,
            out bool addKeyHeader)
        {
            var rows = new List<SheetRow>();
            hasHeader = false;
            addKeyHeader = false;
            if (values == null)
                return rows;

            for (var index = 0; index < values.Count; index++)
            {
                var record = values[index];
                var english = Cell(record, 0).Trim();
                var key = Cell(record, 1).Trim();
                if (index == 0)
                    english = english.TrimStart('\uFEFF');
                if (index == 0 && (IsHeader(english) || string.Equals(key, "key", StringComparison.OrdinalIgnoreCase)))
                {
                    hasHeader = true;
                    addKeyHeader = string.IsNullOrEmpty(key);
                    continue;
                }

                if (english.Length == 0)
                    continue;

                rows.Add(new SheetRow { Row = index + 1, English = english, Key = key });
            }

            return rows;
        }

        public static TabPlan PlanTab(IReadOnlyList<SheetRow> sheetRows, IEnumerable<ExistingLine> existingLines,
            string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException("A key prefix is required.", nameof(prefix));

            var plan = new TabPlan();
            var existing = (existingLines ?? Enumerable.Empty<ExistingLine>())
                .Where(line => !string.IsNullOrEmpty(line.Key) && line.Key.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(line => line.Key, StringComparer.Ordinal)
                .ToList();
            var existingByKey = existing.ToDictionary(line => line.Key, StringComparer.Ordinal);
            var usedKeys = new HashSet<string>(existingByKey.Keys, StringComparer.Ordinal);
            var maxNumber = 0L;
            foreach (var line in existing)
            {
                if (long.TryParse(line.Key.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture,
                        out var number))
                    maxNumber = Math.Max(maxNumber, number);
            }

            var acceptedRows = new List<SheetRow>();
            var importedEnglish = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var explicitKeys = new Dictionary<int, string>();
            var reservedKeys = new HashSet<string>(StringComparer.Ordinal);
            var claimedExplicitKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in (sheetRows ?? Array.Empty<SheetRow>()).OrderBy(row => row.Row))
            {
                var english = NormalizeEnglish(row.English);
                if (english.Length == 0)
                    continue;

                if (!importedEnglish.Add(english))
                {
                    plan.KeyCells[row.Row] = string.Empty;
                    continue;
                }

                var normalizedRow = row;
                normalizedRow.English = english;
                acceptedRows.Add(normalizedRow);
                if (IsValidKey(row.Key, prefix) && claimedExplicitKeys.Add(row.Key))
                {
                    explicitKeys[row.Row] = row.Key;
                    reservedKeys.Add(row.Key);
                }
            }

            var claimedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in acceptedRows)
            {
                string key;
                if (explicitKeys.TryGetValue(row.Row, out var explicitKey))
                {
                    key = explicitKey;
                }
                else
                {
                    key = FindUnclaimedTextMatch(row.English, existing, claimedKeys, reservedKeys);
                    if (key == null)
                        key = AllocateKey(prefix, ref maxNumber, usedKeys, reservedKeys);
                }

                claimedKeys.Add(key);
                plan.KeyCells[row.Row] = key;
                plan.Lines.Add(new SourceLine { Key = key, English = row.English });
            }

            foreach (var line in existing)
            {
                if (!line.Retired && !claimedKeys.Contains(line.Key))
                    plan.RetiredKeys.Add(line.Key);
            }

            return plan;
        }

        private static List<ExistingLine> ReadExistingLines(string table, string prefix)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(table);
            var englishTable = collection?.GetTable(Loc.ProjectLocaleCode) as UnityEngine.Localization.Tables.StringTable;
            var lines = new List<ExistingLine>();
            if (collection == null || englishTable == null)
            {
                return lines;
            }

            foreach (var entry in collection.SharedData.Entries)
            {
                if (!entry.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var localized = englishTable.GetEntry(entry.Id);
                lines.Add(new ExistingLine
                {
                    Key = entry.Key,
                    English = localized == null ? string.Empty : localized.Value,
                    Retired = entry.Metadata.GetMetadata<RetiredEntry>() != null
                });
            }

            return lines;
        }

        private static string FindUnclaimedTextMatch(string english, IEnumerable<ExistingLine> existing,
            HashSet<string> claimedKeys, HashSet<string> reservedKeys)
        {
            foreach (var line in existing)
            {
                if (line.Retired || claimedKeys.Contains(line.Key) || reservedKeys.Contains(line.Key) ||
                    !string.Equals(NormalizeEnglish(line.English), english, StringComparison.OrdinalIgnoreCase))
                    continue;

                return line.Key;
            }

            return null;
        }

        private static string AllocateKey(string prefix, ref long nextNumber, HashSet<string> usedKeys,
            HashSet<string> reservedKeys)
        {
            while (nextNumber < long.MaxValue)
            {
                var key = prefix + nextNumber.ToString("D3", CultureInfo.InvariantCulture);
                nextNumber++;
                if (usedKeys.Add(key) && !reservedKeys.Contains(key))
                    return key;
            }

            throw new InvalidOperationException($"No numeric keys remain for prefix '{prefix}'.");
        }

        private static bool IsValidKey(string key, string prefix)
        {
            if (string.IsNullOrEmpty(key) || !key.StartsWith(prefix, StringComparison.Ordinal) ||
                key.Length == prefix.Length)
                return false;

            for (var index = prefix.Length; index < key.Length; index++)
            {
                if (key[index] < '0' || key[index] > '9')
                    return false;
            }

            return true;
        }

        private static bool SetRetired(UnityEditor.Localization.StringTableCollection collection, string key,
            bool retired)
        {
            var sharedEntry = collection?.SharedData.GetEntry(key);
            if (sharedEntry == null)
                return false;

            var metadata = sharedEntry.Metadata.GetMetadata<RetiredEntry>();
            if (retired)
            {
                if (metadata != null)
                    return false;

                sharedEntry.Metadata.AddMetadata(new RetiredEntry());
            }
            else
            {
                if (metadata == null)
                    return false;

                sharedEntry.Metadata.RemoveMetadata(metadata);
            }

            EditorUtility.SetDirty(collection.SharedData);
            return true;
        }

        private static void WriteKeyCells(SheetsService service, string spreadsheetId, string tabTitle,
            Dictionary<int, string> keyCells, bool addKeyHeader)
        {
            var updates = new List<ValueRange>();
            if (addKeyHeader)
                AddCellUpdate(updates, tabTitle, 1, "key");

            foreach (var keyCell in keyCells.OrderBy(cell => cell.Key))
                AddCellUpdate(updates, tabTitle, keyCell.Key, keyCell.Value);

            if (updates.Count == 0)
                return;

            var request = new BatchUpdateValuesRequest { ValueInputOption = "RAW", Data = updates };
            service.Spreadsheets.Values.BatchUpdate(request, spreadsheetId).Execute();
        }

        private static void AddCellUpdate(List<ValueRange> updates, string tabTitle, int row, string value)
        {
            updates.Add(new ValueRange
            {
                Range = $"'{tabTitle.Replace("'", "''")}'!B{row}",
                Values = new List<IList<object>> { new List<object> { value ?? string.Empty } }
            });
        }

        private static string ResolveTabTitle(Google.Apis.Sheets.v4.Data.Spreadsheet spreadsheet,
            string spreadsheetId, SheetTab tab, int gid)
        {
            var sheets = spreadsheet.Sheets ?? new List<Google.Apis.Sheets.v4.Data.Sheet>();
            var sheet = sheets.FirstOrDefault(candidate =>
                candidate.Properties != null && candidate.Properties.SheetId == gid);
            if (sheet?.Properties == null || string.IsNullOrEmpty(sheet.Properties.Title))
            {
                sheet = sheets.FirstOrDefault(candidate => candidate.Properties != null &&
                    string.Equals(candidate.Properties.Title, tab.TabName, StringComparison.OrdinalIgnoreCase));
            }

            if (sheet?.Properties == null || string.IsNullOrEmpty(sheet.Properties.Title))
            {
                var availableTabs = sheets.Where(candidate => candidate.Properties != null)
                    .Select(candidate => $"'{candidate.Properties.Title}' (gid {candidate.Properties.SheetId})");
                var available = string.Join(", ", availableTabs);
                if (string.IsNullOrEmpty(available))
                    available = "<none>";
                throw new InvalidOperationException(
                    $"Google Sheets tab '{tab.TabName}' (gid {gid}) was not found in workbook '{spreadsheetId}'. " +
                    $"Available tabs: {available}.");
            }

            return sheet.Properties.Title;
        }

        private static string Cell(IList<object> row, int column)
        {
            return row == null || row.Count <= column ? string.Empty : row[column]?.ToString() ?? string.Empty;
        }

        private static bool IsHeader(string value)
        {
            return string.Equals(value, "en", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "english", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "text", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "value", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "popup", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "good popup", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "bad popup", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "good popups", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "bad popups", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "deciding question", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "deciding questions", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "dealer question", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "question", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeEnglish(string english)
        {
            return (english ?? string.Empty).Trim();
        }

        private static void AppendCsv(StringBuilder output, string table, string key, string english, string comment)
        {
            output.Append(Quote(table)).Append(',').Append(Quote(key)).Append(',').Append(Quote(english)).Append(',')
                .Append(Quote(comment)).Append('\n');
        }

        private static string Quote(string value)
        {
            value = value ?? string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string Argument(string[] args, string name)
        {
            for (var index = 0; index + 1 < args.Length; index++)
            {
                if (string.Equals(args[index], name, StringComparison.Ordinal))
                {
                    return args[index + 1];
                }
            }

            return null;
        }

        private static int ArgumentInt(string[] args, string name, int defaultValue)
        {
            var value = Argument(args, name);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : defaultValue;
        }
    }
}