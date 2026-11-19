using System;
using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.Net
{
    public enum NetMessageKind : byte
    {
        Join,
        Welcome,
        StateUpdate,
        Commands,
        Rejected,
        SeatStatus,
        Paused
    }

    public abstract class NetMessage
    {
        protected NetMessage(NetMessageKind kind)
        {
            Kind = kind;
        }

        public NetMessageKind Kind { get; }
    }

    public sealed class JoinMessage : NetMessage
    {
        public JoinMessage(string authenticationId, string displayName)
            : base(NetMessageKind.Join)
        {
            AuthenticationId = authenticationId ?? throw new ArgumentNullException(nameof(authenticationId));
            DisplayName = displayName ?? string.Empty;
        }

        public string AuthenticationId { get; }
        public string DisplayName { get; }
    }

    public sealed class WelcomeMessage : NetMessage
    {
        public WelcomeMessage(int seat, byte[] snapshot)
            : base(NetMessageKind.Welcome)
        {
            Seat = seat;
            Snapshot = snapshot == null ? throw new ArgumentNullException(nameof(snapshot)) : (byte[])snapshot.Clone();
        }

        public int Seat { get; }
        public byte[] Snapshot { get; }
    }

    public sealed class StateUpdateMessage : NetMessage
    {
        public StateUpdateMessage(byte[] snapshot)
            : base(NetMessageKind.StateUpdate)
        {
            Snapshot = snapshot == null ? throw new ArgumentNullException(nameof(snapshot)) : (byte[])snapshot.Clone();
        }

        public byte[] Snapshot { get; }
    }

    public sealed class CommandsMessage : NetMessage
    {
        private readonly GameCommand[] commands;

        public CommandsMessage(IReadOnlyList<GameCommand> commands)
            : base(NetMessageKind.Commands)
        {
            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            this.commands = new GameCommand[commands.Count];
            for (var index = 0; index < commands.Count; index++)
            {
                this.commands[index] = commands[index];
            }
        }

        public IReadOnlyList<GameCommand> Commands => Array.AsReadOnly(commands);
    }

    public sealed class RejectedMessage : NetMessage
    {
        public RejectedMessage(string reason)
            : base(NetMessageKind.Rejected)
        {
            Reason = reason ?? string.Empty;
        }

        public string Reason { get; }
    }

    public sealed class SeatStatusMessage : NetMessage
    {
        public SeatStatusMessage(int seat, string authenticationId, string displayName, bool connected, bool isBot)
            : base(NetMessageKind.SeatStatus)
        {
            Seat = seat;
            AuthenticationId = authenticationId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Connected = connected;
            IsBot = isBot;
        }

        public int Seat { get; }
        public string AuthenticationId { get; }
        public string DisplayName { get; }
        public bool Connected { get; }
        public bool IsBot { get; }
    }

    public sealed class PausedMessage : NetMessage
    {
        public PausedMessage(bool paused, string reason)
            : base(NetMessageKind.Paused)
        {
            Paused = paused;
            Reason = reason ?? string.Empty;
        }

        public bool Paused { get; }
        public string Reason { get; }
    }
}