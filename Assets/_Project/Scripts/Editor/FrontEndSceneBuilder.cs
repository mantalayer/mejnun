using System;
using System.Collections.Generic;
using System.IO;
using Mejnun.Cosmetics;
using Mejnun.Game;
using Mejnun.UI.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Mejnun.Editor
{
    public static class FrontEndSceneBuilder
    {
        private const string MainMenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string TableScenePath = "Assets/_Project/Scenes/Table.unity";
        private const string FrontEndUxmlPath = "Assets/_Project/Scripts/UI/Uxml/FrontEnd.uxml";
        private const string PanelSettingsPath = "Assets/_Project/UI/FlowPanelSettings.asset";
        private const string CosmeticCatalogPath = "Assets/_Project/Data/Cosmetics/CosmeticCatalog.asset";

        [MenuItem("Mejnun/Setup/Build Main Menu Scene")]
        public static void BuildMainMenuScene()
        {
            if (!File.Exists(TableScenePath))
            {
                throw new InvalidOperationException("Build Table.unity before building the MainMenu scene.");
            }

            var frontEndAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(FrontEndUxmlPath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (frontEndAsset == null || panelSettings == null)
            {
                throw new InvalidOperationException("Could not load the MainMenu UXML or PanelSettings asset.");
            }

            var previousScene = SceneManager.GetActiveScene();
            var replaceEmptyUntitledScene = previousScene.IsValid() && string.IsNullOrEmpty(previousScene.path);
            if (replaceEmptyUntitledScene && previousScene.isDirty)
            {
                throw new InvalidOperationException("Save the current Untitled scene before building MainMenu.unity.");
            }

            var sceneMode = replaceEmptyUntitledScene ? NewSceneMode.Single : NewSceneMode.Additive;
            var menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, sceneMode);
            SceneManager.SetActiveScene(menuScene);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.09f, 0.07f, 1f);

            var menuObject = new GameObject("Main Menu");
            menuObject.SetActive(false);
            menuObject.AddComponent<MenuAudioCue>();
            var cosmeticCatalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(CosmeticCatalogPath);
            var deckSkinLoader = menuObject.AddComponent<DeckSkinLoader>();
            var uiDocument = menuObject.AddComponent<UIDocument>();
            uiDocument.panelSettings = panelSettings;
            uiDocument.visualTreeAsset = frontEndAsset;
            var presenter = menuObject.AddComponent<FrontEndPresenter>();
            SetObjectReference(uiDocument, "m_PanelSettings", panelSettings);
            SetObjectReference(deckSkinLoader, "catalog", cosmeticCatalog == null ? null : cosmeticCatalog.Decks);
            SetObjectReference(presenter, "uiDocument", uiDocument);
            SetObjectReference(presenter, "cosmeticCatalog", cosmeticCatalog);
            SetObjectReference(presenter, "deckSkinLoader", deckSkinLoader);
            menuObject.SetActive(true);
            if (uiDocument.panelSettings != panelSettings)
            {
                throw new InvalidOperationException("The MainMenu UIDocument lost its PanelSettings reference.");
            }

            if (!EditorSceneManager.SaveScene(menuScene, MainMenuScenePath))
            {
                throw new InvalidOperationException("Could not save MainMenu.unity.");
            }

            ConfigureBuildSettings();
            if (previousScene.IsValid() && !replaceEmptyUntitledScene)
            {
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(menuScene, true);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Built MainMenu.unity and configured MainMenu=0, Table=1.");
        }

        private static void ConfigureBuildSettings()
        {
            var existingScenes = EditorBuildSettings.scenes;
            var sampleScenePath = string.Empty;
            var otherScenes = new List<EditorBuildSettingsScene>();
            for (var index = 0; index < existingScenes.Length; index++)
            {
                var scene = existingScenes[index];
                if (Path.GetFileNameWithoutExtension(scene.path) == "SampleScene")
                {
                    sampleScenePath = scene.path;
                }
                else if (scene.path != MainMenuScenePath && scene.path != TableScenePath)
                {
                    otherScenes.Add(scene);
                }
            }

            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(TableScenePath, true)
            };
            if (!string.IsNullOrEmpty(sampleScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(sampleScenePath, false));
            }

            scenes.AddRange(otherScenes);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName,
            UnityEngine.Object value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException("Missing serialized property " + propertyName + ".");
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}