namespace Mejnun.Core
{
    public sealed class GameResult
    {
        public GameResult(GameEndKind kind, int? winner, int? nextDealer)
        {
            Kind = kind;
            Winner = winner;
            NextDealer = nextDealer;
        }

        public GameEndKind Kind { get; }
        public int? Winner { get; }
        public int? NextDealer { get; }
    }
}