using System;
using Mejnun.AI;

namespace Mejnun.Net
{
    public interface ISnapshotStore
    {
        void Save(SessionRecord record);
        SessionRecord Load();
    }

    public sealed class SessionRecord
    {
        private readonly SeatRecord[] seats;

        public SessionRecord(byte[] snapshot, SeatRecord[] seats, bool paused)
            : this(snapshot, seats, paused ? PauseReason.SavedByHost : PauseReason.None)
        {
        }

        public SessionRecord(byte[] snapshot, SeatRecord[] seats, PauseReason pauseReason)
        {
            Snapshot = snapshot == null ? throw new ArgumentNullException(nameof(snapshot)) : (byte[])snapshot.Clone();
            this.seats = seats == null ? throw new ArgumentNullException(nameof(seats)) : (SeatRecord[])seats.Clone();
            if (!Enum.IsDefined(typeof(PauseReason), pauseReason))
            {
                throw new ArgumentOutOfRangeException(nameof(pauseReason));
            }

            PauseReason = pauseReason;
        }

        public byte[] Snapshot { get; }
        public SeatRecord[] Seats => (SeatRecord[])seats.Clone();
        public PauseReason PauseReason { get; }
        public bool Paused => PauseReason != Mejnun.Net.PauseReason.None;
    }

    public sealed class SeatRecord
    {
        public SeatRecord(string authenticationId, string displayName, bool connected, bool isBot,
            BotDifficulty difficulty)
            : this(authenticationId, displayName, isBot ? SeatPresence.Bot :
                connected ? SeatPresence.Connected : string.IsNullOrEmpty(authenticationId)
                    ? SeatPresence.Empty
                    : SeatPresence.Disconnected, false, difficulty)
        {
        }

        public SeatRecord(string authenticationId, string displayName, SeatPresence presence,
            bool pendingHandBack, BotDifficulty difficulty)
        {
            AuthenticationId = authenticationId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            if (!Enum.IsDefined(typeof(SeatPresence), presence))
            {
                throw new ArgumentOutOfRangeException(nameof(presence));
            }

            Presence = presence;
            PendingHandBack = pendingHandBack;
            Difficulty = difficulty;
        }

        public string AuthenticationId { get; }
        public string DisplayName { get; }
        public SeatPresence Presence { get; }
        public bool PendingHandBack { get; }
        public bool Connected => Presence == SeatPresence.Connected;
        public bool IsBot => Presence == SeatPresence.Bot || Presence == SeatPresence.BotControlled;
        public BotDifficulty Difficulty { get; }
    }

    public sealed class InMemorySnapshotStore : ISnapshotStore
    {
        private SessionRecord record;

        public void Save(SessionRecord value)
        {
            record = value ?? throw new ArgumentNullException(nameof(value));
        }

        public SessionRecord Load()
        {
            return record;
        }
    }
}