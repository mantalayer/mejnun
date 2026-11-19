using System;
using System.Collections.Generic;
using System.IO;
using Mejnun.AI;
using Mejnun.Core;

namespace Mejnun.Net
{
    public enum DisconnectChoice
    {
        Wait,
        TakeOverWithBot,
        SaveAndPause
    }

    public sealed class HostSession
    {
        private readonly Seat[] seats;
        private readonly ISnapshotStore snapshotStore;
        private double turnElapsed;
        private double botElapsed;

        public HostSession(MejnunGame game, ISnapshotStore snapshotStore = null)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            this.snapshotStore = snapshotStore ?? new InMemorySnapshotStore();
            seats = new Seat[game.Setup.SeatCount];
            for (var index = 0; index < seats.Length; index++)
            {
                seats[index] = new Seat();
            }
        }

        private HostSession(MejnunGame game, ISnapshotStore snapshotStore, SeatRecord[] restoredSeats, bool paused)
            : this(game, snapshotStore)
        {
            for (var index = 0; index < Math.Min(seats.Length, restoredSeats.Length); index++)
            {
                seats[index].AuthenticationId = restoredSeats[index].AuthenticationId;
                seats[index].DisplayName = restoredSeats[index].DisplayName;
                seats[index].Connected = restoredSeats[index].Connected;
                seats[index].IsBot = restoredSeats[index].IsBot;
                seats[index].Difficulty = restoredSeats[index].Difficulty;
            }

            IsPaused = paused;
        }

        public MejnunGame Game { get; private set; }
        public bool IsPaused { get; private set; }
        public ISnapshotStore SnapshotStore => snapshotStore;
        public event Action<string, NetMessage> MessageSent;
        public event Action<MejnunGame> StateChanged;
        public event Action<SeatStatusMessage> SeatStatusChanged;

        public bool AreAllSeatsConnected
        {
            get
            {
                for (var index = 0; index < seats.Length; index++)
                {
                    if (!seats[index].Connected && !seats[index].IsBot)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public static HostSession FromRecord(SessionRecord record, ISnapshotStore snapshotStore = null)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            var game = SnapshotSerializer.Read(record.Snapshot);
            var seatRecords = record.Seats;
            if (seatRecords.Length != game.Setup.SeatCount)
            {
                throw new InvalidDataException("Session record seat count does not match the game setup.");
            }

            return new HostSession(game, snapshotStore, seatRecords, record.Paused);
        }

        public int Join(string authenticationId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(authenticationId))
            {
                Send(authenticationId, new RejectedMessage("Authentication id is required."));
                return -1;
            }

            var seat = FindSeat(authenticationId);
            if (seat < 0)
            {
                seat = FindOpenSeat();
                if (seat < 0)
                {
                    Send(authenticationId, new RejectedMessage("No open seats."));
                    return -1;
                }

                seats[seat].AuthenticationId = authenticationId;
            }

            var entry = seats[seat];
            entry.DisplayName = displayName ?? string.Empty;
            entry.Connected = true;
            entry.IsBot = false;
            Send(authenticationId, new WelcomeMessage(seat, SnapshotSerializer.WriteForSeat(Game, seat)));
            BroadcastSeatStatuses();
            return seat;
        }

        public void AddBot(int seat, string displayName, BotDifficulty difficulty = BotDifficulty.Medium)
        {
            ValidateSeat(seat);
            if (seats[seat].Connected || !string.IsNullOrEmpty(seats[seat].AuthenticationId))
            {
                throw new InvalidOperationException("The seat is already assigned.");
            }

            seats[seat].DisplayName = displayName ?? "Bot";
            seats[seat].IsBot = true;
            seats[seat].Difficulty = difficulty;
            BroadcastSeatStatuses();
        }

        public bool SubmitCommands(string authenticationId, IReadOnlyList<GameCommand> commands)
        {
            var seat = FindSeat(authenticationId);
            if (seat < 0 || !seats[seat].Connected || seats[seat].IsBot)
            {
                Send(authenticationId, new RejectedMessage("The sender has no connected player seat."));
                return false;
            }

            return ApplyGroup(seat, commands, authenticationId);
        }

        public bool Disconnect(string authenticationId, DisconnectChoice choice)
        {
            var seat = FindSeat(authenticationId);
            if (seat < 0 || !seats[seat].Connected)
            {
                return false;
            }

            var entry = seats[seat];
            entry.Connected = false;
            ApplyDisconnectChoice(authenticationId, entry, choice);
            BroadcastSeatStatuses();
            StateChanged?.Invoke(Game);
            return true;
        }

        public bool ResolveDisconnect(string authenticationId, DisconnectChoice choice)
        {
            var seat = FindSeat(authenticationId);
            if (seat < 0 || seats[seat].Connected)
            {
                return false;
            }

            ApplyDisconnectChoice(authenticationId, seats[seat], choice);
            BroadcastSeatStatuses();
            StateChanged?.Invoke(Game);
            return true;
        }

        private void ApplyDisconnectChoice(string authenticationId, Seat entry, DisconnectChoice choice)
        {
            switch (choice)
            {
                case DisconnectChoice.Wait:
                    break;
                case DisconnectChoice.TakeOverWithBot:
                    entry.IsBot = true;
                    entry.Difficulty = BotDifficulty.Medium;
                    break;
                case DisconnectChoice.SaveAndPause:
                    IsPaused = true;
                    SaveSnapshot();
                    SendAll(new PausedMessage(true, "A player disconnected; the match is saved and paused."));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(choice));
            }
        }

        public void Resume()
        {
            if (!IsPaused)
            {
                return;
            }

            IsPaused = false;
            turnElapsed = 0;
            SaveSnapshot();
            SendAll(new PausedMessage(false, string.Empty));
            StateChanged?.Invoke(Game);
            BroadcastStates();
        }

        public void Tick(double elapsedSeconds)
        {
            if (elapsedSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            if (IsPaused || Game.State.Phase != GamePhase.Playing)
            {
                return;
            }

            var currentSeat = Game.State.CurrentSeat;
            if (seats[currentSeat].IsBot)
            {
                botElapsed += elapsedSeconds;
                if (botElapsed >= 0.5)
                {
                    botElapsed = 0;
                    var plan = BotBrain.PlanTurn(Game, seats[currentSeat].Difficulty,
                        Game.State.Rng.State ^ (ulong)Game.State.TurnNumber);
                    if (plan.Commands.Count > 0)
                    {
                        ApplyGroup(currentSeat, plan.Commands, null);
                    }
                }

                return;
            }

            botElapsed = 0;
            if (Game.Setup.TurnSeconds <= 0)
            {
                return;
            }

            turnElapsed += elapsedSeconds;
            if (turnElapsed >= Game.Setup.TurnSeconds)
            {
                turnElapsed = 0;
                ApplyGroup(currentSeat, new[] { GameCommand.TimeoutTurn(currentSeat) }, null);
            }
        }

        public SessionRecord CreateRecord()
        {
            var seatRecords = new SeatRecord[seats.Length];
            for (var index = 0; index < seats.Length; index++)
            {
                var seat = seats[index];
                seatRecords[index] = new SeatRecord(seat.AuthenticationId, seat.DisplayName, seat.Connected,
                    seat.IsBot, seat.Difficulty);
            }

            return new SessionRecord(SnapshotSerializer.Write(Game), seatRecords, IsPaused);
        }

        public void SaveSnapshot()
        {
            snapshotStore.Save(CreateRecord());
        }

        public SeatRecord GetSeat(int seat)
        {
            ValidateSeat(seat);
            var entry = seats[seat];
            return new SeatRecord(entry.AuthenticationId, entry.DisplayName, entry.Connected, entry.IsBot,
                entry.Difficulty);
        }

        private bool ApplyGroup(int seat, IReadOnlyList<GameCommand> commands, string senderId)
        {
            if (commands == null || commands.Count == 0)
            {
                Reject(senderId, "Command group is empty.");
                return false;
            }

            if (IsPaused)
            {
                Reject(senderId, "The match is paused.");
                return false;
            }

            for (var index = 0; index < commands.Count; index++)
            {
                var command = commands[index];
                if (command.Type == CommandType.ChooseRandomDealer)
                {
                    if (Game.State.Phase != GamePhase.ChoosingDealer)
                    {
                        Reject(senderId, "Dealer selection is no longer available.");
                        return false;
                    }
                }
                else if (command.Seat != seat)
                {
                    Reject(senderId, "Command does not belong to the authenticated seat.");
                    return false;
                }
            }

            var backup = SnapshotSerializer.Write(Game);
            var previousTurn = Game.State.TurnNumber;
            var previousSeat = Game.State.CurrentSeat;
            for (var index = 0; index < commands.Count; index++)
            {
                var result = Game.Apply(commands[index]);
                if (!result.Ok)
                {
                    Game = SnapshotSerializer.Read(backup);
                    Reject(senderId, result.Error.ToString());
                    return false;
                }
            }

            if (Game.State.TurnNumber != previousTurn || Game.State.CurrentSeat != previousSeat)
            {
                turnElapsed = 0;
                botElapsed = 0;
                SaveSnapshot();
            }

            StateChanged?.Invoke(Game);
            BroadcastStates();
            return true;
        }

        private void Reject(string authenticationId, string reason)
        {
            Send(authenticationId, new RejectedMessage(reason));
            var seat = FindSeat(authenticationId);
            if (seat >= 0 && seats[seat].Connected)
            {
                Send(authenticationId, new StateUpdateMessage(SnapshotSerializer.WriteForSeat(Game, seat)));
            }
        }

        private void BroadcastStates()
        {
            for (var seat = 0; seat < seats.Length; seat++)
            {
                if (seats[seat].Connected && !string.IsNullOrEmpty(seats[seat].AuthenticationId))
                {
                    Send(seats[seat].AuthenticationId,
                        new StateUpdateMessage(SnapshotSerializer.WriteForSeat(Game, seat)));
                }
            }
        }

        private void BroadcastSeatStatuses()
        {
            for (var seat = 0; seat < seats.Length; seat++)
            {
                var entry = seats[seat];
                var message = new SeatStatusMessage(seat, entry.AuthenticationId, entry.DisplayName,
                    entry.Connected, entry.IsBot);
                SeatStatusChanged?.Invoke(message);
                SendAll(message);
            }
        }

        private void SendAll(NetMessage message)
        {
            for (var seat = 0; seat < seats.Length; seat++)
            {
                if (seats[seat].Connected && !string.IsNullOrEmpty(seats[seat].AuthenticationId))
                {
                    Send(seats[seat].AuthenticationId, message);
                }
            }
        }

        private void Send(string authenticationId, NetMessage message)
        {
            if (!string.IsNullOrEmpty(authenticationId))
            {
                MessageSent?.Invoke(authenticationId, message);
            }
        }

        private int FindSeat(string authenticationId)
        {
            if (string.IsNullOrEmpty(authenticationId))
            {
                return -1;
            }

            for (var seat = 0; seat < seats.Length; seat++)
            {
                if (string.Equals(seats[seat].AuthenticationId, authenticationId, StringComparison.Ordinal))
                {
                    return seat;
                }
            }

            return -1;
        }

        private int FindOpenSeat()
        {
            for (var seat = 0; seat < seats.Length; seat++)
            {
                if (string.IsNullOrEmpty(seats[seat].AuthenticationId) && !seats[seat].IsBot)
                {
                    return seat;
                }
            }

            return -1;
        }

        private void ValidateSeat(int seat)
        {
            if (seat < 0 || seat >= seats.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(seat));
            }
        }

        private sealed class Seat
        {
            public string AuthenticationId = string.Empty;
            public string DisplayName = string.Empty;
            public bool Connected;
            public bool IsBot;
            public BotDifficulty Difficulty = BotDifficulty.Medium;
        }
    }
}