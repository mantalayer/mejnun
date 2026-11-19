using System;
using System.Linq;
using NUnit.Framework;

namespace Mejnun.Core.Tests
{
    public sealed class StackedDeckTests
    {
        [Test]
        public void Deal_UsesAuthoredOrderForHandsAndDrawPile()
        {
            var order = Enumerable.Range(0, Card.CardCount).Select(id => (byte)id).ToArray();
            var setup = new GameSetup(2, 123UL, dealer: 0, shoeOrder: order);
            order[0] = 103;
            var game = new MejnunGame(setup);

            CollectionAssert.AreEqual(Enumerable.Range(1, 11).Select(index => Card.FromId(index * 2 - 1)),
                game.State.Hands[0]);
            CollectionAssert.AreEqual(Enumerable.Range(0, 11).Select(index => Card.FromId(index * 2)),
                game.State.Hands[1]);
            CollectionAssert.AreEqual(Enumerable.Range(22, 82).Select(index => Card.FromId(index)),
                game.State.DrawPile);
            Assert.That(setup.ShoeOrder[0], Is.EqualTo(0));
            Assert.That(game.State.Rng.State, Is.EqualTo(new Pcg32(123UL).State));

            Assert.That(game.Apply(GameCommand.DrawOrPass(1)).Ok, Is.True);
            Assert.That(game.State.Hands[1].Last(), Is.EqualTo(Card.FromId(103)));
        }

        [Test]
        public void Deal_WithoutShoeOrderMatchesTheLegacySeededShuffle()
        {
            const ulong seed = 84721UL;
            var expectedShoe = new Shoe();
            var expectedRng = new Pcg32(seed);
            expectedShoe.Shuffle(expectedRng);
            var game = new MejnunGame(new GameSetup(3, seed, dealer: 1));

            for (var round = 0; round < 11; round++)
            {
                for (var offset = 1; offset <= 3; offset++)
                {
                    var seat = (1 + offset) % 3;
                    Assert.That(game.State.Hands[seat][round], Is.EqualTo(expectedShoe[round * 3 + offset - 1]));
                }
            }

            CollectionAssert.AreEqual(Enumerable.Range(33, Card.CardCount - 33)
                .Select(index => expectedShoe[index]), game.State.DrawPile);
            Assert.That(game.State.Rng.State, Is.EqualTo(expectedRng.State));
        }

        [Test]
        public void GameSetup_RejectsInvalidShoeOrders()
        {
            Assert.Throws<ArgumentException>(() => new GameSetup(2, 1UL, shoeOrder: new byte[Card.CardCount - 1]));

            var duplicate = Enumerable.Range(0, Card.CardCount).Select(id => (byte)id).ToArray();
            duplicate[Card.CardCount - 1] = duplicate[0];
            Assert.Throws<ArgumentException>(() => new GameSetup(2, 1UL, shoeOrder: duplicate));

            var outOfRange = Enumerable.Range(0, Card.CardCount).Select(id => (byte)id).ToArray();
            outOfRange[Card.CardCount - 1] = byte.MaxValue;
            Assert.Throws<ArgumentException>(() => new GameSetup(2, 1UL, shoeOrder: outOfRange));
        }

        [Test]
        public void Snapshot_StackedGameRoundTripsWithoutSerializingShoeOrder()
        {
            var order = Enumerable.Range(0, Card.CardCount).Select(id => (byte)id).ToArray();
            var game = new MejnunGame(new GameSetup(2, 123UL, dealer: 0, shoeOrder: order));

            var bytes = SnapshotSerializer.Write(game);
            var restored = SnapshotSerializer.Read(bytes);

            CollectionAssert.AreEqual(bytes, SnapshotSerializer.Write(restored));
            CollectionAssert.AreEqual(game.State.Hands[0], restored.State.Hands[0]);
            CollectionAssert.AreEqual(game.State.Hands[1], restored.State.Hands[1]);
            CollectionAssert.AreEqual(game.State.DrawPile, restored.State.DrawPile);
        }
    }
}