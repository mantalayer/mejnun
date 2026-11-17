using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.Game
{
    public sealed class CommandBatch
    {
        private readonly List<GameCommand> commands = new List<GameCommand>(2);

        public IReadOnlyList<GameCommand> Commands => commands;
        public int Count => commands.Count;

        public void Add(GameCommand command)
        {
            commands.Add(command);
        }
    }
}