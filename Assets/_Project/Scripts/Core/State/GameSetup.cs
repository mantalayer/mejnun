using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Mejnun.Core
{
    public sealed class GameSetup
    {
        private readonly ReadOnlyCollection<string> facePool;
        private readonly ReadOnlyCollection<string> deckPicks;
        private readonly ReadOnlyCollection<string> feltPool;
        private readonly ReadOnlyCollection<byte> shoeOrder;

        public GameSetup(int seatCount, ulong seed, int? dealer = null, int turnSeconds = 0,
            IReadOnlyList<string> facePool = null, IReadOnlyList<string> deckPicks = null,
            IReadOnlyList<string> feltPool = null, IReadOnlyList<byte> shoeOrder = null)
        {
            if (seatCount < 2 || seatCount > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(seatCount), seatCount,
                    "A game must have between 2 and 6 seats.");
            }

            if (dealer.HasValue && (dealer.Value < 0 || dealer.Value >= seatCount))
            {
                throw new ArgumentOutOfRangeException(nameof(dealer), dealer,
                    "The dealer must be a valid seat.");
            }

            if (turnSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(turnSeconds), turnSeconds,
                    "Turn seconds cannot be negative.");
            }

            var faces = new List<string>();
            if (facePool != null)
            {
                for (var index = 0; index < facePool.Count; index++)
                {
                    if (facePool[index] == null)
                    {
                        throw new ArgumentException("Face ids cannot be null.", nameof(facePool));
                    }

                    faces.Add(facePool[index]);
                }
            }

            List<string> picks = null;
            if (deckPicks != null)
            {
                if (deckPicks.Count != Card.DeckCount)
                {
                    throw new ArgumentException("Exactly two deck picks are required.", nameof(deckPicks));
                }

                picks = new List<string>(deckPicks.Count);
                for (var index = 0; index < deckPicks.Count; index++)
                {
                    var pick = deckPicks[index];
                    if (pick != null && string.IsNullOrWhiteSpace(pick))
                    {
                        throw new ArgumentException("Deck picks must be non-empty ids or null for Random.",
                            nameof(deckPicks));
                    }

                    picks.Add(pick);
                }
            }

            var felts = new List<string>();
            if (feltPool != null)
            {
                for (var index = 0; index < feltPool.Count; index++)
                {
                    if (string.IsNullOrWhiteSpace(feltPool[index]))
                    {
                        throw new ArgumentException("Felt ids cannot be null or empty.", nameof(feltPool));
                    }

                    felts.Add(feltPool[index]);
                }
            }

            List<byte> orderedCards = null;
            if (shoeOrder != null)
            {
                if (shoeOrder.Count != Card.CardCount)
                {
                    throw new ArgumentException("Exactly 104 card ids are required.", nameof(shoeOrder));
                }

                var seen = new bool[Card.CardCount];
                orderedCards = new List<byte>(shoeOrder.Count);
                for (var index = 0; index < shoeOrder.Count; index++)
                {
                    var cardId = shoeOrder[index];
                    if (cardId >= Card.CardCount || seen[cardId])
                    {
                        throw new ArgumentException("Shoe order must contain each card id from 0 to 103 exactly once.",
                            nameof(shoeOrder));
                    }

                    seen[cardId] = true;
                    orderedCards.Add(cardId);
                }
            }

            SeatCount = seatCount;
            Seed = seed;
            Dealer = dealer;
            TurnSeconds = turnSeconds;
            this.facePool = faces.AsReadOnly();
            this.deckPicks = picks?.AsReadOnly();
            this.feltPool = felts.AsReadOnly();
            this.shoeOrder = orderedCards?.AsReadOnly();
        }

        public int SeatCount { get; }
        public ulong Seed { get; }
        public int? Dealer { get; }
        public int TurnSeconds { get; }
        public IReadOnlyList<string> FacePool => facePool;
        public IReadOnlyList<string> DeckPicks => deckPicks;
        public IReadOnlyList<string> FeltPool => feltPool;
        public IReadOnlyList<byte> ShoeOrder => shoeOrder;
    }
}