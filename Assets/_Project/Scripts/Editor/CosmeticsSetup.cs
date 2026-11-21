using System;
using System.Collections.Generic;
using System.IO;
using Mejnun.Cosmetics;
using UnityEditor;
using UnityEngine;

namespace Mejnun.Editor
{
    public static class CosmeticsSetup
    {
        private const string SkinFolder = "Assets/_Project/Data/Skins";
        private const string FeltFolder = "Assets/_Project/Data/Felts";
        private const string CosmeticFolder = "Assets/_Project/Data/Cosmetics";
        private const string ArtFolder = "Assets/_Project/Art/Felts";
        private const string DeckCatalogPath = SkinFolder + "/DeckSkinCatalog.asset";
        private const string FeltCatalogPath = FeltFolder + "/TableFeltCatalog.asset";
        private const string CosmeticCatalogPath = CosmeticFolder + "/CosmeticCatalog.asset";

        [MenuItem("Mejnun/Setup/Apply Cosmetic Unlocks")]
        public static void ApplyCosmeticUnlocks()
        {
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder(FeltFolder);
            EnsureFolder(CosmeticFolder);
            EnsureFolder(ArtFolder);

            var deckCatalog = AssetDatabase.LoadAssetAtPath<DeckSkinCatalog>(DeckCatalogPath);
            if (deckCatalog == null)
            {
                throw new InvalidOperationException("Build the deck skin catalog before applying cosmetic unlocks.");
            }

            RebuildSkinCatalog(deckCatalog);

            var greenTile = EnsureSprite("green_tile", 16, 16, GreenTile, TextureWrapMode.Repeat, Vector4.zero);
            var burgundyTile = EnsureSprite("burgundy_tile", 16, 16, BurgundyTile, TextureWrapMode.Repeat,
                Vector4.zero);
            var burgundyBorder = EnsureSprite("burgundy_border", 64, 64, BurgundyBorder, TextureWrapMode.Clamp,
                new Vector4(8f, 8f, 8f, 8f));
            var midnightImage = EnsureSprite("midnight_image", 256, 256, MidnightImage, TextureWrapMode.Clamp,
                Vector4.zero);

            var green = GetOrCreate<TableFelt>(FeltFolder + "/Green.asset");
            green.Configure("green", "Green", FeltRenderMode.Tiled, greenTile, 1f, null, null,
                new Color(0.7f, 1f, 0.8f, 1f), new Color(0.02f, 0.19f, 0.1f, 1f), UnlockRequirement.Free);
            var burgundy = GetOrCreate<TableFelt>(FeltFolder + "/Burgundy.asset");
            burgundy.Configure("burgundy", "Burgundy", FeltRenderMode.TiledWithBorder, burgundyTile, 1f,
                burgundyBorder, null, Color.white, new Color(0.19f, 0.035f, 0.08f, 1f), UnlockRequirement.Free);
            var midnight = GetOrCreate<TableFelt>(FeltFolder + "/Midnight.asset");
            midnight.Configure("midnight", "Midnight", FeltRenderMode.Image, null, 1f, null, midnightImage,
                Color.white, new Color(0.025f, 0.035f, 0.12f, 1f), new UnlockRequirement(UnlockKind.Level, 3));

            var feltCatalog = GetOrCreate<TableFeltCatalog>(FeltCatalogPath);
            feltCatalog.Configure(new[] { green, burgundy, midnight });

            var cosmeticCatalog = GetOrCreate<CosmeticCatalog>(CosmeticCatalogPath);
            cosmeticCatalog.Configure(deckCatalog, feltCatalog);

            EditorUtility.SetDirty(green);
            EditorUtility.SetDirty(burgundy);
            EditorUtility.SetDirty(midnight);
            EditorUtility.SetDirty(feltCatalog);
            EditorUtility.SetDirty(cosmeticCatalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Applied M6 cosmetic unlocks and generated the felt catalog.");
        }

        private static void RebuildSkinCatalog(DeckSkinCatalog catalog)
        {
            var guids = AssetDatabase.FindAssets("t:DeckSkin", new[] { SkinFolder });
            var skinAssets = new List<DeckSkin>();
            for (var index = 0; index < guids.Length; index++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[index]);
                var skin = AssetDatabase.LoadAssetAtPath<DeckSkin>(path);
                if (skin == null)
                {
                    continue;
                }

                skinAssets.Add(skin);
            }

            skinAssets.Sort((first, second) => string.Compare(first.Id, second.Id, StringComparison.Ordinal));
            var completeCatalog = new List<DeckSkin>(skinAssets.Count);
            var includedIds = new HashSet<string>(StringComparer.Ordinal);
            if (catalog.Skins != null)
            {
                for (var index = 0; index < catalog.Skins.Count; index++)
                {
                    var skin = catalog.Skins[index];
                    if (skin != null && includedIds.Add(skin.Id))
                    {
                        completeCatalog.Add(skin);
                    }
                }
            }

            for (var index = 0; index < skinAssets.Count; index++)
            {
                var skin = skinAssets[index];
                if (!catalog.IsAddressableSkin(skin.Id) && includedIds.Add(skin.Id))
                {
                    completeCatalog.Add(skin);
                }
            }

            catalog.Configure(completeCatalog);
            EditorUtility.SetDirty(catalog);
        }

        private static Sprite EnsureSprite(string name, int width, int height,
            Func<int, int, Color32> pixel, TextureWrapMode wrapMode, Vector4 border)
        {
            var assetPath = ArtFolder + "/" + name + ".png";
            var diskPath = Path.Combine(Application.dataPath,
                assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(diskPath))
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var pixels = new Color32[width * height];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        pixels[y * width + x] = pixel(x, y);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(diskPath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = wrapMode;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.spritePixelsPerUnit = 16f;
                importer.spriteBorder = border;
                var textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                textureSettings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(textureSettings);
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                throw new InvalidOperationException("Could not import felt sprite " + assetPath + ".");
            }

            return sprite;
        }

        private static Color32 GreenTile(int x, int y)
        {
            var shade = (x + y * 3) % 7 == 0 ? (byte)18 : (byte)0;
            return new Color32((byte)shade, (byte)(100 + shade), (byte)(56 + shade), 255);
        }

        private static Color32 BurgundyTile(int x, int y)
        {
            var shade = (x * 3 + y) % 5 == 0 ? (byte)18 : (byte)0;
            return new Color32((byte)(92 + shade), (byte)shade, (byte)(35 + shade), 255);
        }

        private static Color32 BurgundyBorder(int x, int y)
        {
            if (x >= 8 && x < 56 && y >= 8 && y < 56)
            {
                return new Color32(0, 0, 0, 0);
            }

            var shade = (x + y) % 6 < 3 ? (byte)145 : (byte)95;
            return new Color32(shade, (byte)10, (byte)44, 255);
        }

        private static Color32 MidnightImage(int x, int y)
        {
            var star = (x * 17 + y * 31) % 89 == 0;
            var glow = (byte)(Mathf.Max(0, 56 - Mathf.Abs(x - 128) / 3 - Mathf.Abs(y - 128) / 3));
            return star
                ? new Color32(138, 158, 255, 255)
                : new Color32((byte)(12 + glow / 3), (byte)(18 + glow / 2), (byte)(52 + glow), 255);
        }

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}