using System.Collections.Generic;

namespace Mejnun.Cosmetics
{
    public sealed class ProgressionReport
    {
        public ProgressionReport(int xpGained, int levelBefore, int levelAfter,
            IReadOnlyList<string> newAchievements, IReadOnlyList<string> newlyUnlockedItems)
        {
            XpGained = xpGained;
            LevelBefore = levelBefore;
            LevelAfter = levelAfter;
            NewAchievements = newAchievements ?? new string[0];
            NewlyUnlockedItems = newlyUnlockedItems ?? new string[0];
        }

        public int XpGained { get; }
        public int LevelBefore { get; }
        public int LevelAfter { get; }
        public IReadOnlyList<string> NewAchievements { get; }
        public IReadOnlyList<string> NewlyUnlockedItems { get; }
    }
}