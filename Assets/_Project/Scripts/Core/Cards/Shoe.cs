using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Mejnun.Core
{
    public sealed class Shoe
    {
        private readonly Card[] cards;
        private readonly ReadOnlyCollection<Card> readOnlyCards;

        public Shoe()
        {
            cards = new Card[Card.CardCount];
            for (var id = 0; id < cards.Length; id++)
            {
                cards[id] = Card.FromId(id);
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