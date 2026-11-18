using System;
using System.Collections.Generic;
using System.IO;
using Mejnun.AI;
using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Game
{
    public sealed class SavedGameData
    {
        private readonly SeatConfig[] seats;
        private readonly byte[] snapshot;

        public SavedGameData(IReadOnlyList<SeatConfig> seats, int turnSeconds, ulong roundSeed, byte[] snapshot)
        {
            if (seats == null)
            {
                throw new ArgumentNullException(nameof(seats));
            }

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            this.seats = new SeatConfig[seats.Count];
            for (var index = 0; index < seats.Count; index++)
            {
                this.seats[index] = seats[index];
            }

            this.snapshot = (byte[])snapshot.Clone();
            TurnSeconds = turnSeconds;
            RoundSeed = roundSeed;
        }

        public IReadOnlyList<SeatConfig> Seats => seats;
        public int TurnSeconds { get; }
        public ulong RoundSeed { get; }
        public byte[] Snapshot => (byte[])snapshot.Clone();
    }

    public static class SavedGame
    {
        private const uint Magic = 0x4D4A5347;
        private const int Version = 1;
        private const int MaximumSnapshotBytes = 16 * 1024 * 1024;
        private const int MaximumPlayerNameLength = 128;
        private const string FileName = "mejnun_save.bin";

        public static bool HasSave => File.Exists(SavePath);

        public static bool Save(MatchFlow matchFlow)
        {
            if (matchFlow == null || matchFlow.Game == null || matchFlow.Game.State.Phase != GamePhase.Playing)
            {
                return false;
            }

            var temporaryPath = string.Empty;
            try
            {
                var data = new SavedGameData(matchFlow.Seats, matchFlow.TurnSeconds, matchFlow.RoundSeed,
                    SnapshotSerializer.Write(matchFlow.Game));
                var path = SavePath;
                temporaryPath = path + ".tmp";
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(temporaryPath, Encode(data));
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(temporaryPath, path);
                return true;
            }
            catch (Exception exception)
            {
                if (!string.IsNullOrEmpty(temporaryPath))
                {
                    TryDelete(temporaryPath);
                }

                Debug.LogError("Could not save the current Mejnun game: " + exception.Message);
                return false;
            }
        }

        public static bool TryLoad(out SavedGameData data)
        {
            data = null;
            var path = SavePath;
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                var length = new FileInfo(path).Length;
                if (length <= 0 || length > MaximumSnapshotBytes + 4096)
                {
                    throw new InvalidDataException("Invalid saved game size.");
                }

                data = Decode(File.ReadAllBytes(path));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Discarding an invalid saved Mejnun game: " + exception.Message);
                Delete();
                data = null;
                return false;
            }
        }

        public static void Delete()
        {
            TryDelete(SavePath);
        }

        public static byte[] Encode(SavedGameData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            Validate(data);
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(Magic);
                    writer.Write(Version);
                    writer.Write(data.Seats.Count);
                    for (var index = 0; index < data.Seats.Count; index++)
                    {
                        var seat = data.Seats[index];
                        writer.Write(seat.Name);
                        writer.Write(seat.IsBot);
                        writer.Write((byte)seat.Difficulty);
                        writer.Write((byte)seat.Style);
                    }

                    writer.Write(data.TurnSeconds);
                    writer.Write(data.RoundSeed);
                    var snapshot = data.Snapshot;
                    writer.Write(snapshot.Length);
                    writer.Write(snapshot);
                }

                return stream.ToArray();
            }
        }

        public static SavedGameData Decode(byte[] encoded)
        {
            if (encoded == null)
            {
                throw new ArgumentNullException(nameof(encoded));
            }

            if (encoded.Length <= 0 || encoded.Length > MaximumSnapshotBytes + 4096)
            {
                throw new InvalidDataException("Invalid saved game size.");
            }

            using (var stream = new MemoryStream(encoded, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadUInt32() != Magic || reader.ReadInt32() != Version)
                {
                    throw new InvalidDataException("Invalid saved game header.");
                }

                var seatCount = reader.ReadInt32();
                if (seatCount < 2 || seatCount > 6)
                {
                    throw new InvalidDataException("Invalid saved game seat count.");
                }

                var seats = new SeatConfig[seatCount];
                var hasHuman = false;
                for (var index = 0; index < seats.Length; index++)
                {
                    var name = reader.ReadString();
                    var isBot = reader.ReadBoolean();
                    var difficulty = ReadEnum<BotDifficulty>(reader, "bot difficulty");
                    var style = ReadEnum<BotStyle>(reader, "bot style");
                    if (name.Length == 0 || name.Length > MaximumPlayerNameLength)
                    {
                        throw new InvalidDataException("Invalid saved game player name.");
                    }

                    seats[index] = new SeatConfig(name, isBot, difficulty, style);
                    hasHuman |= !isBot;
                }

                var turnSeconds = reader.ReadInt32();
                if (turnSeconds < 0 || turnSeconds > 3600 || !hasHuman)
                {
                    throw new InvalidDataException("Invalid saved game settings.");
                }

                var roundSeed = reader.ReadUInt64();
                var snapshotLength = reader.ReadInt32();
                if (snapshotLength <= 0 || snapshotLength > MaximumSnapshotBytes)
                {
                    throw new InvalidDataException("Invalid saved game snapshot size.");
                }

                var snapshot = reader.ReadBytes(snapshotLength);
                if (snapshot.Length != snapshotLength || stream.Position != stream.Length)
                {
                    throw new InvalidDataException("Incomplete saved game data.");
                }

                var data = new SavedGameData(seats, turnSeconds, roundSeed, snapshot);
                Validate(data);
                return data;
            }
        }

        private static void Validate(SavedGameData data)
        {
            if (data.Seats.Count < 2 || data.Seats.Count > 6 || data.TurnSeconds < 0 || data.TurnSeconds > 3600)
            {
                throw new InvalidDataException("Invalid saved game settings.");
            }

            var hasHuman = false;
            for (var index = 0; index < data.Seats.Count; index++)
            {
                var seat = data.Seats[index];
                if (string.IsNullOrWhiteSpace(seat.Name) || seat.Name.Length > MaximumPlayerNameLength ||
                    !Enum.IsDefined(typeof(BotDifficulty), seat.Difficulty) ||
                    !Enum.IsDefined(typeof(BotStyle), seat.Style))
                {
                    throw new InvalidDataException("Invalid saved game seat configuration.");
                }

                hasHuman |= !seat.IsBot;
            }

            if (!hasHuman)
            {
                throw new InvalidDataException("A saved game must include a human player.");
            }

            var snapshot = data.Snapshot;
            if (snapshot.Length <= 0 || snapshot.Length > MaximumSnapshotBytes)
            {
                throw new InvalidDataException("Invalid saved game snapshot size.");
            }

            var game = SnapshotSerializer.Read(snapshot);
            if (game.State.Phase != GamePhase.Playing || game.Setup.SeatCount != data.Seats.Count ||
                game.Setup.Seed != data.RoundSeed)
            {
                throw new InvalidDataException("Saved game settings do not match its snapshot.");
            }
        }

        private static T ReadEnum<T>(BinaryReader reader, string description) where T : struct
        {
            var value = reader.ReadByte();
            var enumValue = Enum.ToObject(typeof(T), value);
            if (!Enum.IsDefined(typeof(T), enumValue))
            {
                throw new InvalidDataException("Invalid " + description + " in saved game.");
            }

            return (T)enumValue;
        }

        private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException exception)
            {
                Debug.LogWarning("Could not delete a saved Mejnun game: " + exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogWarning("Could not delete a saved Mejnun game: " + exception.Message);
            }
        }
    }
}