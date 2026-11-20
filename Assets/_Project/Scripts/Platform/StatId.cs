namespace Mejnun.Platform
{
    // Store-neutral stat IDs, mapped to per-store names in PlatformSettings.
    // Values are serialized: append new entries with explicit values and never
    // renumber or reuse one.
    public enum StatId
    {
        None = 0,
        GamesPlayed = 1,
        GamesWon = 2,
    }
}
