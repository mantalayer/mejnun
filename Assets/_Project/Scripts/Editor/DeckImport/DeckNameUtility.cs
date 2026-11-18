using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Mejnun.Editor.DeckImport
{
    public static class DeckNameUtility
    {
        public static string GetDisplayName(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return "Deck";
            }

            var normalized = sourcePath.Replace('\\', '/');
            var components = normalized.Split('/');
            var leafComponent = components[components.Length - 1];
            var leaf = string.Equals(Path.GetExtension(leafComponent), ".png", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileNameWithoutExtension(leafComponent)
                : leafComponent;
            var fullPath = normalized.ToLowerInvariant();

            if (fullPath.Contains("blueeyedrat") && leaf.StartsWith("deck_", StringComparison.OrdinalIgnoreCase))
            {
                return "Blueeyedrat " + TitleCase(leaf.Substring("deck_".Length).Replace('_', ' '));
            }

            if (fullPath.Contains("sofu_playing_cards_pack"))
            {
                var size = FindSofuSize(components);
                return "Sofu" + (size == null ? string.Empty : " " + size);
            }

            if (fullPath.Contains("reigns - playing card deck"))
            {
                return "Reigns";
            }

            var name = leaf;
            if (name.EndsWith(" - Playing Card Deck", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - " - Playing Card Deck".Length);
            }

            name = Regex.Replace(name, "([a-z0-9])([A-Z])", "$1 $2");
            name = Regex.Replace(name, "[^a-zA-Z0-9]+", " ").Trim();
            return TitleCase(name);
        }

        public static string GetId(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "deck";
            }

            var builder = new StringBuilder();
            var lastWasSeparator = false;
            for (var index = 0; index < displayName.Length; index++)
            {
                var character = displayName[index];
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                    lastWasSeparator = false;
                }
                else if (!lastWasSeparator && builder.Length > 0)
                {
                    builder.Append('_');
                    lastWasSeparator = true;
                }
            }

            if (builder.Length > 0 && builder[builder.Length - 1] == '_')
            {
                builder.Length--;
            }

            return builder.Length == 0 ? "deck" : builder.ToString();
        }

        private static string FindSofuSize(string[] components)
        {
            for (var index = 0; index < components.Length; index++)
            {
                var component = components[index].ToLowerInvariant();
                if (component.Contains("large"))
                {
                    return "Large";
                }

                if (component.Contains("medium"))
                {
                    return "Medium";
                }

                if (component.Contains("small"))
                {
                    return "Small";
                }
            }

            return null;
        }

        private static string TitleCase(string value)
        {
            var words = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < words.Length; index++)
            {
                var word = words[index];
                words[index] = word.Length == 0
                    ? word
                    : char.ToUpperInvariant(word[0]) + (word.Length > 1 ? word.Substring(1).ToLowerInvariant() : string.Empty);
            }

            return string.Join(" ", words);
        }
    }
}