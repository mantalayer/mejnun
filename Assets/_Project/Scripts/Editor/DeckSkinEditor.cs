using System.Collections.Generic;
using System.Globalization;
using Mejnun.Core;
using Mejnun.Cosmetics;
using UnityEditor;
using UnityEngine;

namespace Mejnun.Editor
{
    [CustomEditor(typeof(DeckSkin))]
    public sealed class DeckSkinEditor : UnityEditor.Editor
    {
        private const int FaceCount = 52;
        private const float CardWidth = 108f;
        private static readonly string[] SuitNames = { "Clubs", "Diamonds", "Hearts", "Spades" };
        private static readonly string[] RankNames =
        {
            "Ace", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Jack", "Queen",
            "King"
        };

        private DeckLayoutPreset preset = DeckLayoutPreset.ZigZag;
        private Vector2 gridScroll;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("artist"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cardBack"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("alternateBacks"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("blank"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pixelArt"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pixelsPerUnit"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("unlock"));

            var faces = serializedObject.FindProperty("faces");
            DrawFacesGrid(faces);
            DrawFaceWarnings(faces);
            DrawPresetControls(faces);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFacesGrid(SerializedProperty faces)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Face Sprites", EditorStyles.boldLabel);
            if (faces == null)
            {
                EditorGUILayout.HelpBox("The face sprite array could not be found.", MessageType.Error);
                return;
            }

            if (faces.arraySize != FaceCount)
            {
                EditorGUILayout.HelpBox("A deck skin must contain exactly 52 face sprites.", MessageType.Warning);
                if (GUILayout.Button("Reset Face Slots"))
                {
                    faces.arraySize = FaceCount;
                }

                if (faces.arraySize != FaceCount)
                {
                    return;
                }
            }

            gridScroll = EditorGUILayout.BeginScrollView(gridScroll, true, true, GUILayout.MinHeight(360f));
            for (var suitIndex = 0; suitIndex < SuitNames.Length; suitIndex++)
            {
                EditorGUILayout.BeginHorizontal();
                for (var rankIndex = 0; rankIndex < RankNames.Length; rankIndex++)
                {
                    var slot = faces.GetArrayElementAtIndex(suitIndex * 13 + rankIndex);
                    var sprite = slot.objectReferenceValue as Sprite;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(CardWidth));
                    EditorGUILayout.LabelField(RankNames[rankIndex] + " of " + SuitNames[suitIndex],
                        EditorStyles.centeredGreyMiniLabel, GUILayout.Height(28f));

                    var previewRect = GUILayoutUtility.GetRect(72f, 90f, GUILayout.Width(CardWidth - 8f),
                        GUILayout.Height(90f));
                    GUI.Box(previewRect, GUIContent.none);
                    DrawSpritePreview(previewRect, sprite);
                    slot.objectReferenceValue = EditorGUILayout.ObjectField(GUIContent.none, sprite, typeof(Sprite),
                        false, GUILayout.Width(CardWidth - 8f)) as Sprite;
                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawFaceWarnings(SerializedProperty faces)
        {
            if (faces == null || faces.arraySize != FaceCount)
            {
                return;
            }

            var missingCards = new List<string>();
            var assignments = new Dictionary<Sprite, string>();
            var duplicateCards = new List<string>();
            for (var suitIndex = 0; suitIndex < SuitNames.Length; suitIndex++)
            {
                for (var rankIndex = 0; rankIndex < RankNames.Length; rankIndex++)
                {
                    var sprite = faces.GetArrayElementAtIndex(suitIndex * 13 + rankIndex).objectReferenceValue as Sprite;
                    var cardName = RankNames[rankIndex] + " of " + SuitNames[suitIndex];
                    if (sprite == null)
                    {
                        missingCards.Add(cardName);
                    }
                    else if (assignments.TryGetValue(sprite, out var firstCard))
                    {
                        duplicateCards.Add(cardName + " (same sprite as " + firstCard + ")");
                    }
                    else
                    {
                        assignments.Add(sprite, cardName);
                    }
                }
            }

            if (missingCards.Count > 0)
            {
                EditorGUILayout.HelpBox("Missing face sprites: " + string.Join(", ", missingCards),
                    MessageType.Warning);
            }

            if (duplicateCards.Count > 0)
            {
                EditorGUILayout.HelpBox("Duplicate face sprite assignments: " + string.Join(", ", duplicateCards),
                    MessageType.Warning);
            }
        }

        private void DrawPresetControls(SerializedProperty faces)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Layout Preset", EditorStyles.boldLabel);
            preset = (DeckLayoutPreset)EditorGUILayout.EnumPopup("Preset", preset);
            using (new EditorGUI.DisabledScope(faces == null || faces.arraySize != FaceCount))
            {
                if (GUILayout.Button("Re-apply layout preset"))
                {
                    ApplyPreset(faces);
                }
            }
        }

        private void ApplyPreset(SerializedProperty faces)
        {
            if (faces == null || faces.arraySize != FaceCount)
            {
                return;
            }

            var sourceSprite = serializedObject.FindProperty("cardBack").objectReferenceValue as Sprite;
            if (sourceSprite == null && faces.arraySize > 0)
            {
                sourceSprite = faces.GetArrayElementAtIndex(0).objectReferenceValue as Sprite;
            }

            if (sourceSprite == null)
            {
                EditorUtility.DisplayDialog("Cannot Apply Preset", "Assign a sprite from the source sheet first.",
                    "OK");
                return;
            }

            var texturePath = AssetDatabase.GetAssetPath(sourceSprite);
            if (string.IsNullOrEmpty(texturePath))
            {
                EditorUtility.DisplayDialog("Cannot Apply Preset", "The selected sprite is not in an asset sheet.",
                    "OK");
                return;
            }

            var indexedSprites = LoadIndexedSprites(texturePath);
            for (var spriteIndex = 0; spriteIndex < 54; spriteIndex++)
            {
                if (!indexedSprites.ContainsKey(spriteIndex))
                {
                    EditorUtility.DisplayDialog("Cannot Apply Preset",
                        "The source sheet is missing sprite " + spriteIndex + ".", "OK");
                    return;
                }
            }

            for (var suitIndex = 0; suitIndex < SuitNames.Length; suitIndex++)
            {
                for (var rankIndex = 0; rankIndex < RankNames.Length; rankIndex++)
                {
                    var spriteIndex = DeckLayoutPresetUtility.GetFaceSpriteIndex(preset, (Suit)suitIndex,
                        (Rank)rankIndex);
                    faces.GetArrayElementAtIndex(suitIndex * 13 + rankIndex).objectReferenceValue =
                        indexedSprites[spriteIndex];
                }
            }

            serializedObject.FindProperty("cardBack").objectReferenceValue =
                indexedSprites[DeckLayoutPresetUtility.GetCardBackSpriteIndex(preset)];
            serializedObject.FindProperty("blank").objectReferenceValue =
                indexedSprites[DeckLayoutPresetUtility.GetBlankSpriteIndex()];
        }

        private static Dictionary<int, Sprite> LoadIndexedSprites(string texturePath)
        {
            var indexedSprites = new Dictionary<int, Sprite>();
            var assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
            foreach (var asset in assets)
            {
                if (asset is Sprite sprite && TryGetTrailingIndex(sprite.name, out var spriteIndex))
                {
                    indexedSprites[spriteIndex] = sprite;
                }
            }

            return indexedSprites;
        }

        private static bool TryGetTrailingIndex(string spriteName, out int index)
        {
            index = 0;
            var separator = spriteName.LastIndexOf('_');
            return separator >= 0 && int.TryParse(spriteName.Substring(separator + 1), NumberStyles.None,
                CultureInfo.InvariantCulture, out index) && index >= 0 && index < 54;
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
    }
}