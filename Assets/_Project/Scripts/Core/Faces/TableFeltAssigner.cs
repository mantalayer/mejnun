using System;
using System.Collections.Generic;

namespace Mejnun.Core
{
    public static class TableFeltAssigner
    {
        public const string GreenFelt = "green";

        public static string Assign(IReadOnlyList<string> feltPool, Pcg32 rng)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            if (feltPool == null || feltPool.Count == 0)
            {
                return GreenFelt;
            }

            for (var index = 0; index < feltPool.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(feltPool[index]))
                {
                    throw new ArgumentException("Felt ids cannot be null or empty.", nameof(feltPool));
                }
            }

            return feltPool[rng.NextInt(feltPool.Count)];
        }
    }
}