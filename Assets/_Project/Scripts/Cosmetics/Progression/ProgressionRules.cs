using System;

namespace Mejnun.Cosmetics
{
    public static class ProgressionRules
    {
        public static int XpFor(MatchSummary summary)
        {
            var baseXp = summary.CanAwardWin && summary.LocalWin && !summary.Stalemate
                ? 60 + (summary.MejnunWin ? 20 : 0)
                : summary.Stalemate ? 15 : 10;
            var difficultyMultiplier = summary.CpuDifficulty == 0 ? 1.0 :
                summary.CpuDifficulty == 1 ? 1.4 : 1.8;
            var duration = summary.DurationSeconds;
            var speedMultiplier = duration <= 300
                ? 1.5
                : duration >= 1800
                    ? 0.75
                    : 1.5 - (duration - 300) * 0.75 / 1500.0;
            return (int)Math.Round(baseXp * difficultyMultiplier * speedMultiplier,
                MidpointRounding.AwayFromZero);
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