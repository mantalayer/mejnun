using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Mejnun.Platform;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace Mejnun.Platform.Editor
{
    public static class SteamUploader
    {
        private const string SteamCmdRelativePath = "builder_osx/steamcmd.sh";
        private const string SteamSettingsAssetPath = "Assets/Resources/PlatformSettings.asset";
        private const string DoNotShipDirectoryName = "_BurstDebugInformation_DoNotShip";
        private const string BackupDirectoryName = "_BackUpThisFolder_ButDontShipItWithYourGame";
        private static readonly ConcurrentQueue<ProcessOutput> s_output = new();
        private static Process s_process;
        private static Action<int> s_processCompleted;

        private struct ProcessOutput
        {
            public string Text;
            public bool IsError;

            public ProcessOutput(string text, bool isError)
            {
                Text = text;
                IsError = isError;
            }
        }

        internal static List<string> Validate(SteamUploadConfig config, SteamUploadApp app,
            SteamUploadLocalSettings localSettings, string branch, bool forUpload)
        {
            branch = NormalizeBranch(branch);
            List<string> errors = new();
            if (config == null)
            {
                errors.Add("Assign a SteamUploadConfig asset.");
                return errors;
            }

            if (app == null)
            {
                errors.Add("Select an app from the upload config.");
                return errors;
            }

            if (app.AppId == 0)
                errors.Add($"App '{app.Name}' has no Steam App ID.");

            if (string.IsNullOrWhiteSpace(app.Name))
                errors.Add("The app name must not be empty.");

            if (!string.IsNullOrEmpty(branch))
            {
                if (string.Equals(branch, "default", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(branch, "public", StringComparison.OrdinalIgnoreCase))
                    errors.Add("Uploads to the default/public branch are blocked; choose a beta branch or leave the branch empty.");
                else if (!IsSafeVdfValue(branch))
                    errors.Add("Branch names may contain only letters, numbers, periods, underscores and hyphens.");
            }

            PlatformSettings platformSettings = AssetDatabase.LoadAssetAtPath<PlatformSettings>(SteamSettingsAssetPath);
            if (platformSettings == null)
            {
                errors.Add($"Missing Steam settings asset at {SteamSettingsAssetPath}.");
            }
            else
            {
                uint expectedAppId = app.IsDemo ? platformSettings.SteamDemoAppId : platformSettings.SteamFullAppId;
                if (expectedAppId == 0)
                    errors.Add(app.IsDemo
                        ? "No demo App ID is configured in PlatformSettings; create the Steam demo app before uploading."
                        : "No full-game App ID is configured in PlatformSettings.");
                else if (app.AppId != expectedAppId)
                    errors.Add($"App '{app.Name}' uses App ID {app.AppId}, but PlatformSettings has {expectedAppId}.");
            }

            if (localSettings == null || string.IsNullOrWhiteSpace(localSettings.ContentBuilderPath))
            {
                errors.Add("Set the local ContentBuilder path in the Steam upload window.");
            }
            else if (!Directory.Exists(localSettings.ContentBuilderPath))
            {
                errors.Add($"ContentBuilder directory does not exist: {localSettings.ContentBuilderPath}");
            }

            if (forUpload)
            {
                if (localSettings == null || string.IsNullOrWhiteSpace(localSettings.BuilderAccount))
                    errors.Add("Set the Steam builder account in the local upload settings.");
                else if (localSettings.BuilderAccount.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '"' }) >= 0)
                    errors.Add("The Steam builder account must not contain whitespace or quotes.");

                if (localSettings != null && !string.IsNullOrWhiteSpace(localSettings.ContentBuilderPath))
                {
                    string steamCmdPath = Path.Combine(localSettings.ContentBuilderPath, SteamCmdRelativePath);
                    if (!File.Exists(steamCmdPath))
                        errors.Add($"SteamCMD was not found at {steamCmdPath}. This uploader requires the macOS ContentBuilder SDK.");
                }
            }

            if (app.Depots == null || app.Depots.Count == 0)
            {
                errors.Add($"App '{app.Name}' has no depots configured.");
                return errors;
            }

            bool hasEnabledBuild = false;
            HashSet<uint> depotIds = new();
            for (int depotIndex = 0; depotIndex < app.Depots.Count; depotIndex++)
            {
                SteamUploadDepot depot = app.Depots[depotIndex];
                if (depot == null || depot.DepotId == 0)
                {
                    errors.Add($"App '{app.Name}' has a depot with an invalid ID.");
                    continue;
                }

                if (!depotIds.Add(depot.DepotId))
                    errors.Add($"App '{app.Name}' has duplicate depot ID {depot.DepotId}.");

                if (depot.Builds == null)
                    continue;

                for (int buildIndex = 0; buildIndex < depot.Builds.Count; buildIndex++)
                {
                    SteamUploadPlayerBuild build = depot.Builds[buildIndex];
                    if (build == null || !build.Enabled)
                        continue;

                    hasEnabledBuild = true;
                    string buildLabel = $"App '{app.Name}', depot {depot.DepotId}, target {build.Target}";
                    if (!IsSupportedStandaloneTarget(build.Target))
                        errors.Add($"{buildLabel} is not a supported standalone target for this uploader.");
                    else if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, build.Target))
                        errors.Add($"{buildLabel} is not installed in this Unity Editor. Install its Build Support module or disable this player.");

                    if (!IsSafePathSegment(build.Subfolder))
                        errors.Add($"{buildLabel} needs a simple subfolder name (letters, numbers, underscores or hyphens).");

                    BuildProfile profile = build.Profile;
                    if (profile == null)
                    {
                        errors.Add($"{buildLabel} has no Build Profile assigned. Create the profile in Unity's Build Profiles window, then assign it here.");
                        continue;
                    }

                    string[] defines = profile.scriptingDefines ?? Array.Empty<string>();
                    if (!HasDefine(defines, "MEJNUN_STEAM"))
                        errors.Add($"{buildLabel} profile '{profile.name}' must define MEJNUN_STEAM.");
                    if (HasDefine(defines, "DISABLESTEAMWORKS"))
                        errors.Add($"{buildLabel} profile '{profile.name}' defines DISABLESTEAMWORKS and cannot build the Steam backend.");
                    if (HasDefine(defines, "MEJNUN_DEMO") != app.IsDemo)
                        errors.Add($"{buildLabel} profile '{profile.name}' must {(app.IsDemo ? "define" : "not define")} MEJNUN_DEMO.");

                    if (forUpload && localSettings != null && !string.IsNullOrWhiteSpace(localSettings.ContentBuilderPath) &&
                        IsSafePathSegment(build.Subfolder))
                    {
                        string outputDirectory = GetBuildDirectory(localSettings.ContentBuilderPath, app.AppId,
                            depot.DepotId, build.Subfolder);
                        if (!Directory.Exists(outputDirectory) || Directory.GetFileSystemEntries(outputDirectory).Length == 0)
                            errors.Add($"{buildLabel} has no built output at {outputDirectory}; build it before uploading.");
                    }
                }
            }

            if (!hasEnabledBuild)
                errors.Add($"App '{app.Name}' has no enabled player builds.");

            return errors;
        }

        internal static bool TryOpenLoginTerminal(SteamUploadLocalSettings localSettings, out string error)
        {
            if (Application.platform != RuntimePlatform.OSXEditor)
            {
                error = "SteamCMD login through Terminal is available only in the macOS Editor.";
                return false;
            }

            if (!TryGetSteamCmdPath(localSettings, out string builderAccount, out string steamCmdPath, out error))
                return false;

            try
            {
                string cacheDirectory = Application.temporaryCachePath;
                Directory.CreateDirectory(cacheDirectory);
                string loginScriptPath = Path.Combine(cacheDirectory, $"SteamCmdLogin-{Guid.NewGuid():N}.command");
                string scriptContents = "#!/bin/bash\n" +
                                       $"cd {QuoteShellArgument(Path.GetDirectoryName(steamCmdPath))} || exit 1\n" +
                                       $"{QuoteShellArgument(steamCmdPath)} +login {QuoteShellArgument(builderAccount)} +quit\n" +
                                       "status=$?\n" +
                                       "printf '\\nSteamCMD exited with status %s.\\n' \"$status\"\n" +
                                       "read -r -p 'Press Return to close this window...' _\n" +
                                       "exit \"$status\"\n";
                File.WriteAllText(loginScriptPath, scriptContents, new UTF8Encoding(false));

                ProcessStartInfo chmodStartInfo = CreateProcessStartInfo("/bin/chmod",
                    $"755 {QuoteArgument(loginScriptPath)}", cacheDirectory);
                using Process chmodProcess = new() { StartInfo = chmodStartInfo };
                if (!chmodProcess.Start())
                {
                    error = "Could not make the SteamCMD login script executable.";
                    return false;
                }

                chmodProcess.WaitForExit();
                if (chmodProcess.ExitCode != 0)
                {
                    string chmodError = chmodProcess.StandardError.ReadToEnd().Trim();
                    error = string.IsNullOrEmpty(chmodError)
                        ? "Could not make the SteamCMD login script executable."
                        : $"Could not make the SteamCMD login script executable: {chmodError}";
                    return false;
                }

                ProcessStartInfo terminalStartInfo = CreateProcessStartInfo("/usr/bin/open",
                    $"-a Terminal {QuoteArgument(loginScriptPath)}", cacheDirectory);
                using Process terminalProcess = new() { StartInfo = terminalStartInfo };
                if (!terminalProcess.Start())
                {
                    error = "Could not open Terminal for SteamCMD login.";
                    return false;
                }

                terminalProcess.WaitForExit();
                if (terminalProcess.ExitCode != 0)
                {
                    string terminalError = terminalProcess.StandardError.ReadToEnd().Trim();
                    error = string.IsNullOrEmpty(terminalError)
                        ? "Could not open Terminal for SteamCMD login."
                        : $"Could not open Terminal for SteamCMD login: {terminalError}";
                    return false;
                }

                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = $"Could not start SteamCMD login in Terminal: {exception.Message}";
                return false;
            }
        }

        internal static bool TryCheckLogin(SteamUploadLocalSettings localSettings, out bool loggedIn, out string error)
        {
            loggedIn = false;
            if (Application.platform != RuntimePlatform.OSXEditor)
            {
                error = "SteamCMD login checks are available only in the macOS Editor.";
                return false;
            }

            if (s_process != null)
            {
                error = "A SteamCMD upload is already running.";
                return false;
            }

            if (!TryGetSteamCmdPath(localSettings, out string builderAccount, out string steamCmdPath, out error))
                return false;

            const int timeoutSeconds = 180;
            ConcurrentQueue<string> output = new();
            Process process = null;
            bool processStarted = false;
            try
            {
                string arguments = $"{QuoteArgument(steamCmdPath)} +@NoPromptForPassword 1 +login {QuoteArgument(builderAccount)} +quit";
                ProcessStartInfo startInfo = CreateProcessStartInfo("/bin/bash", arguments,
                    Path.GetDirectoryName(steamCmdPath));
                startInfo.RedirectStandardInput = true;
                process = new Process { StartInfo = startInfo };
                process.OutputDataReceived += (_, eventArgs) => EnqueueOutput(output, eventArgs.Data);
                process.ErrorDataReceived += (_, eventArgs) => EnqueueOutput(output, eventArgs.Data);
                if (!process.Start())
                {
                    error = "Could not start SteamCMD to check its cached login.";
                    return false;
                }

                processStarted = true;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.StandardInput.Close();
                DateTime startTime = DateTime.UtcNow;
                bool loginSucceeded = false;
                while (!process.HasExited)
                {
                    loginSucceeded |= HasSuccessfulLoginOutput(output, builderAccount);
                    TimeSpan elapsed = DateTime.UtcNow - startTime;
                    if (EditorUtility.DisplayCancelableProgressBar("Checking SteamCMD login",
                        "SteamCMD may update on first run. Cancel to stop the check.",
                        Mathf.Clamp01((float)(elapsed.TotalSeconds / timeoutSeconds))))
                    {
                        StopLoginCheckProcess(process);
                        error = "SteamCMD login check was cancelled.";
                        return false;
                    }

                    if (elapsed.TotalSeconds >= timeoutSeconds)
                    {
                        StopLoginCheckProcess(process);
                        error = $"SteamCMD login check timed out after {timeoutSeconds} seconds.";
                        return false;
                    }

                    Thread.Sleep(100);
                }

                process.WaitForExit();
                loginSucceeded |= HasSuccessfulLoginOutput(output, builderAccount);
                loggedIn = process.ExitCode == 0 && loginSucceeded;
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                if (processStarted)
                    StopLoginCheckProcess(process);

                error = $"Could not check the SteamCMD login: {exception.Message}";
                return false;
            }
            finally
            {
                process?.Dispose();
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool TryGetSteamCmdPath(SteamUploadLocalSettings localSettings, out string builderAccount,
            out string steamCmdPath, out string error)
        {
            builderAccount = null;
            steamCmdPath = null;
            if (localSettings == null || string.IsNullOrWhiteSpace(localSettings.ContentBuilderPath))
            {
                error = "Set the local ContentBuilder path in the Steam upload window.";
                return false;
            }

            if (!Directory.Exists(localSettings.ContentBuilderPath))
            {
                error = $"ContentBuilder directory does not exist: {localSettings.ContentBuilderPath}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(localSettings.BuilderAccount))
            {
                error = "Set the Steam builder account in the local upload settings.";
                return false;
            }

            if (localSettings.BuilderAccount.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '"' }) >= 0)
            {
                error = "The Steam builder account must not contain whitespace or quotes.";
                return false;
            }

            try
            {
                steamCmdPath = Path.Combine(Path.GetFullPath(localSettings.ContentBuilderPath), SteamCmdRelativePath);
            }
            catch (Exception exception)
            {
                error = $"Invalid ContentBuilder path: {exception.Message}";
                return false;
            }

            if (!File.Exists(steamCmdPath))
            {
                error = $"SteamCMD was not found at {steamCmdPath}. This uploader requires the macOS ContentBuilder SDK.";
                return false;
            }

            builderAccount = localSettings.BuilderAccount;
            error = null;
            return true;
        }

        private static bool HasSuccessfulLoginOutput(ConcurrentQueue<string> output, string builderAccount)
        {
            while (output.TryDequeue(out string line))
            {
                if (line.IndexOf("Logging in user", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    line.IndexOf(builderAccount, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    line.TrimEnd().EndsWith("OK", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void StopLoginCheckProcess(Process process)
        {
            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit();
            }
        }

        internal static bool TryBuildApp(SteamUploadConfig config, SteamUploadApp app,
            SteamUploadLocalSettings localSettings, string branch, out string error)
        {
            branch = NormalizeBranch(branch);
            List<string> errors = Validate(config, app, localSettings, branch, false);
            if (errors.Count > 0)
            {
                error = string.Join("\n", errors);
                return false;
            }

            try
            {
                for (int depotIndex = 0; depotIndex < app.Depots.Count; depotIndex++)
                {
                    SteamUploadDepot depot = app.Depots[depotIndex];
                    for (int buildIndex = 0; buildIndex < depot.Builds.Count; buildIndex++)
                    {
                        SteamUploadPlayerBuild build = depot.Builds[buildIndex];
                        if (build == null || !build.Enabled)
                            continue;

                        if (!TryBuildPlayer(localSettings.ContentBuilderPath, app.AppId, depot.DepotId, build,
                            out error))
                            return false;
                    }
                }

                if (!TryWriteVdfFiles(app, branch, localSettings.ContentBuilderPath, out _, out error))
                    return false;

                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = $"Build failed: {exception.Message}";
                return false;
            }
        }

        internal static bool TryStartUpload(SteamUploadConfig config, SteamUploadApp app,
            SteamUploadLocalSettings localSettings, string branch, Action<int> onCompleted, out string error)
        {
            branch = NormalizeBranch(branch);
            List<string> errors = Validate(config, app, localSettings, branch, true);
            if (errors.Count > 0)
            {
                error = string.Join("\n", errors);
                return false;
            }

            if (s_process != null)
            {
                error = "A SteamCMD upload is already running.";
                return false;
            }

            if (!TryWriteVdfFiles(app, branch, localSettings.ContentBuilderPath, out string appBuildVdf, out error))
                return false;

            string contentBuilderPath = Path.GetFullPath(localSettings.ContentBuilderPath);
            string steamCmdPath = Path.Combine(contentBuilderPath, SteamCmdRelativePath);
            string arguments = $"{QuoteArgument(steamCmdPath)} +login {localSettings.BuilderAccount} +run_app_build {QuoteArgument(appBuildVdf)} +quit";
            ProcessStartInfo startInfo = CreateProcessStartInfo("/bin/bash", arguments, Path.GetDirectoryName(steamCmdPath));

            try
            {
                Process process = new() { StartInfo = startInfo, EnableRaisingEvents = true };
                process.OutputDataReceived += (_, eventArgs) => EnqueueOutput(eventArgs.Data, false);
                process.ErrorDataReceived += (_, eventArgs) => EnqueueOutput(eventArgs.Data, true);
                if (!process.Start())
                {
                    process.Dispose();
                    error = "Could not start SteamCMD.";
                    return false;
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                s_process = process;
                s_processCompleted = onCompleted;
                string destination = string.IsNullOrEmpty(branch)
                    ? "without setting a branch live"
                    : $"to branch '{branch}'";
                Debug.Log($"[Steam Upload] Started upload for {app.Name} ({app.AppId}) {destination}.");
                EditorApplication.update += PumpSteamCmdOutput;
                EditorApplication.quitting += StopSteamCmd;
                AssemblyReloadEvents.beforeAssemblyReload += StopSteamCmd;
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = $"Could not start SteamCMD: {exception.Message}";
                return false;
            }
        }

        internal static bool TryWriteVdfFiles(SteamUploadApp app, string branch,
            string contentBuilderPath, out string appBuildVdfPath, out string error)
        {
            appBuildVdfPath = null;
            branch = NormalizeBranch(branch);
            if (app == null || app.AppId == 0)
            {
                error = "Cannot write SteamPipe files without a valid app and App ID.";
                return false;
            }

            if (!string.IsNullOrEmpty(branch) &&
                (!IsSafeVdfValue(branch) ||
                 string.Equals(branch, "default", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(branch, "public", StringComparison.OrdinalIgnoreCase)))
            {
                error = "Choose a valid non-public beta branch or leave the branch empty before generating SteamPipe files.";
                return false;
            }

            if (app.Depots == null || string.IsNullOrWhiteSpace(contentBuilderPath))
            {
                error = "The upload config needs depots and a local ContentBuilder path.";
                return false;
            }

            try
            {
                string scriptsDirectory = Path.Combine(Path.GetFullPath(contentBuilderPath), "scripts");
                Directory.CreateDirectory(scriptsDirectory);
                List<SteamUploadDepot> activeDepots = new();
                for (int depotIndex = 0; depotIndex < app.Depots.Count; depotIndex++)
                {
                    SteamUploadDepot depot = app.Depots[depotIndex];
                    if (depot?.Builds == null)
                        continue;

                    bool hasBuild = false;
                    for (int buildIndex = 0; buildIndex < depot.Builds.Count; buildIndex++)
                    {
                        if (depot.Builds[buildIndex] is { Enabled: true })
                        {
                            hasBuild = true;
                            break;
                        }
                    }

                    if (hasBuild)
                        activeDepots.Add(depot);
                }

                if (activeDepots.Count == 0)
                {
                    error = $"App '{app.Name}' has no enabled depots to write.";
                    return false;
                }

                string contentRoot = $"../content/{app.AppId}/";
                for (int depotIndex = 0; depotIndex < activeDepots.Count; depotIndex++)
                {
                    SteamUploadDepot depot = activeDepots[depotIndex];
                    string depotVdfPath = Path.Combine(scriptsDirectory, $"depot_build_{depot.DepotId}.vdf");
                    File.WriteAllText(depotVdfPath, BuildDepotVdf(depot));
                }

                appBuildVdfPath = Path.Combine(scriptsDirectory, $"app_build_{app.AppId}.vdf");
                string description = $"{PlayerSettings.bundleVersion} ({GetGitShortHash()})";
                File.WriteAllText(appBuildVdfPath, BuildAppVdf(app, activeDepots, branch, contentRoot, description));
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = $"Could not write SteamPipe VDF files: {exception.Message}";
                return false;
            }
        }

        public static void BatchBuildAndUpload()
        {
            int exitCode = 1;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                if (!TryGetArgument(args, "-tfSteamApp", out string appSelector))
                    throw new InvalidOperationException("Pass -tfSteamApp <app name or App ID>.");

                SteamUploadConfig config = AssetDatabase.LoadAssetAtPath<SteamUploadConfig>(SteamUploadConfig.AssetPath);
                SteamUploadApp app = FindApp(config, appSelector);
                if (app == null)
                    throw new InvalidOperationException($"No upload app named or numbered '{appSelector}' exists in {SteamUploadConfig.AssetPath}.");

                SteamUploadLocalSettings localSettings = SteamUploadLocalSettings.Load();
                string branch = TryGetArgument(args, "-tfSteamBranch", out string branchOverride)
                    ? branchOverride
                    : app.Branch;
                if (!TryBuildApp(config, app, localSettings, branch, out string buildError))
                    throw new InvalidOperationException(buildError);

                if (ContainsArgument(args, "-tfSteamSkipUpload"))
                {
                    Debug.Log("[Steam Upload] Player builds and SteamPipe VDF files are ready; upload skipped by -tfSteamSkipUpload.");
                    exitCode = 0;
                }
                else
                {
                    List<string> errors = Validate(config, app, localSettings, branch, true);
                    if (errors.Count > 0)
                        throw new InvalidOperationException(string.Join("\n", errors));

                    exitCode = RunSteamCmdBlocking(app, localSettings, branch);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Steam Upload] {exception.Message}");
                exitCode = 1;
            }

            EditorApplication.Exit(exitCode);
        }

        internal static void RestoreDefaultMacPlatform()
        {
            if (Application.isBatchMode)
                return;

            try
            {
                if (BuildProfile.GetActiveBuildProfile() != null)
                    BuildProfile.SetActiveBuildProfile(null);

                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX &&
                    !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
                    Debug.LogWarning("[Steam Upload] Could not switch the active build target back to macOS.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Steam Upload] Could not restore the default macOS build platform: {exception.Message}");
            }
        }

        private static bool TryBuildPlayer(string contentBuilderPath, uint appId, uint depotId,
            SteamUploadPlayerBuild build, out string error)
        {
            string outputDirectory = GetBuildDirectory(contentBuilderPath, appId, depotId, build.Subfolder);
            string productName = MakeSafeFileName(PlayerSettings.productName);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (EditorUserBuildSettings.activeBuildTarget != build.Target &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, build.Target))
            {
                error = $"Could not switch the active build target to {build.Target}; build cancelled.";
                return false;
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, true);
            Directory.CreateDirectory(outputDirectory);
            string outputPath = GetPlayerOutputPath(outputDirectory, productName, build.Target);

            Debug.Log($"[Steam Upload] Building {build.Target} with profile '{build.Profile.name}' to {outputPath}.");
            BuildPlayerWithProfileOptions options = new()
            {
                buildProfile = build.Profile,
                locationPathName = outputPath,
                options = BuildOptions.StrictMode
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report == null)
            {
                error = $"Unity returned no build report for {build.Target}.";
                return false;
            }

            if (report.summary.result != BuildResult.Succeeded)
            {
                error = $"{build.Target} build failed with result {report.summary.result} ({report.summary.totalErrors} errors).";
                return false;
            }

            if (report.summary.platform != build.Target)
            {
                error = $"Build Profile '{build.Profile.name}' produced {report.summary.platform}, but this depot entry is configured for {build.Target}; upload blocked.";
                return false;
            }

            if (!Directory.Exists(outputDirectory))
            {
                error = $"Build succeeded but output directory is missing: {outputDirectory}";
                return false;
            }

            StripNonDepotContent(outputDirectory);
            error = null;
            return true;
        }

        private static string BuildAppVdf(SteamUploadApp app, List<SteamUploadDepot> depots,
            string branch, string contentRoot, string description)
        {
            StringWriter writer = new();
            writer.WriteLine("\"AppBuild\"");
            writer.WriteLine("{");
            WriteVdfPair(writer, 1, "AppID", app.AppId.ToString());
            WriteVdfPair(writer, 1, "Desc", description);
            WriteVdfPair(writer, 1, "ContentRoot", contentRoot);
            WriteVdfPair(writer, 1, "BuildOutput", "../output/");
            branch = NormalizeBranch(branch);
            if (!string.IsNullOrEmpty(branch))
                WriteVdfPair(writer, 1, "SetLive", branch);
            writer.WriteLine("\t\"Depots\"");
            writer.WriteLine("\t{");
            for (int i = 0; i < depots.Count; i++)
                WriteVdfPair(writer, 2, depots[i].DepotId.ToString(), $"depot_build_{depots[i].DepotId}.vdf");
            writer.WriteLine("\t}");
            writer.WriteLine("}");
            return writer.ToString();
        }

        // Depot scripts inherit the app script's ContentRoot; SteamCMD resolves a depot-level
        // relative ContentRoot against that root, not against the script file.
        private static string BuildDepotVdf(SteamUploadDepot depot)
        {
            StringWriter writer = new();
            writer.WriteLine("\"DepotBuildConfig\"");
            writer.WriteLine("{");
            WriteVdfPair(writer, 1, "DepotID", depot.DepotId.ToString());
            for (int i = 0; i < depot.Builds.Count; i++)
            {
                SteamUploadPlayerBuild build = depot.Builds[i];
                if (build == null || !build.Enabled)
                    continue;

                writer.WriteLine("\t\"FileMapping\"");
                writer.WriteLine("\t{");
                WriteVdfPair(writer, 2, "LocalPath", $"{depot.DepotId}/{build.Subfolder}/*");
                WriteVdfPair(writer, 2, "DepotPath", $"{build.Subfolder}/");
                WriteVdfPair(writer, 2, "recursive", "1");
                writer.WriteLine("\t}");
            }

            WriteVdfPair(writer, 1, "FileExclusion", "steam_appid.txt");
            WriteVdfPair(writer, 1, "FileExclusion", "*.pdb");
            WriteVdfPair(writer, 1, "FileExclusion", ".DS_Store");
            writer.WriteLine("}");
            return writer.ToString();
        }

        private static void WriteVdfPair(TextWriter writer, int indent, string key, string value)
        {
            writer.Write(new string('\t', indent));
            writer.Write('"');
            writer.Write(EscapeVdf(key));
            writer.Write("\"\t\"");
            writer.Write(EscapeVdf(value));
            writer.WriteLine('"');
        }

        private static string EscapeVdf(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", string.Empty).Replace("\n", string.Empty);
        }

        private static string GetGitShortHash()
        {
            try
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                ProcessStartInfo startInfo = CreateProcessStartInfo("git", "rev-parse --short HEAD", projectRoot);
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                using Process process = Process.Start(startInfo);
                if (process == null)
                    return "unknown";

                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return process.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output : "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private static ProcessStartInfo CreateProcessStartInfo(string fileName, string arguments, string workingDirectory)
        {
            return new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        private static int RunSteamCmdBlocking(SteamUploadApp app, SteamUploadLocalSettings localSettings, string branch)
        {
            branch = NormalizeBranch(branch);
            string contentBuilderPath = Path.GetFullPath(localSettings.ContentBuilderPath);
            string steamCmdPath = Path.Combine(contentBuilderPath, SteamCmdRelativePath);
            string appBuildVdfPath = Path.Combine(contentBuilderPath, "scripts", $"app_build_{app.AppId}.vdf");
            string arguments = $"{QuoteArgument(steamCmdPath)} +login {localSettings.BuilderAccount} +run_app_build {QuoteArgument(appBuildVdfPath)} +quit";
            ProcessStartInfo startInfo = CreateProcessStartInfo("/bin/bash", arguments, Path.GetDirectoryName(steamCmdPath));
            ConcurrentQueue<ProcessOutput> output = new();
            using Process process = new() { StartInfo = startInfo };
            process.OutputDataReceived += (_, eventArgs) => EnqueueOutput(output, eventArgs.Data, false);
            process.ErrorDataReceived += (_, eventArgs) => EnqueueOutput(output, eventArgs.Data, true);
            if (!process.Start())
                throw new InvalidOperationException("Could not start SteamCMD.");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            string destination = string.IsNullOrEmpty(branch)
                ? "without setting a branch live"
                : $"to branch '{branch}'";
            Debug.Log($"[Steam Upload] Uploading {app.Name} ({app.AppId}) {destination}.");
            while (!process.HasExited)
            {
                DrainOutput(output);
                Thread.Sleep(100);
            }

            process.WaitForExit();
            DrainOutput(output);
            if (process.ExitCode == 0)
                Debug.Log("[Steam Upload] SteamCMD completed successfully.");
            else
                Debug.LogError($"[Steam Upload] SteamCMD exited with code {process.ExitCode}.");
            return process.ExitCode;
        }

        private static void PumpSteamCmdOutput()
        {
            DrainOutput(s_output);
            if (s_process == null || !s_process.HasExited)
                return;

            s_process.WaitForExit();
            DrainOutput(s_output);
            int exitCode = s_process.ExitCode;
            s_process.Dispose();
            s_process = null;
            EditorApplication.update -= PumpSteamCmdOutput;
            EditorApplication.quitting -= StopSteamCmd;
            AssemblyReloadEvents.beforeAssemblyReload -= StopSteamCmd;
            if (exitCode == 0)
                Debug.Log("[Steam Upload] SteamCMD completed successfully.");
            else
                Debug.LogError($"[Steam Upload] SteamCMD exited with code {exitCode}.");

            Action<int> completed = s_processCompleted;
            s_processCompleted = null;
            completed?.Invoke(exitCode);
        }

        private static void StopSteamCmd()
        {
            EditorApplication.update -= PumpSteamCmdOutput;
            EditorApplication.quitting -= StopSteamCmd;
            AssemblyReloadEvents.beforeAssemblyReload -= StopSteamCmd;
            Process process = s_process;
            s_process = null;
            s_processCompleted = null;
            if (process == null)
                return;

            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Steam Upload] Could not stop SteamCMD: {exception.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }

        private static void StripNonDepotContent(string outputDirectory)
        {
            string[] files = Directory.GetFiles(outputDirectory, "steam_appid.txt", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
                File.Delete(files[i]);

            string[] directories = Directory.GetDirectories(outputDirectory, "*", SearchOption.AllDirectories);
            Array.Sort(directories, (a, b) => b.Length.CompareTo(a.Length));
            for (int i = 0; i < directories.Length; i++)
            {
                // Unity prefixes these folders with the product name, e.g. "<Product>_BurstDebugInformation_DoNotShip".
                string name = Path.GetFileName(directories[i]);
                if (name.EndsWith(DoNotShipDirectoryName, StringComparison.Ordinal) ||
                    name.EndsWith(BackupDirectoryName, StringComparison.Ordinal))
                    Directory.Delete(directories[i], true);
            }
        }

        private static string GetBuildDirectory(string contentBuilderPath, uint appId, uint depotId, string subfolder)
        {
            return Path.Combine(Path.GetFullPath(contentBuilderPath), "content", appId.ToString(), depotId.ToString(), subfolder);
        }

        private static string GetPlayerOutputPath(string outputDirectory, string productName, BuildTarget target)
        {
            string extension = target switch
            {
                BuildTarget.StandaloneWindows64 => ".exe",
                BuildTarget.StandaloneOSX => ".app",
                _ => string.Empty
            };
            return Path.Combine(outputDirectory, productName + extension);
        }

        private static bool IsSupportedStandaloneTarget(BuildTarget target)
        {
            return target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneOSX;
        }

        private static bool HasDefine(string[] defines, string symbol)
        {
            for (int i = 0; i < defines.Length; i++)
            {
                if (string.Equals(defines[i], symbol, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool IsSafePathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!char.IsLetterOrDigit(character) && character != '_' && character != '-')
                    return false;
            }

            return true;
        }

        private static bool IsSafeVdfValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!char.IsLetterOrDigit(character) && character != '.' && character != '_' && character != '-')
                    return false;
            }

            return true;
        }

        private static string NormalizeBranch(string branch)
        {
            return branch?.Trim() ?? string.Empty;
        }

        private static string MakeSafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Mejnun";

            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            char[] characters = value.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
            {
                if (Array.IndexOf(invalidCharacters, characters[i]) >= 0 || characters[i] == '/' || characters[i] == '\\')
                    characters[i] = '_';
            }
            return new string(characters);
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string QuoteShellArgument(string value)
        {
            return "'" + value.Replace("'", "'\\''") + "'";
        }

        private static void EnqueueOutput(string text, bool isError)
        {
            EnqueueOutput(s_output, text, isError);
        }

        private static void EnqueueOutput(ConcurrentQueue<ProcessOutput> output, string text, bool isError)
        {
            if (text != null)
                output.Enqueue(new ProcessOutput(text, isError));
        }

        private static void EnqueueOutput(ConcurrentQueue<string> output, string text)
        {
            if (text != null)
                output.Enqueue(text);
        }

        private static void DrainOutput(ConcurrentQueue<ProcessOutput> output)
        {
            while (output.TryDequeue(out ProcessOutput line))
            {
                if (line.IsError)
                    Debug.LogWarning($"[SteamCMD] {line.Text}");
                else
                    Debug.Log($"[SteamCMD] {line.Text}");
            }
        }

        private static bool TryGetArgument(string[] args, string name, out string value)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == name && i + 1 < args.Length)
                {
                    value = args[i + 1];
                    return true;
                }

                string prefix = name + "=";
                if (args[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    value = args[i].Substring(prefix.Length);
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static bool ContainsArgument(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == name)
                    return true;
            }
            return false;
        }

        private static SteamUploadApp FindApp(SteamUploadConfig config, string selector)
        {
            if (config?.Apps == null)
                return null;

            for (int i = 0; i < config.Apps.Count; i++)
            {
                SteamUploadApp app = config.Apps[i];
                if (app != null && (string.Equals(app.Name, selector, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(app.AppId.ToString(), selector, StringComparison.Ordinal)))
                    return app;
            }

            return null;
        }
    }
}