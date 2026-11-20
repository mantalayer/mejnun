using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mejnun.Platform.Editor
{
    public sealed class SteamUploadWindow : EditorWindow
    {
        private SteamUploadConfig m_config;
        private SteamUploadLocalSettings m_localSettings;
        private UnityEditor.Editor m_configEditor;
        private SteamVersionBump m_versionBump = SteamVersionBump.Revision;
        private bool m_commitAndTag = true;
        private string[] m_gitChanges = Array.Empty<string>();
        private string m_gitStatusError;
        private int m_selectedAppIndex;
        private Vector2 m_scrollPosition;
        private bool m_restorePlatformAfterUpload;

        [MenuItem("Mejnun/Steam/Upload...")]
        private static void Open()
        {
            GetWindow<SteamUploadWindow>("Steam Upload");
        }

        [MenuItem("Mejnun/Steam/Create Default Upload Config")]
        public static void CreateDefaultConfigAsset()
        {
            SteamUploadConfig existing = AssetDatabase.LoadAssetAtPath<SteamUploadConfig>(SteamUploadConfig.AssetPath);
            if (existing != null)
            {
                Debug.LogWarning($"Steam upload config already exists at {SteamUploadConfig.AssetPath}; it was not overwritten.");
                return;
            }

            string directory = Path.GetDirectoryName(SteamUploadConfig.AssetPath);
            Directory.CreateDirectory(directory);
            SteamUploadConfig config = CreateInstance<SteamUploadConfig>();
            config.InitializeDefaults();
            AssetDatabase.CreateAsset(config, SteamUploadConfig.AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created default Steam upload config at {SteamUploadConfig.AssetPath}. Assign Build Profiles before building.");
        }

        private void OnEnable()
        {
            m_localSettings = SteamUploadLocalSettings.Load();
            LoadConfig();
            RefreshGitStatus();
        }

        private void OnFocus()
        {
            RefreshGitStatus();
        }

        private void OnDisable()
        {
            if (m_configEditor != null)
                DestroyImmediate(m_configEditor);
        }

        private void OnGUI()
        {
            if (m_localSettings == null)
                m_localSettings = SteamUploadLocalSettings.Load();

            DrawLocalSettings();
            EditorGUILayout.Space();
            DrawConfig();
        }

        private void DrawLocalSettings()
        {
            EditorGUILayout.LabelField("Local SteamCMD settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Set this to the SDK's ContentBuilder folder. The account name and SDK path are saved only in ignored UserSettings.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            string contentBuilderPath = EditorGUILayout.TextField("ContentBuilder", m_localSettings.ContentBuilderPath ?? string.Empty);
            if (EditorGUI.EndChangeCheck())
            {
                m_localSettings.ContentBuilderPath = contentBuilderPath;
                SaveLocalSettings();
            }

            if (GUILayout.Button("Browse...", GUILayout.Width(80)))
            {
                string selected = EditorUtility.OpenFolderPanel("Select Steam ContentBuilder", m_localSettings.ContentBuilderPath, string.Empty);
                if (!string.IsNullOrEmpty(selected))
                {
                    m_localSettings.ContentBuilderPath = selected;
                    SaveLocalSettings();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            string builderAccount = EditorGUILayout.TextField("Builder account", m_localSettings.BuilderAccount ?? string.Empty);
            if (EditorGUI.EndChangeCheck())
            {
                m_localSettings.BuilderAccount = builderAccount;
                SaveLocalSettings();
            }

            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                if (GUILayout.Button("Log in to SteamCMD (Terminal)..."))
                {
                    if (!SteamUploader.TryOpenLoginTerminal(m_localSettings, out string error))
                        EditorUtility.DisplayDialog("SteamCMD Login", error, "OK");
                }
            }
            else
            {
                EditorGUILayout.HelpBox("SteamCMD login through Terminal is available only in the macOS Editor.", MessageType.Info);
            }

            EditorGUILayout.HelpBox("SteamCMD will prompt for your password and Steam Guard code in Terminal if needed. Unity does not collect or store the password.", MessageType.Warning);
        }

        private void DrawConfig()
        {
            EditorGUILayout.LabelField("Upload configuration", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            m_config = (SteamUploadConfig)EditorGUILayout.ObjectField("Config", m_config,
                typeof(SteamUploadConfig), false);
            if (EditorGUI.EndChangeCheck())
                SetConfigEditor();

            if (m_config == null)
            {
                if (GUILayout.Button("Create default config"))
                {
                    CreateDefaultConfigAsset();
                    LoadConfig();
                }
                return;
            }

            if (m_configEditor != null)
            {
                m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition, GUILayout.MinHeight(180));
                m_configEditor.OnInspectorGUI();
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Build Profiles are created manually in Unity's Build Profiles window. Each enabled entry needs a profile with MEJNUN_STEAM; MEJNUN_DEMO must match the app. Windows and macOS can share a depot when each maps to its own subfolder.", MessageType.Info);
            EditorGUILayout.HelpBox("Building replaces the generated output folder for that app/depot/subfolder. It does not delete other depot outputs.", MessageType.Warning);

            if (m_config.Apps == null || m_config.Apps.Count == 0)
            {
                EditorGUILayout.HelpBox("Add at least one app in the config.", MessageType.Warning);
                return;
            }

            if (m_selectedAppIndex >= m_config.Apps.Count)
                m_selectedAppIndex = 0;
            string[] appNames = new string[m_config.Apps.Count];
            for (int i = 0; i < appNames.Length; i++)
                appNames[i] = $"{m_config.Apps[i].Name} ({m_config.Apps[i].AppId})";
            m_selectedAppIndex = EditorGUILayout.Popup("Steam app", m_selectedAppIndex, appNames);

            SteamUploadApp app = m_config.Apps[m_selectedAppIndex];
            DrawBuildVersion();

            EditorGUILayout.LabelField("Branch", string.IsNullOrWhiteSpace(app.Branch)
                ? "(none: upload only)"
                : app.Branch);
            if (GUILayout.Button("Validate selected app"))
            {
                ShowValidation(app);
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Build"))
                RunAfterLayout(() => BuildSelectedApp(app, false));
            if (GUILayout.Button("Upload"))
                RunAfterLayout(() => UploadSelectedApp(app));
            if (GUILayout.Button("Build + Upload"))
                RunAfterLayout(() => BuildSelectedApp(app, true));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBuildVersion()
        {
            string currentVersion = PlayerSettings.bundleVersion;
            bool canParseVersion = SteamBuildVersion.TryBump(currentVersion, SteamVersionBump.None,
                out _, out string error);

            EditorGUILayout.LabelField("Build version", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Current version", currentVersion);
            if (!canParseVersion)
                m_versionBump = SteamVersionBump.None;

            EditorGUI.BeginDisabledGroup(!canParseVersion);
            m_versionBump = (SteamVersionBump)EditorGUILayout.EnumPopup("Increment", m_versionBump);
            EditorGUI.EndDisabledGroup();

            if (canParseVersion && SteamBuildVersion.TryBump(currentVersion, m_versionBump,
                out string nextVersion, out error))
                EditorGUILayout.LabelField("Next version", nextVersion);
            else
            {
                string warning = canParseVersion
                    ? $"Cannot increment this version: {error}"
                    : $"Cannot increment this version: {error} Build and upload remain available without a version bump.";
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            EditorGUILayout.HelpBox("Upload uses the current bundle version and does not change it.", MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Git build tracking", EditorStyles.boldLabel);
            m_commitAndTag = EditorGUILayout.Toggle("Commit version bump and tag", m_commitAndTag);
            if (m_commitAndTag)
            {
                string tagVersion = currentVersion;
                if (SteamBuildVersion.TryBump(currentVersion, m_versionBump, out string previewVersion, out _))
                    tagVersion = previewVersion;
                string tagName = SteamBuildVersion.GetTagName(tagVersion);
                EditorGUILayout.LabelField("Git tag", tagName ?? "Unavailable for this version");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Working tree", m_gitStatusError == null
                    ? $"{m_gitChanges.Length} uncommitted change(s)"
                    : "Status unavailable");
                if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                    RefreshGitStatus();
                EditorGUILayout.EndHorizontal();

                if (m_gitStatusError != null)
                    EditorGUILayout.HelpBox($"Git status unavailable: {m_gitStatusError} Build can continue without commit/tag.", MessageType.Warning);
                else if (m_gitChanges.Length > 0)
                    EditorGUILayout.HelpBox("Uncommitted files will be listed for confirmation before building. Only ProjectSettings/ProjectSettings.asset is committed by this tool.", MessageType.Info);
            }
        }

        private void RunAfterLayout(Action action)
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                    action();
            };
        }

        private void ShowValidation(SteamUploadApp app)
        {
            System.Collections.Generic.List<string> errors = SteamUploader.Validate(m_config, app,
                m_localSettings, app.Branch, false);
            if (errors.Count == 0)
            {
                string message = string.IsNullOrWhiteSpace(app.Branch)
                    ? $"'{app.Name}' is ready to build; uploads will not set a branch live."
                    : $"'{app.Name}' is ready to build for branch '{app.Branch.Trim()}'.";
                EditorUtility.DisplayDialog("Steam upload validation", message, "OK");
            }
            else
                EditorUtility.DisplayDialog("Steam upload validation", string.Join("\n", errors), "OK");
        }

        private void BuildSelectedApp(SteamUploadApp app, bool uploadAfterBuild)
        {
            if (uploadAfterBuild && !EnsureSteamLogin())
                return;

            string currentVersion = PlayerSettings.bundleVersion;
            string nextVersion = currentVersion;
            bool incrementVersion = m_versionBump != SteamVersionBump.None;
            if (incrementVersion && !SteamBuildVersion.TryBump(currentVersion, m_versionBump,
                out nextVersion, out string versionError))
            {
                EditorUtility.DisplayDialog("Steam build version", versionError, "OK");
                return;
            }

            string buildVersion = incrementVersion ? nextVersion : currentVersion;
            string tagName = SteamBuildVersion.GetTagName(buildVersion);
            bool performGitTracking = false;
            if (m_commitAndTag && !PrepareGitTracking(tagName, out performGitTracking))
                return;

            bool buildSucceeded = false;
            string error = null;
            try
            {
                if (incrementVersion)
                {
                    PlayerSettings.bundleVersion = nextVersion;
                    AssetDatabase.SaveAssets();
                }

                buildSucceeded = SteamUploader.TryBuildApp(m_config, app, m_localSettings, app.Branch, out error);
            }
            catch (Exception exception)
            {
                error = $"Build failed: {exception.Message}";
            }

            if (!buildSucceeded)
            {
                if (incrementVersion)
                {
                    PlayerSettings.bundleVersion = currentVersion;
                    AssetDatabase.SaveAssets();
                }

                SteamUploader.RestoreDefaultMacPlatform();
                EditorUtility.DisplayDialog("Steam build failed", error, "OK");
                return;
            }

            if (!uploadAfterBuild)
                SteamUploader.RestoreDefaultMacPlatform();

            Debug.Log($"[Steam Upload] Built {app.Name}; SteamPipe files are ready in ContentBuilder/scripts.");
            if (performGitTracking)
                CommitAndTagBuild(incrementVersion, buildVersion, tagName);

            if (uploadAfterBuild)
            {
                m_restorePlatformAfterUpload = true;
                UploadSelectedApp(app, true);
            }
        }

        private bool PrepareGitTracking(string tagName, out bool performGitTracking)
        {
            performGitTracking = false;
            if (string.IsNullOrEmpty(tagName))
            {
                EditorUtility.DisplayDialog("Steam build tag unavailable",
                    "The bundle version does not contain a numeric major.minor.revision value. The build will proceed without committing or tagging.", "OK");
                return true;
            }

            if (!SteamBuildGit.TryGetUncommittedChanges(out string[] changes, out string statusError))
            {
                bool continueWithoutGit = EditorUtility.DisplayDialog("Git unavailable",
                    $"{statusError}\n\nContinue building without a commit or tag?", "Build without tag", "Cancel");
                return continueWithoutGit;
            }

            if (!SteamBuildGit.IsValidTagName(tagName, out string tagError))
            {
                EditorUtility.DisplayDialog("Steam build tag invalid", tagError, "OK");
                return false;
            }

            if (!SteamBuildGit.TagExists(tagName, out bool tagExists, out tagError))
            {
                EditorUtility.DisplayDialog("Could not check Steam build tag", tagError, "OK");
                return false;
            }

            if (tagExists)
            {
                EditorUtility.DisplayDialog("Steam build tag already exists",
                    $"Git tag '{tagName}' already exists. Choose a different version before building.", "OK");
                return false;
            }

            if (changes.Length > 0)
            {
                const int maxListedChanges = 30;
                int listedChangeCount = Math.Min(changes.Length, maxListedChanges);
                string[] listedChanges = new string[listedChangeCount];
                Array.Copy(changes, listedChanges, listedChangeCount);
                string moreChanges = changes.Length > listedChangeCount
                    ? $"\n...and {changes.Length - listedChangeCount} more"
                    : string.Empty;
                string message = $"The working tree has {changes.Length} uncommitted change(s):\n\n{string.Join("\n", listedChanges)}{moreChanges}\n\n"
                    + "The version settings file will be committed after a successful build; all other changes remain uncommitted. Continue?";
                if (!EditorUtility.DisplayDialog("Uncommitted changes", message, "Build", "Cancel"))
                    return false;
            }

            performGitTracking = true;
            return true;
        }

        private void CommitAndTagBuild(bool incrementVersion, string version, string tagName)
        {
            string commitError = null;
            if (incrementVersion && !SteamBuildGit.TryCommitVersionFile(version, out commitError))
            {
                EditorUtility.DisplayDialog("Steam build commit failed",
                    $"The build succeeded, but ProjectSettings/ProjectSettings.asset could not be committed. No tag was created.\n\n{commitError}", "OK");
                RefreshGitStatus();
                return;
            }

            if (!SteamBuildGit.TryCreateTag(tagName, version, out string tagError))
                EditorUtility.DisplayDialog("Steam build tag failed",
                    $"The build succeeded, but Git tag '{tagName}' could not be created.\n\n{tagError}", "OK");

            RefreshGitStatus();
        }

        private void RefreshGitStatus()
        {
            if (SteamBuildGit.TryGetUncommittedChanges(out string[] changes, out string error))
            {
                m_gitChanges = changes;
                m_gitStatusError = null;
            }
            else
            {
                m_gitChanges = Array.Empty<string>();
                m_gitStatusError = error;
            }
        }

        private bool EnsureSteamLogin()
        {
            while (true)
            {
                if (!SteamUploader.TryCheckLogin(m_localSettings, out bool loggedIn, out string error))
                    return EditorUtility.DisplayDialog("SteamCMD login check failed",
                        $"{error}\n\nContinue anyway without verifying the login?", "Continue anyway", "Cancel");

                if (loggedIn)
                    return true;

                if (!SteamUploader.TryOpenLoginTerminal(m_localSettings, out error))
                {
                    EditorUtility.DisplayDialog("SteamCMD login could not start", error, "OK");
                    return false;
                }

                if (!EditorUtility.DisplayDialog("SteamCMD login required",
                    "SteamCMD could not confirm a valid cached login. Finish signing in through the Terminal window, then continue here to verify the login before building.",
                    "Continue", "Cancel"))
                    return false;
            }
        }

        private void UploadSelectedApp(SteamUploadApp app, bool loginAlreadyChecked = false)
        {
            if (!loginAlreadyChecked && !EnsureSteamLogin())
                return;

            if (!SteamUploader.TryStartUpload(m_config, app, m_localSettings, app.Branch,
                OnUploadCompleted, out string error))
            {
                if (m_restorePlatformAfterUpload)
                {
                    SteamUploader.RestoreDefaultMacPlatform();
                    m_restorePlatformAfterUpload = false;
                }

                EditorUtility.DisplayDialog("Steam upload could not start", error, "OK");
            }
        }

        private void OnUploadCompleted(int exitCode)
        {
            if (m_restorePlatformAfterUpload)
            {
                SteamUploader.RestoreDefaultMacPlatform();
                m_restorePlatformAfterUpload = false;
            }

            if (exitCode == 0)
                ShowNotification(new GUIContent("SteamCMD upload complete"));
            else
                ShowNotification(new GUIContent($"SteamCMD failed ({exitCode})"));
        }

        private void LoadConfig()
        {
            m_config = AssetDatabase.LoadAssetAtPath<SteamUploadConfig>(SteamUploadConfig.AssetPath);
            SetConfigEditor();
        }

        private void SetConfigEditor()
        {
            if (m_configEditor != null)
                DestroyImmediate(m_configEditor);
            m_configEditor = m_config == null ? null : UnityEditor.Editor.CreateEditor(m_config);
        }

        private void SaveLocalSettings()
        {
            try
            {
                m_localSettings.Save();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Steam Upload] Could not save local settings: {exception.Message}");
            }
        }
    }
}