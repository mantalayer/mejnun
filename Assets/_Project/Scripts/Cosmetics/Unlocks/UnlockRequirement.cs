using System;
using UnityEngine;

namespace Mejnun.Cosmetics
{
    public enum UnlockKind
    {
        Free,
        Level,
        Wins,
        Achievement,
        Purchase
    }

    [Serializable]
    public sealed class UnlockRequirement
    {
        public static readonly UnlockRequirement Free = new UnlockRequirement(UnlockKind.Free);

        [SerializeField] private UnlockKind kind;
        [SerializeField] private int target;
        [SerializeField] private string achievementId;

        public UnlockRequirement()
            : this(UnlockKind.Free)
        {
        }

        public UnlockRequirement(UnlockKind kind, int target = 0, string achievementId = null)
        {
            if (kind == UnlockKind.Level || kind == UnlockKind.Wins)
            {
                if (target < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(target), target,
                        "Countable unlock targets must be at least one.");
                }
            }

            if (kind == UnlockKind.Achievement && string.IsNullOrWhiteSpace(achievementId))
            {
                throw new ArgumentException("Achievement unlocks require an achievement id.", nameof(achievementId));
            }

            if (!Enum.IsDefined(typeof(UnlockKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown unlock kind.");
            }

            this.kind = kind;
            this.target = target;
            this.achievementId = achievementId;
        }

        public UnlockKind Kind => kind;
        public int Target => target;
        public string AchievementId => achievementId;

        public UnlockProgress Evaluate(PlayerProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            switch (kind)
            {
                case UnlockKind.Free:
                    return new UnlockProgress(true, 1, 1, false);
                case UnlockKind.Level:
                    var level = profile.Level;
                    return new UnlockProgress(level >= target, level, target, true);
                case UnlockKind.Wins:
                    var wins = profile.Stats.Wins;
                    return new UnlockProgress(wins >= target, wins, target, true);
                case UnlockKind.Achievement:
                    var hasAchievement = profile.HasAchievement(achievementId);
                    return new UnlockProgress(hasAchievement, hasAchievement ? 1 : 0, 1, true);
                case UnlockKind.Purchase:
                    return new UnlockProgress(false, 0, 0, false);
                default:
                    throw new InvalidOperationException("Unknown unlock kind.");
            }
        }

        public string Describe(UnlockProgress progress)
        {
            switch (kind)
            {
                case UnlockKind.Free:
                    return "Available now";
                case UnlockKind.Level:
                    return "Reach level " + target;
                case UnlockKind.Wins:
                    return "Win " + target + (target == 1 ? " game" : " games");
                case UnlockKind.Achievement:
                    return "Earn " + AchievementIds.DisplayName(achievementId);
                case UnlockKind.Purchase:
                    return "Purchase required";
                default:
                    return string.Empty;
            }
        }
    }
}