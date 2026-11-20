using System;
using System.Collections.Generic;

namespace Mejnun.Localization
{
    // Picks the best shipped locale for an ordered list of preferred BCP-47
    // tags. Tags are tried in order; for each one an exact match wins, then a
    // script/region-aware match (zh-CN -> zh-Hans, pt-PT -> pt-PT,
    // es-419 -> es). Traditional Chinese never falls back to Simplified.
    public static class LocaleMatcher
    {
        public static string Match(IEnumerable<string> preferredTags, IReadOnlyList<string> availableCodes)
        {
            if (preferredTags == null || availableCodes == null || availableCodes.Count == 0)
                return null;

            foreach (string rawTag in preferredTags)
            {
                string match = MatchOne(rawTag, availableCodes);
                if (match != null)
                    return match;
            }

            return null;
        }

        public static string MatchOne(string rawTag, IReadOnlyList<string> availableCodes)
        {
            if (string.IsNullOrWhiteSpace(rawTag) || availableCodes == null)
                return null;

            string tag = rawTag.Trim().Replace('_', '-');
            foreach (string code in availableCodes)
            {
                if (string.Equals(code, tag, StringComparison.OrdinalIgnoreCase))
                    return code;
            }

            string language = BaseLanguage(tag);
            if (language == "zh")
            {
                string script = IsTraditionalChinese(tag) ? "zh-Hant" : "zh-Hans";
                foreach (string code in availableCodes)
                {
                    if (string.Equals(code, script, StringComparison.OrdinalIgnoreCase))
                        return code;
                }

                return null;
            }

            if (language == "pt")
            {
                string preferredPortuguese = IsEuropeanPortuguese(tag) ? "pt-PT" : "pt-BR";
                string preferredMatch = FindCode(preferredPortuguese, availableCodes);
                if (preferredMatch != null)
                    return preferredMatch;

                string otherPortuguese = preferredPortuguese == "pt-PT" ? "pt-BR" : "pt-PT";
                string otherMatch = FindCode(otherPortuguese, availableCodes);
                if (otherMatch != null)
                    return otherMatch;
            }

            foreach (string code in availableCodes)
            {
                if (BaseLanguage(code) == language)
                    return code;
            }

            return null;
        }

        private static string BaseLanguage(string tag)
        {
            int dash = tag.IndexOf('-');
            return (dash < 0 ? tag : tag.Substring(0, dash)).ToLowerInvariant();
        }

        private static string FindCode(string requestedCode, IReadOnlyList<string> availableCodes)
        {
            foreach (string code in availableCodes)
            {
                if (string.Equals(code, requestedCode, StringComparison.OrdinalIgnoreCase))
                    return code;
            }

            return null;
        }

        private static bool IsEuropeanPortuguese(string tag)
        {
            string[] parts = tag.ToUpperInvariant().Split('-');
            for (int index = 1; index < parts.Length; index++)
            {
                switch (parts[index])
                {
                    case "PT":
                    case "AO":
                    case "MZ":
                    case "CV":
                    case "GW":
                    case "ST":
                    case "TL":
                    case "MO":
                        return true;
                }
            }

            return false;
        }

        private static bool IsTraditionalChinese(string tag)
        {
            string upper = tag.ToUpperInvariant();
            return upper.Contains("-HANT") || upper.EndsWith("-TW") || upper.EndsWith("-HK") || upper.EndsWith("-MO");
        }
    }
}
