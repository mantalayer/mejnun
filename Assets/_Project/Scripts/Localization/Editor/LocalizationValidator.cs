using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Mejnun.Localization.Editor
{
    // Checks the String Tables and the game source against the rules in
    // Docs/Localization.md. Run from Mejnun > Localization > Validate, from the
    // pre-build hook, and from the EditMode tests.
    public static class LocalizationValidator
    {
        public const string ReportPath = "Logs/LocalizationReport.txt";

        public enum IssueKind
        {
            // Source problems: something a developer must fix in code or English text.
            EmptySource,
            UnknownKey,
            RawString,
            // Translation problems: fixed by a Sheets sync or a reviewer.
            MissingTranslation,
            PlaceholderMismatch,
            SheetError,
        }

        public struct Issue
        {
            public IssueKind Kind;
            public string Table;
            public string Key;
            public string Locale;
            public string Detail;

            public bool IsTranslationIssue => Kind >= IssueKind.MissingTranslation;

            public override string ToString()
            {
                string where = string.IsNullOrEmpty(Locale) ? $"{Table}/{Key}" : $"{Table}/{Key} [{Locale}]";
                return string.IsNullOrEmpty(Table) ? $"{Kind}: {Detail}" : $"{Kind}: {where} {Detail}".TrimEnd();
            }
        }

        public sealed class Report
        {
            public readonly List<Issue> Issues = new();
            public int EntryCount;
            public int LocaleCount;

            public IEnumerable<Issue> SourceIssues => Issues.Where(i => !i.IsTranslationIssue);
            public IEnumerable<Issue> TranslationIssues => Issues.Where(i => i.IsTranslationIssue);
            public bool HasTranslationIssues => Issues.Any(i => i.IsTranslationIssue);
            public bool HasMissingTranslations => Issues.Any(i => i.Kind == IssueKind.MissingTranslation || i.Kind == IssueKind.SheetError);

            public string Summary()
            {
                var counts = Issues.GroupBy(i => i.Kind).Select(g => $"{g.Key} {g.Count()}");
                string detail = Issues.Count == 0 ? "no issues" : string.Join(", ", counts);
                return $"{EntryCount} entries x {LocaleCount} locales: {detail}";
            }

            public string Format()
            {
                var builder = new StringBuilder();
                builder.AppendLine($"Localization report ({DateTime.Now:yyyy-MM-dd HH:mm})");
                builder.AppendLine(Summary());
                foreach (var group in Issues.GroupBy(i => i.Kind))
                {
                    builder.AppendLine();
                    builder.AppendLine($"## {group.Key} ({group.Count()})");
                    foreach (var issue in group)
                        builder.AppendLine("  " + issue);
                }

                return builder.ToString();
            }
        }

        // {0}, {0:plural:...}, {count}, {player.Name}; the first segment of each placeholder.
        private static readonly Regex Placeholder = new("\\{([A-Za-z_][\\w.]*|\\d+)(?=[:}])", RegexOptions.Compiled);
        private static readonly Regex SheetErrorValue = new("^(#(VALUE!|N/A|ERROR!|REF!|NAME\\?|NUM!|DIV/0!)|Loading\\.\\.\\.)", RegexOptions.Compiled);

        public static bool IsSmart(string text) => !string.IsNullOrEmpty(text) && Placeholder.IsMatch(text);

        public static bool IsSheetError(string text) => !string.IsNullOrEmpty(text) && SheetErrorValue.IsMatch(text.Trim());

        public static IReadOnlyList<string> Placeholders(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text))
                return result;

            foreach (Match match in Placeholder.Matches(text))
                result.Add(match.Groups[1].Value);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        public static bool PlaceholdersMatch(string source, string translation, out string detail)
        {
            var expected = Placeholders(source);
            var actual = Placeholders(translation);
            if (!expected.SequenceEqual(actual))
            {
                detail = $"expects {{{string.Join("}, {", expected)}}} but has {{{string.Join("}, {", actual)}}}";
                return false;
            }

            if (Count(source, '{') - Count(source, '}') != Count(translation, '{') - Count(translation, '}'))
            {
                detail = "unbalanced braces (a Smart String block was broken by translation)";
                return false;
            }

            detail = null;
            return true;
        }

        private static int Count(string text, char c)
        {
            int count = 0;
            foreach (char ch in text)
            {
                if (ch == c)
                    count++;
            }

            return count;
        }

        public static Report Validate(bool includeSourceScan = true)
        {
            var report = new Report();
            var knownKeys = new Dictionary<string, HashSet<string>>();

            foreach (string collectionName in LocalizationProjectSetup.Collections)
            {
                var collection = LocalizationEditorSettings.GetStringTableCollection(collectionName);
                if (collection == null)
                {
                    report.Issues.Add(new Issue { Kind = IssueKind.UnknownKey, Detail = $"String Table collection '{collectionName}' is missing; run Mejnun > Localization > Set Up Project." });
                    continue;
                }

                var keys = new HashSet<string>();
                knownKeys[collectionName] = keys;
                var english = collection.GetTable(Loc.ProjectLocaleCode) as StringTable;
                var targets = collection.StringTables.Where(t => t.LocaleIdentifier.Code != Loc.ProjectLocaleCode).ToList();
                report.LocaleCount = Math.Max(report.LocaleCount, collection.StringTables.Count);

                foreach (var shared in collection.SharedData.Entries)
                {
                    keys.Add(shared.Key);
                    report.EntryCount++;
                    string source = english?.GetEntry(shared.Id)?.Value;
                    if (string.IsNullOrWhiteSpace(source))
                    {
                        report.Issues.Add(new Issue { Kind = IssueKind.EmptySource, Table = collectionName, Key = shared.Key, Detail = "has no English text" });
                        continue;
                    }

                    foreach (var table in targets)
                    {
                        string code = table.LocaleIdentifier.Code;
                        string translation = table.GetEntry(shared.Id)?.Value;
                        if (string.IsNullOrWhiteSpace(translation))
                            report.Issues.Add(new Issue { Kind = IssueKind.MissingTranslation, Table = collectionName, Key = shared.Key, Locale = code });
                        else if (IsSheetError(translation))
                            report.Issues.Add(new Issue { Kind = IssueKind.SheetError, Table = collectionName, Key = shared.Key, Locale = code, Detail = $"'{translation}'" });
                        else if (!PlaceholdersMatch(source, translation, out string detail))
                            report.Issues.Add(new Issue { Kind = IssueKind.PlaceholderMismatch, Table = collectionName, Key = shared.Key, Locale = code, Detail = detail });
                    }
                }
            }

            if (includeSourceScan)
            {
                foreach (var reference in LocalizationSourceScanner.FindKeyReferences())
                {
                    string table = string.IsNullOrEmpty(reference.Table) ? Loc.UiTable : reference.Table;
                    if (!knownKeys.TryGetValue(table, out var keys) || !keys.Contains(reference.Key))
                        report.Issues.Add(new Issue { Kind = IssueKind.UnknownKey, Table = table, Key = reference.Key, Detail = $"referenced at {reference.File}:{reference.Line} but not in the table" });
                }

                foreach (var finding in LocalizationSourceScanner.FindRawStrings())
                    report.Issues.Add(new Issue { Kind = IssueKind.RawString, Detail = finding.ToString() });
            }

            return report;
        }

        public static void WriteReport(Report report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
            File.WriteAllText(ReportPath, report.Format(), Encoding.UTF8);
        }

        public static void LogReport(Report report, string prefix)
        {
            WriteReport(report);
            string message = $"[Localization] {prefix}{report.Summary()}. Full report: {ReportPath}";
            if (report.Issues.Count == 0)
                Debug.Log(message);
            else
                Debug.LogWarning(message + "\n" + string.Join("\n", report.Issues.Take(40)) + (report.Issues.Count > 40 ? "\n..." : string.Empty));
        }

        [MenuItem("Mejnun/Localization/Validate", priority = 10)]
        private static void ValidateFromMenu()
        {
            var report = Validate();
            LogReport(report, string.Empty);
            EditorUtility.RevealInFinder(ReportPath);
        }
    }
}
