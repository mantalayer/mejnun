using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mejnun.AI;
using Unity.Services.CloudSave;
using UnityEngine;

namespace Mejnun.Net.Unity
{
    public sealed class CloudSaveSnapshotStore : ISnapshotStore
    {
        private const string Key = "mejnun_online_session_v1";
        private const int CurrentVersion = 2;
        private SessionRecord cachedRecord;

        public Task PendingSave { get; private set; } = Task.CompletedTask;
        public event Action<Exception> SaveFailed;

        public void Save(SessionRecord record)
        {
            cachedRecord = record ?? throw new ArgumentNullException(nameof(record));
            PendingSave = PersistAsync(record);
        }

        public SessionRecord Load()
        {
            return cachedRecord;
        }

        public async Task<SessionRecord> LoadAsync()
        {
            var items = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { Key });
            if (!items.TryGetValue(Key, out var item) || item?.Value == null)
            {
                cachedRecord = null;
                return null;
            }

            var data = JsonUtility.FromJson<CloudRecord>(item.Value.GetAsString());
            cachedRecord = FromCloudRecord(data);
            return cachedRecord;
        }

        private async Task PersistAsync(SessionRecord record)
        {
            try
            {
                var value = JsonUtility.ToJson(ToCloudRecord(record));
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object>
                {
                    { Key, value }
                });
            }
            catch (Exception exception)
            {
                SaveFailed?.Invoke(exception);
            }
        }

        private static CloudRecord ToCloudRecord(SessionRecord record)
        {
            var records = record.Seats;
            var seats = new CloudSeat[records.Length];
            for (var index = 0; index < records.Length; index++)
            {
                var seat = records[index];
                seats[index] = new CloudSeat
                {
                    AuthenticationId = seat.AuthenticationId,
                    DisplayName = seat.DisplayName,
                    Connected = seat.Connected,
                    IsBot = seat.IsBot,
                    Difficulty = (int)seat.Difficulty,
                    Presence = (int)seat.Presence,
                    PendingHandBack = seat.PendingHandBack
                };
            }

            return new CloudRecord
            {
                Version = CurrentVersion,
                Snapshot = Convert.ToBase64String(record.Snapshot),
                Paused = record.Paused,
                PauseReason = (int)record.PauseReason,
                Seats = seats
            };
        }

        private static SessionRecord FromCloudRecord(CloudRecord data)
        {
            if (data == null || string.IsNullOrEmpty(data.Snapshot) || data.Seats == null)
            {
                throw new InvalidOperationException("Cloud Save contains an invalid Mejnun session record.");
            }

            if (data.Version > CurrentVersion)
            {
                throw new InvalidOperationException("Cloud Save contains an unsupported Mejnun session record version.");
            }

            var seats = new SeatRecord[data.Seats.Length];
            for (var index = 0; index < seats.Length; index++)
            {
                var seat = data.Seats[index];
                var difficulty = seat.Difficulty >= byte.MinValue && seat.Difficulty <= byte.MaxValue
                    ? (BotDifficulty)seat.Difficulty
                    : BotDifficulty.Medium;
                if (!Enum.IsDefined(typeof(BotDifficulty), difficulty))
                {
                    difficulty = BotDifficulty.Medium;
                }

                var presence = data.Version >= CurrentVersion
                    ? ReadPresence(seat.Presence)
                    : ReadLegacyPresence(seat);
                seats[index] = new SeatRecord(seat.AuthenticationId, seat.DisplayName, presence,
                    data.Version >= CurrentVersion && seat.PendingHandBack, difficulty);
            }

            var pauseReason = data.Version >= CurrentVersion
                ? ReadPauseReason(data.PauseReason)
                : data.Paused ? PauseReason.SavedByHost : PauseReason.None;
            return new SessionRecord(Convert.FromBase64String(data.Snapshot), seats, pauseReason);
        }

        private static SeatPresence ReadLegacyPresence(CloudSeat seat)
        {
            if (seat.IsBot)
            {
                return SeatPresence.Bot;
            }

            if (string.IsNullOrEmpty(seat.AuthenticationId))
            {
                return SeatPresence.Empty;
            }

            return seat.Connected ? SeatPresence.Reconnecting : SeatPresence.Disconnected;
        }

        private static SeatPresence ReadPresence(int value)
        {
            if (value < byte.MinValue || value > byte.MaxValue ||
                !Enum.IsDefined(typeof(SeatPresence), (byte)value))
            {
                throw new InvalidOperationException("Cloud Save contains an invalid seat presence value.");
            }

            return (SeatPresence)value;
        }

        private static PauseReason ReadPauseReason(int value)
        {
            if (value < byte.MinValue || value > byte.MaxValue ||
                !Enum.IsDefined(typeof(PauseReason), (byte)value))
            {
                throw new InvalidOperationException("Cloud Save contains an invalid pause reason value.");
            }

            return (PauseReason)value;
        }

        [Serializable]
        private sealed class CloudRecord
        {
            public int Version;
            public string Snapshot;
            public bool Paused;
            public int PauseReason;
            public CloudSeat[] Seats;
        }

        [Serializable]
        private sealed class CloudSeat
        {
            public string AuthenticationId;
            public string DisplayName;
            public bool Connected;
            public bool IsBot;
            public int Difficulty;
            public int Presence;
            public bool PendingHandBack;
        }
    }
}