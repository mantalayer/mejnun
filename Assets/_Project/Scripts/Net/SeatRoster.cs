using System;
using System.Collections.Generic;
using Mejnun.AI;

namespace Mejnun.Net
{
    public sealed class SeatRoster
    {
        public const double DefaultGraceSeconds = 20;

        private readonly Seat[] seats;
        private readonly double graceSeconds;

        public SeatRoster(int seatCount, double graceSeconds = DefaultGraceSeconds)
        {
            if (seatCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(seatCount));
            }

            if (graceSeconds <= 0 || double.IsNaN(graceSeconds) || double.IsInfinity(graceSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(graceSeconds));
            }

            this.graceSeconds = graceSeconds;
            seats = new Seat[seatCount];
            for (var index = 0; index < seats.Length; index++)
            {
                seats[index] = new Seat();
            }
        }

        public int SeatCount => seats.Length;

        public bool HasBlockingAbsence
        {
            get
            {
                for (var index = 0; index < seats.Length; index++)
                {
                    if (seats[index].Presence == SeatPresence.Reconnecting ||
                        seats[index].Presence == SeatPresence.Disconnected)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool HasReconnectingSeats
        {
            get
            {
                for (var index = 0; index < seats.Length; index++)
                {
                    if (seats[index].Presence == SeatPresence.Reconnecting)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public int Claim(string authenticationId, string displayName, int currentSeat, out bool pendingHandBack)
        {
            pendingHandBack = false;
            if (string.IsNullOrWhiteSpace(authenticationId))
            {
                return -1;
            }

            var seatIndex = FindSeat(authenticationId);
            if (seatIndex < 0)
            {
                seatIndex = FindClaimableSeat();
            }

            if (seatIndex < 0)
            {
                return -1;
            }

            var seat = seats[seatIndex];
            var wasBotControlled = seat.Presence == SeatPresence.BotControlled;
            seat.AuthenticationId = authenticationId;
            seat.DisplayName = displayName ?? string.Empty;
            seat.GraceRemaining = 0;
            if (wasBotControlled && seatIndex == currentSeat)
            {
                seat.PendingHandBack = true;
                pendingHandBack = true;
            }
            else
            {
                seat.Presence = SeatPresence.Connected;
                seat.PendingHandBack = false;
            }

            return seatIndex;
        }

        public void MarkAway(string authenticationId)
        {
            MarkReconnecting(authenticationId);
        }

        public void MarkAway(string authenticationId, double reconnectGraceSeconds)
        {
            ValidateGrace(reconnectGraceSeconds);
            var seatIndex = FindSeat(authenticationId);
            if (seatIndex < 0)
            {
                return;
            }

            var seat = seats[seatIndex];
            if (seat.Presence == SeatPresence.Bot)
            {
                return;
            }

            seat.Presence = SeatPresence.Reconnecting;
            seat.PendingHandBack = false;
            seat.GraceRemaining = reconnectGraceSeconds;
        }

        public void MarkDropped(string authenticationId)
        {
            MarkReconnecting(authenticationId);
        }

        public void SetBotControl(int seatIndex, BotDifficulty difficulty)
        {
            ValidateSeat(seatIndex);
            if (!Enum.IsDefined(typeof(BotDifficulty), difficulty))
            {
                throw new ArgumentOutOfRangeException(nameof(difficulty));
            }

            var seat = seats[seatIndex];
            if (seat.Presence != SeatPresence.Disconnected)
            {
                throw new InvalidOperationException("Only a disconnected seat can be taken over by a bot.");
            }

            seat.Presence = SeatPresence.BotControlled;
            seat.PendingHandBack = false;
            seat.GraceRemaining = 0;
            seat.Difficulty = difficulty;
        }

        public void AddBot(int seatIndex, string displayName, BotDifficulty difficulty)
        {
            ValidateSeat(seatIndex);
            if (!Enum.IsDefined(typeof(BotDifficulty), difficulty))
            {
                throw new ArgumentOutOfRangeException(nameof(difficulty));
            }

            var seat = seats[seatIndex];
            if (seat.Presence != SeatPresence.Empty)
            {
                throw new InvalidOperationException("The seat is already assigned.");
            }

            seat.DisplayName = displayName ?? "Bot";
            seat.Presence = SeatPresence.Bot;
            seat.Difficulty = difficulty;
        }

        public void Restore(int seatIndex, SeatRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            ValidateSeat(seatIndex);
            var seat = seats[seatIndex];
            seat.AuthenticationId = record.AuthenticationId;
            seat.DisplayName = record.DisplayName;
            seat.Presence = record.Presence;
            seat.Difficulty = record.Difficulty;
            seat.PendingHandBack = record.PendingHandBack;
            seat.GraceRemaining = record.Presence == SeatPresence.Reconnecting ? graceSeconds : 0;
        }

        public void CompleteHandBacks(int currentSeat)
        {
            ValidateSeat(currentSeat);
            for (var index = 0; index < seats.Length; index++)
            {
                var seat = seats[index];
                if (seat.PendingHandBack && index != currentSeat)
                {
                    seat.Presence = SeatPresence.Connected;
                    seat.PendingHandBack = false;
                }
            }
        }

        public IReadOnlyList<int> Tick(double elapsedSeconds)
        {
            if (elapsedSeconds < 0 || double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            var expired = new List<int>();
            for (var index = 0; index < seats.Length; index++)
            {
                var seat = seats[index];
                if (seat.Presence != SeatPresence.Reconnecting)
                {
                    continue;
                }

                seat.GraceRemaining = Math.Max(0, seat.GraceRemaining - elapsedSeconds);
                if (seat.GraceRemaining <= 0)
                {
                    seat.Presence = SeatPresence.Disconnected;
                    expired.Add(index);
                }
            }

            return expired.AsReadOnly();
        }

        public SeatState Get(int seatIndex)
        {
            ValidateSeat(seatIndex);
            var seat = seats[seatIndex];
            return new SeatState(seatIndex, seat.AuthenticationId, seat.DisplayName, seat.Presence,
                seat.Difficulty, seat.PendingHandBack, seat.GraceRemaining);
        }

        public bool HasReadySeat(int seatIndex)
        {
            ValidateSeat(seatIndex);
            var presence = seats[seatIndex].Presence;
            return presence == SeatPresence.Connected || presence == SeatPresence.BotControlled ||
                   presence == SeatPresence.Bot;
        }

        public bool IsConnected(string authenticationId)
        {
            var seatIndex = FindSeat(authenticationId);
            return seatIndex >= 0 && seats[seatIndex].Presence == SeatPresence.Connected;
        }

        private void MarkReconnecting(string authenticationId)
        {
            var seatIndex = FindSeat(authenticationId);
            if (seatIndex < 0)
            {
                return;
            }

            var seat = seats[seatIndex];
            if (seat.Presence == SeatPresence.Connected)
            {
                seat.Presence = SeatPresence.Reconnecting;
                seat.GraceRemaining = graceSeconds;
            }
            else if (seat.Presence == SeatPresence.BotControlled && seat.PendingHandBack)
            {
                seat.PendingHandBack = false;
            }
        }

        private static void ValidateGrace(double reconnectGraceSeconds)
        {
            if (reconnectGraceSeconds < 0 || double.IsNaN(reconnectGraceSeconds) ||
                double.IsInfinity(reconnectGraceSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(reconnectGraceSeconds));
            }
        }

        private int FindSeat(string authenticationId)
        {
            if (string.IsNullOrEmpty(authenticationId))
            {
                return -1;
            }

            for (var index = 0; index < seats.Length; index++)
            {
                if (string.Equals(seats[index].AuthenticationId, authenticationId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindClaimableSeat()
        {
            for (var index = 0; index < seats.Length; index++)
            {
                if (seats[index].Presence == SeatPresence.Disconnected ||
                    seats[index].Presence == SeatPresence.BotControlled)
                {
                    return index;
                }
            }

            for (var index = 0; index < seats.Length; index++)
            {
                if (seats[index].Presence == SeatPresence.Empty)
                {
                    return index;
                }
            }

            return -1;
        }

        private void ValidateSeat(int seatIndex)
        {
            if (seatIndex < 0 || seatIndex >= seats.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(seatIndex));
            }
        }

        private sealed class Seat
        {
            public string AuthenticationId = string.Empty;
            public string DisplayName = string.Empty;
            public SeatPresence Presence = SeatPresence.Empty;
            public BotDifficulty Difficulty = BotDifficulty.Medium;
            public bool PendingHandBack;
            public double GraceRemaining;
        }
    }

    public sealed class SeatState
    {
        public SeatState(int seat, string authenticationId, string displayName, SeatPresence presence,
            BotDifficulty difficulty, bool pendingHandBack, double graceRemaining)
        {
            Seat = seat;
            AuthenticationId = authenticationId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Presence = presence;
            Difficulty = difficulty;
            PendingHandBack = pendingHandBack;
            GraceRemaining = graceRemaining;
        }

        public int Seat { get; }
        public string AuthenticationId { get; }
        public string DisplayName { get; }
        public SeatPresence Presence { get; }
        public BotDifficulty Difficulty { get; }
        public bool PendingHandBack { get; }
        public double GraceRemaining { get; }
    }
}