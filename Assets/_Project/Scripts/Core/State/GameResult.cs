namespace Mejnun.Core
{
    public sealed class GameResult
    {
        public GameResult(GameEndKind kind, int? winner, int? nextFirstPlayer)
        {
            Kind = kind;
            Winner = winner;
            NextFirstPlayer = nextFirstPlayer;
        }

        public GameEndKind Kind { get; }
        public int? Winner { get; }

        /// <summary>The seat that takes the first turn next game (the winner), or null to ask "Who goes first?".</summary>
        public int? NextFirstPlayer { get; }
    }
}
