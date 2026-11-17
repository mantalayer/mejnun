using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Mejnun.Core
{
    public sealed class GameSetup
    {
        private readonly ReadOnlyCollection<string> facePool;

        public GameSetup(int seatCount, ulong seed, int? dealer = null, int turnSeconds = 0,
            IReadOnlyList<string> facePool = null)
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

            SeatCount = seatCount;
            Seed = seed;
            Dealer = dealer;
            TurnSeconds = turnSeconds;
            this.facePool = faces.AsReadOnly();
        }

        public int SeatCount { get; }
        public ulong Seed { get; }
        public int? Dealer { get; }
        public int TurnSeconds { get; }
        public IReadOnlyList<string> FacePool => facePool;
    }
}