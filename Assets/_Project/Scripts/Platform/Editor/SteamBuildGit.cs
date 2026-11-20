using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

namespace Mejnun.Platform.Editor
{
    internal static class SteamBuildGit
    {
        private const int m_processTimeoutMilliseconds = 15000;
        private const string m_versionSettingsPath = "ProjectSettings/ProjectSettings.asset";
        private static string m_gitExecutable;

        internal static bool TryGetUncommittedChanges(out string[] changes, out string error)
        {
            changes = Array.Empty<string>();
            if (!TryEnsureGit(out error) || !TryRun("status --porcelain --untracked-files=all",
                out string output, out error, out int exitCode))
                return false;

            if (exitCode != 0)
            {
                error = $"Git status failed: {output}";
                return false;
            }

            changes = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            return true;
        }

        internal static bool IsValidTagName(string tagName, out string error)
        {
            if (!TryEnsureGit(out error) || !TryRun($"check-ref-format {QuoteArgument($"refs/tags/{tagName}")}",
                out string output, out error, out int exitCode))
                return false;

            if (exitCode == 0)
                return true;

            error = string.IsNullOrWhiteSpace(output)
                ? $"'{tagName}' is not a valid Git tag name."
                : output.Trim();
            return false;
        }

        internal static bool TagExists(string tagName, out bool exists, out string error)
        {
            exists = false;
            if (!TryEnsureGit(out error) || !TryRun($"show-ref --verify --quiet {QuoteArgument($"refs/tags/{tagName}")}",
                out string output, out error, out int exitCode))
                return false;

            if (exitCode == 0)
            {
                exists = true;
                return true;
            }

            if (exitCode == 1)
                return true;

            error = string.IsNullOrWhiteSpace(output)
                ? $"Git could not check whether tag '{tagName}' exists."
                : output.Trim();
            return false;
        }

        internal static bool TryCommitVersionFile(string version, out string error)
        {
            string message = $"Bump Steam build version to {version}";
            string arguments = $"commit --only -m {QuoteArgument(message)} -- {QuoteArgument(m_versionSettingsPath)}";
            return TryRunGit(arguments, out _, out error);
        }

        internal static bool TryCreateTag(string tagName, string version, out string error)
        {
            string message = $"Steam build {version}";
            string arguments = $"tag -a {QuoteArgument(tagName)} -m {QuoteArgument(message)}";
            return TryRunGit(arguments, out _, out error);
        }

        private static bool TryRunGit(string arguments, out string output, out string error)
        {
            output = null;
            if (!TryEnsureGit(out error) || !TryRun(arguments, out output, out error, out int exitCode))
                return false;

            if (exitCode == 0)
                return true;

            error = string.IsNullOrWhiteSpace(output)
                ? $"Git {arguments} failed with exit code {exitCode}."
                : output.Trim();
            return false;
        }

        private static bool TryEnsureGit(out string error)
        {
            error = null;
            if (!string.IsNullOrEmpty(m_gitExecutable))
                return true;

            string[] candidates = { "git", "/usr/bin/git", "/opt/homebrew/bin/git", "/usr/local/bin/git" };
            string lastError = null;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (!TryRunProcess(candidates[i], "--version", out _, out string processError, out int exitCode))
                {
                    lastError = processError;
                    continue;
                }

                if (exitCode == 0)
                {
                    m_gitExecutable = candidates[i];
                    return true;
                }
            }

            error = $"Git is unavailable. Install Git or launch Unity from a shell with Git on PATH. {lastError}".Trim();
            return false;
        }

        private static bool TryRun(string arguments, out string output, out string error, out int exitCode)
        {
            return TryRunProcess(m_gitExecutable, arguments, out output, out error, out exitCode);
        }

        private static bool TryRunProcess(string executable, string arguments, out string output,
            out string error, out int exitCode)
        {
            output = string.Empty;
            error = null;
            exitCode = -1;
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = executable,
                        Arguments = arguments,
                        WorkingDirectory = GetProjectRoot(),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    process.StartInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
                    process.StartInfo.EnvironmentVariables["LC_ALL"] = "C";

                    if (!process.Start())
                    {
                        error = $"Could not start Git executable '{executable}'.";
                        return false;
                    }

                    System.Threading.Tasks.Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                    System.Threading.Tasks.Task<string> errorTask = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(m_processTimeoutMilliseconds))
                    {
                        process.Kill();
                        process.WaitForExit();
                        error = $"Git {arguments} timed out after {m_processTimeoutMilliseconds / 1000} seconds.";
                        return false;
                    }

                    output = outputTask.GetAwaiter().GetResult();
                    string standardError = errorTask.GetAwaiter().GetResult();
                    exitCode = process.ExitCode;
                    if (exitCode != 0 && !string.IsNullOrWhiteSpace(standardError))
                        output = standardError;
                    return true;
                }
            }
            catch (Exception exception)
            {
                error = $"Could not run Git: {exception.Message}";
                return false;
            }
        }

        private static string GetProjectRoot()
        {
            DirectoryInfo projectDirectory = Directory.GetParent(Application.dataPath);
            return projectDirectory == null ? Directory.GetCurrentDirectory() : projectDirectory.FullName;
        }

        private static string QuoteArgument(string value)
        {
            StringBuilder quoted = new StringBuilder("\"");
            int backslashCount = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (character == '\\')
                {
                    backslashCount++;
                    continue;
                }

                if (character == '"')
                {
                    quoted.Append('\\', backslashCount * 2 + 1);
                    quoted.Append(character);
                    backslashCount = 0;
                    continue;
                }

                quoted.Append('\\', backslashCount);
                quoted.Append(character);
                backslashCount = 0;
            }

            quoted.Append('\\', backslashCount * 2);
            quoted.Append('"');
            return quoted.ToString();
        }
    }
}