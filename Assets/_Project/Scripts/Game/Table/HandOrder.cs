using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.Game
{
    public sealed class HandOrder
    {
        private readonly Dictionary<int, List<byte>> orders = new Dictionary<int, List<byte>>();

        public IReadOnlyList<byte> GetCardIds(int seat)
        {
            return GetOrCreateOrder(seat);
        }

        public void Synchronize(int seat, IReadOnlyList<Card> hand)
        {
            var order = GetOrCreateOrder(seat);
            for (var index = order.Count - 1; index >= 0; index--)
            {
                if (!Contains(hand, order[index]))
                {
                    order.RemoveAt(index);
                }
            }

            for (var index = 0; index < hand.Count; index++)
            {
                var cardId = hand[index].Id;
                if (!order.Contains(cardId))
                {
                    order.Add(cardId);
                }
            }
        }

        public void Reorder(int seat, byte cardId, int targetIndex)
        {
            var order = GetOrCreateOrder(seat);
            var currentIndex = order.IndexOf(cardId);
            if (currentIndex < 0)
            {
                return;
            }

            order.RemoveAt(currentIndex);
            var clampedIndex = targetIndex < 0 ? 0 : targetIndex > order.Count ? order.Count : targetIndex;
            order.Insert(clampedIndex, cardId);
        }

        public void ReorderGroup(int seat, IReadOnlyList<byte> cardIds, int targetIndex)
        {
            if (cardIds == null || cardIds.Count == 0)
            {
                return;
            }

            var order = GetOrCreateOrder(seat);
            var group = new List<byte>(cardIds.Count);
            var firstIndex = int.MaxValue;
            var lastIndex = -1;
            for (var index = 0; index < order.Count; index++)
            {
                if (Contains(cardIds, order[index]))
                {
                    group.Add(order[index]);
                    firstIndex = System.Math.Min(firstIndex, index);
                    lastIndex = index;
                }
            }

            if (group.Count == 0)
            {
                return;
            }

            var originalCount = order.Count;
            if (targetIndex >= firstIndex && targetIndex <= lastIndex + 1)
            {
                return;
            }

            var removedBeforeTarget = 0;
            for (var index = 0; index < order.Count && index < targetIndex; index++)
            {
                if (Contains(group, order[index]))
                {
                    removedBeforeTarget++;
                }
            }

            order.RemoveAll(id => group.Contains(id));
            var insertion = targetIndex >= originalCount - 1 ? order.Count : targetIndex - removedBeforeTarget;
            insertion = insertion < 0 ? 0 : insertion > order.Count ? order.Count : insertion;

            order.InsertRange(insertion, group);
        }

        public void SortBySuit(int seat, IReadOnlyList<Card> hand)
        {
            Synchronize(seat, hand);
            GetOrCreateOrder(seat).Sort(CompareBySuit);
        }

        public void SortByRank(int seat, IReadOnlyList<Card> hand)
        {
            Synchronize(seat, hand);
            GetOrCreateOrder(seat).Sort(CompareByRank);
        }

        public void CopyCards(int seat, List<Card> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            var order = GetOrCreateOrder(seat);
            for (var index = 0; index < order.Count; index++)
            {
                destination.Add(Card.FromId(order[index]));
            }
        }

        private List<byte> GetOrCreateOrder(int seat)
        {
            if (!orders.TryGetValue(seat, out var order))
            {
                order = new List<byte>();
                orders.Add(seat, order);
            }

            return order;
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

        private static bool Contains(IReadOnlyList<byte> cardIds, byte cardId)
        {
            for (var index = 0; index < cardIds.Count; index++)
            {
                if (cardIds[index] == cardId)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareBySuit(byte leftId, byte rightId)
        {
            var left = Card.FromId(leftId);
            var right = Card.FromId(rightId);
            var result = left.Suit.CompareTo(right.Suit);
            if (result != 0)
            {
                return result;
            }

            result = left.Rank.CompareTo(right.Rank);
            return result != 0 ? result : left.Deck.CompareTo(right.Deck);
        }

        private static int CompareByRank(byte leftId, byte rightId)
        {
            var left = Card.FromId(leftId);
            var right = Card.FromId(rightId);
            var result = left.Rank.CompareTo(right.Rank);
            if (result != 0)
            {
                return result;
            }

            result = left.Suit.CompareTo(right.Suit);
            return result != 0 ? result : left.Deck.CompareTo(right.Deck);
        }
    }
}