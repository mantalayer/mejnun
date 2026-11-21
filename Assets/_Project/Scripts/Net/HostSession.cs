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
        public const double MigrationReconnectGraceSeconds = 60;
        private readonly SeatRoster roster;
        private readonly ISnapshotStore snapshotStore;
        private readonly int[] lastGraceSeconds;
        private bool[] rematchVotes;
        private bool isRematchPending;
        private int? pendingRematchFirstPlayer;
        private bool firstPlayerVoteOpen;
        private string firstPlayerVoteQuestionKey = string.Empty;
        private bool[] firstPlayerVoteCandidates;
        private int[] firstPlayerVotes;
        private readonly Random firstPlayerVoteRandom = new Random();
        private double turnElapsed;
        private double botElapsed;

        public HostSession(MejnunGame game, ISnapshotStore snapshotStore = null)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            this.snapshotStore = snapshotStore ?? new InMemorySnapshotStore();
            roster = new SeatRoster(game.Setup.SeatCount);
            lastGraceSeconds = new int[game.Setup.SeatCount];
            for (var index = 0; index < lastGraceSeconds.Length; index++)
            {
                lastGraceSeconds[index] = -1;
            }
        }

        private HostSession(MejnunGame game, ISnapshotStore snapshotStore, SeatRecord[] restoredSeats,
            PauseReason pauseReason)
            : this(game, snapshotStore)
        {
            for (var index = 0; index < Math.Min(roster.SeatCount, restoredSeats.Length); index++)
            {
                roster.Restore(index, restoredSeats[index]);
            }

            PauseReason = pauseReason;
            RefreshPresencePauseReason();
        }

        public MejnunGame Game { get; private set; }
        public PauseReason PauseReason { get; private set; }
        public bool IsPaused => PauseReason != Mejnun.Net.PauseReason.None;
        public bool IsRematchPending => isRematchPending;
        public int RematchReadyCount => CountRematchVotes();
        public bool IsFirstPlayerVoteOpen => firstPlayerVoteOpen;
        public Func<string> QuestionKeyProvider { get; set; }
        public SeatRoster Roster => roster;
        public ISnapshotStore SnapshotStore => snapshotStore;
        public event Action<string, NetMessage> MessageSent;
        public event Action<MejnunGame> StateChanged;
        public event Action<SeatStatusMessage> SeatStatusChanged;
        public event Action<int> SeatExpired;
        public event Action<PauseReason> PauseReasonChanged;
        public event Action<RematchStatusMessage> RematchStatusChanged;
        public event Action<FirstPlayerVoteStatusMessage> FirstPlayerVoteChanged;

        public bool AreAllSeatsConnected
        {
            get
            {
                for (var index = 0; index < roster.SeatCount; index++)
                {
                    if (!roster.HasReadySeat(index))
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

            return new HostSession(game, snapshotStore, seatRecords, record.PauseReason);
        }

        public int Join(string authenticationId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(authenticationId))
            {
                Send(authenticationId, new RejectedMessage(JoinRejectReason.MissingAuthentication,
                    "Authentication id is required."));
                return -1;
            }

            var seat = roster.Claim(authenticationId, displayName, Game.State.CurrentSeat, out _);
            if (seat < 0)
            {
                Send(authenticationId, new RejectedMessage(JoinRejectReason.NoFreeSeat, "No open seats."));
                return -1;
            }

            Send(authenticationId, new WelcomeMessage(seat, SnapshotSerializer.WriteForSeat(Game, seat)));
            RefreshPresencePauseReason();
            Send(authenticationId, new PausedMessage(PauseReason));
            BroadcastSeatStatuses();
            if (IsRematchPending)
            {
                Send(authenticationId, CreateRematchStatus());
            }

            if (firstPlayerVoteOpen)
            {
                Send(authenticationId, CreateFirstPlayerVoteStatus());
            }

            TryStartRematch();
            return seat;
        }

        public bool BeginFirstPlayerVote(string questionKey)
        {
            if (Game.State.Phase != GamePhase.ChoosingDealer || IsRematchPending)
            {
                return false;
            }

            if (firstPlayerVoteOpen)
            {
                return true;
            }

            firstPlayerVoteCandidates = new bool[roster.SeatCount];
            firstPlayerVotes = new int[roster.SeatCount];
            var candidateCount = 0;
            var soleCandidate = -1;
            for (var seat = 0; seat < roster.SeatCount; seat++)
            {
                firstPlayerVotes[seat] = FirstPlayerVote.NoVote;
                var authenticationId = roster.Get(seat).AuthenticationId;
                if (!roster.IsConnected(authenticationId))
                {
                    continue;
                }

                firstPlayerVoteCandidates[seat] = true;
                candidateCount++;
                soleCandidate = seat;
            }

            if (candidateCount == 0)
            {
                firstPlayerVoteCandidates = null;
                firstPlayerVotes = null;
                return false;
            }

            if (candidateCount == 1)
            {
                firstPlayerVoteQuestionKey = questionKey ?? string.Empty;
                return CompleteFirstPlayerVote(soleCandidate);
            }

            firstPlayerVoteQuestionKey = questionKey ?? string.Empty;
            firstPlayerVoteOpen = true;
            BroadcastFirstPlayerVoteStatus();
            return true;
        }

        public bool CastFirstPlayerVote(string authenticationId, int choice)
        {
            var seat = FindSeat(authenticationId);
            if (!firstPlayerVoteOpen || seat < 0 || !firstPlayerVoteCandidates[seat] ||
                !roster.IsConnected(authenticationId))
            {
                return false;
            }

            if (choice != FirstPlayerVote.RandomVote &&
                (choice < 0 || choice >= firstPlayerVoteCandidates.Length || !firstPlayerVoteCandidates[choice]))
            {
                return false;
            }

            firstPlayerVotes[seat] = choice;
            BroadcastFirstPlayerVoteStatus();
            TryResolveFirstPlayerVote();
            return true;
        }

        public void AddBot(int seat, string displayName, BotDifficulty difficulty = BotDifficulty.Medium)
        {
            roster.AddBot(seat, displayName, difficulty);
            BroadcastSeatStatuses();
            TryStartRematch();
        }

        public bool RequestRematch(string authenticationId)
        {
            var seat = FindSeat(authenticationId);
            if (seat < 0 || !roster.IsConnected(authenticationId))
            {
                return false;
            }

            if (!IsRematchPending)
            {
                if (Game.State.Phase != GamePhase.Finished)
                {
                    return false;
                }

                var previousSetup = Game.Setup;
                pendingRematchFirstPlayer = Game.State.Result?.NextFirstPlayer;
                var nextSeed = unchecked(previousSetup.Seed + 1UL);
                Game = new MejnunGame(new GameSetup(previousSetup.SeatCount, nextSeed, null,
                    previousSetup.TurnSeconds, previousSetup.FacePool, previousSetup.DeckPicks,
                    previousSetup.FeltPool, previousSetup.ShoeOrder));
                rematchVotes = new bool[roster.SeatCount];
                isRematchPending = true;
                turnElapsed = 0;
                botElapsed = 0;
                for (var index = 0; index < roster.SeatCount; index++)
                {
                    var presence = roster.Get(index).Presence;
                    rematchVotes[index] = presence == SeatPresence.Bot || presence == SeatPresence.BotControlled;
                }

                SaveSnapshot();
                StateChanged?.Invoke(Game);
                BroadcastStates();
            }
            else if (Game.State.Phase != GamePhase.ChoosingDealer)
            {
                return false;
            }

            rematchVotes[seat] = true;
            BroadcastRematchStatus();
            TryStartRematch();
            return true;
        }

        public bool SubmitCommands(string authenticationId, IReadOnlyList<GameCommand> commands)
        {
            var seat = FindSeat(authenticationId);
            if (seat < 0 || !roster.IsConnected(authenticationId))
            {
                Send(authenticationId, new RejectedMessage(null, "The sender has no connected player seat."));
                return false;
            }

            if (IsRematchPending)
            {
                Send(authenticationId, new RejectedMessage(null, "The rematch is waiting for all seats."));
                return false;
            }

            return ApplyGroup(seat, commands, authenticationId);
        }

        public bool Disconnect(string authenticationId, DisconnectChoice choice)
        {
            var seat = FindSeat(authenticationId);
            if (seat < 0 || !roster.IsConnected(authenticationId))
            {
                return false;
            }

            roster.MarkDropped(authenticationId);
            if (choice == DisconnectChoice.SaveAndPause)
            {
                SetPauseReason(PauseReason.SavedByHost);
                SaveSnapshot();
            }

            RefreshPresencePauseReason();
            BroadcastSeatStatuses();
            StateChanged?.Invoke(Game);
            TryStartRematch();
            return true;
        }

        public void MarkAway(string authenticationId)
        {
            roster.MarkAway(authenticationId);
            RefreshPresencePauseReason();
            BroadcastSeatStatuses();
        }

        public int PrepareForPromotion(string authenticationId)
        {
            var localSeat = FindSeat(authenticationId);
            if (localSeat < 0)
            {
                return -1;
            }

            for (var seat = 0; seat < roster.SeatCount; seat++)
            {
                if (seat == localSeat)
                {
                    continue;
                }

                var state = roster.Get(seat);
                if (!string.IsNullOrWhiteSpace(state.AuthenticationId) && state.Presence != SeatPresence.Bot)
                {
                    roster.MarkAway(state.AuthenticationId, MigrationReconnectGraceSeconds);
                }
            }

            var localState = roster.Get(localSeat);
            roster.Claim(authenticationId, localState.DisplayName, Game.State.CurrentSeat, out _);
            if (PauseReason == Mejnun.Net.PauseReason.HostAway)
            {
                SetPauseReason(Mejnun.Net.PauseReason.None);
            }

            RefreshPresencePauseReason();
            BroadcastSeatStatuses();
            return localSeat;
        }

        public bool ResolveDisconnect(string authenticationId, DisconnectChoice choice)
        {
            var seat = FindSeat(authenticationId);
            if (seat < 0 || roster.Get(seat).Presence != SeatPresence.Disconnected)
            {
                return false;
            }

            switch (choice)
            {
                case DisconnectChoice.Wait:
                    break;
                case DisconnectChoice.TakeOverWithBot:
                    roster.SetBotControl(seat, BotDifficulty.Medium);
                    break;
                case DisconnectChoice.SaveAndPause:
                    SetPauseReason(PauseReason.SavedByHost);
                    SaveSnapshot();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(choice));
            }

            RefreshPresencePauseReason();
            BroadcastSeatStatuses();
            StateChanged?.Invoke(Game);
            TryStartRematch();
            return true;
        }

        public void SetBotControl(int seat, BotDifficulty difficulty)
        {
            roster.SetBotControl(seat, difficulty);
            RefreshPresencePauseReason();
            BroadcastSeatStatuses();
            StateChanged?.Invoke(Game);
            TryStartRematch();
        }

        public void Resume()
        {
            if (!IsPaused)
            {
                return;
            }

            turnElapsed = 0;
            if (PauseReason == Mejnun.Net.PauseReason.SavedByHost ||
                PauseReason == Mejnun.Net.PauseReason.HostAway)
            {
                SetPauseReason(Mejnun.Net.PauseReason.None);
            }

            RefreshPresencePauseReason();
            SaveSnapshot();
            StateChanged?.Invoke(Game);
            BroadcastStates();
            TryStartRematch();
        }

        public void Tick(double elapsedSeconds)
        {
            if (elapsedSeconds < 0 || double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            var expiredSeats = roster.Tick(elapsedSeconds);
            if (expiredSeats.Count > 0)
            {
                RefreshPresencePauseReason();
                BroadcastSeatStatuses();
                for (var index = 0; index < expiredSeats.Count; index++)
                {
                    SeatExpired?.Invoke(expiredSeats[index]);
                }
            }
            else if (HasGraceCountdownChanged())
            {
                BroadcastSeatStatuses();
            }

            if (IsPaused || roster.HasBlockingAbsence || Game.State.Phase != GamePhase.Playing)
            {
                return;
            }

            var currentSeat = Game.State.CurrentSeat;
            var current = roster.Get(currentSeat);
            if (current.Presence == SeatPresence.Bot || current.Presence == SeatPresence.BotControlled)
            {
                botElapsed += elapsedSeconds;
                if (botElapsed >= 0.5)
                {
                    botElapsed = 0;
                    var plan = BotBrain.PlanTurn(Game, current.Difficulty,
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
            var seatRecords = new SeatRecord[roster.SeatCount];
            for (var index = 0; index < roster.SeatCount; index++)
            {
                var seat = roster.Get(index);
                seatRecords[index] = new SeatRecord(seat.AuthenticationId, seat.DisplayName, seat.Presence,
                    seat.PendingHandBack, seat.Difficulty);
            }

            return new SessionRecord(SnapshotSerializer.Write(Game), seatRecords, PauseReason);
        }

        public void SaveSnapshot()
        {
            snapshotStore.Save(CreateRecord());
        }

        public SeatRecord GetSeat(int seat)
        {
            ValidateSeat(seat);
            var entry = roster.Get(seat);
            return new SeatRecord(entry.AuthenticationId, entry.DisplayName, entry.Presence,
                entry.PendingHandBack, entry.Difficulty);
        }

        private bool ApplyGroup(int seat, IReadOnlyList<GameCommand> commands, string senderId)
        {
            if (commands == null || commands.Count == 0)
            {
                Reject(senderId, "Command group is empty.");
                return false;
            }

            if (IsPaused || roster.HasBlockingAbsence)
            {
                Reject(senderId, "The match is paused.");
                return false;
            }

            for (var index = 0; index < commands.Count; index++)
            {
                var command = commands[index];
                if (firstPlayerVoteOpen &&
                    (command.Type == CommandType.ChooseDealer || command.Type == CommandType.ChooseRandomDealer))
                {
                    Reject(senderId, "First-player voting is open.");
                    return false;
                }

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
                roster.CompleteHandBacks(Game.State.CurrentSeat);
                SaveSnapshot();
                BroadcastSeatStatuses();
            }

            StateChanged?.Invoke(Game);
            BroadcastStates();
            return true;
        }

        private void Reject(string authenticationId, string reason)
        {
            Send(authenticationId, new RejectedMessage(null, reason));
            var seat = FindSeat(authenticationId);
            if (seat >= 0 && IsMessageRecipient(seat))
            {
                Send(authenticationId, new StateUpdateMessage(SnapshotSerializer.WriteForSeat(Game, seat)));
            }
        }

        private void BroadcastStates()
        {
            for (var seat = 0; seat < roster.SeatCount; seat++)
            {
                var entry = roster.Get(seat);
                if (IsMessageRecipient(seat) && !string.IsNullOrEmpty(entry.AuthenticationId))
                {
                    Send(entry.AuthenticationId,
                        new StateUpdateMessage(SnapshotSerializer.WriteForSeat(Game, seat)));
                }
            }
        }

        private void TryStartRematch()
        {
            if (!IsRematchPending)
            {
                TryResolveFirstPlayerVote();
                return;
            }

            if (IsPaused || Game.State.Phase != GamePhase.ChoosingDealer)
            {
                return;
            }

            var readyCountBeforeBots = CountRematchVotes();
            var allSeatsVoted = true;
            for (var seat = 0; seat < roster.SeatCount; seat++)
            {
                var presence = roster.Get(seat).Presence;
                if (presence == SeatPresence.Bot || presence == SeatPresence.BotControlled)
                {
                    rematchVotes[seat] = true;
                }

                if (!rematchVotes[seat])
                {
                    allSeatsVoted = false;
                }
            }

            if (CountRematchVotes() != readyCountBeforeBots)
            {
                BroadcastRematchStatus();
            }

            if (!allSeatsVoted)
            {
                return;
            }

            var seatCount = Game.Setup.SeatCount;
            var nextFirstPlayer = pendingRematchFirstPlayer;
            if (!nextFirstPlayer.HasValue)
            {
                isRematchPending = false;
                rematchVotes = null;
                pendingRematchFirstPlayer = null;
                turnElapsed = 0;
                botElapsed = 0;
                SaveSnapshot();
                StateChanged?.Invoke(Game);
                BroadcastStates();
                BeginFirstPlayerVote(QuestionKeyProvider == null ? null : QuestionKeyProvider());
                return;
            }

            var dealer = (nextFirstPlayer.Value - 1 + seatCount) % seatCount;
            var result = Game.Apply(GameCommand.ChooseDealer(dealer));
            if (!result.Ok)
            {
                return;
            }

            isRematchPending = false;
            rematchVotes = null;
            pendingRematchFirstPlayer = null;
            turnElapsed = 0;
            botElapsed = 0;
            SaveSnapshot();
            StateChanged?.Invoke(Game);
            BroadcastStates();
        }

        private void TryResolveFirstPlayerVote()
        {
            if (!firstPlayerVoteOpen || IsPaused || Game.State.Phase != GamePhase.ChoosingDealer)
            {
                return;
            }

            var candidatesChanged = false;
            var voterCount = 0;
            var allCandidatesVoted = true;
            for (var seat = 0; seat < firstPlayerVoteCandidates.Length; seat++)
            {
                if (!firstPlayerVoteCandidates[seat])
                {
                    continue;
                }

                var entry = roster.Get(seat);
                if (entry.Presence != SeatPresence.Connected)
                {
                    firstPlayerVoteCandidates[seat] = false;
                    firstPlayerVotes[seat] = FirstPlayerVote.NoVote;
                    candidatesChanged = true;
                    continue;
                }

                voterCount++;
                if (firstPlayerVotes[seat] == FirstPlayerVote.NoVote)
                {
                    allCandidatesVoted = false;
                }
            }

            if (candidatesChanged)
            {
                BroadcastFirstPlayerVoteStatus();
            }

            if (!allCandidatesVoted || voterCount == 0)
            {
                return;
            }

            var winner = FirstPlayerVote.Resolve(firstPlayerVotes, firstPlayerVoteCandidates,
                firstPlayerVoteCandidates, firstPlayerVoteRandom);
            CompleteFirstPlayerVote(winner);
        }

        private bool CompleteFirstPlayerVote(int firstPlayerSeat)
        {
            if (firstPlayerSeat < 0 || firstPlayerSeat >= Game.Setup.SeatCount)
            {
                return false;
            }

            var dealer = (firstPlayerSeat - 1 + Game.Setup.SeatCount) % Game.Setup.SeatCount;
            var result = Game.Apply(GameCommand.ChooseDealer(dealer));
            if (!result.Ok)
            {
                return false;
            }

            firstPlayerVoteOpen = false;
            SaveSnapshot();
            BroadcastFirstPlayerVoteStatus();
            StateChanged?.Invoke(Game);
            BroadcastStates();
            return true;
        }

        private FirstPlayerVoteStatusMessage CreateFirstPlayerVoteStatus()
        {
            return new FirstPlayerVoteStatusMessage(firstPlayerVoteOpen, firstPlayerVoteQuestionKey,
                firstPlayerVoteCandidates, firstPlayerVotes);
        }

        private void BroadcastFirstPlayerVoteStatus()
        {
            if (firstPlayerVoteCandidates == null || firstPlayerVotes == null)
            {
                return;
            }

            var status = CreateFirstPlayerVoteStatus();
            FirstPlayerVoteChanged?.Invoke(status);
            SendAll(status);
        }

        private int CountRematchVotes()
        {
            if (!IsRematchPending || rematchVotes == null)
            {
                return 0;
            }

            var count = 0;
            for (var seat = 0; seat < rematchVotes.Length; seat++)
            {
                if (rematchVotes[seat])
                {
                    count++;
                }
            }

            return count;
        }

        private RematchStatusMessage CreateRematchStatus()
        {
            return new RematchStatusMessage(RematchReadyCount, roster.SeatCount);
        }

        private void BroadcastRematchStatus()
        {
            var message = CreateRematchStatus();
            RematchStatusChanged?.Invoke(message);
            SendAll(message);
        }

        private void BroadcastSeatStatuses()
        {
            for (var seat = 0; seat < roster.SeatCount; seat++)
            {
                var entry = roster.Get(seat);
                var message = new SeatStatusMessage(seat, entry.AuthenticationId, entry.DisplayName,
                    entry.Presence, entry.PendingHandBack, (float)entry.GraceRemaining);
                SeatStatusChanged?.Invoke(message);
                SendAll(message);
                lastGraceSeconds[seat] = entry.Presence == SeatPresence.Reconnecting
                    ? (int)Math.Ceiling(entry.GraceRemaining)
                    : -1;
            }
        }

        private void SendAll(NetMessage message)
        {
            for (var seat = 0; seat < roster.SeatCount; seat++)
            {
                var entry = roster.Get(seat);
                if (IsMessageRecipient(seat) && !string.IsNullOrEmpty(entry.AuthenticationId))
                {
                    Send(entry.AuthenticationId, message);
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

            for (var seat = 0; seat < roster.SeatCount; seat++)
            {
                if (string.Equals(roster.Get(seat).AuthenticationId, authenticationId, StringComparison.Ordinal))
                {
                    return seat;
                }
            }

            return -1;
        }

        private bool IsMessageRecipient(int seat)
        {
            var entry = roster.Get(seat);
            return entry.Presence == SeatPresence.Connected || entry.PendingHandBack;
        }

        private void ValidateSeat(int seat)
        {
            if (seat < 0 || seat >= roster.SeatCount)
            {
                throw new ArgumentOutOfRangeException(nameof(seat));
            }
        }

        private bool HasGraceCountdownChanged()
        {
            for (var seat = 0; seat < roster.SeatCount; seat++)
            {
                var entry = roster.Get(seat);
                var seconds = entry.Presence == SeatPresence.Reconnecting
                    ? (int)Math.Ceiling(entry.GraceRemaining)
                    : -1;
                if (seconds != lastGraceSeconds[seat])
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshPresencePauseReason()
        {
            if (PauseReason == Mejnun.Net.PauseReason.SavedByHost ||
                PauseReason == Mejnun.Net.PauseReason.HostAway)
            {
                return;
            }

            SetPauseReason(roster.HasBlockingAbsence
                ? Mejnun.Net.PauseReason.WaitingForPlayers
                : Mejnun.Net.PauseReason.None);
        }

        public void SetPauseReason(PauseReason reason)
        {
            if (!Enum.IsDefined(typeof(PauseReason), reason))
            {
                throw new ArgumentOutOfRangeException(nameof(reason));
            }

            if (reason == Mejnun.Net.PauseReason.None && roster.HasBlockingAbsence)
            {
                reason = Mejnun.Net.PauseReason.WaitingForPlayers;
            }

            if (PauseReason == reason)
            {
                return;
            }

            PauseReason = reason;
            PauseReasonChanged?.Invoke(reason);
            SendAll(new PausedMessage(reason));
            if (reason == Mejnun.Net.PauseReason.SavedByHost)
            {
                SaveSnapshot();
            }
        }
    }
}