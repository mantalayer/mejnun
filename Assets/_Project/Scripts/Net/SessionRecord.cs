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
        {
            Snapshot = snapshot == null ? throw new ArgumentNullException(nameof(snapshot)) : (byte[])snapshot.Clone();
            this.seats = seats == null ? throw new ArgumentNullException(nameof(seats)) : (SeatRecord[])seats.Clone();
            Paused = paused;
        }

        public byte[] Snapshot { get; }
        public SeatRecord[] Seats => (SeatRecord[])seats.Clone();
        public bool Paused { get; }
    }

    public sealed class SeatRecord
    {
        public SeatRecord(string authenticationId, string displayName, bool connected, bool isBot,
            BotDifficulty difficulty)
        {
            AuthenticationId = authenticationId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Connected = connected;
            IsBot = isBot;
            Difficulty = difficulty;
        }

        public string AuthenticationId { get; }
        public string DisplayName { get; }
        public bool Connected { get; }
        public bool IsBot { get; }
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