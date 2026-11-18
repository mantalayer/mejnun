using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mejnun.Core;
using Mejnun.Cosmetics;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Mejnun.Editor.DeckImport
{
    public static class UnsortedDeckImporter
    {
        public const string UnsortedFolder = "Assets/_Project/Art/Cards/Decks/Unsorted";
        public const string DecksFolder = "Assets/_Project/Art/Cards/Decks";
        public const string SkinFolder = "Assets/_Project/Data/Skins";
        public const string CatalogPath = SkinFolder + "/DeckSkinCatalog.asset";
        public const string ImportReportPath = DecksFolder + "/ImportReport.md";

        public static UnsortedDeckScanResult Scan()
        {
            var absoluteFolder = Path.Combine(Application.dataPath, "_Project/Art/Cards/Decks/Unsorted");
            if (!Directory.Exists(absoluteFolder))
            {
                throw new DirectoryNotFoundException("The unsorted deck folder was not found: " + UnsortedFolder);
            }

            var paths = Directory.GetFiles(absoluteFolder, "*.png", SearchOption.AllDirectories)
                .Select(path => "Assets/" + path.Substring(Application.dataPath.Length + 1).Replace('\\', '/'));
            return UnsortedDeckScanner.Scan(paths);
        }

        public static void ImportAll()
        {
            Import(Scan());
        }

        public static void Import(UnsortedDeckScanResult scanResult)
        {
            if (scanResult == null)
            {
                throw new ArgumentNullException(nameof(scanResult));
            }

            new ImportSession().Import(scanResult);
        }

        private sealed class ImportSession
        {
            private readonly Dictionary<string, string> movedAssetPaths =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, Dictionary<string, Sprite>> spriteCache =
                new Dictionary<string, Dictionary<string, Sprite>>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> slicedAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly List<string> reportWarnings = new List<string>();
            private readonly List<ImportReportEntry> reportEntries = new List<ImportReportEntry>();

            public void Import(UnsortedDeckScanResult scanResult)
            {
                EnsureFolder(DecksFolder);
                EnsureFolder(SkinFolder);
                var catalog = AssetDatabase.LoadAssetAtPath<DeckSkinCatalog>(CatalogPath);
                if (catalog == null)
                {
                    throw new InvalidOperationException("The deck skin catalog was not found at " + CatalogPath + ".");
                }

                for (var index = 0; index < scanResult.Warnings.Count; index++)
                {
                    reportWarnings.Add(scanResult.Warnings[index]);
                }

                var existingIds = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < catalog.Skins.Count; index++)
                {
                    if (catalog.Skins[index] != null && !string.IsNullOrWhiteSpace(catalog.Skins[index].Id))
                    {
                        existingIds.Add(catalog.Skins[index].Id);
                    }
                }

                var importedSkins = new List<DeckSkin>();
                for (var index = 0; index < scanResult.Decks.Count; index++)
                {
                    var plan = scanResult.Decks[index];
                    if (!IsComplete(plan))
                    {
                        reportWarnings.Add(plan.Name + ": one or more face slots are missing; left in Unsorted.");
                        continue;
                    }

                    plan.Name = string.IsNullOrWhiteSpace(plan.Name) ? "Deck" : plan.Name.Trim();
                    plan.Id = DeckNameUtility.GetId(plan.Id);
                    if (!existingIds.Add(plan.Id))
                    {
                        reportWarnings.Add(plan.Name + ": id '" + plan.Id +
                                           "' already exists in the catalog; files were left in Unsorted.");
                        continue;
                    }

                    var skin = ImportPlan(plan);
                    importedSkins.Add(skin);
                    var entry = new ImportReportEntry(plan);
                    reportEntries.Add(entry);
                    for (var warningIndex = 0; warningIndex < plan.Warnings.Count; warningIndex++)
                    {
                        reportWarnings.Add(plan.Name + ": " + plan.Warnings[warningIndex]);
                    }
                }

                if (importedSkins.Count > 0)
                {
                    var catalogSkins = new List<DeckSkin>(catalog.Skins.Count + importedSkins.Count);
                    for (var index = 0; index < catalog.Skins.Count; index++)
                    {
                        if (catalog.Skins[index] != null)
                        {
                            catalogSkins.Add(catalog.Skins[index]);
                        }
                    }

                    catalogSkins.AddRange(importedSkins);
                    catalog.Configure(catalogSkins);
                    EditorUtility.SetDirty(catalog);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                WriteReport();
                Debug.Log("Imported " + importedSkins.Count + " deck skins. Report: " + ImportReportPath);
            }

            private DeckSkin ImportPlan(DeckImportPlan plan)
            {
                var deckFolder = DecksFolder + "/" + SanitizeFolderName(plan.Name);
                var facesFolder = deckFolder + "/Faces";
                var backsFolder = deckFolder + "/Backs";
                var extrasFolder = deckFolder + "/Extras";
                EnsureFolder(facesFolder);
                EnsureFolder(backsFolder);
                EnsureFolder(extrasFolder);

                var movedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var sheetSources = new Dictionary<string, List<DeckImportSource>>(StringComparer.OrdinalIgnoreCase);
                var singleSprites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (var faceIndex = 0; faceIndex < plan.Faces.Count; faceIndex++)
                {
                    var source = plan.Faces[faceIndex];
                    RegisterSource(source, facesFolder,
                        source.SpriteIndex >= 0 ? Path.GetFileName(source.Path) : source.SpriteName + ".png",
                        movedPaths, sheetSources, singleSprites);
                }

                for (var backIndex = 0; backIndex < plan.Backs.Count; backIndex++)
                {
                    var source = plan.Backs[backIndex];
                    var isFaceSheet = plan.Faces.Count > 0 &&
                                      string.Equals(source.Path, plan.Faces[0].Path,
                                          StringComparison.OrdinalIgnoreCase);
                    RegisterSource(source, isFaceSheet ? facesFolder : backsFolder,
                        source.SpriteIndex >= 0 ? Path.GetFileName(source.Path) : source.SpriteName + ".png",
                        movedPaths, sheetSources, singleSprites);
                }

                RegisterOptionalSource(plan.Blank, plan, facesFolder, extrasFolder, movedPaths, sheetSources,
                    singleSprites);
                for (var jokerIndex = 0; jokerIndex < plan.Jokers.Count; jokerIndex++)
                {
                    RegisterOptionalSource(plan.Jokers[jokerIndex], plan, facesFolder, extrasFolder, movedPaths,
                        sheetSources, singleSprites);
                }

                for (var extraIndex = 0; extraIndex < plan.Extras.Count; extraIndex++)
                {
                    var extraPath = plan.Extras[extraIndex];
                    var targetPath = MoveSourceAsset(extraPath, extrasFolder, Path.GetFileName(extraPath));
                    movedPaths[extraPath] = targetPath;
                    singleSprites.Add(targetPath);
                }

                foreach (var sheet in sheetSources)
                {
                    var isBackSheet = Path.GetFileNameWithoutExtension(sheet.Key)
                        .IndexOf("backs", StringComparison.OrdinalIgnoreCase) >= 0;
                    SliceSheet(sheet.Key, sheet.Value, isBackSheet ? 7 : 14, 4, plan.PixelArt);
                }

                foreach (var singlePath in singleSprites)
                {
                    ConfigureSingleSprite(singlePath, plan.PixelArt);
                }

                var faceSprites = new Sprite[Card.CardsPerDeck];
                for (var faceIndex = 0; faceIndex < faceSprites.Length; faceIndex++)
                {
                    faceSprites[faceIndex] = LoadSprite(plan.Faces[faceIndex], movedPaths);
                }

                Sprite backSprite;
                var alternateBacks = new List<DeckSkinBack>();
                if (plan.Backs.Count == 0)
                {
                    backSprite = GetClassicBack();
                }
                else
                {
                    backSprite = LoadSprite(plan.Backs[0], movedPaths);
                    for (var backIndex = 1; backIndex < plan.Backs.Count; backIndex++)
                    {
                        var alternate = plan.Backs[backIndex];
                        alternateBacks.Add(new DeckSkinBack(alternate.SpriteName,
                            LoadSprite(alternate, movedPaths)));
                    }
                }

                var blankSprite = plan.Blank == null ? null : LoadSprite(plan.Blank, movedPaths);
                var skinPath = SkinFolder + "/" + plan.Id + ".asset";
                var skin = AssetDatabase.LoadAssetAtPath<DeckSkin>(skinPath);
                if (skin == null)
                {
                    skin = ScriptableObject.CreateInstance<DeckSkin>();
                    AssetDatabase.CreateAsset(skin, skinPath);
                }

                skin.Configure(plan.Id, plan.Name, faceSprites, backSprite, alternateBacks, blankSprite,
                    plan.PixelArt, 100f);
                EditorUtility.SetDirty(skin);
                return skin;
            }

            private void RegisterOptionalSource(DeckImportSource source, DeckImportPlan plan, string facesFolder,
                string extrasFolder, Dictionary<string, string> planMovedPaths,
                Dictionary<string, List<DeckImportSource>> sheetSources, HashSet<string> singleSprites)
            {
                if (source == null)
                {
                    return;
                }

                var isFaceSheet = source.SpriteIndex >= 0 && plan.Faces.Count > 0 &&
                                  string.Equals(source.Path, plan.Faces[0].Path, StringComparison.OrdinalIgnoreCase);
                RegisterSource(source, isFaceSheet ? facesFolder : extrasFolder,
                    source.SpriteIndex >= 0 ? Path.GetFileName(source.Path) : source.SpriteName + ".png",
                    planMovedPaths, sheetSources, singleSprites);
            }

            private void RegisterSource(DeckImportSource source, string destinationFolder, string desiredName,
                Dictionary<string, string> planMovedPaths,
                Dictionary<string, List<DeckImportSource>> sheetSources, HashSet<string> singleSprites)
            {
                if (source == null)
                {
                    throw new InvalidOperationException("A deck import plan contains an empty source entry.");
                }

                var targetPath = MoveSourceAsset(source.Path, destinationFolder, desiredName);
                planMovedPaths[source.Path] = targetPath;
                if (source.SpriteIndex >= 0)
                {
                    if (!sheetSources.TryGetValue(targetPath, out var sources))
                    {
                        sources = new List<DeckImportSource>();
                        sheetSources.Add(targetPath, sources);
                    }

                    if (!sources.Any(existing => existing.SpriteIndex == source.SpriteIndex &&
                                                 string.Equals(existing.SpriteName, source.SpriteName,
                                                     StringComparison.Ordinal)))
                    {
                        sources.Add(source);
                    }
                }
                else
                {
                    singleSprites.Add(targetPath);
                }
            }

            private string MoveSourceAsset(string sourcePath, string destinationFolder, string desiredName)
            {
                if (movedAssetPaths.TryGetValue(sourcePath, out var existingPath))
                {
                    return existingPath;
                }

                if (string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(sourcePath)))
                {
                    throw new FileNotFoundException("The source asset was not found: " + sourcePath);
                }

                var targetPath = GetAvailableAssetPath(destinationFolder + "/" + desiredName);
                var error = AssetDatabase.MoveAsset(sourcePath, targetPath);
                if (!string.IsNullOrEmpty(error))
                {
                    throw new IOException("Could not move " + sourcePath + " to " + targetPath + ": " + error);
                }

                movedAssetPaths.Add(sourcePath, targetPath);
                return targetPath;
            }

            private static string GetAvailableAssetPath(string desiredPath)
            {
                if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(desiredPath)))
                {
                    return desiredPath;
                }

                var directory = Path.GetDirectoryName(desiredPath)?.Replace('\\', '/');
                var extension = Path.GetExtension(desiredPath);
                var name = Path.GetFileNameWithoutExtension(desiredPath);
                for (var index = 2; index < 1000; index++)
                {
                    var candidate = directory + "/" + name + "_" + index + extension;
                    if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(candidate)))
                    {
                        return candidate;
                    }
                }

                throw new IOException("Could not find an available asset name for " + desiredPath + ".");
            }

            private static void ConfigureSingleSprite(string assetPath, bool pixelArt)
            {
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    throw new InvalidOperationException("The moved image is not a texture: " + assetPath);
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                var usePixelArt = pixelArt && texture != null && texture.width <= 128 && texture.height <= 128;
                ConfigureTextureImporter(importer, SpriteImportMode.Single, usePixelArt);
            }

            private static void ConfigureTextureImporter(TextureImporter importer, SpriteImportMode mode,
                bool pixelArt)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = mode;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.filterMode = pixelArt ? FilterMode.Point : FilterMode.Bilinear;
                importer.textureCompression = pixelArt
                    ? TextureImporterCompression.Uncompressed
                    : TextureImporterCompression.Compressed;
                importer.crunchedCompression = false;
                importer.SaveAndReimport();
            }

            private void SliceSheet(string assetPath, List<DeckImportSource> sources, int columns, int rows,
                bool pixelArt)
            {
                if (!slicedAssets.Add(assetPath))
                {
                    return;
                }

                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    throw new InvalidOperationException("The sprite sheet is not a texture: " + assetPath);
                }

                var texture = LoadPngTexture(assetPath);
                try
                {
                    var cellWidth = texture.width / columns;
                    var cellHeight = texture.height / rows;
                    if (cellWidth * columns != texture.width || cellHeight * rows != texture.height)
                    {
                        throw new InvalidOperationException("The sprite sheet dimensions do not match its grid: " +
                                                            assetPath);
                    }

                    ConfigureTextureImporter(importer, SpriteImportMode.Multiple,
                        pixelArt && cellWidth <= 128 && cellHeight <= 128);
                    var factories = new SpriteDataProviderFactories();
                    factories.Init();
                    var dataProvider = factories.GetSpriteEditorDataProviderFromObject(
                        AssetImporter.GetAtPath(assetPath));
                    if (dataProvider == null)
                    {
                        throw new InvalidOperationException("A sprite data provider is unavailable for " + assetPath);
                    }

                    dataProvider.InitSpriteEditorDataProvider();
                    var existingRects = dataProvider.GetSpriteRects();
                    var existingIds = new Dictionary<string, UnityEngine.GUID>(StringComparer.Ordinal);
                    for (var index = 0; index < existingRects.Length; index++)
                    {
                        existingIds[existingRects[index].name] = existingRects[index].spriteID;
                    }

                    var uniqueSources = new Dictionary<int, DeckImportSource>();
                    for (var index = 0; index < sources.Count; index++)
                    {
                        if (!uniqueSources.ContainsKey(sources[index].SpriteIndex))
                        {
                            uniqueSources.Add(sources[index].SpriteIndex, sources[index]);
                        }
                    }

                    var rectangles = new List<SpriteRect>(uniqueSources.Count);
                    var pixels = texture.GetPixels32();
                    foreach (var pair in uniqueSources.OrderBy(entry => entry.Key))
                    {
                        var source = pair.Value;
                        var rectangle = new SpriteRect
                        {
                            name = source.SpriteName,
                            rect = GetTrimmedRect(pixels, texture.width, texture.height, columns, rows,
                                source.SpriteIndex),
                            alignment = SpriteAlignment.Center,
                            pivot = new Vector2(0.5f, 0.5f),
                            border = Vector4.zero
                        };
                        rectangle.spriteID = existingIds.TryGetValue(source.SpriteName, out var spriteId)
                            ? spriteId
                            : UnityEngine.GUID.Generate();
                        rectangles.Add(rectangle);
                    }

                    dataProvider.SetSpriteRects(rectangles.ToArray());
                    dataProvider.Apply();
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }

                importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    throw new InvalidOperationException("The imported sprite sheet is no longer available: " + assetPath);
                }

                importer.SaveAndReimport();
                spriteCache.Remove(assetPath);
            }

            private static Rect GetTrimmedRect(Color32[] pixels, int textureWidth, int textureHeight,
                int columns, int rows, int spriteIndex)
            {
                var cellWidth = textureWidth / columns;
                var cellHeight = textureHeight / rows;
                var cellX = spriteIndex % columns * cellWidth;
                var rowFromTop = spriteIndex / columns;
                var cellY = textureHeight - (rowFromTop + 1) * cellHeight;
                var minX = cellWidth;
                var minY = cellHeight;
                var maxX = -1;
                var maxY = -1;
                for (var y = 0; y < cellHeight; y++)
                {
                    for (var x = 0; x < cellWidth; x++)
                    {
                        if (pixels[(cellY + y) * textureWidth + cellX + x].a == 0)
                        {
                            continue;
                        }

                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                    }
                }

                if (maxX < minX || maxY < minY)
                {
                    return new Rect(cellX, cellY, cellWidth, cellHeight);
                }

                return new Rect(cellX + minX, cellY + minY, maxX - minX + 1, maxY - minY + 1);
            }

            private static Texture2D LoadPngTexture(string assetPath)
            {
                var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                var absolutePath = Path.Combine(projectRoot, assetPath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(File.ReadAllBytes(absolutePath), false))
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    throw new InvalidDataException("Could not read the PNG sprite sheet: " + assetPath);
                }

                return texture;
            }

            private Sprite LoadSprite(DeckImportSource source, Dictionary<string, string> planMovedPaths)
            {
                if (!planMovedPaths.TryGetValue(source.Path, out var assetPath) &&
                    !movedAssetPaths.TryGetValue(source.Path, out assetPath))
                {
                    throw new InvalidOperationException("No moved asset was recorded for " + source.Path);
                }

                if (source.SpriteIndex < 0)
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    if (sprite == null)
                    {
                        throw new InvalidOperationException("A Sprite could not be loaded from " + assetPath);
                    }

                    return sprite;
                }

                if (!spriteCache.TryGetValue(assetPath, out var sprites))
                {
                    sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>()
                        .GroupBy(item => item.name, StringComparer.Ordinal)
                        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
                    spriteCache.Add(assetPath, sprites);
                }

                if (!sprites.TryGetValue(source.SpriteName, out var sheetSprite))
                {
                    throw new InvalidOperationException("Sprite slice '" + source.SpriteName +
                                                        "' was not imported from " + assetPath);
                }

                return sheetSprite;
            }

            private static Sprite GetClassicBack()
            {
                var catalog = AssetDatabase.LoadAssetAtPath<DeckSkinCatalog>(CatalogPath);
                if (catalog != null)
                {
                    for (var index = 0; index < catalog.Skins.Count; index++)
                    {
                        var skin = catalog.Skins[index];
                        if (skin != null && string.Equals(skin.Id, DeckFaceAssigner.ClassicFace,
                                StringComparison.Ordinal))
                        {
                            return skin.CardBack;
                        }
                    }
                }

                throw new InvalidOperationException("A deck without a back requires the classic skin in the catalog.");
            }

            private void WriteReport()
            {
                var report = new StringBuilder();
                report.AppendLine("# Unsorted Deck Import Report");
                report.AppendLine();
                report.AppendLine("Imported " + reportEntries.Count + " deck skins on " +
                                  DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ".");
                report.AppendLine();
                report.AppendLine("## Decks");
                report.AppendLine();
                for (var index = 0; index < reportEntries.Count; index++)
                {
                    var entry = reportEntries[index];
                    report.AppendLine("- **" + entry.Plan.Name + "** (`" + entry.Plan.Id + "`): " +
                                      entry.Plan.Backs.Count + " back(s), 52 faces; folder `" + entry.Folder + "`.");
                    report.AppendLine("  - Sources: `" + entry.Plan.SourceRoot + "`");
                    report.AppendLine("  - Face assignments:");
                    for (var faceIndex = 0; faceIndex < entry.Plan.Faces.Count; faceIndex++)
                    {
                        var face = entry.Plan.Faces[faceIndex];
                        var sheetCell = face.SpriteIndex < 0 ? string.Empty : " (sheet cell " + face.SpriteIndex + ")";
                        report.AppendLine("    - `" + face.SpriteName + "`: `" + face.Path + "`" + sheetCell);
                    }

                    if (entry.Plan.Backs.Count > 0)
                    {
                        report.AppendLine("  - Back IDs: " + string.Join(", ", entry.Plan.Backs
                            .Select(back => "`" + back.SpriteName + "`")));
                    }
                }

                report.AppendLine();
                report.AppendLine("## Warnings");
                report.AppendLine();
                if (reportWarnings.Count == 0)
                {
                    report.AppendLine("- None.");
                }
                else
                {
                    for (var index = 0; index < reportWarnings.Count; index++)
                    {
                        report.AppendLine("- " + reportWarnings[index]);
                    }
                }

                File.WriteAllText(ImportReportPath, report.ToString());
                AssetDatabase.ImportAsset(ImportReportPath, ImportAssetOptions.ForceUpdate);
            }

            private static bool IsComplete(DeckImportPlan plan)
            {
                if (plan == null || string.IsNullOrWhiteSpace(plan.Name) || string.IsNullOrWhiteSpace(plan.Id) ||
                    plan.Faces == null || plan.Faces.Count != Card.CardsPerDeck)
                {
                    return false;
                }

                for (var index = 0; index < plan.Faces.Count; index++)
                {
                    if (plan.Faces[index] == null)
                    {
                        return false;
                    }
                }

                return true;
            }

            private static string SanitizeFolderName(string value)
            {
                var invalidCharacters = Path.GetInvalidFileNameChars();
                var builder = new StringBuilder(value.Length);
                for (var index = 0; index < value.Length; index++)
                {
                    var character = value[index];
                    builder.Append(character == '/' || character == '\\' || Array.IndexOf(invalidCharacters,
                        character) >= 0 ? '_' : character);
                }

                return builder.Length == 0 ? "Deck" : builder.ToString();
            }

            private static void EnsureFolder(string assetFolder)
            {
                if (AssetDatabase.IsValidFolder(assetFolder))
                {
                    return;
                }

                var parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
                var folderName = Path.GetFileName(assetFolder);
                if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolder(parent);
                }

                if (string.IsNullOrEmpty(parent))
                {
                    throw new InvalidOperationException("An asset folder must have a parent: " + assetFolder);
                }

                var guid = AssetDatabase.CreateFolder(parent, folderName);
                if (string.IsNullOrEmpty(guid))
                {
                    throw new IOException("Could not create asset folder " + assetFolder);
                }
            }

            private sealed class ImportReportEntry
            {
                public DeckImportPlan Plan { get; }
                public string Folder { get; }

                public ImportReportEntry(DeckImportPlan plan)
                {
                    Plan = plan;
                    Folder = DecksFolder + "/" + SanitizeFolderName(plan.Name);
                }
            }
        }
    }
}