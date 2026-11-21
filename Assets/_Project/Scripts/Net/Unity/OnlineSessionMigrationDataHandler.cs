using System;
using Mejnun.Net;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Mejnun.Net.Unity
{
    public sealed class OnlineSessionMigrationDataHandler : IMigrationDataHandler
    {
        private byte[] lastNonEmptyMigrationData;

        public HostSession AuthoritativeSession { get; set; }
        public SessionRecord RestoredRecord { get; private set; }
        public event Action<SessionRecord> DataRestored;

        public byte[] Generate()
        {
            try
            {
                if (AuthoritativeSession == null)
                {
                    return GetLastGoodData();
                }

                var migrationData = SessionRecordCodec.Write(AuthoritativeSession.CreateRecord());
                if (migrationData.Length > 0)
                {
                    lastNonEmptyMigrationData = (byte[])migrationData.Clone();
                    return migrationData;
                }

                return GetLastGoodData();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Generating online migration data failed: " + exception.Message);
                return GetLastGoodData();
            }
        }

        public void Apply(byte[] migrationData)
        {
            if (!SessionRecordCodec.TryRead(migrationData, out var record))
            {
                return;
            }

            lastNonEmptyMigrationData = (byte[])migrationData.Clone();
            RestoredRecord = record;
            try
            {
                DataRestored?.Invoke(RestoredRecord);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Applying online migration data failed: " + exception.Message);
            }
        }

        private byte[] GetLastGoodData()
        {
            return lastNonEmptyMigrationData == null
                ? Array.Empty<byte>()
                : (byte[])lastNonEmptyMigrationData.Clone();
        }
    }
}