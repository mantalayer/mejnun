namespace Mejnun.Core
{
    public readonly struct Move
    {
        internal Move(GameCommand command)
        {
            Command = command;
        }

        public GameCommand Command { get; }
        public CommandType Type => Command.Type;
    }
}