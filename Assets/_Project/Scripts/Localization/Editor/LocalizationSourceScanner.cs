using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Mejnun.Localization.Editor
{
    // Static checks over game source:
    //  * raw player-facing literals in UXML text/label/tooltip attributes and
    //    in common C# text assignments (.text = "...", new Label("..."));
    //  * String Table keys referenced from code (Loc.Get("key"), ...) and from
    //    UXML bindings (<UnityEngine.Localization.LocalizedString entry="key">).
    // Suppress a deliberate exception with "// loc-ignore" on the C# line, or
    // the "loc-ignore" USS class on the UXML element (for example, a glyph or
    // a proper noun that must never change).
    public static class LocalizationSourceScanner
    {
        public const string ScriptsRoot = "Assets/_Project/Scripts";
        public const string IgnoreMarker = "loc-ignore";

        public struct Finding
        {
            public string File;
            public int Line;
            public string Text;

            public override string ToString() => $"{File}:{Line}: {Text}";
        }

        public struct KeyReference
        {
            public string Table;
            public string Key;
            public string File;
            public int Line;
        }

        private static readonly Regex UxmlTextAttribute = new(
            "\\s(text|label|tooltip|title|placeholder-text)=\"([^\"]*)\"", RegexOptions.Compiled);
        private static readonly Regex UxmlBinding = new(
            "<UnityEngine\\.Localization\\.LocalizedString\\b[^>]*>", RegexOptions.Compiled);
        private static readonly Regex AttributeValue = new("\\s(\\w[\\w-]*)=\"([^\"]*)\"", RegexOptions.Compiled);

        private static readonly Regex CsLocCall = new(
            "Loc\\.(Get|Format|Has)\\(\\s*\"([^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex CsLocTableCall = new(
            "Loc\\.(GetFrom|FormatFrom|Has)\\(\\s*Loc\\.(UiTable|DealerQuestionsTable)\\s*,\\s*\"([^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex CsLocArgument = new(
            "Loc\\.\\w+\\(\\s*(Loc\\.\\w+\\s*,\\s*)?\\$?\"[^\"]*\"", RegexOptions.Compiled);
        private static readonly Regex CsTextAssignment = new(
            "\\.(text|label|tooltip|title)\\s*=(?!=)(.*)", RegexOptions.Compiled);
        private static readonly Regex CsTextConstructor = new(
            "new\\s+(Label|Button|Toggle|Foldout|TextField|DropdownField|Tab|GroupBox)\\s*\\((.*)", RegexOptions.Compiled);
        private static readonly Regex CsStringLiteral = new("\\$?@?\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);
        private static readonly Regex InterpolationHole = new("\\{[^{}]*\\}", RegexOptions.Compiled);
        private static readonly Regex Letter = new("\\p{L}", RegexOptions.Compiled);

        public static IEnumerable<string> SourceFiles(string extension)
        {
            if (!Directory.Exists(ScriptsRoot))
                yield break;

            foreach (string file in Directory.GetFiles(ScriptsRoot, "*" + extension, SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                // Editor tooling is developer-facing; tests may assert on literals.
                if (path.Contains("/Editor/") || path.Contains("/Tests/"))
                    continue;
                yield return path;
            }
        }

        public static List<Finding> FindRawStrings()
        {
            var findings = new List<Finding>();
            foreach (string file in SourceFiles(".uxml"))
                ScanUxmlRawStrings(file, File.ReadAllLines(file), findings);
            foreach (string file in SourceFiles(".cs"))
                ScanCsRawStrings(file, File.ReadAllLines(file), findings);
            return findings;
        }

        public static List<KeyReference> FindKeyReferences()
        {
            var references = new List<KeyReference>();
            foreach (string file in SourceFiles(".uxml"))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (Match binding in UxmlBinding.Matches(lines[i]))
                    {
                        string table = null, entry = null;
                        foreach (Match attribute in AttributeValue.Matches(binding.Value))
                        {
                            if (attribute.Groups[1].Value == "table")
                                table = attribute.Groups[2].Value;
                            else if (attribute.Groups[1].Value == "entry")
                                entry = attribute.Groups[2].Value;
                        }

                        if (!string.IsNullOrEmpty(entry))
                            references.Add(new KeyReference { Table = table ?? string.Empty, Key = entry, File = file, Line = i + 1 });
                    }
                }
            }

            foreach (string file in SourceFiles(".cs"))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (Match match in CsLocCall.Matches(lines[i]))
                        references.Add(new KeyReference { Table = Loc.UiTable, Key = match.Groups[2].Value, File = file, Line = i + 1 });

                    foreach (Match match in CsLocTableCall.Matches(lines[i]))
                    {
                        string table = match.Groups[2].Value == "UiTable" ? Loc.UiTable : Loc.DealerQuestionsTable;
                        references.Add(new KeyReference { Table = table, Key = match.Groups[3].Value, File = file, Line = i + 1 });
                    }
                }
            }

            return references;
        }

        public static void ScanUxmlRawStrings(string file, string[] lines, List<Finding> findings)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Contains(IgnoreMarker) || line.TrimStart().StartsWith("<UnityEngine.Localization"))
                    continue;

                foreach (Match match in UxmlTextAttribute.Matches(line))
                {
                    string value = match.Groups[2].Value;
                    if (Letter.IsMatch(value))
                        findings.Add(new Finding { File = file, Line = i + 1, Text = $"{match.Groups[1].Value}=\"{value}\"" });
                }
            }
        }

        public static void ScanCsRawStrings(string file, string[] lines, List<Finding> findings)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || line.Contains(IgnoreMarker))
                    continue;

                string rhs = null;
                var assignment = CsTextAssignment.Match(line);
                if (assignment.Success)
                    rhs = assignment.Groups[2].Value;
                else
                {
                    var constructor = CsTextConstructor.Match(line);
                    if (constructor.Success)
                        rhs = constructor.Groups[2].Value;
                }

                if (rhs == null)
                    continue;

                rhs = CsLocArgument.Replace(rhs, "Loc()");
                foreach (Match literal in CsStringLiteral.Matches(rhs))
                {
                    string content = InterpolationHole.Replace(literal.Groups[1].Value, string.Empty);
                    if (Letter.IsMatch(content))
                    {
                        findings.Add(new Finding { File = file, Line = i + 1, Text = trimmed });
                        break;
                    }
                }
            }
        }
    }
}
