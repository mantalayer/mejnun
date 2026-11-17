namespace Mejnun.Core
{
    public enum CommandType : byte
    {
        Invalid,
        ChooseDealer,
        ChooseRandomDealer,
        MoveCards,
        SplitMeld,
        MergeMelds,
        GoDown,
        Undo,
        Revert,
        EndTurn,
        DrawOrPass,
        TimeoutTurn
    }
}