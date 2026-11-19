using System;
using Mejnun.Net;
using Unity.Services.Multiplayer;

namespace Mejnun.Net.Unity
{
    public sealed class OnlineSessionMigrationDataHandler : IMigrationDataHandler
    {
        public HostSession AuthoritativeSession { get; set; }
        public SessionRecord RestoredRecord { get; private set; }
        public event Action<SessionRecord> DataRestored;

        public byte[] Generate()
        {
            return AuthoritativeSession == null
                ? Array.Empty<byte>()
                : SessionRecordCodec.Write(AuthoritativeSession.CreateRecord());
        }

        public void Apply(byte[] migrationData)
        {
            if (migrationData == null || migrationData.Length == 0)
            {
                RestoredRecord = null;
                return;
            }

            RestoredRecord = SessionRecordCodec.Read(migrationData);
            DataRestored?.Invoke(RestoredRecord);
        }
    }
}