using System.Collections.Generic;

namespace Mejnun.Cosmetics
{
    public static class AchievementTracker
    {
        public static IReadOnlyList<string> Evaluate(PlayerProfile profile, MatchSummary summary)
        {
            var newlyEarned = new List<string>();
            AddIfEarned(profile, newlyEarned, AchievementIds.FirstGame, profile.Stats.Played >= 1);
            AddIfEarned(profile, newlyEarned, AchievementIds.FirstWin, profile.Stats.Wins >= 1);
            AddIfEarned(profile, newlyEarned, AchievementIds.FirstMejnun, profile.Stats.MejnunWins >= 1);
            AddIfEarned(profile, newlyEarned, AchievementIds.TenWins, profile.Stats.Wins >= 10);
            AddIfEarned(profile, newlyEarned, AchievementIds.BeatHardBot, summary.BeatHardBot);
            AddIfEarned(profile, newlyEarned, AchievementIds.FullTable, summary.SeatCount == 6);
            return newlyEarned.AsReadOnly();
        }

        private static void AddIfEarned(PlayerProfile profile, List<string> newlyEarned, string achievementId,
            bool condition)
        {
            if (condition && !profile.HasAchievement(achievementId))
            {
                newlyEarned.Add(achievementId);
            }
        }
    }
}