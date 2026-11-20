using System.Collections.Generic;

namespace Mejnun.Cosmetics
{
    public static class AchievementTracker
    {
        public static IReadOnlyList<string> Evaluate(PlayerProfile profile, MatchSummary summary,
            bool collectorUnlocked = false)
        {
            var details = summary.Details;
            var eligibleWin = summary.CanAwardWin && summary.LocalWin && !summary.Stalemate;
            var newlyEarned = new List<string>();
            AddIfEarned(profile, newlyEarned, AchievementIds.FirstGame, profile.Stats.Played >= 1);
            AddIfEarned(profile, newlyEarned, AchievementIds.FirstWin, profile.Stats.Wins >= 1);
            AddIfEarned(profile, newlyEarned, AchievementIds.FirstMejnun, profile.Stats.MejnunWins >= 1);
            AddIfEarned(profile, newlyEarned, AchievementIds.TenWins, profile.Stats.Wins >= 10);
            AddIfEarned(profile, newlyEarned, AchievementIds.BeatHardBot, summary.BeatHardBot && eligibleWin);
            AddIfEarned(profile, newlyEarned, AchievementIds.FullTable, summary.SeatCount == 6);
            AddIfEarned(profile, newlyEarned, AchievementIds.Regular, profile.Stats.Played >= 50);
            AddIfEarned(profile, newlyEarned, AchievementIds.Veteran, profile.Stats.Played >= 250);
            AddIfEarned(profile, newlyEarned, AchievementIds.Seasoned, profile.Stats.Wins >= 50);
            AddIfEarned(profile, newlyEarned, AchievementIds.MasterOfMejnun, profile.Stats.Wins >= 200);
            AddIfEarned(profile, newlyEarned, AchievementIds.LevelTen, profile.Level >= 10);
            AddIfEarned(profile, newlyEarned, AchievementIds.LevelTwentyFive, profile.Level >= 25);
            AddIfEarned(profile, newlyEarned, AchievementIds.LevelFifty, profile.Level >= 50);
            AddIfEarned(profile, newlyEarned, AchievementIds.Collector, collectorUnlocked);
            AddIfEarned(profile, newlyEarned, AchievementIds.MejnunTen, profile.Stats.MejnunWins >= 10);
            AddIfEarned(profile, newlyEarned, AchievementIds.CleanSweep,
                eligibleWin && details.CleanSweep && !details.HintsUsed && details.FixBoardsUsed == 0);
            AddIfEarned(profile, newlyEarned, AchievementIds.Lightning,
                eligibleWin && details.LocalTurns > 0 && details.LocalTurns <= 5);
            AddIfEarned(profile, newlyEarned, AchievementIds.LastGasp, eligibleWin && details.LastGasp);
            AddIfEarned(profile, newlyEarned, AchievementIds.Comeback,
                eligibleWin && details.LargestHand >= 20);
            AddIfEarned(profile, newlyEarned, AchievementIds.DownToTheWire,
                eligibleWin && details.CountWin && details.WinMargin == 1);
            AddIfEarned(profile, newlyEarned, AchievementIds.RoyalRoad, details.RoyalRoad);
            AddIfEarned(profile, newlyEarned, AchievementIds.AceHigh, details.AceHigh);
            AddIfEarned(profile, newlyEarned, AchievementIds.BigEntrance, details.BigEntrance);
            AddIfEarned(profile, newlyEarned, AchievementIds.Architect, details.Architect);
            AddIfEarned(profile, newlyEarned, AchievementIds.Surgeon, details.Surgeon);
            AddIfEarned(profile, newlyEarned, AchievementIds.BorrowedTime, details.BorrowedTime);
            AddIfEarned(profile, newlyEarned, AchievementIds.SeeingDouble, details.SeeingDouble);
            AddIfEarned(profile, newlyEarned, AchievementIds.Patient,
                eligibleWin && !details.HintsUsed && details.UndosUsed == 0 &&
                details.RevertsUsed == 0 && details.FixBoardsUsed == 0);
            AddIfEarned(profile, newlyEarned, AchievementIds.NoTrainingWheels,
                eligibleWin && !details.HintsUsed);
            AddIfEarned(profile, newlyEarned, AchievementIds.Halas, details.Timeouts > 0);
            AddIfEarned(profile, newlyEarned, AchievementIds.CoolUnderPressure, details.CoolUnderPressure);
            AddIfEarned(profile, newlyEarned, AchievementIds.SpeedDemon,
                eligibleWin && details.TurnSeconds == 30);
            AddIfEarned(profile, newlyEarned, AchievementIds.GiantSlayer,
                eligibleWin && summary.SeatCount == 6 && details.AllBotsHard);
            AddIfEarned(profile, newlyEarned, AchievementIds.HatTrick, profile.Stats.BestBotWinStreak >= 3);
            AddIfEarned(profile, newlyEarned, AchievementIds.Unstoppable, profile.Stats.BestWinStreak >= 5);
            AddIfEarned(profile, newlyEarned, AchievementIds.GoingOnline, summary.Mode == MatchMode.Online);
            AddIfEarned(profile, newlyEarned, AchievementIds.CrowdPleaser,
                summary.Mode == MatchMode.Online && eligibleWin && summary.HumanCount >= 4);
            AddIfEarned(profile, newlyEarned, AchievementIds.PartyHost, profile.Stats.OnlineGamesHosted >= 10);
            AddIfEarned(profile, newlyEarned, AchievementIds.PassItOn,
                summary.Mode == MatchMode.PassAndPlay && summary.HumanCount >= 4);
            AddIfEarned(profile, newlyEarned, AchievementIds.EvenSteven, summary.Stalemate);
            AddIfEarned(profile, newlyEarned, AchievementIds.HotSeat, profile.Stats.HotSeatQuestionCount >= 10);
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