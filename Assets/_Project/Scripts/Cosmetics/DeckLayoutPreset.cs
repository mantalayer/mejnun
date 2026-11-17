using System;
using Mejnun.Core;

namespace Mejnun.Cosmetics
{
    public enum DeckLayoutPreset
    {
        ZigZag,
        RowMajor
    }

    public static class DeckLayoutPresetUtility
    {
        public static int GetFaceSpriteIndex(DeckLayoutPreset preset, Suit suit, Rank rank)
        {
            var suitIndex = (int)suit;
            var rankIndex = (int)rank;
            if (suitIndex < 0 || suitIndex >= 4)
            {
                throw new ArgumentOutOfRangeException(nameof(suit), suit, "Suit must be a standard card suit.");
            }

            if (rankIndex < 0 || rankIndex >= 13)
            {
                throw new ArgumentOutOfRangeException(nameof(rank), rank, "Rank must be a standard card rank.");
            }

            if (preset == DeckLayoutPreset.RowMajor)
            {
                return suitIndex * 13 + rankIndex;
            }

            if (preset != DeckLayoutPreset.ZigZag)
            {
                throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown deck layout preset.");
            }

            var visualRank = rank == Rank.Ace ? 12 : rankIndex - (int)Rank.Two;
            var pairIndex = suit == Suit.Clubs || suit == Suit.Hearts ? 0 : 1;
            var firstSuit = pairIndex == 0 ? suit == Suit.Clubs : suit == Suit.Spades;
            var startsWithFirstSuit = visualRank <= 8 ? visualRank % 2 == 0 : visualRank % 2 != 0;
            var firstSlot = startsWithFirstSuit == firstSuit;
            return pairIndex * 27 + visualRank * 2 + (firstSlot ? 0 : 1);
        }

        public static int GetCardBackSpriteIndex(DeckLayoutPreset preset)
        {
            if (preset == DeckLayoutPreset.ZigZag)
            {
                return 26;
            }

            if (preset == DeckLayoutPreset.RowMajor)
            {
                return 52;
            }

            throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown deck layout preset.");
        }

        public static int GetBlankSpriteIndex()
        {
            return 53;
        }
    }
}