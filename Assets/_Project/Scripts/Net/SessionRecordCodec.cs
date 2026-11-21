using System;
using System.IO;
using System.Text;
using Mejnun.AI;
using Mejnun.Core;

namespace Mejnun.Net
{
    public static class SessionRecordCodec
    {
        private const uint Magic = 0x4D4A5352;
        private const int Version = 2;
        private const int MaximumSnapshotBytes = 1024 * 1024;
        private const int MaximumSeats = 6;
        private const int MaximumStringLength = 256;

        public static byte[] Write(SessionRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            if (record.Snapshot.Length > MaximumSnapshotBytes)
            {
                throw new InvalidDataException("Session snapshot is too large.");
            }

            var seats = record.Seats;
            if (seats.Length < 2 || seats.Length > MaximumSeats)
            {
                throw new InvalidDataException("Session record contains an invalid seat count.");
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Magic);
                writer.Write(Version);
                writer.Write((byte)record.PauseReason);
                writer.Write(record.Snapshot.Length);
                writer.Write(record.Snapshot);
                writer.Write(seats.Length);
                for (var index = 0; index < seats.Length; index++)
                {
                    var seat = seats[index];
                    WriteString(writer, seat.AuthenticationId);
                    WriteString(writer, seat.DisplayName);
                    writer.Write((byte)seat.Presence);
                    writer.Write(seat.PendingHandBack);
                    writer.Write((byte)seat.Difficulty);
                }

                return stream.ToArray();
            }
        }

        public static SessionRecord Read(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length > MaximumSnapshotBytes + 65536)
            {
                throw new InvalidDataException("Session record is too large.");
            }

            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.ReadUInt32() != Magic)
                {
                    throw new InvalidDataException("Invalid session record header.");
                }

                var version = reader.ReadInt32();
                if (version < 1 || version > Version)
                {
                    throw new InvalidDataException("Invalid session record version.");
                }

                var pauseReason = version == 1
                    ? reader.ReadBoolean() ? PauseReason.SavedByHost : PauseReason.None
                    : ReadEnum<PauseReason>(reader, "pause reason");
                var snapshotLength = reader.ReadInt32();
                if (snapshotLength < 0 || snapshotLength > MaximumSnapshotBytes)
                {
                    throw new InvalidDataException("Invalid session snapshot length.");
                }

                var snapshot = reader.ReadBytes(snapshotLength);
                if (snapshot.Length != snapshotLength)
                {
                    throw new EndOfStreamException("The session record ended while reading its snapshot.");
                }

                SnapshotSerializer.Read(snapshot);
                var seatCount = reader.ReadInt32();
                if (seatCount < 2 || seatCount > MaximumSeats)
                {
                    throw new InvalidDataException("Invalid session seat count.");
                }

                var seats = new SeatRecord[seatCount];
                for (var index = 0; index < seatCount; index++)
                {
                    var authenticationId = ReadString(reader);
                    var displayName = ReadString(reader);
                    var presence = version == 1
                        ? ReadLegacyPresence(reader, authenticationId)
                        : ReadEnum<SeatPresence>(reader, "seat presence");
                    var pendingHandBack = version > 1 && reader.ReadBoolean();
                    var difficultyValue = reader.ReadByte();
                    var difficulty = (BotDifficulty)difficultyValue;
                    if (!Enum.IsDefined(typeof(BotDifficulty), difficulty))
                    {
                        throw new InvalidDataException("Invalid bot difficulty in session record.");
                    }

                    seats[index] = new SeatRecord(authenticationId, displayName, presence, pendingHandBack,
                        difficulty);
                }

                if (stream.Position != stream.Length)
                {
                    throw new InvalidDataException("Unexpected trailing data in session record.");
                }

                return new SessionRecord(snapshot, seats, pauseReason);
            }
        }

        public static bool TryRead(byte[] bytes, out SessionRecord record)
        {
            record = null;
            if (bytes == null || bytes.Length == 0)
            {
                return false;
            }

            try
            {
                record = Read(bytes);
                return true;
            }
            catch (Exception)
            {
                record = null;
                return false;
            }
        }

        private static SeatPresence ReadLegacyPresence(BinaryReader reader, string authenticationId)
        {
            var connected = reader.ReadBoolean();
            var isBot = reader.ReadBoolean();
            if (isBot)
            {
                return SeatPresence.Bot;
            }

            if (string.IsNullOrEmpty(authenticationId))
            {
                return SeatPresence.Empty;
            }

            return connected ? SeatPresence.Reconnecting : SeatPresence.Disconnected;
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            value = value ?? string.Empty;
            if (value.Length > MaximumStringLength)
            {
                throw new InvalidDataException("Session string is too long.");
            }

            writer.Write(value);
        }

        private static string ReadString(BinaryReader reader)
        {
            var value = reader.ReadString();
            if (value.Length > MaximumStringLength)
            {
                throw new InvalidDataException("Session string is too long.");
            }

            return value;
        }

        private static T ReadEnum<T>(BinaryReader reader, string description) where T : struct
        {
            var value = reader.ReadByte();
            var enumValue = Enum.ToObject(typeof(T), value);
            if (!Enum.IsDefined(typeof(T), enumValue))
            {
                throw new InvalidDataException("Invalid " + description + " in session record.");
            }

            return (T)enumValue;
        }
    }
}