using System;
using System.Collections.Generic;
using Mejnun.Cosmetics;

namespace Mejnun.Game
{
    public sealed class MatchRequest
    {
        private readonly SeatConfig[] seats;
        private readonly string[] deckPicks;
        private readonly string[] randomFacePool;
        private readonly string[] feltPool;
        private readonly byte[] shoeOrder;

        public MatchRequest(IReadOnlyList<SeatConfig> seats = null, int turnSeconds = 0,
            MatchMode mode = MatchMode.Solo, bool resume = false, IReadOnlyList<string> deckPicks = null,
            IReadOnlyList<string> randomFacePool = null, IReadOnlyList<string> feltPool = null, ulong? seed = null,
            IReadOnlyList<byte> shoeOrder = null, int? dealer = null)
        {
            if (turnSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(turnSeconds));
            }

            if (!Enum.IsDefined(typeof(MatchMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown match mode.");
            }

            this.seats = Copy(seats);
            this.deckPicks = Copy(deckPicks);
            this.randomFacePool = Copy(randomFacePool);
            this.feltPool = Copy(feltPool);
            this.shoeOrder = Copy(shoeOrder);
            Dealer = dealer;
            TurnSeconds = turnSeconds;
            Mode = mode;
            Resume = resume;
            Seed = seed;
        }

        public IReadOnlyList<SeatConfig> Seats => seats;
        public int TurnSeconds { get; }
        public MatchMode Mode { get; }
        public bool Resume { get; }
        public IReadOnlyList<string> DeckPicks => deckPicks;
        public IReadOnlyList<string> RandomFacePool => randomFacePool;
        public IReadOnlyList<string> FeltPool => feltPool;
        public ulong? Seed { get; }

        /// <summary>An exact 104-card deal order (the tutorial); null shuffles from the seed.</summary>
        public IReadOnlyList<byte> ShoeOrder => shoeOrder;

        /// <summary>A fixed dealer, which skips the "who goes first" question; null asks.</summary>
        public int? Dealer { get; }

        public bool IsTutorial => Mode == MatchMode.Tutorial;

        private static T[] Copy<T>(IReadOnlyList<T> values)
        {
            if (values == null)
            {
                return null;
            }

            var copy = new T[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                copy[index] = values[index];
            }

            return copy;
        }
    }
}