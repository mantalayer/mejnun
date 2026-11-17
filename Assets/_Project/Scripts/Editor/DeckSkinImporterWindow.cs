using System;
using System.Collections.Generic;
using System.Globalization;
using Mejnun.Core;
using Mejnun.Cosmetics;
using UnityEditor;
using UnityEngine;

namespace Mejnun.Editor
{
    public sealed class DeckSkinImporterWindow : EditorWindow
    {
        private const string ClassicTexturePath =
            "Assets/_Project/Art/Cards/Decks/Classic/cards.png";
        private const string IvoryTexturePath =
            "Assets/_Project/Art/Cards/Decks/ClassicIvory/cards_ivory.png";
        private const string SkinFolder = "Assets/_Project/Data/Skins";
        private const string CatalogPath = SkinFolder + "/DeckSkinCatalog.asset";

        private static readonly string[] SuitLabels = { "Clubs", "Diamonds", "Hearts", "Spades" };
        private static readonly string[] RankLabels =
        {
            "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K"
        };

        private readonly Sprite[] assignedFaces = new Sprite[Card.CardsPerDeck];
        private readonly Dictionary<int, Sprite> spritesByIndex = new Dictionary<int, Sprite>();
        private Texture2D sourceTexture;
        private Sprite[] sourceSprites = Array.Empty<Sprite>();
        private DeckLayoutPreset preset = DeckLayoutPreset.ZigZag;
        private Sprite selectedSprite;
        private string skinId = "custom";
        private string skinDisplayName = "Custom";
        private Vector2 gridScroll;
        private Vector2 sourceScroll;

        [MenuItem("Mejnun/Deck Skin Importer")]
        public static void Open()
        {
            GetWindow<DeckSkinImporterWindow>("Deck Skin Importer");
        }

        public static void GenerateBaseSkinAssets()
        {
            EnsureFolder(SkinFolder);
            var classic = GenerateSkinAsset(ClassicTexturePath, "classic", "Classic", DeckLayoutPreset.ZigZag,
                SkinFolder + "/Classic.asset");
            var ivory = GenerateSkinAsset(IvoryTexturePath, "classic_ivory", "Classic Ivory",
                DeckLayoutPreset.ZigZag, SkinFolder + "/ClassicIvory.asset");

            var catalog = LoadOrCreateAsset<DeckSkinCatalog>(CatalogPath);
            catalog.Configure(new[] { classic, ivory });
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static DeckSkin GenerateSkinAsset(string texturePath, string id, string displayName,
            DeckLayoutPreset layout, string assetPath)
        {
            var sprites = LoadIndexedSprites(texturePath);
            var faces = new Sprite[Card.CardsPerDeck];
            for (var suitIndex = 0; suitIndex < 4; suitIndex++)
            {
                for (var rankIndex = 0; rankIndex < 13; rankIndex++)
                {
                    var spriteIndex = DeckLayoutPresetUtility.GetFaceSpriteIndex(layout, (Suit)suitIndex,
                        (Rank)rankIndex);
                    if (!sprites.TryGetValue(spriteIndex, out faces[suitIndex * 13 + rankIndex]))
                    {
                        throw new InvalidOperationException("Sprite " + spriteIndex + " is missing from " + texturePath + ".");
                    }
                }
            }

            var backIndex = DeckLayoutPresetUtility.GetCardBackSpriteIndex(layout);
            var blankIndex = DeckLayoutPresetUtility.GetBlankSpriteIndex();
            if (!sprites.TryGetValue(backIndex, out var backSprite))
            {
                throw new InvalidOperationException("Card-back sprite " + backIndex + " is missing from " + texturePath + ".");
            }

            if (!sprites.TryGetValue(blankIndex, out var blankSprite))
            {
                throw new InvalidOperationException("Blank sprite " + blankIndex + " is missing from " + texturePath + ".");
            }

            var skin = LoadOrCreateAsset<DeckSkin>(assetPath);
            skin.Configure(id, displayName, faces, backSprite, blankSprite, true, 100f);
            EditorUtility.SetDirty(skin);
            return skin;
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Source Texture", sourceTexture,
                typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck())
            {
                LoadSourceSprites();
            }

            preset = (DeckLayoutPreset)EditorGUILayout.EnumPopup("Layout Preset", preset);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Preset"))
            {
                ApplyPreset();
            }

            if (GUILayout.Button("Generate Classic Assets"))
            {
                GenerateBaseSkinAssets();
            }

            EditorGUILayout.EndHorizontal();

            skinId = EditorGUILayout.TextField("Skin Id", skinId);
            skinDisplayName = EditorGUILayout.TextField("Display Name", skinDisplayName);

            if (sourceTexture == null)
            {
                EditorGUILayout.HelpBox("Choose a sliced Sprite texture, then apply a preset or assign sprites manually.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Face Mapping", EditorStyles.boldLabel);
            gridScroll = EditorGUILayout.BeginScrollView(gridScroll, GUILayout.Height(370));
            DrawFaceGrid();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.LabelField("Source Sprites — click one, then click a face slot", EditorStyles.boldLabel);
            sourceScroll = EditorGUILayout.BeginScrollView(sourceScroll, GUILayout.Height(150));
            DrawSourceSprites();
            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(!HasCompleteMapping()))
            {
                if (GUILayout.Button("Save Deck Skin"))
                {
                    SaveConfiguredSkin();
                }
            }
        }

        private void DrawFaceGrid()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(string.Empty, GUILayout.Width(80));
            for (var rankIndex = 0; rankIndex < RankLabels.Length; rankIndex++)
            {
                GUILayout.Label(RankLabels[rankIndex], GUILayout.Width(48));
            }

            EditorGUILayout.EndHorizontal();

            for (var suitIndex = 0; suitIndex < SuitLabels.Length; suitIndex++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(SuitLabels[suitIndex], GUILayout.Width(80));
                for (var rankIndex = 0; rankIndex < RankLabels.Length; rankIndex++)
                {
                    var slotIndex = suitIndex * 13 + rankIndex;
                    var rect = GUILayoutUtility.GetRect(46, 64, GUILayout.Width(46), GUILayout.Height(64));
                    GUI.Box(rect, GUIContent.none);
                    DrawSpritePreview(rect, assignedFaces[slotIndex]);
                    if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                    {
                        if (selectedSprite != null)
                        {
                            assignedFaces[slotIndex] = selectedSprite;
                        }

                        Event.current.Use();
                        Repaint();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawSourceSprites()
        {
            const int columns = 10;
            var spriteIndex = 0;
            while (spriteIndex < sourceSprites.Length)
            {
                EditorGUILayout.BeginHorizontal();
                for (var column = 0; column < columns && spriteIndex < sourceSprites.Length; column++, spriteIndex++)
                {
                    var sprite = sourceSprites[spriteIndex];
                    var rect = GUILayoutUtility.GetRect(48, 66, GUILayout.Width(48), GUILayout.Height(66));
                    GUI.Box(rect, GUIContent.none);
                    DrawSpritePreview(rect, sprite);
                    if (sprite == selectedSprite)
                    {
                        GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
                    }

                    if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                    {
                        selectedSprite = sprite;
                        Event.current.Use();
                        Repaint();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private static void DrawSpritePreview(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return;
            }

            var textureRect = sprite.rect;
            var uv = new Rect(textureRect.x / sprite.texture.width, textureRect.y / sprite.texture.height,
                textureRect.width / sprite.texture.width, textureRect.height / sprite.texture.height);
            var inset = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
            GUI.DrawTextureWithTexCoords(inset, sprite.texture, uv, true);
        }

        private void LoadSourceSprites()
        {
            Array.Clear(assignedFaces, 0, assignedFaces.Length);
            spritesByIndex.Clear();
            sourceSprites = Array.Empty<Sprite>();
            selectedSprite = null;
            if (sourceTexture == null)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(sourceTexture);
            sourceSprites = LoadSprites(path);
            for (var index = 0; index < sourceSprites.Length; index++)
            {
                if (TryGetTrailingIndex(sourceSprites[index].name, out var spriteIndex))
                {
                    spritesByIndex[spriteIndex] = sourceSprites[index];
                }
            }
        }

        private void ApplyPreset()
        {
            Array.Clear(assignedFaces, 0, assignedFaces.Length);
            for (var suitIndex = 0; suitIndex < 4; suitIndex++)
            {
                for (var rankIndex = 0; rankIndex < 13; rankIndex++)
                {
                    var spriteIndex = DeckLayoutPresetUtility.GetFaceSpriteIndex(preset, (Suit)suitIndex,
                        (Rank)rankIndex);
                    spritesByIndex.TryGetValue(spriteIndex, out assignedFaces[suitIndex * 13 + rankIndex]);
                }
            }
        }

        private bool HasCompleteMapping()
        {
            if (string.IsNullOrWhiteSpace(skinId))
            {
                return false;
            }

            for (var index = 0; index < assignedFaces.Length; index++)
            {
                if (assignedFaces[index] == null)
                {
                    return false;
                }
            }

            return spritesByIndex.ContainsKey(DeckLayoutPresetUtility.GetCardBackSpriteIndex(preset)) &&
                   spritesByIndex.ContainsKey(DeckLayoutPresetUtility.GetBlankSpriteIndex());
        }

        private void SaveConfiguredSkin()
        {
            var path = EditorUtility.SaveFilePanelInProject("Save Deck Skin", skinDisplayName, "asset",
                "Choose where to save this deck skin.", SkinFolder);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var indexedSprites = LoadIndexedSprites(AssetDatabase.GetAssetPath(sourceTexture));
            var backIndex = DeckLayoutPresetUtility.GetCardBackSpriteIndex(preset);
            var blankIndex = DeckLayoutPresetUtility.GetBlankSpriteIndex();
            var skin = LoadOrCreateAsset<DeckSkin>(path);
            skin.Configure(skinId, skinDisplayName, assignedFaces, indexedSprites[backIndex], indexedSprites[blankIndex],
                true, 100f);
            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
            Selection.activeObject = skin;
        }

        private static Dictionary<int, Sprite> LoadIndexedSprites(string texturePath)
        {
            var indexedSprites = new Dictionary<int, Sprite>();
            var sprites = LoadSprites(texturePath);
            for (var index = 0; index < sprites.Length; index++)
            {
                if (TryGetTrailingIndex(sprites[index].name, out var spriteIndex))
                {
                    indexedSprites[spriteIndex] = sprites[index];
                }
            }

            return indexedSprites;
        }

        private static Sprite[] LoadSprites(string texturePath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
            var sprites = new List<Sprite>(assets.Length);
            for (var index = 0; index < assets.Length; index++)
            {
                if (assets[index] is Sprite sprite)
                {
                    sprites.Add(sprite);
                }
            }

            return sprites.ToArray();
        }

        private static bool TryGetTrailingIndex(string spriteName, out int index)
        {
            index = 0;
            if (string.IsNullOrEmpty(spriteName))
            {
                return false;
            }

            var separator = spriteName.LastIndexOf('_');
            return separator >= 0 && int.TryParse(spriteName.Substring(separator + 1), NumberStyles.None,
                CultureInfo.InvariantCulture, out index);
        }

        private static T LoadOrCreateAsset<T>(string assetPath) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                return asset;
            }

            asset = CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var separator = folderPath.LastIndexOf('/');
            var parent = folderPath.Substring(0, separator);
            var folderName = folderPath.Substring(separator + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}