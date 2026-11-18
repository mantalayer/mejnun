using System;
using System.Collections.Generic;
using System.IO;
using Mejnun.Core;

namespace Mejnun.Editor.DeckImport
{
    public sealed class DeckImportSource
    {
        public string Path { get; }
        public string SpriteName { get; }
        public int SpriteIndex { get; }

        public DeckImportSource(string path, string spriteName, int spriteIndex = -1)
        {
            Path = path;
            SpriteName = spriteName;
            SpriteIndex = spriteIndex;
        }
    }

    public sealed class DeckImportPlan
    {
        private readonly DeckImportSource[] faces = new DeckImportSource[Card.CardsPerDeck];

        public string Name { get; set; }
        public string Id { get; set; }
        public string SourceRoot { get; }
        public bool PixelArt { get; set; }
        public IReadOnlyList<DeckImportSource> Faces => faces;
        public List<DeckImportSource> Backs { get; } = new List<DeckImportSource>();
        public List<DeckImportSource> Jokers { get; } = new List<DeckImportSource>();
        public List<string> Extras { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public DeckImportSource Blank { get; set; }

        public DeckImportPlan(string name, string sourceRoot, bool pixelArt)
        {
            Name = name;
            Id = DeckNameUtility.GetId(name);
            SourceRoot = sourceRoot;
            PixelArt = pixelArt;
        }

        public DeckImportSource GetFace(Suit suit, Rank rank)
        {
            return faces[(int)suit * 13 + (int)rank];
        }

        internal void SetFace(int index, DeckImportSource source)
        {
            faces[index] = source;
        }

        public List<string> GetSourcePaths()
        {
            var paths = new List<string>();
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < faces.Length; index++)
            {
                AddPath(paths, uniquePaths, faces[index]);
            }

            AddPath(paths, uniquePaths, Blank);
            AddPaths(paths, uniquePaths, Backs);
            AddPaths(paths, uniquePaths, Jokers);
            for (var index = 0; index < Extras.Count; index++)
            {
                if (uniquePaths.Add(Extras[index]))
                {
                    paths.Add(Extras[index]);
                }
            }

            return paths;
        }

        private static void AddPaths(List<string> paths, HashSet<string> uniquePaths,
            IReadOnlyList<DeckImportSource> sources)
        {
            for (var index = 0; index < sources.Count; index++)
            {
                AddPath(paths, uniquePaths, sources[index]);
            }
        }

        private static void AddPath(List<string> paths, HashSet<string> uniquePaths, DeckImportSource source)
        {
            if (source != null && uniquePaths.Add(source.Path))
            {
                paths.Add(source.Path);
            }
        }
    }

    public sealed class UnsortedDeckScanResult
    {
        public IReadOnlyList<DeckImportPlan> Decks { get; }
        public IReadOnlyList<string> Warnings { get; }

        public UnsortedDeckScanResult(List<DeckImportPlan> decks, List<string> warnings)
        {
            Decks = decks;
            Warnings = warnings;
        }
    }

    public static class UnsortedDeckScanner
    {
        private const int FaceSheetColumns = 14;
        private const int FaceSheetRows = 4;
        private const int BackSheetColumns = 7;
        private const int BackSheetRows = 4;

        private static readonly string[] RankNames =
        {
            "ace", "2", "3", "4", "5", "6", "7", "8", "9", "10", "jack", "queen", "king"
        };

        private static readonly string[] SuitNames = { "clubs", "diamonds", "hearts", "spades" };

        public static UnsortedDeckScanResult Scan(IEnumerable<string> relativePngPaths)
        {
            if (relativePngPaths == null)
            {
                throw new ArgumentNullException(nameof(relativePngPaths));
            }

            var paths = NormalizePaths(relativePngPaths);
            var backSheets = new List<string>();
            var faceSheets = new List<string>();
            for (var index = 0; index < paths.Count; index++)
            {
                if (IsBackSheet(paths[index]))
                {
                    backSheets.Add(paths[index]);
                }
                else if (IsFaceSheet(paths[index]))
                {
                    faceSheets.Add(paths[index]);
                }
            }

            var plans = new List<DeckImportPlan>();
            var usedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < faceSheets.Count; index++)
            {
                var plan = CreateSheetPlan(faceSheets[index], FindSharedBackSheet(faceSheets[index], backSheets));
                plans.Add(plan);
                AddPaths(usedPaths, plan.GetSourcePaths());
            }

            var candidates = FindDirectoryCandidates(paths, faceSheets, backSheets);
            var selectedCandidates = SelectOuterCandidates(candidates);
            for (var index = 0; index < selectedCandidates.Count; index++)
            {
                var candidate = selectedCandidates[index];
                var plan = CreateIndividualPlan(candidate, paths);
                plans.Add(plan);
                AddPaths(usedPaths, plan.GetSourcePaths());
            }

            plans.Sort((left, right) =>
            {
                var rootCompare = string.Compare(left.SourceRoot, right.SourceRoot, StringComparison.OrdinalIgnoreCase);
                return rootCompare != 0
                    ? rootCompare
                    : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });

            AttachLooseFiles(paths, plans, backSheets, usedPaths);
            var warnings = FindUnassignedFaceWarnings(paths, usedPaths, faceSheets, backSheets);
            for (var planIndex = 0; planIndex < plans.Count; planIndex++)
            {
                for (var warningIndex = 0; warningIndex < plans[planIndex].Warnings.Count; warningIndex++)
                {
                    warnings.Add(plans[planIndex].Name + ": " + plans[planIndex].Warnings[warningIndex]);
                }
            }

            return new UnsortedDeckScanResult(plans, warnings);
        }

        private static List<string> NormalizePaths(IEnumerable<string> input)
        {
            var paths = new List<string>();
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var inputPath in input)
            {
                if (string.IsNullOrWhiteSpace(inputPath) ||
                    !string.Equals(Path.GetExtension(inputPath), ".png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var path = inputPath.Replace('\\', '/').TrimStart('/');
                while (path.StartsWith("./", StringComparison.Ordinal))
                {
                    path = path.Substring(2);
                }

                if (uniquePaths.Add(path))
                {
                    paths.Add(path);
                }
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths;
        }

        private static List<DirectoryCandidate> FindDirectoryCandidates(List<string> paths,
            List<string> faceSheets, List<string> backSheets)
        {
            var sheetPaths = new HashSet<string>(faceSheets, StringComparer.OrdinalIgnoreCase);
            AddPaths(sheetPaths, backSheets);
            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                var components = paths[pathIndex].Split('/');
                var directory = string.Empty;
                for (var componentIndex = 0; componentIndex < components.Length - 1; componentIndex++)
                {
                    directory = directory.Length == 0 ? components[componentIndex]
                        : directory + "/" + components[componentIndex];
                    directories.Add(directory);
                }
            }

            var candidates = new List<DirectoryCandidate>();
            foreach (var directory in directories)
            {
                var facePaths = new Dictionary<int, string>();
                var faceCount = 0;
                var duplicate = false;
                for (var pathIndex = 0; pathIndex < paths.Count; pathIndex++)
                {
                    var path = paths[pathIndex];
                    if (sheetPaths.Contains(path) || !IsWithin(path, directory))
                    {
                        continue;
                    }

                    var parsed = CardFileNameParser.Parse(path);
                    if (parsed.Kind != CardFileKind.Face)
                    {
                        continue;
                    }

                    faceCount++;
                    var faceIndex = (int)parsed.Suit * 13 + (int)parsed.Rank;
                    if (facePaths.ContainsKey(faceIndex))
                    {
                        duplicate = true;
                    }
                    else
                    {
                        facePaths.Add(faceIndex, path);
                    }
                }

                if (faceCount == Card.CardsPerDeck && facePaths.Count == Card.CardsPerDeck && !duplicate)
                {
                    candidates.Add(new DirectoryCandidate(directory, facePaths));
                }
            }

            return candidates;
        }

        private static List<DirectoryCandidate> SelectOuterCandidates(List<DirectoryCandidate> candidates)
        {
            var selected = new List<DirectoryCandidate>();
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                var hasMatchingAncestor = false;
                for (var otherIndex = 0; otherIndex < candidates.Count; otherIndex++)
                {
                    var other = candidates[otherIndex];
                    if (otherIndex != index && IsWithin(candidate.Root, other.Root) &&
                        candidate.FacePaths.Count == other.FacePaths.Count &&
                        ContainsSamePaths(candidate.FacePaths, other.FacePaths))
                    {
                        hasMatchingAncestor = true;
                        break;
                    }
                }

                if (!hasMatchingAncestor)
                {
                    selected.Add(candidate);
                }
            }

            selected.Sort((left, right) => string.Compare(left.Root, right.Root,
                StringComparison.OrdinalIgnoreCase));
            return selected;
        }

        private static DeckImportPlan CreateIndividualPlan(DirectoryCandidate candidate, List<string> paths)
        {
            var plan = new DeckImportPlan(DeckNameUtility.GetDisplayName(candidate.Root), candidate.Root,
                candidate.Root.IndexOf("hack", StringComparison.OrdinalIgnoreCase) >= 0);
            for (var faceIndex = 0; faceIndex < Card.CardsPerDeck; faceIndex++)
            {
                candidate.FacePaths.TryGetValue(faceIndex, out var path);
                plan.SetFace(faceIndex, new DeckImportSource(path, FaceSpriteName(faceIndex)));
            }

            var backIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                var path = paths[pathIndex];
                if (!IsWithin(path, candidate.Root) || ContainsFacePath(candidate.FacePaths, path))
                {
                    continue;
                }

                var parsed = CardFileNameParser.Parse(path);
                if (parsed.Kind == CardFileKind.Back)
                {
                    if (backIds.Add(parsed.BackId))
                    {
                        plan.Backs.Add(new DeckImportSource(path, parsed.BackId));
                    }
                    else
                    {
                        plan.Warnings.Add("Duplicate back id " + parsed.BackId + " was ignored.");
                    }
                }
                else if (parsed.Kind == CardFileKind.Joker)
                {
                    var name = string.IsNullOrEmpty(parsed.JokerColor) ? "joker" : "joker_" + parsed.JokerColor;
                    plan.Jokers.Add(new DeckImportSource(path, name));
                }
                else if (parsed.Kind == CardFileKind.Blank)
                {
                    if (plan.Blank == null)
                    {
                        plan.Blank = new DeckImportSource(path, "blank");
                    }
                    else
                    {
                        plan.Extras.Add(path);
                    }
                }
                else
                {
                    plan.Extras.Add(path);
                }
            }

            plan.Backs.Sort(CompareBacks);
            if (plan.Backs.Count == 0)
            {
                plan.Warnings.Add("No card back was found; the classic back will be used.");
            }

            return plan;
        }

        private static DeckImportPlan CreateSheetPlan(string sheetPath, string sharedBacksPath)
        {
            var root = GetDirectory(sheetPath);
            var plan = new DeckImportPlan(DeckNameUtility.GetDisplayName(sheetPath), root, true);
            for (var suitIndex = 0; suitIndex < 4; suitIndex++)
            {
                var row = SuitRow(suitIndex);
                for (var rankIndex = 0; rankIndex < 13; rankIndex++)
                {
                    var faceIndex = suitIndex * 13 + rankIndex;
                    var cellIndex = row * FaceSheetColumns + rankIndex;
                    plan.SetFace(faceIndex, new DeckImportSource(sheetPath, FaceSpriteName(faceIndex), cellIndex));
                }
            }

            plan.Jokers.Add(new DeckImportSource(sheetPath, "joker_black", 13));
            plan.Jokers.Add(new DeckImportSource(sheetPath, "joker_red", FaceSheetColumns + 13));
            plan.Blank = new DeckImportSource(sheetPath, "blank", 2 * FaceSheetColumns + 13);
            if (string.IsNullOrEmpty(sharedBacksPath))
            {
                plan.Backs.Add(new DeckImportSource(sheetPath, "back_01",
                    (FaceSheetRows - 1) * FaceSheetColumns + FaceSheetColumns - 1));
            }
            else
            {
                for (var backIndex = 0; backIndex < BackSheetColumns * BackSheetRows; backIndex++)
                {
                    plan.Backs.Add(new DeckImportSource(sharedBacksPath,
                        "back_" + (backIndex + 1).ToString(backIndex < 9 ? "00" : "0"), backIndex));
                }
            }

            return plan;
        }

        private static void AttachLooseFiles(List<string> paths, List<DeckImportPlan> plans,
            List<string> backSheets, HashSet<string> usedPaths)
        {
            var sharedBacks = new HashSet<string>(backSheets, StringComparer.OrdinalIgnoreCase);
            for (var pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                var path = paths[pathIndex];
                if (usedPaths.Contains(path) || sharedBacks.Contains(path) ||
                    CardFileNameParser.Parse(path).Kind == CardFileKind.Face)
                {
                    continue;
                }

                var key = GetCollectionKey(path);
                for (var planIndex = 0; planIndex < plans.Count; planIndex++)
                {
                    if (!string.Equals(key, GetCollectionKey(plans[planIndex].SourceRoot),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    plans[planIndex].Extras.Add(path);
                    usedPaths.Add(path);
                    break;
                }
            }
        }

        private static List<string> FindUnassignedFaceWarnings(List<string> paths, HashSet<string> usedPaths,
            List<string> faceSheets, List<string> backSheets)
        {
            var warnings = new List<string>();
            var knownSheets = new HashSet<string>(faceSheets, StringComparer.OrdinalIgnoreCase);
            AddPaths(knownSheets, backSheets);
            var warnedCollections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < paths.Count; index++)
            {
                var path = paths[index];
                if (usedPaths.Contains(path) || knownSheets.Contains(path) ||
                    CardFileNameParser.Parse(path).Kind != CardFileKind.Face)
                {
                    continue;
                }

                var key = GetCollectionKey(path);
                if (warnedCollections.Add(key))
                {
                    warnings.Add("Skipped incomplete or ambiguous face collection '" + key +
                                 "'; every card must have exactly one face.");
                }
            }

            return warnings;
        }

        private static string FindSharedBackSheet(string faceSheetPath, List<string> backSheets)
        {
            var directory = GetDirectory(faceSheetPath);
            for (var index = 0; index < backSheets.Count; index++)
            {
                if (string.Equals(GetDirectory(backSheets[index]), directory, StringComparison.OrdinalIgnoreCase))
                {
                    return backSheets[index];
                }
            }

            return null;
        }

        private static bool IsFaceSheet(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            return name.StartsWith("deck_", StringComparison.OrdinalIgnoreCase) && !IsBackSheet(path);
        }

        private static bool IsBackSheet(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            return name.IndexOf("backs", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsWithin(string path, string directory)
        {
            return path.StartsWith(directory.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsSamePaths(Dictionary<int, string> left, Dictionary<int, string> right)
        {
            foreach (var pair in left)
            {
                if (!right.TryGetValue(pair.Key, out var path) ||
                    !string.Equals(path, pair.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsFacePath(Dictionary<int, string> facePaths, string path)
        {
            foreach (var facePath in facePaths.Values)
            {
                if (string.Equals(facePath, path, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareBacks(DeckImportSource left, DeckImportSource right)
        {
            var leftNumber = ParseBackNumber(left.SpriteName);
            var rightNumber = ParseBackNumber(right.SpriteName);
            var compare = leftNumber.CompareTo(rightNumber);
            return compare != 0 ? compare : string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase);
        }

        private static int ParseBackNumber(string backId)
        {
            var separator = backId.LastIndexOf('_');
            return int.TryParse(separator < 0 ? backId : backId.Substring(separator + 1), out var number)
                ? number
                : int.MaxValue;
        }

        private static string FaceSpriteName(int faceIndex)
        {
            var suitIndex = faceIndex / 13;
            var rankIndex = faceIndex % 13;
            return RankNames[rankIndex] + "_of_" + SuitNames[suitIndex];
        }

        private static int SuitRow(int suitIndex)
        {
            if (suitIndex == (int)Suit.Spades)
            {
                return 0;
            }

            if (suitIndex == (int)Suit.Hearts)
            {
                return 1;
            }

            return suitIndex == (int)Suit.Clubs ? 2 : 3;
        }

        private static string GetDirectory(string path)
        {
            var separator = path.LastIndexOf('/');
            return separator < 0 ? string.Empty : path.Substring(0, separator);
        }

        private static string GetCollectionKey(string path)
        {
            const string unsortedSegment = "/Unsorted/";
            var normalized = path.Replace('\\', '/');
            var unsortedIndex = normalized.IndexOf(unsortedSegment, StringComparison.OrdinalIgnoreCase);
            var collectionStart = unsortedIndex >= 0 ? unsortedIndex + unsortedSegment.Length : 0;
            var nextSeparator = normalized.IndexOf('/', collectionStart);
            return nextSeparator < 0 ? normalized.Substring(collectionStart)
                : normalized.Substring(collectionStart, nextSeparator - collectionStart);
        }

        private static void AddPaths(HashSet<string> target, IReadOnlyList<string> paths)
        {
            for (var index = 0; index < paths.Count; index++)
            {
                target.Add(paths[index]);
            }
        }

        private static void AddPaths(HashSet<string> target, List<string> paths)
        {
            for (var index = 0; index < paths.Count; index++)
            {
                target.Add(paths[index]);
            }
        }

        private sealed class DirectoryCandidate
        {
            public string Root { get; }
            public Dictionary<int, string> FacePaths { get; }

            public DirectoryCandidate(string root, Dictionary<int, string> facePaths)
            {
                Root = root;
                FacePaths = facePaths;
            }
        }
    }
}