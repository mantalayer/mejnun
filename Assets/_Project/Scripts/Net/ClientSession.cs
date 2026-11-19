using System;
using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.Net
{
    public sealed class ClientSession
    {
        private byte[] lastSnapshot;

        public MejnunGame Game { get; private set; }
        public int LocalSeat { get; private set; } = -1;
        public bool IsPaused { get; private set; }
        public event Action<MejnunGame> StateChanged;
        public event Action<NetMessage> StatusChanged;

        public void Submit(IReadOnlyList<GameCommand> commands)
        {
            if (commands == null || commands.Count == 0)
            {
                throw new ArgumentException("A command group must contain at least one command.", nameof(commands));
            }

            CommandsReady?.Invoke(new CommandsMessage(commands));
        }

        public event Action<CommandsMessage> CommandsReady;

        public void Receive(NetMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            switch (message.Kind)
            {
                case NetMessageKind.Welcome:
                {
                    var welcome = (WelcomeMessage)message;
                    LocalSeat = welcome.Seat;
                    ReplaceView(welcome.Snapshot);
                    break;
                }
                case NetMessageKind.StateUpdate:
                    ReplaceView(((StateUpdateMessage)message).Snapshot);
                    break;
                case NetMessageKind.Paused:
                    IsPaused = ((PausedMessage)message).Paused;
                    StatusChanged?.Invoke(message);
                    break;
                case NetMessageKind.SeatStatus:
                case NetMessageKind.Rejected:
                    StatusChanged?.Invoke(message);
                    break;
                default:
                    throw new ArgumentException("Message is not valid for a client session.", nameof(message));
            }
        }

        private void ReplaceView(byte[] snapshot)
        {
            if (lastSnapshot != null && BytesEqual(lastSnapshot, snapshot))
            {
                return;
            }

            Game = SnapshotSerializer.Read(snapshot);
            lastSnapshot = (byte[])snapshot.Clone();
            StateChanged?.Invoke(Game);
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (var index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}