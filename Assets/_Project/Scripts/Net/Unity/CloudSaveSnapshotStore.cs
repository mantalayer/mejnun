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
                    Difficulty = (int)seat.Difficulty
                };
            }

            return new CloudRecord
            {
                Snapshot = Convert.ToBase64String(record.Snapshot),
                Paused = record.Paused,
                Seats = seats
            };
        }

        private static SessionRecord FromCloudRecord(CloudRecord data)
        {
            if (data == null || string.IsNullOrEmpty(data.Snapshot) || data.Seats == null)
            {
                throw new InvalidOperationException("Cloud Save contains an invalid Mejnun session record.");
            }

            var seats = new SeatRecord[data.Seats.Length];
            for (var index = 0; index < seats.Length; index++)
            {
                var seat = data.Seats[index];
                var difficulty = seat.Difficulty >= byte.MinValue && seat.Difficulty <= byte.MaxValue
                    ? (BotDifficulty)seat.Difficulty
                    : BotDifficulty.Medium;
                seats[index] = new SeatRecord(seat.AuthenticationId, seat.DisplayName, seat.Connected,
                    seat.IsBot, Enum.IsDefined(typeof(BotDifficulty), difficulty)
                        ? difficulty
                        : BotDifficulty.Medium);
            }

            return new SessionRecord(Convert.FromBase64String(data.Snapshot), seats, data.Paused);
        }

        [Serializable]
        private sealed class CloudRecord
        {
            public string Snapshot;
            public bool Paused;
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
        }
    }
}