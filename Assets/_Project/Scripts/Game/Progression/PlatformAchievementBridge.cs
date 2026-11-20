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
            if (services?.Stats == null)
            {
                return;
            }

            services.Stats.IncrementInt(StatId.GamesPlayed, 1);
            if (summary.CanAwardWin && summary.LocalWin)
            {
                services.Stats.IncrementInt(StatId.GamesWon, 1);
            }

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
    }
}