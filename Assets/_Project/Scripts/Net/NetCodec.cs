using System;
using System.IO;
using System.Text;
using Mejnun.Core;

namespace Mejnun.Net
{
    public static class NetCodec
    {
        private const uint Magic = 0x4D4A4E4D;
        private const int MaximumSnapshotBytes = 1024 * 1024;
        private const int MaximumCommands = 256;
        private const int MaximumStringLength = 256;

        public static byte[] Encode(NetMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Magic);
                writer.Write((byte)message.Kind);
                switch (message.Kind)
                {
                    case NetMessageKind.Join:
                    {
                        var join = (JoinMessage)message;
                        WriteString(writer, join.AuthenticationId);
                        WriteString(writer, join.DisplayName);
                        break;
                    }
                    case NetMessageKind.Welcome:
                    {
                        var welcome = (WelcomeMessage)message;
                        writer.Write(welcome.Seat);
                        WriteBytes(writer, welcome.Snapshot);
                        break;
                    }
                    case NetMessageKind.StateUpdate:
                        WriteBytes(writer, ((StateUpdateMessage)message).Snapshot);
                        break;
                    case NetMessageKind.Commands:
                    {
                        var commands = ((CommandsMessage)message).Commands;
                        if (commands.Count == 0 || commands.Count > MaximumCommands)
                        {
                            throw new InvalidDataException("Command group has an invalid number of commands.");
                        }

                        writer.Write(commands.Count);
                        for (var index = 0; index < commands.Count; index++)
                        {
                            WriteCommand(writer, commands[index]);
                        }

                        break;
                    }
                    case NetMessageKind.Rejected:
                    {
                        var rejected = (RejectedMessage)message;
                        writer.Write(rejected.Code.HasValue);
                        if (rejected.Code.HasValue)
                        {
                            writer.Write((byte)rejected.Code.Value);
                        }

                        WriteString(writer, rejected.DebugReason);
                        break;
                    }
                    case NetMessageKind.SeatStatus:
                    {
                        var status = (SeatStatusMessage)message;
                        writer.Write(status.Seat);
                        WriteString(writer, status.AuthenticationId);
                        WriteString(writer, status.DisplayName);
                        writer.Write((byte)status.Presence);
                        writer.Write(status.PendingHandBack);
                        writer.Write(status.GraceRemaining);
                        break;
                    }
                    case NetMessageKind.Paused:
                    {
                        var paused = (PausedMessage)message;
                        writer.Write((byte)paused.Reason);
                        break;
                    }
                    case NetMessageKind.Away:
                        break;
                    case NetMessageKind.Rematch:
                        break;
                    case NetMessageKind.RematchStatus:
                    {
                        var status = (RematchStatusMessage)message;
                        writer.Write(status.ReadySeats);
                        writer.Write(status.SeatCount);
                        break;
                    }
                    default:
                        throw new InvalidDataException("Unsupported network message.");
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        public static NetMessage Decode(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length > MaximumSnapshotBytes + 65536)
            {
                throw new InvalidDataException("Network message is too large.");
            }

            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.ReadUInt32() != Magic)
                {
                    throw new InvalidDataException("Invalid network message header.");
                }

                var kind = ReadEnum<NetMessageKind>(reader, "message kind");
                NetMessage message;
                switch (kind)
                {
                    case NetMessageKind.Join:
                        message = new JoinMessage(ReadString(reader), ReadString(reader));
                        break;
                    case NetMessageKind.Welcome:
                        message = new WelcomeMessage(reader.ReadInt32(), ReadBytes(reader));
                        break;
                    case NetMessageKind.StateUpdate:
                        message = new StateUpdateMessage(ReadBytes(reader));
                        break;
                    case NetMessageKind.Commands:
                    {
                        var count = ReadCount(reader, MaximumCommands, "commands");
                        if (count == 0)
                        {
                            throw new InvalidDataException("Command group cannot be empty.");
                        }

                        var commands = new GameCommand[count];
                        for (var index = 0; index < count; index++)
                        {
                            commands[index] = ReadCommand(reader);
                        }

                        message = new CommandsMessage(commands);
                        break;
                    }
                    case NetMessageKind.Rejected:
                    {
                        var code = reader.ReadBoolean() ? (JoinRejectReason?)ReadEnum<JoinRejectReason>(reader,
                            "join reject reason") : null;
                        message = new RejectedMessage(code, ReadString(reader));
                        break;
                    }
                    case NetMessageKind.SeatStatus:
                        message = new SeatStatusMessage(reader.ReadInt32(), ReadString(reader), ReadString(reader),
                            ReadEnum<SeatPresence>(reader, "seat presence"), reader.ReadBoolean(), reader.ReadSingle());
                        break;
                    case NetMessageKind.Paused:
                        message = new PausedMessage(ReadEnum<PauseReason>(reader, "pause reason"));
                        break;
                    case NetMessageKind.Away:
                        message = new AwayMessage();
                        break;
                    case NetMessageKind.Rematch:
                        message = new RematchMessage();
                        break;
                    case NetMessageKind.RematchStatus:
                        message = new RematchStatusMessage(reader.ReadInt32(), reader.ReadInt32());
                        break;
                    default:
                        throw new InvalidDataException("Unsupported network message.");
                }

                if (stream.Position != stream.Length)
                {
                    throw new InvalidDataException("Unexpected trailing data in network message.");
                }

                return message;
            }
        }

        private static void WriteCommand(BinaryWriter writer, GameCommand command)
        {
            writer.Write((byte)command.Type);
            writer.Write(command.Seat);
            var cardIds = command.CardIds;
            if (cardIds.Length > Card.CardCount)
            {
                throw new InvalidDataException("Command contains too many card ids.");
            }

            writer.Write(cardIds.Length);
            writer.Write(cardIds);
            writer.Write((byte)command.Target.Kind);
            writer.Write(command.Target.MeldId);
            writer.Write(command.Target.Index);
            writer.Write(command.MeldId);
            writer.Write(command.OtherMeldId);
            writer.Write(command.Index);
        }

        private static GameCommand ReadCommand(BinaryReader reader)
        {
            var type = ReadEnum<CommandType>(reader, "command type");
            var seat = reader.ReadInt32();
            var cardCount = ReadCount(reader, Card.CardCount, "command cards");
            var ids = reader.ReadBytes(cardCount);
            if (ids.Length != cardCount)
            {
                throw new EndOfStreamException("The network message ended while reading command cards.");
            }

            for (var index = 0; index < ids.Length; index++)
            {
                if (ids[index] >= Card.CardCount)
                {
                    throw new InvalidDataException("Command contains an invalid card id.");
                }
            }

            var targetKind = ReadEnum<TargetKind>(reader, "target kind");
            var target = new CardTarget(targetKind, reader.ReadInt32(), reader.ReadInt32());
            return new GameCommand(type, seat, ids, target, reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        }

        private static void WriteBytes(BinaryWriter writer, byte[] bytes)
        {
            if (bytes.Length > MaximumSnapshotBytes)
            {
                throw new InvalidDataException("Snapshot is too large.");
            }

            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static byte[] ReadBytes(BinaryReader reader)
        {
            var count = ReadCount(reader, MaximumSnapshotBytes, "snapshot bytes");
            var bytes = reader.ReadBytes(count);
            if (bytes.Length != count)
            {
                throw new EndOfStreamException("The network message ended while reading a snapshot.");
            }

            return bytes;
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            if (value.Length > MaximumStringLength)
            {
                throw new InvalidDataException("Network string is too long.");
            }

            writer.Write(value);
        }

        private static string ReadString(BinaryReader reader)
        {
            var value = reader.ReadString();
            if (value.Length > MaximumStringLength)
            {
                throw new InvalidDataException("Network string is too long.");
            }

            return value;
        }

        private static int ReadCount(BinaryReader reader, int maximum, string description)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > maximum)
            {
                throw new InvalidDataException("Invalid " + description + " count.");
            }

            return count;
        }

        private static T ReadEnum<T>(BinaryReader reader, string description) where T : struct
        {
            var value = reader.ReadByte();
            var enumValue = Enum.ToObject(typeof(T), value);
            if (!Enum.IsDefined(typeof(T), enumValue))
            {
                throw new InvalidDataException("Invalid " + description + ".");
            }

            return (T)enumValue;
        }
    }
}