using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Mejnun.Editor
{
    public static class FontChainSetup
    {
        private const string FontRoot = "Assets/_Project/Art/Fonts";
        private const string ResourceRoot = "Assets/_Project/UI/Resources/Fonts";
        private const string TextSettingsPath = "Assets/_Project/UI/Resources/MejnunTextSettings.asset";
        private const string AddressablesFolder = "Assets/AddressableAssetsData";

        private static readonly FontDefinition[] BaseFonts =
        {
            new FontDefinition("NotoSans", "NotoSans/NotoSans.ttf", "base", "A"),
            new FontDefinition("Vazirmatn", "Vazirmatn/Vazirmatn.ttf", "base", "ا"),
            new FontDefinition("NotoSansArmenian", "NotoSansArmenian/NotoSansArmenian.ttf", "base", "Ա"),
            new FontDefinition("NotoSansDevanagari", "NotoSansDevanagari/NotoSansDevanagari.ttf", "base", "अ"),
            new FontDefinition("NotoSansThai", "NotoSansThai/NotoSansThai.ttf", "base", "ก"),
        };

        private static readonly FontDefinition[] LocaleFonts =
        {
            new FontDefinition("NotoSansJP", "NotoSansJP/NotoSansJP.ttf", "ja", "あ"),
            new FontDefinition("NotoSansKR", "NotoSansKR/NotoSansKR.ttf", "ko", "가"),
            new FontDefinition("NotoSansSC", "NotoSansSC/NotoSansSC.ttf", "zh-Hans", "中"),
            new FontDefinition("NotoSansTC", "NotoSansTC/NotoSansTC.ttf", "zh-Hant", "中"),
        };

        [MenuItem("Mejnun/Localization/Set Up Fonts", priority = 13)]
        public static void SetUpFonts()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for Unity to finish importing assets before setting up fonts.");

            var settings = GetAddressableSettings();
            var assets = new Dictionary<string, FontAsset>(StringComparer.Ordinal);
            foreach (var definition in BaseFonts)
                assets[definition.Name] = CreateFontAsset(definition, true, settings);
            foreach (var definition in LocaleFonts)
                assets[definition.Name] = CreateFontAsset(definition, false, settings);

            var textSettings = CreateTextSettings(assets["NotoSans"], new[]
            {
                assets["Vazirmatn"], assets["NotoSansArmenian"], assets["NotoSansDevanagari"], assets["NotoSansThai"]
            });
            AssignTextSettings("Assets/_Project/UI/PanelSettings.asset", textSettings);
            AssignTextSettings("Assets/_Project/UI/FlowPanelSettings.asset", textSettings);

            foreach (var definition in LocaleFonts)
                AddAddressable(settings, definition);

            bool advancedTextEnabled = EnableAdvancedTextGenerator();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Fonts] Set up {assets.Count} dynamic multi-atlas fonts and {LocaleFonts.Length} locale groups. " +
                      (advancedTextEnabled ? "Advanced Text Generator enabled." : "Advanced Text Generator project property was not available; root USS selects the advanced generator."));
        }

        public static void SetUpFontsFromCommandLine() => SetUpFonts();

        private static FontAsset CreateFontAsset(FontDefinition definition, bool inResources, AddressableAssetSettings settings)
        {
            string sourcePath = FontRoot + "/" + definition.RelativeFontPath;
            string outputPath = inResources
                ? ResourceRoot + "/" + definition.Name + ".asset"
                : FontRoot + "/Addressables/" + definition.Locale + "/" + definition.Name + ".asset";
            EnsureFolder(System.IO.Path.GetDirectoryName(outputPath)?.Replace('\\', '/'));

            var existing = AssetDatabase.LoadAssetAtPath<FontAsset>(outputPath);
            if (existing != null && HasAtlasTexture(existing))
            {
                EnsureFontMaterial(existing);
                if (!existing.HasCharacter(definition.SeedCharacters[0]) &&
                    !existing.TryAddCharacters(definition.SeedCharacters, out string missingCharacters))
                    throw new InvalidOperationException($"Could not populate {definition.Name}; missing glyphs: {missingCharacters}.");
                if (!existing.HasCharacter(definition.SeedCharacters[0]))
                    throw new InvalidOperationException("The saved font asset could not populate its seed glyph: " + definition.Name + ".");
                existing.isMultiAtlasTexturesEnabled = true;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            if (existing != null)
            {
                if (!inResources)
                    settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(outputPath), false);
                AssetDatabase.DeleteAsset(outputPath);
            }

            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (sourceFont == null)
                throw new InvalidOperationException("Could not load font source " + sourcePath + ".");

            var fontAsset = FontAsset.CreateFontAsset(sourceFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            if (fontAsset == null)
                throw new InvalidOperationException("Could not create dynamic font asset for " + sourcePath + ".");

            AssetDatabase.CreateAsset(fontAsset, outputPath);
            var atlas = new Texture2D(1, 1, TextureFormat.Alpha8, false)
            {
                name = definition.Name + " Atlas",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            var serialized = new SerializedObject(fontAsset);
            var atlasProperty = serialized.FindProperty("m_AtlasTextures");
            if (atlasProperty == null || !atlasProperty.isArray)
                throw new InvalidOperationException("Could not find the atlas texture array on FontAsset.");
            atlasProperty.arraySize = 1;
            atlasProperty.GetArrayElementAtIndex(0).objectReferenceValue = atlas;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            fontAsset.ReadFontAssetDefinition();
            EnsureFontMaterial(fontAsset);

            if (!fontAsset.TryAddCharacters(definition.SeedCharacters, out string missing))
                throw new InvalidOperationException($"Could not initialize {definition.Name}; missing glyphs: {missing}.");
            if (!fontAsset.HasCharacter(definition.SeedCharacters[0]))
                throw new InvalidOperationException("The font atlas did not initialize correctly for " + definition.Name + ".");

            fontAsset.name = definition.Name;
            fontAsset.isMultiAtlasTexturesEnabled = true;
            EditorUtility.SetDirty(fontAsset);
            EditorUtility.SetDirty(atlas);
            return fontAsset;
        }

        private static void EnsureFontMaterial(FontAsset fontAsset)
        {
            if (fontAsset.material != null)
                return;

            Shader shader = Shader.Find("TextMeshPro/Distance Field") ??
                            Shader.Find("Hidden/Internal-GUITextureClip") ??
                            Shader.Find("UI/Default");
            if (shader == null)
                throw new InvalidOperationException("Could not find a compatible TextCore font shader.");

            var material = new Material(shader)
            {
                name = fontAsset.name + " Material",
                hideFlags = HideFlags.HideInHierarchy,
            };
            var atlas = fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0
                ? fontAsset.atlasTextures[0]
                : null;
            material.mainTexture = atlas;
            SetMaterialFloat(material, "_GradientScale", fontAsset.atlasPadding + 1);
            SetMaterialFloat(material, "_TextureWidth", fontAsset.atlasWidth);
            SetMaterialFloat(material, "_TextureHeight", fontAsset.atlasHeight);
            SetMaterialFloat(material, "_WeightNormal", 0f);
            SetMaterialFloat(material, "_WeightBold", 0.75f);
            fontAsset.material = material;
            AssetDatabase.AddObjectToAsset(material, fontAsset);
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(fontAsset);
        }

        private static void SetMaterialFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        private static bool HasAtlasTexture(FontAsset fontAsset)
        {
            var property = new SerializedObject(fontAsset).FindProperty("m_AtlasTextures");
            var texture = property != null && property.isArray && property.arraySize > 0
                ? property.GetArrayElementAtIndex(0).objectReferenceValue as Texture2D
                : null;
            return texture != null;
        }

        private static ScriptableObject CreateTextSettings(FontAsset defaultFont, IReadOnlyList<FontAsset> fallbacks)
        {
            var textSettingsMember = FindTextSettingsMember();
            var settingsType = MemberType(textSettingsMember);
            if (settingsType == null || !typeof(ScriptableObject).IsAssignableFrom(settingsType))
                throw new InvalidOperationException("PanelSettings does not expose a ScriptableObject textSettings asset.");

            EnsureFolder("Assets/_Project/UI/Resources");
            EnsureFolder(ResourceRoot);
            var textSettings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(TextSettingsPath);
            if (textSettings == null)
            {
                textSettings = ScriptableObject.CreateInstance(settingsType);
                textSettings.name = "MejnunTextSettings";
                AssetDatabase.CreateAsset(textSettings, TextSettingsPath);
            }

            var serialized = new SerializedObject(textSettings);
            var defaultProperty = FindSerializedProperty(serialized, "defaultFontAsset", "m_DefaultFontAsset", "m_DefaultFont");
            if (defaultProperty == null || defaultProperty.propertyType != SerializedPropertyType.ObjectReference)
                throw new InvalidOperationException("Could not find the default font asset field on " + settingsType.Name + ".");
            defaultProperty.objectReferenceValue = defaultFont;

            var fallbackProperty = FindSerializedProperty(serialized, "fallbackFontAssets", "m_FallbackFontAssets", "m_FallbackFonts");
            if (fallbackProperty == null || !fallbackProperty.isArray)
                throw new InvalidOperationException("Could not find the fallback font asset list on " + settingsType.Name + ".");
            fallbackProperty.arraySize = fallbacks.Count;
            for (int index = 0; index < fallbacks.Count; index++)
                fallbackProperty.GetArrayElementAtIndex(index).objectReferenceValue = fallbacks[index];

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(textSettings);
            return textSettings;
        }

        private static void AssignTextSettings(string panelSettingsPath, ScriptableObject textSettings)
        {
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelSettingsPath);
            if (panelSettings == null)
                throw new InvalidOperationException("Could not load PanelSettings at " + panelSettingsPath + ".");

            var serialized = new SerializedObject(panelSettings);
            var property = FindSerializedProperty(serialized, "textSettings", "m_TextSettings");
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                throw new InvalidOperationException("Could not find the textSettings field on " + panelSettingsPath + ".");
            property.objectReferenceValue = textSettings;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panelSettings);
        }

        private static AddressableAssetSettings GetAddressableSettings()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                settings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(
                    AddressableAssetSettingsDefaultObject.DefaultAssetPath);
            }

            if (settings == null)
                settings = AddressableAssetSettings.Create(AddressablesFolder, "AddressableAssetSettings", true, true);

            AddressableAssetSettingsDefaultObject.Settings = settings;
            return settings;
        }

        private static void AddAddressable(AddressableAssetSettings settings, FontDefinition definition)
        {
            string path = FontRoot + "/Addressables/" + definition.Locale + "/" + definition.Name + ".asset";
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException("Font asset has no GUID: " + path);

            string groupName = "Fonts-" + definition.Locale;
            var group = settings.FindGroup(groupName);
            if (group == null)
                group = settings.CreateGroup(groupName, false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = "fonts/" + definition.Locale;
        }

        private static bool EnableAdvancedTextGenerator()
        {
            const BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.GetName().Name.Contains("UnityEditor"))
                    continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }

                foreach (var type in types)
                {
                    if (type == null)
                        continue;

                    bool projectSettingsType = type.Name.IndexOf("UIElements", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               type.Name.IndexOf("UIToolkit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                               type.Name.IndexOf("TextGenerator", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!projectSettingsType)
                        continue;

                    foreach (var property in type.GetProperties(staticFlags))
                    {
                        if (property.PropertyType == typeof(bool) && property.CanWrite && IsAdvancedTextSetting(property.Name))
                        {
                            property.SetValue(null, true);
                            Debug.Log("[Fonts] Enabled " + type.FullName + "." + property.Name + ".");
                            return true;
                        }
                    }

                    foreach (var field in type.GetFields(staticFlags))
                    {
                        if (field.FieldType == typeof(bool) && !field.IsInitOnly && IsAdvancedTextSetting(field.Name))
                        {
                            field.SetValue(null, true);
                            Debug.Log("[Fonts] Enabled " + type.FullName + "." + field.Name + ".");
                            return true;
                        }
                    }

                    if (typeof(ScriptableObject).IsAssignableFrom(type))
                    {
                        foreach (var property in type.GetProperties(staticFlags))
                        {
                            if (!type.IsAssignableFrom(property.PropertyType) || !property.CanRead)
                                continue;
                            var instance = property.GetValue(null) as ScriptableObject;
                            if (instance != null && SetInstanceAdvancedTextSetting(instance, instanceFlags))
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool SetInstanceAdvancedTextSetting(object target, BindingFlags flags)
        {
            var type = target.GetType();
            foreach (var property in type.GetProperties(flags))
            {
                if (property.PropertyType == typeof(bool) && property.CanWrite && IsAdvancedTextSetting(property.Name))
                {
                    property.SetValue(target, true);
                    EditorUtility.SetDirty(target as UnityEngine.Object);
                    return true;
                }
            }

            foreach (var field in type.GetFields(flags))
            {
                if (field.FieldType == typeof(bool) && !field.IsInitOnly && IsAdvancedTextSetting(field.Name))
                {
                    field.SetValue(target, true);
                    EditorUtility.SetDirty(target as UnityEngine.Object);
                    return true;
                }
            }

            return false;
        }

        private static bool IsAdvancedTextSetting(string name)
        {
            string normalized = name.Replace("_", string.Empty).ToLowerInvariant();
            return normalized.Contains("advanced") && normalized.Contains("text") &&
                   (normalized.Contains("enabled") || normalized.Contains("enable") || normalized.Contains("generator"));
        }

        private static MemberInfo FindTextSettingsMember()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            return (MemberInfo)typeof(PanelSettings).GetProperty("textSettings", flags) ??
                   typeof(PanelSettings).GetField("textSettings", flags) ??
                   typeof(PanelSettings).GetField("m_TextSettings", flags);
        }

        private static Type MemberType(MemberInfo member)
        {
            if (member is PropertyInfo property)
                return property.PropertyType;
            if (member is FieldInfo field)
                return field.FieldType;
            return null;
        }

        private static SerializedProperty FindSerializedProperty(SerializedObject serialized, params string[] names)
        {
            foreach (string name in names)
            {
                var property = serialized.FindProperty(name);
                if (property != null)
                    return property;
            }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        private readonly struct FontDefinition
        {
            public readonly string Name;
            public readonly string RelativeFontPath;
            public readonly string Locale;
            public readonly string SeedCharacters;

            public FontDefinition(string name, string relativeFontPath, string locale, string seedCharacters)
            {
                Name = name;
                RelativeFontPath = relativeFontPath;
                Locale = locale;
                SeedCharacters = seedCharacters;
            }
        }
    }
}