using Mejnun.AI;

namespace Mejnun.Game
{
    /// <summary>Who controls a seat: a local human or a bot of a given difficulty.</summary>
    public readonly struct SeatConfig
    {
        public SeatConfig(string name, bool isBot, BotDifficulty difficulty)
            : this(name, isBot, difficulty, BotStyle.Balanced)
        {
        }

        public SeatConfig(string name, bool isBot, BotDifficulty difficulty, BotStyle style)
        {
            Name = name;
            IsBot = isBot;
            Difficulty = difficulty;
            Style = style;
        }

        public string Name { get; }
        public bool IsBot { get; }
        public BotDifficulty Difficulty { get; }
        public BotStyle Style { get; }

        public static SeatConfig Human(string name)
        {
            return new SeatConfig(name, false, BotDifficulty.Medium);
        }

        public static SeatConfig Bot(string name, BotDifficulty difficulty, BotStyle style = BotStyle.Balanced)
        {
            return new SeatConfig(name, true, difficulty, style);
        }
    }
}
