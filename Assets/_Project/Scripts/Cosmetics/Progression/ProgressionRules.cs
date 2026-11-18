using System;

namespace Mejnun.Cosmetics
{
    public static class ProgressionRules
    {
        public static int XpFor(MatchSummary summary)
        {
            var baseXp = summary.CanAwardWin && summary.LocalWin
                ? 60 + (summary.MejnunWin ? 20 : 0)
                : summary.Stalemate ? 15 : 10;
            var difficultyMultiplier = summary.CpuDifficulty == 0 ? 1f :
                summary.CpuDifficulty == 1 ? 1.4f : 1.8f;
            var duration = summary.DurationSeconds;
            var speedMultiplier = duration <= 300
                ? 1.5f
                : duration >= 1800
                    ? 0.75f
                    : 1.5f - (duration - 300) * 0.75f / 1500f;
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