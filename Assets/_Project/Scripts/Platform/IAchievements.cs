namespace Mejnun.Platform
{
    public interface IAchievements
    {
        void Unlock(AchievementId id);

        // Reports progress toward a counted achievement; unlocks it once
        // current reaches max.
        void SetProgress(AchievementId id, int current, int max);

        bool IsUnlocked(AchievementId id);
    }
}
