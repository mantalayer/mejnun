using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.AI
{
    public sealed class BotKnowledge
    {
        private readonly byte[][] knownCardsBySeat;
        private readonly bool[] drewWhileDown;
        private readonly int[] handCounts;

        public BotKnowledge(byte[][] cardsBySeat, bool[] drewDown, int[] counts)
        {
            knownCardsBySeat = Clone(cardsBySeat);
            drewWhileDown = drewDown == null ? new bool[0] : (bool[])drewDown.Clone();
            handCounts = counts == null ? new int[0] : (int[])counts.Clone();
        }

        public bool DrewWhileDown(int seat)
        {
            return seat >= 0 && seat < drewWhileDown.Length && drewWhileDown[seat];
        }

        public int HandCount(int seat)
        {
            return seat >= 0 && seat < handCounts.Length ? handCounts[seat] : int.MaxValue;
        }

        public bool WouldEnableKnownExtender(IReadOnlyList<IReadOnlyList<Card>> board, int actingSeat)
        {
            if (board == null)
            {
                return false;
            }

            for (var seat = 0; seat < knownCardsBySeat.Length; seat++)
            {
                if (seat == actingSeat || DrewWhileDown(seat))
                {
                    continue;
                }

                var knownCards = knownCardsBySeat[seat];
                for (var knownIndex = 0; knownIndex < knownCards.Length; knownIndex++)
                {
                    var card = Card.FromId(knownCards[knownIndex]);
                    for (var meldIndex = 0; meldIndex < board.Count; meldIndex++)
                    {
                        var meld = board[meldIndex];
                        if (!MeldValidator.Classify(meld).IsValid)
                        {
                            continue;
                        }

                        var candidate = new List<Card>(meld.Count + 1);
                        for (var cardIndex = 0; cardIndex < meld.Count; cardIndex++)
                        {
                            candidate.Add(meld[cardIndex]);
                        }

                        candidate.Add(card);
                        if (MeldValidator.Classify(candidate).IsValid)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static byte[][] Clone(byte[][] source)
        {
            if (source == null)
            {
                return new byte[0][];
            }

            var clone = new byte[source.Length][];
            for (var index = 0; index < source.Length; index++)
            {
                clone[index] = source[index] == null ? new byte[0] : (byte[])source[index].Clone();
            }

            return clone;
        }
    }
}