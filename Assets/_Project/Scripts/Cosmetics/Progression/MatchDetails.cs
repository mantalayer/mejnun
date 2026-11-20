namespace Mejnun.Cosmetics
{
    public sealed class MatchDetails
    {
        public int TurnsPlayed { get; set; }
        public int Draws { get; set; }
        public int Passes { get; set; }
        public int Timeouts { get; set; }
        public int UndosUsed { get; set; }
        public int RevertsUsed { get; set; }
        public int FixBoardsUsed { get; set; }
        public int CardsPlayedFromHand { get; set; }
        public int SetsCreated { get; set; }
        public int StraightsCreated { get; set; }
        public int SameKindsCreated { get; set; }
        public int LongestStraight { get; set; }
        public int MostCardsInOneTurn { get; set; }
        public int GoDowns { get; set; }
        public int TurnsToGoDown { get; set; }
        public int LargestHand { get; set; }
        public int FastestWinTurns { get; set; }
        public int FastestWinSeconds { get; set; }
        public int CurrentWinStreak { get; set; }
        public int BestWinStreak { get; set; }
        public int WinMargin { get; set; }
        public bool CountWin { get; set; }
        public int TurnSeconds { get; set; }
        public int LocalTurns { get; set; }
        public int SetsAtGoDown { get; set; }
        public bool CleanSweep { get; set; }
        public bool LastGasp { get; set; }
        public bool RoyalRoad { get; set; }
        public bool AceHigh { get; set; }
        public bool BigEntrance { get; set; }
        public bool Architect { get; set; }
        public bool Surgeon { get; set; }
        public bool BorrowedTime { get; set; }
        public bool SeeingDouble { get; set; }
        public bool CoolUnderPressure { get; set; }
        public bool HintsUsed { get; set; }
        public bool IsHost { get; set; }
        public bool ChosenFirstByQuestion { get; set; }
        public bool AllBotsHard { get; set; }
    }
}