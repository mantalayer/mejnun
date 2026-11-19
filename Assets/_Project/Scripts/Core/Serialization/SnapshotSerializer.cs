using System;
using System.Collections.Generic;
using System.IO;

namespace Mejnun.Core
{
    public static class SnapshotSerializer
    {
        private const uint Magic = 0x4D4A4E31;
        private const int MaximumSerializedMoves = 100000;

        public const int Version = 2;

        public static byte[] Write(MejnunGame game)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            return WriteSnapshot(game, false);
        }

        public static byte[] WriteForSeat(MejnunGame game, int viewerSeat)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            if (viewerSeat < 0 || viewerSeat >= game.Setup.SeatCount)
            {
                throw new ArgumentOutOfRangeException(nameof(viewerSeat));
            }

            var redacted = Read(Write(game));
            RedactHiddenCards(redacted, viewerSeat);
            redacted.State.Rng = Pcg32.FromState(0UL, 1UL);

            if (redacted.Turn != null && redacted.Turn.Seat != viewerSeat)
            {
                redacted.RestoreTurnSession(new BoardSnapshot(redacted.State, redacted.State.CurrentSeat),
                    new List<Move>());
            }

            return WriteSnapshot(redacted, true);
        }

        private static byte[] WriteSnapshot(MejnunGame game, bool redactSeed)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                {
                    writer.Write(Magic);
                    writer.Write(Version);
                    WriteSetup(writer, game.Setup, redactSeed);
                    WriteState(writer, game.State);
                    WriteTurn(writer, game.Turn);
                }

                return stream.ToArray();
            }
        }

        private static void RedactHiddenCards(MejnunGame game, int viewerSeat)
        {
            var state = game.State;
            var visible = new bool[Card.CardCount];
            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                var cards = state.Board[meldIndex].Cards;
                for (var cardIndex = 0; cardIndex < cards.Count; cardIndex++)
                {
                    visible[cards[cardIndex].Id] = true;
                }
            }

            for (var cardIndex = 0; cardIndex < state.Hands[viewerSeat].Count; cardIndex++)
            {
                visible[state.Hands[viewerSeat][cardIndex].Id] = true;
            }

            var canonicalIds = new List<byte>();
            for (byte cardId = 0; cardId < Card.CardCount; cardId++)
            {
                if (!visible[cardId])
                {
                    canonicalIds.Add(cardId);
                }
            }

            var nextCard = 0;
            for (var seat = 0; seat < state.Hands.Count; seat++)
            {
                if (seat == viewerSeat)
                {
                    continue;
                }

                var hand = state.Hands[seat];
                for (var cardIndex = 0; cardIndex < hand.Count; cardIndex++)
                {
                    hand[cardIndex] = Card.FromId(GetCanonicalCard(canonicalIds, nextCard++));
                }
            }

            for (var cardIndex = 0; cardIndex < state.DrawPile.Count; cardIndex++)
            {
                state.DrawPile[cardIndex] = Card.FromId(GetCanonicalCard(canonicalIds, nextCard++));
            }
        }

        private static int GetCanonicalCard(IReadOnlyList<byte> canonicalIds, int index)
        {
            if (index >= canonicalIds.Count)
            {
                throw new InvalidDataException("Game contains more hidden cards than the deck.");
            }

            return canonicalIds[index];
        }

        public static MejnunGame Read(byte[] snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            using (var stream = new MemoryStream(snapshot, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadUInt32() != Magic)
                {
                    throw new InvalidDataException("Invalid Mejnun snapshot header.");
                }

                var version = reader.ReadInt32();
                if (version < 1 || version > Version)
                {
                    throw new InvalidDataException("Unsupported Mejnun snapshot version.");
                }

                var setup = ReadSetup(reader, version);
                var game = new MejnunGame(setup, false);
                ReadState(reader, game.State, setup.SeatCount, version);

                if (reader.ReadBoolean())
                {
                    var turnSeat = reader.ReadInt32();
                    ValidateSeat(turnSeat, setup.SeatCount, "turn");
                    var startSnapshot = ReadBoardSnapshot(reader, setup.SeatCount);
                    if (startSnapshot.Seat != turnSeat)
                    {
                        throw new InvalidDataException("Turn and board snapshot seats do not match.");
                    }

                    var moveCount = ReadCount(reader, MaximumSerializedMoves, "turn moves");
                    var moves = new List<Move>(moveCount);
                    for (var index = 0; index < moveCount; index++)
                    {
                        moves.Add(new Move(ReadCommand(reader)));
                    }

                    game.RestoreTurnSession(startSnapshot, moves);
                }
                else
                {
                    game.RestoreTurnSession(null, null);
                }

                if (stream.Position != stream.Length)
                {
                    throw new InvalidDataException("Unexpected trailing data in Mejnun snapshot.");
                }

                ValidateRestoredGame(game);
                return game;
            }
        }

        private static void WriteSetup(BinaryWriter writer, GameSetup setup, bool redactSeed)
        {
            writer.Write(setup.SeatCount);
            writer.Write(redactSeed ? 0UL : setup.Seed);
            WriteNullableInt(writer, setup.Dealer);
            writer.Write(setup.TurnSeconds);
            writer.Write(setup.FacePool.Count);
            for (var index = 0; index < setup.FacePool.Count; index++)
            {
                writer.Write(setup.FacePool[index]);
            }

            writer.Write(setup.DeckPicks != null);
            if (setup.DeckPicks != null)
            {
                writer.Write(setup.DeckPicks.Count);
                for (var index = 0; index < setup.DeckPicks.Count; index++)
                {
                    WriteNullableString(writer, setup.DeckPicks[index]);
                }
            }

            writer.Write(setup.FeltPool.Count);
            for (var index = 0; index < setup.FeltPool.Count; index++)
            {
                writer.Write(setup.FeltPool[index]);
            }
        }

        private static GameSetup ReadSetup(BinaryReader reader, int version)
        {
            var seatCount = reader.ReadInt32();
            var seed = reader.ReadUInt64();
            var dealer = ReadNullableInt(reader);
            var turnSeconds = reader.ReadInt32();
            var faceCount = ReadCount(reader, 4096, "face pool");
            var faces = new string[faceCount];
            for (var index = 0; index < faceCount; index++)
            {
                faces[index] = reader.ReadString();
            }

            string[] deckPicks = null;
            var feltPool = Array.Empty<string>();
            if (version >= 2)
            {
                if (reader.ReadBoolean())
                {
                    var pickCount = ReadCount(reader, Card.DeckCount, "deck picks");
                    if (pickCount != Card.DeckCount)
                    {
                        throw new InvalidDataException("A snapshot must contain exactly two deck picks.");
                    }

                    deckPicks = new string[pickCount];
                    for (var index = 0; index < pickCount; index++)
                    {
                        deckPicks[index] = ReadNullableString(reader);
                    }
                }

                var feltCount = ReadCount(reader, 4096, "felt pool");
                feltPool = new string[feltCount];
                for (var index = 0; index < feltCount; index++)
                {
                    feltPool[index] = reader.ReadString();
                }
            }

            try
            {
                return new GameSetup(seatCount, seed, dealer, turnSeconds, faces, deckPicks, feltPool);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("Invalid game setup in Mejnun snapshot.", exception);
            }
        }

        private static void WriteState(BinaryWriter writer, GameState state)
        {
            writer.Write((byte)state.Phase);
            writer.Write(state.CurrentSeat);
            WriteNullableInt(writer, state.Dealer);
            writer.Write(state.TurnNumber);
            writer.Write(state.ConsecutivePasses);
            writer.Write(state.NextMeldId);
            writer.Write(state.Rng.State);
            writer.Write(state.Rng.Increment);

            writer.Write(state.Hands.Count);
            for (var seat = 0; seat < state.Hands.Count; seat++)
            {
                WriteCards(writer, state.Hands[seat]);
            }

            writer.Write(state.PlayerPhases.Count);
            for (var seat = 0; seat < state.PlayerPhases.Count; seat++)
            {
                writer.Write((byte)state.PlayerPhases[seat]);
            }

            WriteMelds(writer, state.Board);
            WriteCards(writer, state.DrawPile);

            if (state.DeckFaces == null || state.DeckFaces.Length != Card.DeckCount)
            {
                throw new InvalidDataException("Game state must contain exactly two deck faces.");
            }

            for (var index = 0; index < state.DeckFaces.Length; index++)
            {
                WriteNullableString(writer, state.DeckFaces[index]);
            }

            if (string.IsNullOrWhiteSpace(state.TableFeltId))
            {
                throw new InvalidDataException("Game state must contain a table felt id.");
            }

            writer.Write(state.TableFeltId);

            writer.Write(state.Result != null);
            if (state.Result != null)
            {
                writer.Write((byte)state.Result.Kind);
                WriteNullableInt(writer, state.Result.Winner);
                WriteNullableInt(writer, state.Result.NextFirstPlayer);
            }
        }

        private static void ReadState(BinaryReader reader, GameState state, int expectedSeatCount, int version)
        {
            state.Phase = ReadEnum<GamePhase>(reader, "game phase");
            state.CurrentSeat = reader.ReadInt32();
            state.Dealer = ReadNullableInt(reader);
            state.TurnNumber = reader.ReadInt32();
            state.ConsecutivePasses = reader.ReadInt32();
            state.NextMeldId = reader.ReadInt32();

            var rngState = reader.ReadUInt64();
            var rngIncrement = reader.ReadUInt64();
            try
            {
                state.Rng = Pcg32.FromState(rngState, rngIncrement);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new InvalidDataException("Invalid random-generator state in Mejnun snapshot.", exception);
            }

            var handCount = ReadCount(reader, 6, "hands");
            if (handCount != expectedSeatCount)
            {
                throw new InvalidDataException("Snapshot hand count does not match the seat count.");
            }

            for (var seat = 0; seat < handCount; seat++)
            {
                ReadCards(reader, state.Hands[seat]);
            }

            var phaseCount = ReadCount(reader, 6, "player phases");
            if (phaseCount != expectedSeatCount)
            {
                throw new InvalidDataException("Snapshot player-phase count does not match the seat count.");
            }

            for (var seat = 0; seat < phaseCount; seat++)
            {
                state.PlayerPhases[seat] = ReadEnum<PlayerPhase>(reader, "player phase");
            }

            ReadMelds(reader, state.Board);
            ReadCards(reader, state.DrawPile);
            for (var index = 0; index < state.DeckFaces.Length; index++)
            {
                state.DeckFaces[index] = ReadNullableString(reader);
            }

            if (version >= 2)
            {
                state.TableFeltId = reader.ReadString();
                if (string.IsNullOrWhiteSpace(state.TableFeltId))
                {
                    throw new InvalidDataException("Invalid table felt id in Mejnun snapshot.");
                }
            }

            if (reader.ReadBoolean())
            {
                state.Result = new GameResult(ReadEnum<GameEndKind>(reader, "game result"),
                    ReadNullableInt(reader), ReadNullableInt(reader));
            }
        }

        private static void WriteTurn(BinaryWriter writer, TurnSession turn)
        {
            writer.Write(turn != null);
            if (turn == null)
            {
                return;
            }

            writer.Write(turn.Seat);
            WriteBoardSnapshot(writer, turn.StartSnapshot);
            writer.Write(turn.Moves.Count);
            for (var index = 0; index < turn.Moves.Count; index++)
            {
                WriteCommand(writer, turn.Moves[index].Command);
            }
        }

        private static void WriteBoardSnapshot(BinaryWriter writer, BoardSnapshot snapshot)
        {
            writer.Write(snapshot.Seat);
            writer.Write((byte)snapshot.PlayerPhase);
            writer.Write(snapshot.NextMeldId);
            WriteCards(writer, snapshot.HandCards);
            WriteMelds(writer, snapshot.BoardMelds);
        }

        private static BoardSnapshot ReadBoardSnapshot(BinaryReader reader, int seatCount)
        {
            var seat = reader.ReadInt32();
            ValidateSeat(seat, seatCount, "snapshot");
            var playerPhase = ReadEnum<PlayerPhase>(reader, "snapshot player phase");
            var nextMeldId = reader.ReadInt32();
            var hand = ReadCards(reader);
            var board = ReadMelds(reader);
            return new BoardSnapshot(seat, hand, board, playerPhase, nextMeldId);
        }

        private static void WriteCommand(BinaryWriter writer, GameCommand command)
        {
            writer.Write((byte)command.Type);
            writer.Write(command.Seat);
            var cardIds = command.CardIdsUnsafe;
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
            var type = ReadEnum<CommandType>(reader, "move command type");
            if (type != CommandType.MoveCards && type != CommandType.SplitMeld &&
                type != CommandType.MergeMelds && type != CommandType.GoDown)
            {
                throw new InvalidDataException("A turn move contains an unsupported command type.");
            }

            var seat = reader.ReadInt32();
            var cardIds = ReadByteArray(reader, Card.CardCount, "command cards");
            var targetKind = ReadEnum<TargetKind>(reader, "card target kind");
            var target = new CardTarget(targetKind, reader.ReadInt32(), reader.ReadInt32());
            var meldId = reader.ReadInt32();
            var otherMeldId = reader.ReadInt32();
            var index = reader.ReadInt32();
            return new GameCommand(type, seat, cardIds, target, meldId, otherMeldId, index);
        }

        private static void WriteMelds(BinaryWriter writer, IReadOnlyList<Meld> melds)
        {
            writer.Write(melds.Count);
            for (var index = 0; index < melds.Count; index++)
            {
                writer.Write(melds[index].Id);
                WriteCards(writer, melds[index].Cards);
            }
        }

        private static Meld[] ReadMelds(BinaryReader reader)
        {
            var count = ReadCount(reader, Card.CardCount, "melds");
            var melds = new Meld[count];
            for (var index = 0; index < count; index++)
            {
                var id = reader.ReadInt32();
                melds[index] = new Meld(id, ReadCards(reader));
            }

            return melds;
        }

        private static void ReadMelds(BinaryReader reader, List<Meld> melds)
        {
            var read = ReadMelds(reader);
            melds.AddRange(read);
        }

        private static void WriteCards(BinaryWriter writer, IReadOnlyList<Card> cards)
        {
            writer.Write(cards.Count);
            for (var index = 0; index < cards.Count; index++)
            {
                writer.Write(cards[index].Id);
            }
        }

        private static Card[] ReadCards(BinaryReader reader)
        {
            var ids = ReadByteArray(reader, Card.CardCount, "cards");
            var cards = new Card[ids.Length];
            for (var index = 0; index < ids.Length; index++)
            {
                cards[index] = Card.FromId(ids[index]);
            }

            return cards;
        }

        private static void ReadCards(BinaryReader reader, List<Card> cards)
        {
            cards.AddRange(ReadCards(reader));
        }

        private static byte[] ReadByteArray(BinaryReader reader, int maximumCount, string description)
        {
            var count = ReadCount(reader, maximumCount, description);
            var bytes = reader.ReadBytes(count);
            if (bytes.Length != count)
            {
                throw new EndOfStreamException("The snapshot ended while reading " + description + ".");
            }

            for (var index = 0; index < bytes.Length; index++)
            {
                if (bytes[index] >= Card.CardCount)
                {
                    throw new InvalidDataException("Snapshot contains an invalid card id.");
                }
            }

            return bytes;
        }

        private static int ReadCount(BinaryReader reader, int maximum, string description)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > maximum)
            {
                throw new InvalidDataException("Invalid " + description + " count in Mejnun snapshot.");
            }

            return count;
        }

        private static T ReadEnum<T>(BinaryReader reader, string description) where T : struct
        {
            var value = reader.ReadByte();
            var enumValue = Enum.ToObject(typeof(T), value);
            if (!Enum.IsDefined(typeof(T), enumValue))
            {
                throw new InvalidDataException("Invalid " + description + " in Mejnun snapshot.");
            }

            return (T)enumValue;
        }

        private static void WriteNullableInt(BinaryWriter writer, int? value)
        {
            writer.Write(value.HasValue);
            if (value.HasValue)
            {
                writer.Write(value.Value);
            }
        }

        private static int? ReadNullableInt(BinaryReader reader)
        {
            return reader.ReadBoolean() ? (int?)reader.ReadInt32() : null;
        }

        private static void WriteNullableString(BinaryWriter writer, string value)
        {
            writer.Write(value != null);
            if (value != null)
            {
                writer.Write(value);
            }
        }

        private static string ReadNullableString(BinaryReader reader)
        {
            return reader.ReadBoolean() ? reader.ReadString() : null;
        }

        private static void ValidateSeat(int seat, int seatCount, string description)
        {
            if (seat < 0 || seat >= seatCount)
            {
                throw new InvalidDataException("Invalid " + description + " seat in Mejnun snapshot.");
            }
        }

        private static void ValidateRestoredGame(MejnunGame game)
        {
            var state = game.State;
            var seatCount = game.Setup.SeatCount;
            if (state.CurrentSeat < -1 || state.CurrentSeat >= seatCount ||
                (state.Dealer.HasValue && (state.Dealer.Value < 0 || state.Dealer.Value >= seatCount)) ||
                state.TurnNumber < 0 || state.ConsecutivePasses < 0 || state.NextMeldId < 1)
            {
                throw new InvalidDataException("Invalid game state in Mejnun snapshot.");
            }

            if (state.Phase != GamePhase.ChoosingDealer && state.CurrentSeat < 0)
            {
                throw new InvalidDataException("A playing or finished snapshot must have a current seat.");
            }

            if (string.IsNullOrWhiteSpace(state.TableFeltId))
            {
                throw new InvalidDataException("Invalid table felt id in Mejnun snapshot.");
            }

            if (state.Phase == GamePhase.ChoosingDealer && game.Turn != null)
            {
                throw new InvalidDataException("A dealer-choice snapshot cannot contain a turn session.");
            }

            if (state.Phase == GamePhase.Playing && game.Turn == null)
            {
                throw new InvalidDataException("A playing snapshot must contain a turn session.");
            }

            if (state.Result != null)
            {
                if (state.Phase != GamePhase.Finished ||
                    (state.Result.Winner.HasValue && (state.Result.Winner.Value < 0 || state.Result.Winner.Value >= seatCount)) ||
                    (state.Result.NextFirstPlayer.HasValue &&
                     (state.Result.NextFirstPlayer.Value < 0 || state.Result.NextFirstPlayer.Value >= seatCount)))
                {
                    throw new InvalidDataException("Invalid game result in Mejnun snapshot.");
                }
            }
        }
    }
}