using Mejnun.AI;
using Mejnun.Core;
using NUnit.Framework;

namespace Mejnun.Game.Tests
{
    public sealed class SavedGameTests
    {
        [Test]
        public void EncodeDecodeRetainsSettingsAndSnapshot()
        {
            const ulong seed = 0x123456789ABCDEF0UL;
            var game = new MejnunGame(new GameSetup(2, seed, 0));
            var seats = new[]
            {
                SeatConfig.Human("Player One"),
                SeatConfig.Bot("Nora", BotDifficulty.Hard, BotStyle.Blocker)
            };
            var snapshot = SnapshotSerializer.Write(game);
            var data = new SavedGameData(seats, 90, seed, snapshot);

            var decoded = SavedGame.Decode(SavedGame.Encode(data));

            Assert.That(decoded.Seats.Count, Is.EqualTo(seats.Length));
            for (var index = 0; index < seats.Length; index++)
            {
                Assert.That(decoded.Seats[index].Name, Is.EqualTo(seats[index].Name));
                Assert.That(decoded.Seats[index].IsBot, Is.EqualTo(seats[index].IsBot));
                Assert.That(decoded.Seats[index].Difficulty, Is.EqualTo(seats[index].Difficulty));
                Assert.That(decoded.Seats[index].Style, Is.EqualTo(seats[index].Style));
            }

            Assert.That(decoded.TurnSeconds, Is.EqualTo(90));
            Assert.That(decoded.RoundSeed, Is.EqualTo(seed));
            CollectionAssert.AreEqual(snapshot, decoded.Snapshot);
        }
    }
}