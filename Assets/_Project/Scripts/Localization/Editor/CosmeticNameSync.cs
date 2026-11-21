using System;
using System.Collections.Generic;
using Mejnun.Cosmetics;
using Mejnun.Localization;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace Mejnun.Localization.Editor
{
    public static class CosmeticNameSync
    {
        private const string DeckNameComment = "Localized display name of a deck skin, shown in the deck picker and collection.";
        private const string FeltNameComment = "Localized display name of a table felt, shown in the felt picker and collection.";

        [MenuItem("Mejnun/Localization/Sync Deck & Felt Names", priority = 29)]
        private static void SyncFromMenu()
        {
            int changed = SyncAll();
            Debug.Log($"[Localization] Synced {changed} deck and felt name entries.");
        }

        public static int SyncAll()
        {
            if (LocalizationEditorSettings.GetStringTableCollection(Loc.UiTable) == null)
                return 0;

            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:DeckSkin", new[] { "Assets/_Project" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var skin = AssetDatabase.LoadAssetAtPath<DeckSkin>(path);
                if (skin != null && SyncName(skin.Id, skin.SerializedDisplayName, DeckSkin.NameKey, DeckNameComment))
                    changed++;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:TableFelt", new[] { "Assets/_Project" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var felt = AssetDatabase.LoadAssetAtPath<TableFelt>(path);
                if (felt != null && SyncName(felt.Id, felt.SerializedDisplayName, TableFelt.NameKey, FeltNameComment))
                    changed++;
            }

            if (changed > 0)
                AssetDatabase.SaveAssets();

            return changed;
        }

        internal static bool IsCosmeticAsset(string path)
        {
            Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
            return type == typeof(DeckSkin) || type == typeof(TableFelt);
        }

        internal static int SyncImportedAssets(IEnumerable<string> paths)
        {
            if (LocalizationEditorSettings.GetStringTableCollection(Loc.UiTable) == null)
                return 0;

            int changed = 0;
            foreach (string path in paths)
            {
                var skin = AssetDatabase.LoadAssetAtPath<DeckSkin>(path);
                if (skin != null)
                {
                    if (SyncName(skin.Id, skin.SerializedDisplayName, DeckSkin.NameKey, DeckNameComment))
                        changed++;
                    continue;
                }

                var felt = AssetDatabase.LoadAssetAtPath<TableFelt>(path);
                if (felt != null && SyncName(felt.Id, felt.SerializedDisplayName, TableFelt.NameKey, FeltNameComment))
                    changed++;
            }

            if (changed > 0)
                AssetDatabase.SaveAssets();

            return changed;
        }

        private static bool SyncName(string id, string english, Func<string, string> keyBuilder, string comment)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(english))
                return false;

            string key = keyBuilder(id);
            var collection = LocalizationEditorSettings.GetStringTableCollection(Loc.UiTable);
            string newEntryComment = collection?.SharedData.GetEntry(key) == null ? comment : null;
            return LocalizationSourceImporter.Upsert(Loc.UiTable, key, english, newEntryComment);
        }
    }

    internal sealed class CosmeticNameSyncAssetPostprocessor : AssetPostprocessor
    {
        private static readonly HashSet<string> PendingPaths = new HashSet<string>(StringComparer.Ordinal);
        private static bool syncScheduled;

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool queued = false;
            foreach (string path in importedAssets)
            {
                if (CosmeticNameSync.IsCosmeticAsset(path))
                    queued |= PendingPaths.Add(path);
            }

            if (!queued || syncScheduled)
                return;

            syncScheduled = true;
            EditorApplication.delayCall += SyncImportedAssets;
        }

        private static void SyncImportedAssets()
        {
            syncScheduled = false;
            var paths = new List<string>(PendingPaths);
            PendingPaths.Clear();

            int changed = CosmeticNameSync.SyncImportedAssets(paths);
            if (changed > 0)
                Debug.Log($"[Localization] Synced {changed} deck and felt name entries.");
        }
    }
}