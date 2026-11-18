using System;

namespace Mejnun.Cosmetics
{
    public static class ProgressionRules
    {
        public static int XpFor(MatchSummary summary)
        {
            var xp = 20;
            if (!summary.CanAwardWin)
            {
                return xp;
            }

            if (summary.LocalWin)
            {
                xp += 40;
                if (summary.MejnunWin)
                {
                    xp += 20;
                }
            }

            if (summary.Stalemate)
            {
                xp += 10;
            }

            return xp;
        }

        public static int LevelForXp(int xp)
        {
            return GetLevelProgress(xp, out _, out _);
        }

        public static (int into, int needed) LevelProgress(int xp)
        {
            GetLevelProgress(xp, out var into, out var needed);
            return (into, needed);
        }

        public static int XpToNextLevel(int level)
        {
            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Player levels start at one.");
            }

            return 100 + 25 * (level - 1);
        }

        private static int GetLevelProgress(int xp, out int into, out int needed)
        {
            var remaining = Math.Max(0, xp);
            var level = 1;
            needed = XpToNextLevel(level);
            while (remaining >= needed)
            {
                remaining -= needed;
                level++;
                needed = XpToNextLevel(level);
            }

            into = remaining;
            return level;
        }
    }
}