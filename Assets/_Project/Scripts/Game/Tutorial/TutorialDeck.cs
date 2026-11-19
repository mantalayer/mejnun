using System;
using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.Game
{
    /// <summary>
    /// Builds an exact 104-card shoe order for <see cref="GameSetup.ShoeOrder"/>: the authored hands are interleaved
    /// in deal order (the seat after the dealer first), unused cards fill the middle of the pile, and the authored
    /// draws sit at the end of the shoe so the first draw takes the first card in <c>drawsTopFirst</c>.
    /// </summary>
    public static class TutorialDeck
    {
        public const int HandSize = 11;

        public static byte[] Build(int dealer, IReadOnlyList<Card>[] hands, IReadOnlyList<Card> drawsTopFirst)
        {
            if (hands == null)
            {
                throw new ArgumentNullException(nameof(hands));
            }

            var seatCount = hands.Length;
            if (seatCount < 2 || seatCount > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(hands), seatCount, "Between 2 and 6 hands are required.");
            }

            if (dealer < 0 || dealer >= seatCount)
            {
                throw new ArgumentOutOfRangeException(nameof(dealer), dealer, "The dealer must be a valid seat.");
            }

            drawsTopFirst = drawsTopFirst ?? Array.Empty<Card>();
            var dealtCount = seatCount * HandSize;
            if (drawsTopFirst.Count > Card.CardCount - dealtCount)
            {
                throw new ArgumentException("Too many authored draws for the draw pile.", nameof(drawsTopFirst));
            }

            var order = new byte[Card.CardCount];
            var used = new bool[Card.CardCount];
            for (var seat = 0; seat < seatCount; seat++)
            {
                if (hands[seat] == null || hands[seat].Count != HandSize)
                {
                    throw new ArgumentException("Every hand must have exactly 11 cards.", nameof(hands));
                }
            }

            for (var round = 0; round < HandSize; round++)
            {
                for (var offset = 1; offset <= seatCount; offset++)
                {
                    var seat = (dealer + offset) % seatCount;
                    order[round * seatCount + offset - 1] = Claim(hands[seat][round], used);
                }
            }

            for (var index = 0; index < drawsTopFirst.Count; index++)
            {
                order[Card.CardCount - 1 - index] = Claim(drawsTopFirst[index], used);
            }

            var fillIndex = dealtCount;
            for (var id = 0; id < Card.CardCount; id++)
            {
                if (!used[id])
                {
                    order[fillIndex++] = (byte)id;
                }
            }

            return order;
        }

        private static byte Claim(Card card, bool[] used)
        {
            if (used[card.Id])
            {
                throw new ArgumentException("Card " + card.Id + " is authored more than once.");
            }

            used[card.Id] = true;
            return card.Id;
        }
    }
}
