using System;
using System.IO;
using NUnit.Framework;
using Mejnun.Core.Tests.TestUtil;

namespace Mejnun.Core.Tests
{
    public sealed class SnapshotTests
    {
        [Test]
        public void Snapshot_MidTurnRoundTripsAndPreservesUndoHistory()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H", "4H", "5C");
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "3H"),
                CardTarget.NewMeld(0))).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("4H"), CardTarget.Meld(1, 2))).Ok, Is.True);

            var bytes = SnapshotSerializer.Write(game);
            var restored = SnapshotSerializer.Read(bytes);

            CollectionAssert.AreEqual(bytes, SnapshotSerializer.Write(restored));
            Assert.That(restored.State.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(restored.State.CurrentSeat, Is.EqualTo(game.State.CurrentSeat));
            Assert.That(restored.State.Rng.State, Is.EqualTo(game.State.Rng.State));
            Assert.That(restored.Turn.Moves.Count, Is.EqualTo(2));
            Assert.That(restored.Apply(GameCommand.Undo(0)).Ok, Is.True);
            Assert.That(restored.State.Board.Count, Is.EqualTo(1));
            Assert.That(restored.State.Board[0].Cards.Count, Is.EqualTo(2));
            Assert.That(restored.State.Hands[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void Snapshot_DealerChoicePhaseRoundTripsWithoutTurnSession()
        {
            var game = new MejnunGame(new GameSetup(4, 55UL, facePool: new[] { "classic", "fa_پ" }));

            var restored = SnapshotSerializer.Read(SnapshotSerializer.Write(game));

            Assert.That(restored.State.Phase, Is.EqualTo(GamePhase.ChoosingDealer));
            Assert.That(restored.State.Dealer, Is.Null);
            Assert.That(restored.Turn, Is.Null);
            Assert.That(restored.Apply(GameCommand.ChooseRandomDealer()).Ok, Is.True);
        }

        [Test]
        public void Snapshot_FinishedStalemateRoundTripsWithNoNextDealer()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H");
            GameBuilder.SetHand(game, 1, "4C", "5C");
            GameBuilder.SetDrawPile(game);
            Assert.That(game.Apply(GameCommand.DrawOrPass(0)).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.DrawOrPass(1)).Ok, Is.True);

            var bytes = SnapshotSerializer.Write(game);
            var restored = SnapshotSerializer.Read(bytes);

            Assert.That(restored.State.Phase, Is.EqualTo(GamePhase.Finished));
            Assert.That(restored.State.Result.Kind, Is.EqualTo(GameEndKind.Stalemate));
            Assert.That(restored.State.Result.NextFirstPlayer, Is.Null);
            CollectionAssert.AreEqual(bytes, SnapshotSerializer.Write(restored));
        }

        [Test]
        public void Snapshot_BoardSnapshotViewsDoNotExposeMutableSnapshotCollections()
        {
            var game = NewGame();
            GameBuilder.SetBoard(game, new[] { "2H", "3H", "4H" });
            GameBuilder.SetHand(game, 0, "5C");
            game = SnapshotSerializer.Read(SnapshotSerializer.Write(game));

            var snapshot = game.Turn.StartSnapshot;
            Assert.That(snapshot.Hand.Count, Is.EqualTo(1));
            var boardView = snapshot.Board;
            Assert.That(boardView[0].Cards.Count, Is.EqualTo(3));
            boardView[0].Cards.Clear();

            Assert.That(snapshot.Board[0].Cards.Count, Is.EqualTo(3));
            Assert.Throws<System.NotSupportedException>(() => ((System.Collections.Generic.IList<Card>)snapshot.Hand)[0] =
                GameBuilder.ParseCard("6C"));
        }

        [Test]
        public void Snapshot_ReadRejectsUnsupportedVersion()
        {
            var bytes = SnapshotSerializer.Write(NewGame());
            bytes[4] = (byte)(SnapshotSerializer.Version + 1);

            Assert.Throws<InvalidDataException>(() => SnapshotSerializer.Read(bytes));
        }

        [Test]
        public void Snapshot_ReadRejectsInvalidHeader()
        {
            var bytes = SnapshotSerializer.Write(NewGame());
            bytes[0] ^= 0xFF;

            Assert.Throws<InvalidDataException>(() => SnapshotSerializer.Read(bytes));
        }

        [Test]
        public void Snapshot_ReadRejectsTrailingBytes()
        {
            var bytes = SnapshotSerializer.Write(NewGame());
            Array.Resize(ref bytes, bytes.Length + 1);

            Assert.Throws<InvalidDataException>(() => SnapshotSerializer.Read(bytes));
        }

        [Test]
        public void Snapshot_ReadRejectsTruncatedHeader()
        {
            Assert.Throws<EndOfStreamException>(() => SnapshotSerializer.Read(new byte[1]));
        }

        [Test]
        public void Snapshot_ReadRejectsUnknownGamePhase()
        {
            var bytes = SnapshotSerializer.Write(NewGame());
            bytes[GetStateOffset(bytes)] = byte.MaxValue;

            Assert.Throws<InvalidDataException>(() => SnapshotSerializer.Read(bytes));
        }

        [Test]
        public void Snapshot_ReadRejectsPlayingStateWithoutCurrentSeat()
        {
            var bytes = SnapshotSerializer.Write(NewGame());
            var currentSeatOffset = GetStateOffset(bytes) + 1;
            for (var index = 0; index < sizeof(int); index++)
            {
                bytes[currentSeatOffset + index] = byte.MaxValue;
            }

            Assert.Throws<InvalidDataException>(() => SnapshotSerializer.Read(bytes));
        }

        [Test]
        public void Snapshot_WriteRejectsNullGame()
        {
            Assert.Throws<ArgumentNullException>(() => SnapshotSerializer.Write(null));
        }

        private static MejnunGame NewGame()
        {
            return GameBuilder.Create(dealer: 1);
        }

        private static byte[] Ids(params string[] cards)
        {
            var ids = new byte[cards.Length];
            for (var index = 0; index < cards.Length; index++)
            {
                ids[index] = GameBuilder.ParseCard(cards[index]).Id;
            }

            return ids;
        }

        private static int GetStateOffset(byte[] snapshot)
        {
            using (var stream = new MemoryStream(snapshot, false))
            using (var reader = new BinaryReader(stream))
            {
                reader.ReadUInt32();
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadUInt64();
                if (reader.ReadBoolean())
                {
                    reader.ReadInt32();
                }

                reader.ReadInt32();
                var faceCount = reader.ReadInt32();
                for (var index = 0; index < faceCount; index++)
                {
                    reader.ReadString();
                }

                return (int)stream.Position;
            }
        }
    }
}