using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Mejnun.Localization.Editor
{
    // Runs before every player build, ahead of the Addressables content build
    // that packs the String Tables:
    //   1. validate every table and the game source;
    //   2. if any translation is missing or broken and Google Sheets is
    //      configured, push the new English text, let GOOGLETRANSLATE fill
    //      it, and pull the results into the String Tables;
    //   3. validate again and report.
    // Problems are warnings (English is shown at run time) unless
    // LocalizationPipelineSettings.FailBuildOnIssues is on.
    // Batch builds only talk to Google when -locSync is passed, because the
    // first OAuth sign-in needs a browser.
    public sealed class LocalizationPreBuild : IPreprocessBuildWithReport
    {
        public const string BatchSyncArgument = "-locSync";

        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            CosmeticNameSync.SyncAll();
            var settings = LocalizationPipelineSettings.Load();
            var validation = LocalizationValidator.Validate();

            if (validation.HasTranslationIssues)
            {
                string skipped = TrySync(settings);
                if (skipped == null)
                    validation = LocalizationValidator.Validate();
                else
                    Debug.LogWarning($"[Localization] Translations are incomplete and the Google Sheets sync was skipped: {skipped}. See Docs/Localization.md.");
            }

            LocalizationValidator.LogReport(validation, "Pre-build check: ");

            if (settings != null && settings.FailBuildOnIssues && validation.Issues.Count > 0)
                throw new BuildFailedException($"Localization issues block this build ({validation.Summary()}). See {LocalizationValidator.ReportPath}.");
        }

        // Returns null when a sync ran, otherwise why it was skipped.
        private static string TrySync(LocalizationPipelineSettings settings)
        {
            if (settings == null)
                return $"{LocalizationPipelineSettings.AssetPath} does not exist (run Mejnun > Localization > Set Up Project)";
            if (!settings.SyncBeforeBuild)
                return "SyncBeforeBuild is off";
            if (!settings.IsSheetsConfigured(out string reason))
                return reason;
            if (Application.isBatchMode && Array.IndexOf(Environment.GetCommandLineArgs(), BatchSyncArgument) < 0)
                return $"batch-mode build without {BatchSyncArgument}";

            try
            {
                var result = LocalizationSheetsSync.Sync(settings);
                Debug.Log($"[Localization] Pre-build Sheets sync: {result}.");
                return null;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return $"the sync failed ({exception.Message})";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
