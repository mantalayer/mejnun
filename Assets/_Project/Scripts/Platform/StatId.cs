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
        MejnunWins = 3,
        CountWins = 4,
        Stalemates = 5,
        GamesPlayedSolo = 6,
        GamesPlayedOnline = 7,
        GamesPlayedPassAndPlay = 8,
        WinsVsEasy = 9,
        WinsVsMedium = 10,
        WinsVsHard = 11,
        OnlineWins = 12,
        CurrentWinStreak = 13,
        BestWinStreak = 14,
        CardsPlayedFromHand = 15,
        SetsCreated = 16,
        StraightsCreated = 17,
        SameKindsCreated = 18,
        LongestStraight = 19,
        MostCardsInOneTurn = 20,
        GoDowns = 21,
        AvgTurnsToGoDown = 22,
        TurnsPlayed = 23,
        Draws = 24,
        Passes = 25,
        Timeouts = 26,
        UndosUsed = 27,
        RevertsUsed = 28,
        FixBoardsUsed = 29,
        FastestWinTurns = 30,
        FastestWinSeconds = 31,
        LargestHandAtWin = 32,
        TotalPlaySeconds = 33,
        OnlineGamesHosted = 34,
        HotSeatQuestions = 35,
    }
}
