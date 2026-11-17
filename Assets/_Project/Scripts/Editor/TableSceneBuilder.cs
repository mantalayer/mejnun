using System.Collections.Generic;
using Mejnun.Cosmetics;
using Mejnun.Game;
using Mejnun.UI;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Mejnun.Editor
{
    public static class TableSceneBuilder
    {
        private const string CardPrefabPath = "Assets/_Project/Prefabs/Card.prefab";
        private const string TableScenePath = "Assets/_Project/Scenes/Table.unity";
        private const string SkinCatalogPath = "Assets/_Project/Data/Skins/DeckSkinCatalog.asset";
        private const string HudAssetPath = "Assets/_Project/Scripts/UI/Uxml/Hud.uxml";
        private const string FlowScreensAssetPath = "Assets/_Project/Scripts/UI/Uxml/FlowScreens.uxml";
        private const string PanelSettingsPath = "Assets/_Project/UI/PanelSettings.asset";
        private const string FlowPanelSettingsPath = "Assets/_Project/UI/FlowPanelSettings.asset";
        private const string DealerQuestionsPath = "Assets/_Project/Data/DealerQuestions.asset";

        [MenuItem("Mejnun/Setup/Build Table Scene")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/UI");
            BuildCardPrefab();
            var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            var panelSettings = LoadOrCreatePanelSettings(PanelSettingsPath, 10);
            var flowPanelSettings = LoadOrCreatePanelSettings(FlowPanelSettingsPath, 20);
            var hudAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudAssetPath);
            var flowScreensAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(FlowScreensAssetPath);
            if (hudAsset == null || flowScreensAsset == null)
            {
                throw new System.InvalidOperationException("Could not load one of the HUD UXML assets.");
            }

            var questions = LoadOrCreateDealerQuestions();
            BuildTableScene(cardPrefab, panelSettings, hudAsset, flowPanelSettings, flowScreensAsset, questions);
            AddSceneToBuildSettings(TableScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Built the Mejnun Table scene and Card prefab.");
        }

        public static void BuildMacOSPlayer()
        {
            var enabledScenes = new List<string>();
            var buildScenes = EditorBuildSettings.scenes;
            for (var index = 0; index < buildScenes.Length; index++)
            {
                if (buildScenes[index].enabled)
                {
                    enabledScenes.Add(buildScenes[index].path);
                }
            }

            var options = new BuildPlayerOptions
            {
                scenes = enabledScenes.ToArray(),
                locationPathName = "Builds/MejnunM2MacOSBuild.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report == null)
            {
                throw new System.InvalidOperationException("Unity returned no report for the macOS player build.");
            }

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.InvalidOperationException("The macOS scripts-only player build failed: " +
                                                          report.summary.result);
            }

            Debug.Log("Built the Mejnun macOS player.");
        }

        private static GameObject BuildCardPrefab()
        {
            var cardObject = new GameObject("Card");
            try
            {
                var renderer = cardObject.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 10;
                cardObject.AddComponent<CardView>();
                return PrefabUtility.SaveAsPrefabAsset(cardObject, CardPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(cardObject);
            }
        }

        private static void BuildTableScene(GameObject cardPrefab, PanelSettings panelSettings, VisualTreeAsset hudAsset,
            PanelSettings flowPanelSettings, VisualTreeAsset flowScreensAsset, DealerQuestions dealerQuestions)
        {
            var previousScene = SceneManager.GetActiveScene();
            var replaceEmptyUntitledScene = previousScene.IsValid() && string.IsNullOrEmpty(previousScene.path);
            if (replaceEmptyUntitledScene && previousScene.isDirty)
            {
                throw new System.InvalidOperationException(
                    "Save the current Untitled scene before building Table.unity.");
            }

            var sceneMode = replaceEmptyUntitledScene ? NewSceneMode.Single : NewSceneMode.Additive;
            var tableScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, sceneMode);
            SceneManager.SetActiveScene(tableScene);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.2f, 0.12f, 1f);
            var tableCamera = cameraObject.AddComponent<TableCamera>();

            var tableRoot = new GameObject("Table");
            var boardObject = new GameObject("Board");
            boardObject.transform.SetParent(tableRoot.transform, false);
            var boardView = boardObject.AddComponent<BoardView>();
            var handObject = new GameObject("Hand");
            handObject.transform.SetParent(tableRoot.transform, false);
            var handView = handObject.AddComponent<HandView>();
            var tableView = tableRoot.AddComponent<TableView>();
            var pointerRouter = tableRoot.AddComponent<PointerRouter>();
            var tableController = tableRoot.AddComponent<TableController>();
            var matchFlow = tableRoot.AddComponent<MatchFlow>();
            var hudObject = new GameObject("HUD");
            hudObject.SetActive(false);
            hudObject.transform.SetParent(tableRoot.transform, false);
            var uiDocument = hudObject.AddComponent<UIDocument>();
            uiDocument.panelSettings = panelSettings;
            uiDocument.visualTreeAsset = hudAsset;
            var hudPresenter = hudObject.AddComponent<HudPresenter>();
            var flowObject = new GameObject("Flow Screens");
            flowObject.SetActive(false);
            flowObject.transform.SetParent(tableRoot.transform, false);
            var flowDocument = flowObject.AddComponent<UIDocument>();
            flowDocument.panelSettings = flowPanelSettings;
            flowDocument.visualTreeAsset = flowScreensAsset;
            var flowPresenter = flowObject.AddComponent<MatchFlowPresenter>();
            var skins = AssetDatabase.LoadAssetAtPath<DeckSkinCatalog>(SkinCatalogPath);
            if (skins == null)
            {
                throw new System.InvalidOperationException("Build the Classic deck skins before building Table.unity.");
            }

            SetObjectReference(tableView, "cardPrefab", cardPrefab);
            SetObjectReference(tableView, "skinCatalog", skins);
            SetObjectReference(tableView, "boardView", boardView);
            SetObjectReference(tableView, "handView", handView);
            SetObjectReference(tableView, "tableCamera", tableCamera);
            SetObjectReference(tableCamera, "pointerRouter", pointerRouter);
            SetObjectReference(tableController, "tableView", tableView);
            SetObjectReference(tableController, "pointerRouter", pointerRouter);
            SetObjectReference(matchFlow, "tableView", tableView);
            SetObjectReference(matchFlow, "tableController", tableController);
            SetObjectReference(matchFlow, "dealerQuestions", dealerQuestions);
            SetObjectReference(hudPresenter, "uiDocument", uiDocument);
            SetObjectReference(hudPresenter, "tableController", tableController);
            SetObjectReference(hudPresenter, "tableView", tableView);
            SetObjectReference(flowPresenter, "uiDocument", flowDocument);
            SetObjectReference(flowPresenter, "matchFlow", matchFlow);
            SetIntValue(tableView, "pooledCardCount", 104);
            hudObject.SetActive(true);
            flowObject.SetActive(true);

            EditorSceneManager.SaveScene(tableScene, TableScenePath);
            if (!replaceEmptyUntitledScene && previousScene.IsValid() && previousScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(tableScene, true);
            }
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName,
            UnityEngine.Object value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new System.InvalidOperationException("Missing serialized property " + propertyName + ".");
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetIntValue(UnityEngine.Object target, string propertyName, int value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new System.InvalidOperationException("Missing serialized property " + propertyName + ".");
            }

            property.intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static PanelSettings LoadOrCreatePanelSettings(string assetPath, int sortingOrder)
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(assetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, assetPath);
            }

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.match = 0.5f;
            settings.sortingOrder = sortingOrder;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static DealerQuestions LoadOrCreateDealerQuestions()
        {
            var asset = AssetDatabase.LoadAssetAtPath<DealerQuestions>(DealerQuestionsPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DealerQuestions>();
                AssetDatabase.CreateAsset(asset, DealerQuestionsPath);
            }

            asset.Configure(new[]
            {
                "Who would be most likely to forget why they walked into a room?",
                "Who tells the worst jokes?",
                "Who would be first to dance at a wedding?",
                "Who has the most chaotic phone gallery?",
                "Who would survive longest on a desert island?",
                "Who is most likely to accidentally start a trend?"
            });
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            var updatedScenes = new List<EditorBuildSettingsScene>(scenes.Length + 1);
            var found = false;
            for (var index = 0; index < scenes.Length; index++)
            {
                if (scenes[index].path == scenePath)
                {
                    updatedScenes.Add(new EditorBuildSettingsScene(scenePath, true));
                    found = true;
                }
                else
                {
                    updatedScenes.Add(scenes[index]);
                }
            }

            if (!found)
            {
                updatedScenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }

            EditorBuildSettings.scenes = updatedScenes.ToArray();
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