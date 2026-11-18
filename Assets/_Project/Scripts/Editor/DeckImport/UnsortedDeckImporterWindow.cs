using System;
using Mejnun.Editor.DeckImport;
using UnityEditor;
using UnityEngine;

namespace Mejnun.Editor
{
    public sealed class UnsortedDeckImporterWindow : EditorWindow
    {
        private UnsortedDeckScanResult scanResult;
        private Vector2 scrollPosition;

        [MenuItem("Mejnun/Unsorted Deck Importer")]
        public static void Open()
        {
            GetWindow<UnsortedDeckImporterWindow>("Unsorted Deck Importer");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Scan the Unsorted card art folder, review the detected face assignments, names, ids and warnings, then import the complete decks.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan Unsorted"))
            {
                Scan();
            }

            using (new EditorGUI.DisabledScope(scanResult == null || scanResult.Decks.Count == 0))
            {
                if (GUILayout.Button("Import Decks"))
                {
                    Import();
                }
            }

            EditorGUILayout.EndHorizontal();

            if (scanResult == null)
            {
                return;
            }

            EditorGUILayout.LabelField(scanResult.Decks.Count + " deck(s) found; " +
                                       scanResult.Warnings.Count + " scan warning(s).");
            DrawWarnings();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (var index = 0; index < scanResult.Decks.Count; index++)
            {
                DrawDeck(scanResult.Decks[index], index);
            }

            EditorGUILayout.EndScrollView();
        }

        private void Scan()
        {
            try
            {
                scanResult = UnsortedDeckImporter.Scan();
            }
            catch (Exception exception)
            {
                scanResult = null;
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Deck Scan Failed", exception.Message, "OK");
            }
        }

        private void Import()
        {
            if (!EditorUtility.DisplayDialog("Import Decks",
                    "Move the detected source images into deck folders and add generated skins to the catalog?",
                    "Import", "Cancel"))
            {
                return;
            }

            try
            {
                UnsortedDeckImporter.Import(scanResult);
                scanResult = null;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Deck Import Failed", exception.Message,
                    "Inspect the Console and ImportReport.md");
            }
        }

        private void DrawWarnings()
        {
            for (var index = 0; index < scanResult.Warnings.Count; index++)
            {
                EditorGUILayout.HelpBox(scanResult.Warnings[index], MessageType.Warning);
            }
        }

        private static void DrawDeck(DeckImportPlan plan, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField((index + 1) + ". " + plan.SourceRoot, EditorStyles.miniLabel);
            plan.Name = EditorGUILayout.TextField("Deck Name", plan.Name);
            plan.Id = EditorGUILayout.TextField("Deck ID", plan.Id);
            EditorGUILayout.LabelField("Faces: " + plan.Faces.Count + "   Backs: " + plan.Backs.Count +
                                       "   Jokers: " + plan.Jokers.Count + "   Extras: " + plan.Extras.Count);
            for (var warningIndex = 0; warningIndex < plan.Warnings.Count; warningIndex++)
            {
                EditorGUILayout.HelpBox(plan.Warnings[warningIndex], MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }
    }
}