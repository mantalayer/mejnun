using System.Collections.Generic;
using Mejnun.Localization;
using UnityEngine;

namespace Mejnun.UI
{
    public static class FlavorLines
    {
        private const float PopupChance = 1f / 3f;
        private static List<string> uiKeys;

        public static string PickGood()
        {
            return PickWithPrefix("flavor.good.");
        }

        public static string PickBad()
        {
            return PickWithPrefix("flavor.bad.");
        }

        public static IReadOnlyList<string> KeysForPrefix(string prefix)
        {
            if (uiKeys == null || uiKeys.Count == 0)
            {
                uiKeys = new List<string>(Loc.KeysOf(Loc.UiTable));
            }

            var matches = new List<string>();
            for (var index = 0; index < uiKeys.Count; index++)
            {
                if (uiKeys[index].StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    matches.Add(uiKeys[index]);
                }
            }

            return matches;
        }

        private static string PickWithPrefix(string prefix)
        {
            var keys = KeysForPrefix(prefix);
            if (keys.Count == 0 || Random.value >= PopupChance)
            {
                return null;
            }

            return keys[Random.Range(0, keys.Count)];
        }
    }
}