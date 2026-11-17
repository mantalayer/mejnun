using System;

namespace Mejnun.Core
{
    public readonly struct Card : IEquatable<Card>
    {
        public const int CardsPerDeck = 52;
        public const int DeckCount = 2;
        public const int CardCount = CardsPerDeck * DeckCount;

        private Card(byte id)
        {
            Id = id;
            Deck = (byte)(id / CardsPerDeck);
            var deckOffset = id % CardsPerDeck;
            Suit = (Suit)(deckOffset / 13);
            Rank = (Rank)(deckOffset % 13);
        }

        public byte Id { get; }
        public Suit Suit { get; }
        public Rank Rank { get; }
        public byte Deck { get; }

        public static Card FromId(int id)
        {
            if (id < 0 || id >= CardCount)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Card id must be between 0 and 103.");
            }

            return new Card((byte)id);
        }

        public bool IsDoubleOf(Card other)
        {
            return Suit == other.Suit && Rank == other.Rank && Deck != other.Deck;
        }

        public bool Equals(Card other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            return obj is Card other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public static bool operator ==(Card left, Card right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Card left, Card right)
        {
            return !left.Equals(right);
        }
    }
}