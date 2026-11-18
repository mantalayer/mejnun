using System;
using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.Game
{
    public sealed class SetMarkers
    {
        private static readonly string[] Symbols = { "★", "●", "▲", "◆" };
        private readonly List<MarkedSet> marks = new List<MarkedSet>();
        private readonly List<MarkedCard> cardMarks = new List<MarkedCard>();

        public IReadOnlyList<MarkedSet> Marks => marks;
        public IReadOnlyList<MarkedCard> CardMarks => cardMarks;
        public event Action<MarkedSet> Broken;

        public void Clear()
        {
            marks.Clear();
            cardMarks.Clear();
        }

        public void CycleCard(byte cardId)
        {
            var existing = FindCardMark(cardId);
            if (existing == null)
            {
                cardMarks.Add(new MarkedCard(Symbols[0], cardId));
                return;
            }

            var symbolIndex = Array.IndexOf(Symbols, existing.Symbol);
            if (symbolIndex + 1 >= Symbols.Length)
            {
                cardMarks.Remove(existing);
            }
            else
            {
                existing.Symbol = Symbols[symbolIndex + 1];
            }
        }

        public void Cycle(int meldId, IReadOnlyList<Meld> board)
        {
            var meld = FindMeld(board, meldId);
            if (meld == null)
            {
                return;
            }

            var existing = FindMark(meldId);
            if (existing == null)
            {
                var cardIds = new byte[meld.Cards.Count];
                for (var index = 0; index < meld.Cards.Count; index++)
                {
                    cardIds[index] = meld.Cards[index].Id;
                }

                marks.Add(new MarkedSet(Symbols[0], meldId, cardIds));
                return;
            }

            var symbolIndex = Array.IndexOf(Symbols, existing.Symbol);
            if (symbolIndex + 1 >= Symbols.Length)
            {
                marks.Remove(existing);
            }
            else
            {
                existing.Symbol = Symbols[symbolIndex + 1];
            }
        }

        public void Update(IReadOnlyList<Meld> board)
        {
            for (var markIndex = marks.Count - 1; markIndex >= 0; markIndex--)
            {
                var mark = marks[markIndex];
                var containingMeld = FindContainingMeld(mark.CardIds, board);
                if (containingMeld != null)
                {
                    mark.MeldId = containingMeld.Id;
                    continue;
                }

                var largestMeld = FindLargestFragment(mark.CardIds, board, out var fragment);
                Broken?.Invoke(mark);
                if (largestMeld == null || fragment.Count == 0)
                {
                    marks.RemoveAt(markIndex);
                    continue;
                }

                mark.MeldId = largestMeld.Id;
                mark.CardIds = fragment.ToArray();
            }
        }

        private MarkedSet FindMark(int meldId)
        {
            for (var index = 0; index < marks.Count; index++)
            {
                if (marks[index].MeldId == meldId)
                {
                    return marks[index];
                }
            }

            return null;
        }

        private MarkedCard FindCardMark(byte cardId)
        {
            for (var index = 0; index < cardMarks.Count; index++)
            {
                if (cardMarks[index].CardId == cardId)
                {
                    return cardMarks[index];
                }
            }

            return null;
        }

        private static Meld FindContainingMeld(IReadOnlyList<byte> cardIds, IReadOnlyList<Meld> board)
        {
            if (board == null)
            {
                return null;
            }

            for (var meldIndex = 0; meldIndex < board.Count; meldIndex++)
            {
                var meld = board[meldIndex];
                var containsAll = true;
                for (var idIndex = 0; idIndex < cardIds.Count && containsAll; idIndex++)
                {
                    containsAll = Contains(meld.Cards, cardIds[idIndex]);
                }

                if (containsAll)
                {
                    return meld;
                }
            }

            return null;
        }

        private static Meld FindLargestFragment(IReadOnlyList<byte> cardIds, IReadOnlyList<Meld> board,
            out List<byte> fragment)
        {
            fragment = new List<byte>();
            Meld largest = null;
            if (board == null)
            {
                return null;
            }

            for (var meldIndex = 0; meldIndex < board.Count; meldIndex++)
            {
                var candidate = new List<byte>();
                for (var idIndex = 0; idIndex < cardIds.Count; idIndex++)
                {
                    if (Contains(board[meldIndex].Cards, cardIds[idIndex]))
                    {
                        candidate.Add(cardIds[idIndex]);
                    }
                }

                if (candidate.Count > fragment.Count)
                {
                    fragment = candidate;
                    largest = board[meldIndex];
                }
            }

            return largest;
        }

        private static bool Contains(IReadOnlyList<Card> cards, byte cardId)
        {
            for (var index = 0; index < cards.Count; index++)
            {
                if (cards[index].Id == cardId)
                {
                    return true;
                }
            }

            return false;
        }

        private static Meld FindMeld(IReadOnlyList<Meld> board, int meldId)
        {
            if (board == null)
            {
                return null;
            }

            for (var index = 0; index < board.Count; index++)
            {
                if (board[index].Id == meldId)
                {
                    return board[index];
                }
            }

            return null;
        }
    }

    public sealed class MarkedSet
    {
        internal MarkedSet(string symbol, int meldId, byte[] cardIds)
        {
            Symbol = symbol;
            MeldId = meldId;
            CardIds = cardIds;
        }

        public string Symbol { get; internal set; }
        public int MeldId { get; internal set; }
        public IReadOnlyList<byte> CardIds { get; internal set; }
    }

    public sealed class MarkedCard
    {
        internal MarkedCard(string symbol, byte cardId)
        {
            Symbol = symbol;
            CardId = cardId;
        }

        public string Symbol { get; internal set; }
        public byte CardId { get; }
    }
}