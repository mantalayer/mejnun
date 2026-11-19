using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Mejnun.Core
{
    public sealed class Shoe
    {
        private readonly Card[] cards;
        private readonly ReadOnlyCollection<Card> readOnlyCards;

        public Shoe() : this(null)
        {
        }

        public Shoe(IReadOnlyList<byte> cardOrder)
        {
            cards = new Card[Card.CardCount];
            if (cardOrder != null && cardOrder.Count != Card.CardCount)
            {
                throw new ArgumentException("Exactly 104 card ids are required.", nameof(cardOrder));
            }

            var seen = new bool[Card.CardCount];
            for (var index = 0; index < cards.Length; index++)
            {
                var cardId = cardOrder == null ? index : cardOrder[index];
                if (cardId >= Card.CardCount || seen[cardId])
                {
                    throw new ArgumentException("Card order must contain each card id from 0 to 103 exactly once.",
                        nameof(cardOrder));
                }

                seen[cardId] = true;
                cards[index] = Card.FromId(cardId);
            }

            readOnlyCards = Array.AsReadOnly(cards);
        }

        public int Count => cards.Length;
        public IReadOnlyList<Card> Cards => readOnlyCards;
        public Card this[int index] => cards[index];

        public void Shuffle(Pcg32 rng)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            for (var index = cards.Length - 1; index > 0; index--)
            {
                var otherIndex = rng.NextInt(index + 1);
                var card = cards[index];
                cards[index] = cards[otherIndex];
                cards[otherIndex] = card;
            }
        }
    }
}