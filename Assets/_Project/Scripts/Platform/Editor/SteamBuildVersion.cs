using System.Globalization;
using System.Text.RegularExpressions;

namespace Mejnun.Platform.Editor
{
    internal enum SteamVersionBump
    {
        None,
        Major,
        Minor,
        Revision
    }

    internal static class SteamBuildVersion
    {
        private static readonly Regex m_versionPattern = new(@"[0-9]+\.[0-9]+\.[0-9]+");

        internal static string GetTagName(string version)
        {
            if (string.IsNullOrEmpty(version))
                return null;

            MatchCollection matches = m_versionPattern.Matches(version);
            return matches.Count == 0 ? null : $"v{matches[matches.Count - 1].Value}";
        }

        internal static bool TryBump(string currentVersion, SteamVersionBump bump,
            out string nextVersion, out string error)
        {
            nextVersion = currentVersion;
            error = null;
            if (string.IsNullOrEmpty(currentVersion))
            {
                error = "The bundle version is empty.";
                return false;
            }

            MatchCollection matches = m_versionPattern.Matches(currentVersion);
            if (matches.Count == 0)
            {
                error = "The bundle version must contain a major.minor.revision number.";
                return false;
            }

            Match match = matches[matches.Count - 1];
            string[] fields = match.Value.Split('.');
            if (!TryParseField(fields[0], out int major) ||
                !TryParseField(fields[1], out int minor) ||
                !TryParseField(fields[2], out int revision))
            {
                error = "A bundle version component is too large to increment.";
                return false;
            }

            switch (bump)
            {
                case SteamVersionBump.None:
                    return true;
                case SteamVersionBump.Major:
                    if (major == int.MaxValue)
                        return SetOverflowError(out error);

                    major++;
                    minor = 0;
                    revision = 0;
                    break;
                case SteamVersionBump.Minor:
                    if (minor == int.MaxValue)
                        return SetOverflowError(out error);

                    minor++;
                    revision = 0;
                    break;
                case SteamVersionBump.Revision:
                    if (revision == int.MaxValue)
                        return SetOverflowError(out error);

                    revision++;
                    break;
                default:
                    error = "The selected bundle version increment is not supported.";
                    return false;
            }

            string bumpedVersion = string.Join(".",
                FormatField(major, fields[0].Length),
                FormatField(minor, fields[1].Length),
                FormatField(revision, fields[2].Length));
            nextVersion = string.Concat(currentVersion.Substring(0, match.Index), bumpedVersion,
                currentVersion.Substring(match.Index + match.Length));
            return true;
        }

        private static bool TryParseField(string field, out int value)
        {
            return int.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        private static string FormatField(int value, int width)
        {
            return value.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
        }

        private static bool SetOverflowError(out string error)
        {
            error = "The selected bundle version component cannot be incremented any further.";
            return false;
        }
    }
}