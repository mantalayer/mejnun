using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Mejnun.Localization.Editor
{
    // Round trip with the translation workbook (plan §7.1, Docs/Localization.md):
    //   1. Push: write Key + en only. New keys get a row with a GOOGLETRANSLATE
    //      formula and status "auto" in every target-locale column. Existing
    //      translations, formulas and reviewer edits are never overwritten;
    //      when the English text of a reviewed row changes, its status becomes
    //      "needs-review".
    //   2. Wait until Google has finished evaluating the formulas.
    //   3. Pull: copy each locale column into its String Table. Key, en and
    //      status columns are never imported; empty and error cells are skipped.
    // Uses the OAuth client of Unity Localization's Sheets Service Provider.
    public static class LocalizationSheetsSync
    {
        public const string StatusAuto = "auto";
        public const string StatusReviewed = "reviewed";
        public const string StatusNeedsReview = "needs-review";

        private const string KeyHeader = "Key";
        private const string StatusSuffix = " status";
        private const string LoadingValue = "Loading...";

        public struct Result
        {
            public int RowsAdded;
            public int SourcesUpdated;
            public int FormulasAdded;
            public int TranslationsPulled;
            public bool TimedOut;

            public override string ToString() =>
                $"{RowsAdded} rows added, {SourcesUpdated} English cells updated, {FormulasAdded} formulas added, " +
                $"{TranslationsPulled} translations pulled{(TimedOut ? " (timed out waiting for GOOGLETRANSLATE; re-run later)" : string.Empty)}";
        }

        [MenuItem("Mejnun/Localization/Sync with Google Sheets (Push + Translate + Pull)", priority = 30)]
        private static void SyncFromMenu()
        {
            var settings = LocalizationPipelineSettings.LoadOrCreate();
            if (!settings.IsSheetsConfigured(out string reason))
            {
                EditorUtility.DisplayDialog("Localization", $"Google Sheets is not configured: {reason}.\nSee Docs/Localization.md, \"One-time Google setup\".", "OK");
                Selection.activeObject = settings;
                return;
            }

            try
            {
                var result = Sync(settings);
                LocalizationValidator.LogReport(LocalizationValidator.Validate(), $"Sheets sync: {result}. ");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // Batch: -executeMethod Mejnun.Localization.Editor.LocalizationSheetsSync.SyncBatch
        public static void SyncBatch()
        {
            var settings = LocalizationPipelineSettings.LoadOrCreate();
            if (!settings.IsSheetsConfigured(out string reason))
                throw new InvalidOperationException($"Google Sheets is not configured: {reason}.");

            var result = Sync(settings);
            LocalizationValidator.LogReport(LocalizationValidator.Validate(), $"Sheets sync: {result}. ");
        }

        public static Result Sync(LocalizationPipelineSettings settings)
        {
            var service = settings.SheetsServiceProvider.Service;
            var result = new Result();
            var deadline = DateTime.UtcNow.AddSeconds(settings.TranslationWaitSeconds);

            foreach (string collectionName in LocalizationProjectSetup.Collections)
            {
                string tab = settings.TabFor(collectionName);
                var collection = LocalizationEditorSettings.GetStringTableCollection(collectionName);
                if (tab == null || collection == null)
                    continue;

                Progress($"Pushing {collectionName}", 0.1f);
                EnsureTab(service, settings.SpreadsheetId, tab);
                Push(service, settings, tab, collection, ref result);
            }

            foreach (string collectionName in LocalizationProjectSetup.Collections)
            {
                string tab = settings.TabFor(collectionName);
                var collection = LocalizationEditorSettings.GetStringTableCollection(collectionName);
                if (tab == null || collection == null)
                    continue;

                Progress($"Waiting for GOOGLETRANSLATE ({collectionName})", 0.5f);
                var sheet = WaitForTranslations(service, settings, tab, deadline, ref result);
                Progress($"Pulling {collectionName}", 0.8f);
                Pull(settings, sheet, collection, ref result);
                HandwrittenTranslationApplier.ApplyCollection(collectionName);
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        private static void Progress(string message, float value)
        {
            if (!Application.isBatchMode)
                EditorUtility.DisplayProgressBar("Localization: Google Sheets", message, value);
        }

        private sealed class SheetData
        {
            public List<IList<object>> Rows = new();
            public Dictionary<string, int> Columns = new(StringComparer.OrdinalIgnoreCase);

            public string Cell(int row, string header)
            {
                if (!Columns.TryGetValue(header, out int column) || row >= Rows.Count)
                    return string.Empty;
                var values = Rows[row];
                return column < values.Count ? values[column]?.ToString() ?? string.Empty : string.Empty;
            }
        }

        private static void EnsureTab(SheetsService service, string spreadsheetId, string tab)
        {
            var spreadsheet = service.Spreadsheets.Get(spreadsheetId).Execute();
            if (spreadsheet.Sheets.Any(s => s.Properties.Title == tab))
                return;

            var request = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request>
                {
                    new() { AddSheet = new AddSheetRequest { Properties = new SheetProperties { Title = tab } } },
                },
            };
            service.Spreadsheets.BatchUpdate(request, spreadsheetId).Execute();
        }

        private static SheetData Read(SheetsService service, string spreadsheetId, string tab,
            SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum render)
        {
            var get = service.Spreadsheets.Values.Get(spreadsheetId, $"'{tab}'!A1:ZZ");
            get.ValueRenderOption = render;
            var range = get.Execute();
            var data = new SheetData();
            if (range.Values != null)
                data.Rows.AddRange(range.Values);

            if (data.Rows.Count > 0)
            {
                var header = data.Rows[0];
                for (int i = 0; i < header.Count; i++)
                {
                    string name = header[i]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(name) && !data.Columns.ContainsKey(name))
                        data.Columns[name] = i;
                }
            }

            return data;
        }

        private static string ColumnLetter(int index)
        {
            string letters = string.Empty;
            for (index++; index > 0; index = (index - 1) / 26)
                letters = (char)('A' + (index - 1) % 26) + letters;
            return letters;
        }

        private static string Formula(int row, string googleCode) =>
            $"=IF($B{row}=\"\",\"\",GOOGLETRANSLATE($B{row},\"en\",\"{googleCode}\"))";

        private static void Push(SheetsService service, LocalizationPipelineSettings settings, string tab,
            UnityEditor.Localization.StringTableCollection collection, ref Result result)
        {
            var formulas = Read(service, settings.SpreadsheetId, tab,
                SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.FORMULA);
            var updates = new List<ValueRange>();

            // Header: Key | en | <locale> | <locale> status | ...; missing
            // columns are appended on the right so existing columns never move.
            var header = formulas.Rows.Count > 0 ? formulas.Rows[0].Select(v => v?.ToString() ?? string.Empty).ToList() : new List<string>();
            void EnsureHeader(string name)
            {
                if (formulas.Columns.ContainsKey(name))
                    return;
                formulas.Columns[name] = header.Count;
                header.Add(name);
            }

            EnsureHeader(KeyHeader);
            EnsureHeader(Loc.ProjectLocaleCode);
            foreach (var locale in settings.TargetLocales)
            {
                EnsureHeader(locale.LocaleCode);
                EnsureHeader(locale.LocaleCode + StatusSuffix);
            }

            if (formulas.Columns[KeyHeader] != 0 || formulas.Columns[Loc.ProjectLocaleCode] != 1)
                throw new InvalidOperationException($"Tab '{tab}' must have 'Key' in column A and 'en' in column B.");

            updates.Add(new ValueRange { Range = $"'{tab}'!A1", Values = new List<IList<object>> { header.Cast<object>().ToList() } });

            var rowByKey = new Dictionary<string, int>();
            var handwritten = new Dictionary<(string Key, string Locale), HandwrittenTranslationApplier.Translation>();
            foreach (var translation in HandwrittenTranslationApplier.ForTable(collection.TableCollectionName))
                handwritten[(translation.Key, translation.Locale)] = translation;
            for (int row = 1; row < formulas.Rows.Count; row++)
            {
                string key = formulas.Cell(row, KeyHeader).Trim();
                if (!string.IsNullOrEmpty(key) && !rowByKey.ContainsKey(key))
                    rowByKey[key] = row;
            }

            var english = collection.GetTable(Loc.ProjectLocaleCode) as StringTable;
            int nextRow = Math.Max(formulas.Rows.Count, 1);
            foreach (var shared in collection.SharedData.Entries)
            {
                string source = english?.GetEntry(shared.Id)?.Value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(source))
                    continue;

                if (!rowByKey.TryGetValue(shared.Key, out int row))
                {
                    row = nextRow++;
                    rowByKey[shared.Key] = row;
                    result.RowsAdded++;
                    SetText(updates, tab, row, 0, shared.Key);
                    SetText(updates, tab, row, 1, source);
                    foreach (var locale in settings.TargetLocales)
                    {
                        if (handwritten.TryGetValue((shared.Key, locale.LocaleCode), out var translation))
                        {
                            SetText(updates, tab, row, formulas.Columns[locale.LocaleCode], translation.Text);
                            SetCell(updates, tab, row, formulas.Columns[locale.LocaleCode + StatusSuffix], StatusReviewed);
                        }
                        else
                        {
                            SetCell(updates, tab, row, formulas.Columns[locale.LocaleCode], Formula(row + 1, locale.GoogleLanguageCode));
                            SetCell(updates, tab, row, formulas.Columns[locale.LocaleCode + StatusSuffix], StatusAuto);
                            result.FormulasAdded++;
                        }
                    }

                    continue;
                }

                bool sourceChanged = formulas.Cell(row, Loc.ProjectLocaleCode) != source;
                if (sourceChanged)
                {
                    SetText(updates, tab, row, 1, source);
                    result.SourcesUpdated++;
                }

                foreach (var locale in settings.TargetLocales)
                {
                    string status = formulas.Cell(row, locale.LocaleCode + StatusSuffix).Trim();
                    string cell = formulas.Cell(row, locale.LocaleCode);
                    if (sourceChanged && status == StatusReviewed)
                        SetCell(updates, tab, row, formulas.Columns[locale.LocaleCode + StatusSuffix], StatusNeedsReview);

                    if (handwritten.TryGetValue((shared.Key, locale.LocaleCode), out var translation))
                    {
                        if (cell != translation.Text)
                            SetText(updates, tab, row, formulas.Columns[locale.LocaleCode], translation.Text);
                        if (status != (sourceChanged ? StatusNeedsReview : StatusReviewed))
                            SetCell(updates, tab, row, formulas.Columns[locale.LocaleCode + StatusSuffix], sourceChanged ? StatusNeedsReview : StatusReviewed);
                        continue;
                    }

                    // Fill only empty cells (a new language column, or a row
                    // added by hand). Plain-text reviewer edits are kept.
                    if (string.IsNullOrWhiteSpace(cell))
                    {
                        SetCell(updates, tab, row, formulas.Columns[locale.LocaleCode], Formula(row + 1, locale.GoogleLanguageCode));
                        if (string.IsNullOrEmpty(status))
                            SetCell(updates, tab, row, formulas.Columns[locale.LocaleCode + StatusSuffix], StatusAuto);
                        result.FormulasAdded++;
                    }
                }
            }

            var batch = new BatchUpdateValuesRequest { ValueInputOption = "USER_ENTERED", Data = updates };
            service.Spreadsheets.Values.BatchUpdate(batch, settings.SpreadsheetId).Execute();
        }

        // Values are written USER_ENTERED so formulas evaluate; a leading
        // apostrophe keeps source text such as "+5 XP" or "1/3" from being
        // parsed as a formula, number or date. Sheets does not store it.
        private static void SetText(List<ValueRange> updates, string tab, int row, int column, string value)
        {
            SetCell(updates, tab, row, column, "'" + value);
        }

        private static void SetCell(List<ValueRange> updates, string tab, int row, int column, string value)
        {
            updates.Add(new ValueRange
            {
                Range = $"'{tab}'!{ColumnLetter(column)}{row + 1}",
                Values = new List<IList<object>> { new List<object> { value } },
            });
        }

        private static SheetData WaitForTranslations(SheetsService service, LocalizationPipelineSettings settings,
            string tab, DateTime deadline, ref Result result)
        {
            while (true)
            {
                var values = Read(service, settings.SpreadsheetId, tab,
                    SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.FORMATTEDVALUE);
                bool loading = false;
                for (int row = 1; row < values.Rows.Count && !loading; row++)
                {
                    foreach (var locale in settings.TargetLocales)
                    {
                        if (values.Cell(row, locale.LocaleCode).Trim() == LoadingValue)
                        {
                            loading = true;
                            break;
                        }
                    }
                }

                if (!loading)
                    return values;

                if (DateTime.UtcNow >= deadline)
                {
                    result.TimedOut = true;
                    return values;
                }

                Thread.Sleep(3000);
            }
        }

        private static void Pull(LocalizationPipelineSettings settings, SheetData sheet,
            UnityEditor.Localization.StringTableCollection collection, ref Result result)
        {
            for (int row = 1; row < sheet.Rows.Count; row++)
            {
                string key = sheet.Cell(row, KeyHeader).Trim();
                var shared = string.IsNullOrEmpty(key) ? null : collection.SharedData.GetEntry(key);
                if (shared == null)
                    continue;

                string source = (collection.GetTable(Loc.ProjectLocaleCode) as StringTable)?.GetEntry(shared.Id)?.Value;
                bool smart = LocalizationValidator.IsSmart(source);
                foreach (var locale in settings.TargetLocales)
                {
                    string value = sheet.Cell(row, locale.LocaleCode);
                    if (string.IsNullOrWhiteSpace(value) || value.Trim() == LoadingValue || LocalizationValidator.IsSheetError(value))
                        continue;

                    if (collection.GetTable(locale.LocaleCode) is not StringTable table)
                        continue;

                    var entry = table.GetEntry(shared.Id);
                    if (entry == null)
                        entry = table.AddEntry(shared.Id, value);
                    else if (entry.Value != value)
                        entry.Value = value;
                    else
                        continue;

                    entry.IsSmart = smart;
                    EditorUtility.SetDirty(table);
                    result.TranslationsPulled++;
                }
            }
        }
    }
}
