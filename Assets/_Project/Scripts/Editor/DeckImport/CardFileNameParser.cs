using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Mejnun.Core;

namespace Mejnun.Editor.DeckImport
{
    public enum CardFileKind
    {
        Unknown,
        Face,
        Back,
        Joker,
        Blank
    }

    public sealed class CardFileName
    {
        public CardFileKind Kind { get; }
        public Suit Suit { get; }
        public Rank Rank { get; }
        public string BackId { get; }
        public string JokerColor { get; }

        public CardFileName(CardFileKind kind, Suit suit = Suit.Clubs, Rank rank = Rank.Ace,
            string backId = null, string jokerColor = null)
        {
            Kind = kind;
            Suit = suit;
            Rank = rank;
            BackId = backId;
            JokerColor = jokerColor;
        }
    }

    public static class CardFileNameParser
    {
        private static readonly Dictionary<string, Suit> Suits = new Dictionary<string, Suit>(StringComparer.Ordinal)
        {
            { "club", Suit.Clubs }, { "clubs", Suit.Clubs },
            { "diamond", Suit.Diamonds }, { "diamonds", Suit.Diamonds }, { "dmnds", Suit.Diamonds },
            { "heart", Suit.Hearts }, { "hearts", Suit.Hearts },
            { "spade", Suit.Spades }, { "spades", Suit.Spades }
        };

        private static readonly Dictionary<string, Rank> Ranks = new Dictionary<string, Rank>(StringComparer.Ordinal)
        {
            { "a", Rank.Ace }, { "ace", Rank.Ace }, { "1", Rank.Ace },
            { "2", Rank.Two }, { "two", Rank.Two }, { "3", Rank.Three }, { "three", Rank.Three },
            { "4", Rank.Four }, { "four", Rank.Four }, { "5", Rank.Five }, { "five", Rank.Five },
            { "6", Rank.Six }, { "six", Rank.Six }, { "7", Rank.Seven }, { "seven", Rank.Seven },
            { "8", Rank.Eight }, { "eight", Rank.Eight }, { "9", Rank.Nine }, { "nine", Rank.Nine },
            { "10", Rank.Ten }, { "ten", Rank.Ten }, { "j", Rank.Jack }, { "jack", Rank.Jack },
            { "11", Rank.Jack }, { "q", Rank.Queen }, { "queen", Rank.Queen }, { "12", Rank.Queen },
            { "k", Rank.King }, { "king", Rank.King }, { "13", Rank.King }
        };

        public static CardFileName Parse(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return new CardFileName(CardFileKind.Unknown);
            }

            var stem = Path.GetFileNameWithoutExtension(filePath);
            var camelSeparated = Regex.Replace(stem, "([a-z0-9])([A-Z])", "$1 $2");
            var expanded = Regex.Replace(camelSeparated, "(?i)of", " ").ToLowerInvariant();
            var tokens = Regex.Matches(expanded, "[a-z0-9]+");
            var words = new List<string>(tokens.Count);
            for (var index = 0; index < tokens.Count; index++)
            {
                words.Add(tokens[index].Value);
            }

            if (Contains(words, "blank") || Contains(words, "empty"))
            {
                return new CardFileName(CardFileKind.Blank);
            }

            if (Contains(words, "joker") || Contains(words, "jokers"))
            {
                return new CardFileName(CardFileKind.Joker, jokerColor: Contains(words, "red")
                    ? "red"
                    : Contains(words, "black") ? "black" : null);
            }

            var backId = FindBackId(words);
            if (backId != null)
            {
                return new CardFileName(CardFileKind.Back, backId: backId);
            }

            if (!TryFindSuit(words, out var suit) || !TryFindRank(words, out var rank))
            {
                return new CardFileName(CardFileKind.Unknown);
            }

            return new CardFileName(CardFileKind.Face, suit, rank);
        }

        private static string FindBackId(IReadOnlyList<string> words)
        {
            for (var index = 0; index < words.Count; index++)
            {
                var word = words[index];
                if (word == "backs" || word == "cardbacks")
                {
                    continue;
                }

                var prefixLength = IsBackToken(word, "cardback", 8) ? 8
                    : IsBackToken(word, "back", 4) ? 4
                    : 0;
                if (prefixLength == 0)
                {
                    continue;
                }

                var suffix = word.Substring(prefixLength);
                if (suffix.Length == 0 && index + 1 < words.Count && IsBackNumber(words[index + 1]))
                {
                    suffix = words[index + 1];
                }

                if (suffix.Length == 0 || !int.TryParse(suffix, out var number) || number == 256)
                {
                    return "back_01";
                }

                return FormatBackId(number);
            }

            return null;
        }

        private static bool IsBackToken(string word, string prefix, int prefixLength)
        {
            if (string.Equals(word, prefix, StringComparison.Ordinal))
            {
                return true;
            }

            if (!word.StartsWith(prefix, StringComparison.Ordinal) || word.Length <= prefixLength)
            {
                return false;
            }

            for (var index = prefixLength; index < word.Length; index++)
            {
                if (!char.IsDigit(word[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsBackNumber(string word)
        {
            return int.TryParse(word, out var number) && number >= 0 && number != 256;
        }

        private static string FormatBackId(int number)
        {
            return "back_" + number.ToString(number < 10 ? "00" : "0");
        }

        private static bool TryFindSuit(IReadOnlyList<string> words, out Suit suit)
        {
            for (var index = 0; index < words.Count; index++)
            {
                if (Suits.TryGetValue(words[index], out suit))
                {
                    return true;
                }
            }

            suit = Suit.Clubs;
            return false;
        }

        private static bool TryFindRank(IReadOnlyList<string> words, out Rank rank)
        {
            var found = false;
            rank = Rank.Ace;
            for (var index = 0; index < words.Count; index++)
            {
                if (!TryParseRank(words[index], out var candidate))
                {
                    continue;
                }

                if (found && rank != candidate)
                {
                    return false;
                }

                rank = candidate;
                found = true;
            }

            return found;
        }

        private static bool TryParseRank(string word, out Rank rank)
        {
            if (Ranks.TryGetValue(word, out rank))
            {
                return true;
            }

            return int.TryParse(word, out var number) && Ranks.TryGetValue(number.ToString(), out rank);
        }

        private static bool Contains(IReadOnlyList<string> words, string value)
        {
            for (var index = 0; index < words.Count; index++)
            {
                if (string.Equals(words[index], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}