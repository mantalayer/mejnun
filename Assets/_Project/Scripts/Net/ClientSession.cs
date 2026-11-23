using System;
using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.Net
{
    public sealed class ClientSession
    {
        private byte[] lastSnapshot;
        private readonly List<SeatStatusMessage> seatStatuses = new List<SeatStatusMessage>();

        public MejnunGame Game { get; private set; }
        public int LocalSeat { get; private set; } = -1;
        public PauseReason PauseReason { get; private set; }
        public bool IsPaused => PauseReason != Mejnun.Net.PauseReason.None;
        public RematchStatusMessage RematchStatus { get; private set; }
        public bool IsRematchPending => RematchStatus != null;
        public FirstPlayerVoteStatusMessage FirstPlayerVoteStatus { get; private set; }
        public IReadOnlyList<SeatStatusMessage> SeatStatuses => seatStatuses.AsReadOnly();
        public event Action<MejnunGame> StateChanged;
        public event Action<NetMessage> StatusChanged;
        public event Action<RematchMessage> RematchRequested;
        public event Action<FirstPlayerVoteMessage> FirstPlayerVoteRequested;
        public event Action<ReactionMessage> ReactionRequested;

        public void Submit(IReadOnlyList<GameCommand> commands)
        {
            if (commands == null || commands.Count == 0)
            {
                throw new ArgumentException("A command group must contain at least one command.", nameof(commands));
            }

            CommandsReady?.Invoke(new CommandsMessage(commands));
        }

        public event Action<CommandsMessage> CommandsReady;
        public event Action<int, Reaction> ReactionReceived;

        public void SubmitRematch()
        {
            RematchRequested?.Invoke(new RematchMessage());
        }

        public void SubmitFirstPlayerVote(int choice)
        {
            FirstPlayerVoteRequested?.Invoke(new FirstPlayerVoteMessage(choice));
        }

        public void SubmitReaction(Reaction reaction)
        {
            ReactionRequested?.Invoke(new ReactionMessage(-1, reaction));
        }

        public void SubmitHurryUp()
        {
            SubmitReaction(Reaction.HurryUp);
        }

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
                    PauseReason = ((PausedMessage)message).Reason;
                    StatusChanged?.Invoke(message);
                    break;
                case NetMessageKind.RematchStatus:
                    RematchStatus = (RematchStatusMessage)message;
                    StatusChanged?.Invoke(message);
                    break;
                case NetMessageKind.FirstPlayerVoteStatus:
                    FirstPlayerVoteStatus = (FirstPlayerVoteStatusMessage)message;
                    if (FirstPlayerVoteStatus.IsOpen)
                    {
                        RematchStatus = null;
                    }

                    StatusChanged?.Invoke(message);
                    break;
                case NetMessageKind.Reaction:
                {
                    var reaction = (ReactionMessage)message;
                    ReactionReceived?.Invoke(reaction.FromSeat, reaction.Reaction);
                    break;
                }
                case NetMessageKind.SeatStatus:
                {
                    var status = (SeatStatusMessage)message;
                    while (seatStatuses.Count <= status.Seat)
                    {
                        seatStatuses.Add(null);
                    }

                    seatStatuses[status.Seat] = status;
                    StatusChanged?.Invoke(message);
                    break;
                }
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
            if (Game.State.Phase != GamePhase.ChoosingDealer)
            {
                RematchStatus = null;
                FirstPlayerVoteStatus = null;
            }

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