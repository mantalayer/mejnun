using System;

namespace Mejnun.Core
{
    public sealed class Pcg32
    {
        private const ulong DefaultIncrement = 1442695040888963407UL;
        private ulong state;

        public Pcg32(ulong seed)
        {
            Increment = DefaultIncrement;
            state = 0;
            NextUInt();
            state = unchecked(state + seed);
            NextUInt();
        }

        private Pcg32(ulong state, ulong increment)
        {
            if ((increment & 1UL) == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(increment), "PCG stream increments must be odd.");
            }

            this.state = state;
            Increment = increment;
        }

        public ulong State => state;
        public ulong Increment { get; }

        public static Pcg32 FromState(ulong state, ulong increment)
        {
            return new Pcg32(state, increment);
        }

        public uint NextUInt()
        {
            unchecked
            {
                var oldState = state;
                state = oldState * 6364136223846793005UL + Increment;
                var xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
                var rotation = (int)(oldState >> 59);
                return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
            }
        }

        public int NextInt(int exclusiveUpperBound)
        {
            if (exclusiveUpperBound <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveUpperBound), exclusiveUpperBound,
                    "The upper bound must be positive.");
            }

            var bound = (uint)exclusiveUpperBound;
            var threshold = unchecked(0u - bound) % bound;
            while (true)
            {
                var value = NextUInt();
                if (value >= threshold)
                {
                    return (int)(value % bound);
                }
            }
        }
    }
}