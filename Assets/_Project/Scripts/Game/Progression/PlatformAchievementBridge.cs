using System;
using System.Collections.Generic;
using Mejnun.Cosmetics;
using Mejnun.Platform;
using UnityEngine;

namespace Mejnun.Game
{
    public static class PlatformAchievementBridge
    {
        private static bool s_ownedAchievementsSynced;

        public static bool TryMapAchievement(string achievementId, out AchievementId platformId)
        {
            switch (achievementId)
            {
                case AchievementIds.FirstGame:
                    platformId = AchievementId.FirstGame;
                    return true;
                case AchievementIds.FirstWin:
                    platformId = AchievementId.FirstWin;
                    return true;
                case AchievementIds.FirstMejnun:
                    platformId = AchievementId.FirstMejnun;
                    return true;
                case AchievementIds.TenWins:
                    platformId = AchievementId.TenWins;
                    return true;
                case AchievementIds.BeatHardBot:
                    platformId = AchievementId.BeatHardBot;
                    return true;
                case AchievementIds.FullTable:
                    platformId = AchievementId.FullTable;
                    return true;
                case AchievementIds.Regular: platformId = AchievementId.Regular; return true;
                case AchievementIds.Veteran: platformId = AchievementId.Veteran; return true;
                case AchievementIds.Seasoned: platformId = AchievementId.Seasoned; return true;
                case AchievementIds.MasterOfMejnun: platformId = AchievementId.MasterOfMejnun; return true;
                case AchievementIds.LevelTen: platformId = AchievementId.LevelTen; return true;
                case AchievementIds.LevelTwentyFive: platformId = AchievementId.LevelTwentyFive; return true;
                case AchievementIds.LevelFifty: platformId = AchievementId.LevelFifty; return true;
                case AchievementIds.Collector: platformId = AchievementId.Collector; return true;
                case AchievementIds.MejnunTen: platformId = AchievementId.MejnunTen; return true;
                case AchievementIds.CleanSweep: platformId = AchievementId.CleanSweep; return true;
                case AchievementIds.Lightning: platformId = AchievementId.Lightning; return true;
                case AchievementIds.LastGasp: platformId = AchievementId.LastGasp; return true;
                case AchievementIds.Comeback: platformId = AchievementId.Comeback; return true;
                case AchievementIds.DownToTheWire: platformId = AchievementId.DownToTheWire; return true;
                case AchievementIds.RoyalRoad: platformId = AchievementId.RoyalRoad; return true;
                case AchievementIds.AceHigh: platformId = AchievementId.AceHigh; return true;
                case AchievementIds.BigEntrance: platformId = AchievementId.BigEntrance; return true;
                case AchievementIds.Architect: platformId = AchievementId.Architect; return true;
                case AchievementIds.Surgeon: platformId = AchievementId.Surgeon; return true;
                case AchievementIds.BorrowedTime: platformId = AchievementId.BorrowedTime; return true;
                case AchievementIds.SeeingDouble: platformId = AchievementId.SeeingDouble; return true;
                case AchievementIds.Patient: platformId = AchievementId.Patient; return true;
                case AchievementIds.NoTrainingWheels: platformId = AchievementId.NoTrainingWheels; return true;
                case AchievementIds.Halas: platformId = AchievementId.Halas; return true;
                case AchievementIds.CoolUnderPressure: platformId = AchievementId.CoolUnderPressure; return true;
                case AchievementIds.SpeedDemon: platformId = AchievementId.SpeedDemon; return true;
                case AchievementIds.GiantSlayer: platformId = AchievementId.GiantSlayer; return true;
                case AchievementIds.HatTrick: platformId = AchievementId.HatTrick; return true;
                case AchievementIds.Unstoppable: platformId = AchievementId.Unstoppable; return true;
                case AchievementIds.GoingOnline: platformId = AchievementId.GoingOnline; return true;
                case AchievementIds.CrowdPleaser: platformId = AchievementId.CrowdPleaser; return true;
                case AchievementIds.PartyHost: platformId = AchievementId.PartyHost; return true;
                case AchievementIds.PassItOn: platformId = AchievementId.PassItOn; return true;
                case AchievementIds.EvenSteven: platformId = AchievementId.EvenSteven; return true;
                case AchievementIds.HotSeat: platformId = AchievementId.HotSeat; return true;
                default:
                    platformId = AchievementId.None;
                    return false;
            }
        }

        public static void UnlockAchievements(IEnumerable<string> achievementIds)
        {
            IPlatformServices services = GetInitializedServices();
            if (services?.Achievements == null || achievementIds == null)
            {
                return;
            }

            foreach (string achievementId in achievementIds)
            {
                if (TryMapAchievement(achievementId, out AchievementId platformId))
                {
                    services.Achievements.Unlock(platformId);
                }
            }
        }

        public static void SyncOwnedAchievements(IEnumerable<string> achievementIds)
        {
            if (s_ownedAchievementsSynced || achievementIds == null)
            {
                return;
            }

            IPlatformServices services = GetInitializedServices();
            if (services?.Achievements == null)
            {
                return;
            }

            UnlockAchievements(achievementIds);
            s_ownedAchievementsSynced = true;
        }

        public static void RecordMatch(MatchSummary summary, IReadOnlyList<string> newlyEarnedAchievements)
        {
            IPlatformServices services = GetInitializedServices();
            if (services == null)
            {
                return;
            }

            IStats stats = services.Stats;
            if (stats != null)
            {
                var details = summary.Details;
                Increment(stats, StatId.GamesPlayed, 1);
                if (summary.CanAwardWin && summary.LocalWin && !summary.Stalemate)
                {
                    Increment(stats, StatId.GamesWon, 1);
                    if (summary.MejnunWin)
                    {
                        Increment(stats, StatId.MejnunWins, 1);
                    }

                    if (details.CountWin)
                    {
                        Increment(stats, StatId.CountWins, 1);
                    }

                    if (summary.Mode == MatchMode.Online)
                    {
                        Increment(stats, StatId.OnlineWins, 1);
                    }

                    if (summary.Mode == MatchMode.Solo)
                    {
                        switch (summary.CpuDifficulty)
                        {
                            case 0: Increment(stats, StatId.WinsVsEasy, 1); break;
                            case 1: Increment(stats, StatId.WinsVsMedium, 1); break;
                            default: Increment(stats, StatId.WinsVsHard, 1); break;
                        }
                    }
                }

                if (summary.Stalemate)
                {
                    Increment(stats, StatId.Stalemates, 1);
                }

                switch (summary.Mode)
                {
                    case MatchMode.Solo: Increment(stats, StatId.GamesPlayedSolo, 1); break;
                    case MatchMode.Online: Increment(stats, StatId.GamesPlayedOnline, 1); break;
                    case MatchMode.PassAndPlay: Increment(stats, StatId.GamesPlayedPassAndPlay, 1); break;
                }

                Increment(stats, StatId.CardsPlayedFromHand, details.CardsPlayedFromHand);
                Increment(stats, StatId.SetsCreated, details.SetsCreated);
                Increment(stats, StatId.StraightsCreated, details.StraightsCreated);
                Increment(stats, StatId.SameKindsCreated, details.SameKindsCreated);
                SetMax(stats, StatId.LongestStraight, details.LongestStraight);
                SetMax(stats, StatId.MostCardsInOneTurn, details.MostCardsInOneTurn);
                var priorGoDowns = stats.GetInt(StatId.GoDowns);
                var priorAverage = stats.GetInt(StatId.AvgTurnsToGoDown);
                if (details.GoDowns > 0)
                {
                    var totalTurns = (long)priorAverage * priorGoDowns +
                                     (long)details.TurnsToGoDown * details.GoDowns;
                    var totalGoDowns = (long)priorGoDowns + details.GoDowns;
                    var average = (int)Math.Min(int.MaxValue, (totalTurns + totalGoDowns / 2) / totalGoDowns);
                    stats.IncrementInt(StatId.GoDowns, details.GoDowns);
                    stats.SetInt(StatId.AvgTurnsToGoDown, average);
                }

                Increment(stats, StatId.TurnsPlayed, details.TurnsPlayed);
                Increment(stats, StatId.Draws, details.Draws);
                Increment(stats, StatId.Passes, details.Passes);
                Increment(stats, StatId.Timeouts, details.Timeouts);
                Increment(stats, StatId.UndosUsed, details.UndosUsed);
                Increment(stats, StatId.RevertsUsed, details.RevertsUsed);
                Increment(stats, StatId.FixBoardsUsed, details.FixBoardsUsed);
                Increment(stats, StatId.TotalPlaySeconds, summary.DurationSeconds);

                if (summary.CanAwardWin && summary.LocalWin && !summary.Stalemate)
                {
                    SetMin(stats, StatId.FastestWinTurns, details.FastestWinTurns);
                    SetMin(stats, StatId.FastestWinSeconds,
                        details.FastestWinSeconds > 0 ? details.FastestWinSeconds : summary.DurationSeconds);
                    SetMax(stats, StatId.LargestHandAtWin, details.LargestHand);
                }

                stats.SetInt(StatId.CurrentWinStreak, details.CurrentWinStreak);
                SetMax(stats, StatId.BestWinStreak, details.BestWinStreak);
                if (summary.Mode == MatchMode.Online && details.IsHost)
                {
                    Increment(stats, StatId.OnlineGamesHosted, 1);
                }

                if (details.ChosenFirstByQuestion)
                {
                    Increment(stats, StatId.HotSeatQuestions, 1);
                }
            }

            UpdateAchievementProgress(stats, services.Achievements);
            UnlockAchievements(newlyEarnedAchievements);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            s_ownedAchievementsSynced = false;
        }

        private static IPlatformServices GetInitializedServices()
        {
            if (!PlatformServices.IsInitialized)
            {
                return null;
            }

            IPlatformServices services = PlatformServices.Current;
            return services != null && services.IsInitialized ? services : null;
        }

        private static void Increment(IStats stats, StatId id, int amount)
        {
            if (amount > 0)
            {
                stats.IncrementInt(id, amount);
            }
        }

        private static void SetMax(IStats stats, StatId id, int value)
        {
            if (value > stats.GetInt(id))
            {
                stats.SetInt(id, value);
            }
        }

        private static void SetMin(IStats stats, StatId id, int value)
        {
            if (value > 0 && (stats.GetInt(id) == 0 || value < stats.GetInt(id)))
            {
                stats.SetInt(id, value);
            }
        }

        private static void UpdateAchievementProgress(IStats stats, IAchievements achievements)
        {
            if (stats == null || achievements == null)
            {
                return;
            }

            achievements.SetProgress(AchievementId.TenWins, stats.GetInt(StatId.GamesWon), 10);
            achievements.SetProgress(AchievementId.Regular, stats.GetInt(StatId.GamesPlayed), 50);
            achievements.SetProgress(AchievementId.Veteran, stats.GetInt(StatId.GamesPlayed), 250);
            achievements.SetProgress(AchievementId.Seasoned, stats.GetInt(StatId.GamesWon), 50);
            achievements.SetProgress(AchievementId.MasterOfMejnun, stats.GetInt(StatId.GamesWon), 200);
            achievements.SetProgress(AchievementId.MejnunTen, stats.GetInt(StatId.MejnunWins), 10);
            achievements.SetProgress(AchievementId.Unstoppable, stats.GetInt(StatId.BestWinStreak), 5);
            achievements.SetProgress(AchievementId.PartyHost, stats.GetInt(StatId.OnlineGamesHosted), 10);
            achievements.SetProgress(AchievementId.HotSeat, stats.GetInt(StatId.HotSeatQuestions), 10);
        }
    }
}