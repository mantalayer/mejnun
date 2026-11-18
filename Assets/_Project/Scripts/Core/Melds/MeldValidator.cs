using System;
using System.Collections.Generic;

namespace Mejnun.Core
{
    public static class MeldValidator
    {
        private const int MinimumMeldSize = 3;
        private const int MaximumStraightSize = 13;
        private const int MaximumSameKindSize = 4;
        private const int AceBit = 1 << (int)Rank.Ace;
        private const int TwoBit = 1 << (int)Rank.Two;
        private const int KingBit = 1 << (int)Rank.King;

        public static MeldClassification Classify(IReadOnlyList<Card> cards)
        {
            if (cards == null)
            {
                throw new ArgumentNullException(nameof(cards));
            }

            var count = cards.Count;
            if (count < MinimumMeldSize)
            {
                return Invalid(InvalidReason.TooShort);
            }

            if (count > MaximumStraightSize)
            {
                return Invalid(InvalidReason.TooLong);
            }

            var first = cards[0];
            var sameRank = true;
            var sameSuit = true;
            var rankMask = 0;
            var physicalCardMask = 0UL;
            var hasDouble = false;

            for (var index = 0; index < count; index++)
            {
                var card = cards[index];
                sameRank &= card.Rank == first.Rank;
                sameSuit &= card.Suit == first.Suit;

                var rankBit = 1 << (int)card.Rank;
                rankMask |= rankBit;

                var physicalCardBit = 1UL << ((int)card.Suit * 13 + (int)card.Rank);
                if ((physicalCardMask & physicalCardBit) != 0)
                {
                    hasDouble = true;
                }

                physicalCardMask |= physicalCardBit;
            }

            if (sameRank && count > MaximumSameKindSize)
            {
                return Invalid(InvalidReason.TooLong);
            }

            if (hasDouble)
            {
                return Invalid(InvalidReason.Double);
            }

            if (sameRank)
            {
                return new MeldClassification(MeldKind.SameKind, InvalidReason.None);
            }

            if (sameSuit)
            {
                if (IsConsecutive(rankMask, count, false))
                {
                    return new MeldClassification(MeldKind.StraightLow, InvalidReason.None);
                }

                if (IsConsecutive(rankMask, count, true))
                {
                    return new MeldClassification(MeldKind.StraightHigh, InvalidReason.None);
                }

                if ((rankMask & (AceBit | TwoBit | KingBit)) == (AceBit | TwoBit | KingBit))
                {
                    return Invalid(InvalidReason.WrapAround);
                }

                return Invalid(InvalidReason.NotConsecutive);
            }

            return IsConsecutive(rankMask, count, false) || IsConsecutive(rankMask, count, true)
                ? Invalid(InvalidReason.MixedSuits)
                : Invalid(InvalidReason.MixedRanks);
        }

        public static bool CanPlaceInto(IReadOnlyList<Card> existing, IReadOnlyList<Card> resulting)
        {
            return !Classify(existing).IsValid || Classify(resulting).IsValid;
        }

        public static bool IsBoardValid(IReadOnlyList<Meld> melds)
        {
            if (melds == null)
            {
                throw new ArgumentNullException(nameof(melds));
            }

            for (var index = 0; index < melds.Count; index++)
            {
                if (!Classify(melds[index].Cards).IsValid)
                {
                    return false;
                }
            }

            return true;
        }

        public static void CanonicalizeStraight(List<Card> cards)
        {
            if (cards == null)
            {
                throw new ArgumentNullException(nameof(cards));
            }

            var classification = Classify(cards);
            if (classification.Kind != MeldKind.StraightLow && classification.Kind != MeldKind.StraightHigh)
            {
                return;
            }

            var aceHigh = classification.Kind == MeldKind.StraightHigh;
            for (var index = 1; index < cards.Count; index++)
            {
                var card = cards[index];
                var value = StraightValue(card.Rank, aceHigh);
                var insertAt = index - 1;
                while (insertAt >= 0 && StraightValue(cards[insertAt].Rank, aceHigh) > value)
                {
                    cards[insertAt + 1] = cards[insertAt];
                    insertAt--;
                }

                cards[insertAt + 1] = card;
            }
        }

        private static bool IsConsecutive(int rankMask, int count, bool aceHigh)
        {
            var minimum = int.MaxValue;
            var maximum = int.MinValue;
            var ranksFound = 0;

            for (var rank = 0; rank <= (int)Rank.King; rank++)
            {
                if ((rankMask & (1 << rank)) == 0)
                {
                    continue;
                }

                var value = StraightValue((Rank)rank, aceHigh);
                minimum = Math.Min(minimum, value);
                maximum = Math.Max(maximum, value);
                ranksFound++;
            }

            return ranksFound == count && maximum - minimum + 1 == count;
        }

        private static int StraightValue(Rank rank, bool aceHigh)
        {
            if (rank == Rank.Ace)
            {
                return aceHigh ? 14 : 1;
            }

            return (int)rank + 1;
        }

        private static MeldClassification Invalid(InvalidReason reason)
        {
            return new MeldClassification(MeldKind.Invalid, reason);
        }
    }
}