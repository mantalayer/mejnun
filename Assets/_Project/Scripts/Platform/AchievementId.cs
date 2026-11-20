namespace Mejnun.Platform
{
    // Store-neutral achievement IDs, mapped to per-store names in
    // PlatformSettings. Values are serialized: append new entries with explicit
    // values and never renumber or reuse one.
    public enum AchievementId
    {
        None = 0,
        FirstGame = 1,
        FirstWin = 2,
        FirstMejnun = 3,
        TenWins = 4,
        BeatHardBot = 5,
        FullTable = 6,
    }
}
