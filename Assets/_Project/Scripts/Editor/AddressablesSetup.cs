using System;
using System.Linq;
using Mejnun.Cosmetics;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Mejnun.Editor
{
    public static class AddressablesSetup
    {
        private const string SkinCatalogPath = "Assets/_Project/Data/Skins/DeckSkinCatalog.asset";
        private const string SkinFolder = "Assets/_Project/Data/Skins";
        private const string AddressablesFolder = "Assets/AddressableAssetsData";
        private const string OptionalDeckGroup = "OptionalDecks";

        [MenuItem("Mejnun/Setup/Configure Addressables")]
        public static void Configure()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DeckSkinCatalog>(SkinCatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException("Build the deck skin catalog before configuring Addressables.");
            }

            var skin = AssetDatabase.LoadAssetAtPath<DeckSkin>(SkinFolder + "/PixelPastel.asset");
            if (skin == null)
            {
                throw new InvalidOperationException("Could not find PixelPastel.asset.");
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                settings = AssetDatabase.LoadAssetAtPath<AddressableAssetSettings>(
                    AddressableAssetSettingsDefaultObject.DefaultAssetPath);
            }

            if (settings == null)
            {
                settings = AddressableAssetSettings.Create(AddressablesFolder, "AddressableAssetSettings", true,
                    true);
            }

            AddressableAssetSettingsDefaultObject.Settings = settings;

            var group = settings.FindGroup(OptionalDeckGroup);
            if (group == null)
            {
                group = settings.CreateGroup(OptionalDeckGroup, false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            }

            var assetPath = AssetDatabase.GetAssetPath(skin);
            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(assetGuid))
            {
                throw new InvalidOperationException("Pixel Pastel has no Asset GUID.");
            }

            var entry = settings.CreateOrMoveEntry(assetGuid, group);
            entry.address = "decks/" + skin.Id;
            var retainedSkins = catalog.Skins.Where(candidate => candidate != null && candidate.Id != skin.Id).ToArray();
            catalog.Configure(retainedSkins);
            catalog.ConfigureAddressableSkins(new[]
            {
                new AddressableDeckSkinEntry(skin.Id, skin.DisplayName, skin.Unlock,
                    new AssetReferenceT<DeckSkin>(assetGuid))
            });

            EditorUtility.SetDirty(catalog);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
            AssetDatabase.SaveAssets();
            Debug.Log("Configured Pixel Pastel in the local " + OptionalDeckGroup + " Addressables group.");
        }

        public static void BuildContent()
        {
            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                throw new InvalidOperationException("Configure Addressables before building the player content.");
            }

            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error))
            {
                throw new InvalidOperationException("Addressables content build failed: " + result.Error);
            }
        }

        [MenuItem("Mejnun/Setup/Build Addressables Content")]
        public static void BuildContentFromMenu()
        {
            BuildContent();
            Debug.Log("Built Addressables player content.");
        }
    }
}