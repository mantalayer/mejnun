using Mejnun.Game;

namespace Mejnun.UI
{
    /// <summary>
    /// Stock bot names for the setup screen, in the current language. The names are the <c>bot.name.NNN</c> entries
    /// of <see cref="BotNameCatalog"/>; when a match starts, <see cref="MatchFlow"/> turns an untouched name back into
    /// its key, so saves and seats keep the ID and every client shows it in its own language.
    /// </summary>
    internal static class BotNames
    {
        internal static int Count => BotNameCatalog.Count;

        /// <summary>True for a stock bot name: its key, or its current-language or English text.</summary>
        internal static bool Contains(string name)
        {
            return BotNameCatalog.TryFind(name, out _);
        }

        /// <summary><paramref name="count"/> different bot names, in the current language.</summary>
        internal static string[] PickDistinct(int count)
        {
            var indices = BotNameCatalog.PickDistinct(count);
            var result = new string[indices.Length];
            for (var index = 0; index < indices.Length; index++)
            {
                result[index] = BotNameCatalog.DisplayName(indices[index]);
            }

            return result;
        }

        /// <summary>The current-language text of a bot name key; other names are returned as is.</summary>
        internal static string Display(string name)
        {
            return BotNameCatalog.TryParseKey(name, out var index) ? BotNameCatalog.DisplayName(index) : name;
        }
    }
}
