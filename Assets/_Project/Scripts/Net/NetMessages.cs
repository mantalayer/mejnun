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
        Paused,
        Away,
        Rematch,
        RematchStatus,
        FirstPlayerVote,
        FirstPlayerVoteStatus,
        Reaction
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
        public RejectedMessage(string debugReason)
            : this(null, debugReason)
        {
        }

        public RejectedMessage(JoinRejectReason? code, string debugReason = null)
            : base(NetMessageKind.Rejected)
        {
            if (code.HasValue && !Enum.IsDefined(typeof(JoinRejectReason), code.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(code));
            }

            Code = code;
            DebugReason = debugReason ?? string.Empty;
        }

        public JoinRejectReason? Code { get; }
        public string DebugReason { get; }
    }

    public sealed class SeatStatusMessage : NetMessage
    {
        public SeatStatusMessage(int seat, string authenticationId, string displayName, SeatPresence presence,
            bool pendingHandBack, float graceRemaining)
            : base(NetMessageKind.SeatStatus)
        {
            if (seat < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seat));
            }

            if (!Enum.IsDefined(typeof(SeatPresence), presence))
            {
                throw new ArgumentOutOfRangeException(nameof(presence));
            }

            if (graceRemaining < 0 || float.IsNaN(graceRemaining) || float.IsInfinity(graceRemaining))
            {
                throw new ArgumentOutOfRangeException(nameof(graceRemaining));
            }

            Seat = seat;
            AuthenticationId = authenticationId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Presence = presence;
            PendingHandBack = pendingHandBack;
            GraceRemaining = graceRemaining;
        }

        public int Seat { get; }
        public string AuthenticationId { get; }
        public string DisplayName { get; }
        public SeatPresence Presence { get; }
        public bool PendingHandBack { get; }
        public float GraceRemaining { get; }
        public bool Connected => Presence == SeatPresence.Connected;
        public bool IsBot => Presence == SeatPresence.Bot || Presence == SeatPresence.BotControlled;
    }

    public sealed class PausedMessage : NetMessage
    {
        public PausedMessage(PauseReason reason)
            : base(NetMessageKind.Paused)
        {
            if (!Enum.IsDefined(typeof(PauseReason), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason));
            }

            Reason = reason;
        }

        public PauseReason Reason { get; }
        public bool Paused => Reason != PauseReason.None;
    }

    public sealed class AwayMessage : NetMessage
    {
        public AwayMessage()
            : base(NetMessageKind.Away)
        {
        }
    }

    public sealed class RematchMessage : NetMessage
    {
        public RematchMessage()
            : base(NetMessageKind.Rematch)
        {
        }
    }

    public sealed class RematchStatusMessage : NetMessage
    {
        public RematchStatusMessage(int readySeats, int seatCount)
            : base(NetMessageKind.RematchStatus)
        {
            if (seatCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seatCount));
            }

            if (readySeats < 0 || readySeats > seatCount)
            {
                throw new ArgumentOutOfRangeException(nameof(readySeats));
            }

            ReadySeats = readySeats;
            SeatCount = seatCount;
        }

        public int ReadySeats { get; }
        public int SeatCount { get; }
    }

    public sealed class FirstPlayerVoteMessage : NetMessage
    {
        public FirstPlayerVoteMessage(int choice)
            : base(NetMessageKind.FirstPlayerVote)
        {
            if (choice < FirstPlayerVote.RandomVote || choice >= 6)
            {
                throw new ArgumentOutOfRangeException(nameof(choice));
            }

            Choice = choice;
        }

        public int Choice { get; }
    }

    public sealed class FirstPlayerVoteStatusMessage : NetMessage
    {
        private readonly bool[] candidates;
        private readonly int[] votes;

        public FirstPlayerVoteStatusMessage(bool isOpen, string questionKey, bool[] candidates, int[] votes)
            : base(NetMessageKind.FirstPlayerVoteStatus)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (votes == null)
            {
                throw new ArgumentNullException(nameof(votes));
            }

            if (candidates.Length == 0 || candidates.Length > 6 || candidates.Length != votes.Length)
            {
                throw new ArgumentException("Vote status must contain matching seat arrays of one to six seats.");
            }

            var candidateCount = 0;
            for (var seat = 0; seat < candidates.Length; seat++)
            {
                if (candidates[seat])
                {
                    candidateCount++;
                }

                if (votes[seat] != FirstPlayerVote.NoVote && votes[seat] != FirstPlayerVote.RandomVote &&
                    (votes[seat] < 0 || votes[seat] >= candidates.Length || !candidates[votes[seat]]))
                {
                    throw new ArgumentOutOfRangeException(nameof(votes));
                }
            }

            if (isOpen && candidateCount == 0)
            {
                throw new ArgumentException("An open vote must have at least one candidate.", nameof(candidates));
            }

            IsOpen = isOpen;
            QuestionKey = questionKey ?? string.Empty;
            this.candidates = (bool[])candidates.Clone();
            this.votes = (int[])votes.Clone();
        }

        public bool IsOpen { get; }
        public string QuestionKey { get; }
        public bool[] Candidates => (bool[])candidates.Clone();
        public int[] Votes => (int[])votes.Clone();
    }

    public sealed class ReactionMessage : NetMessage
    {
        public ReactionMessage(int fromSeat, Mejnun.Core.Reaction reaction)
            : base(NetMessageKind.Reaction)
        {
            if (fromSeat < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(fromSeat));
            }

            if (!Enum.IsDefined(typeof(Mejnun.Core.Reaction), reaction))
            {
                throw new ArgumentOutOfRangeException(nameof(reaction));
            }

            FromSeat = fromSeat;
            Reaction = reaction;
        }

        public int FromSeat { get; }
        public Mejnun.Core.Reaction Reaction { get; }
    }
}