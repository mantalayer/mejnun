using NUnit.Framework;

namespace Mejnun.Core.Tests
{
    public sealed class FeltAssignmentTests
    {
        [Test]
        public void Felt_EmptyPoolDefaultsToGreen()
        {
            Assert.That(TableFeltAssigner.Assign(new string[0], new Pcg32(1UL)),
                Is.EqualTo(TableFeltAssigner.GreenFelt));
        }

        [Test]
        public void Felt_NullPoolDefaultsToGreen()
        {
            Assert.That(TableFeltAssigner.Assign(null, new Pcg32(1UL)),
                Is.EqualTo(TableFeltAssigner.GreenFelt));
        }

        [Test]
        public void Felt_AssignmentIsSelectedFromPool()
        {
            var feltPool = new[] { "green", "burgundy", "midnight" };

            var felt = TableFeltAssigner.Assign(feltPool, new Pcg32(45UL));

            CollectionAssert.Contains(feltPool, felt);
        }

        [Test]
        public void Felt_AssignmentIsDeterministicForTheSameSeed()
        {
            var feltPool = new[] { "green", "burgundy", "midnight" };

            Assert.That(TableFeltAssigner.Assign(feltPool, new Pcg32(45UL)),
                Is.EqualTo(TableFeltAssigner.Assign(feltPool, new Pcg32(45UL))));
        }

        [Test]
        public void FeltSelectionDoesNotChangeTheSeededDeal()
        {
            var facePool = new[] { "classic", "classic_ivory", "pixelPastel" };
            var picks = new string[] { null, null };
            var withoutFelt = new MejnunGame(new GameSetup(4, 109UL, 2, facePool: facePool, deckPicks: picks));
            var withFelt = new MejnunGame(new GameSetup(4, 109UL, 2, facePool: facePool, deckPicks: picks,
                feltPool: new[] { "green", "burgundy" }));

            for (var seat = 0; seat < withoutFelt.State.Hands.Count; seat++)
            {
                var firstHand = new byte[withoutFelt.State.Hands[seat].Count];
                var secondHand = new byte[withFelt.State.Hands[seat].Count];
                for (var card = 0; card < firstHand.Length; card++)
                {
                    firstHand[card] = withoutFelt.State.Hands[seat][card].Id;
                    secondHand[card] = withFelt.State.Hands[seat][card].Id;
                }

                CollectionAssert.AreEqual(firstHand, secondHand);
            }
        }

        [Test]
        public void Felt_AssignmentRejectsInvalidIds()
        {
            Assert.Throws<System.ArgumentException>(() => TableFeltAssigner.Assign(
                new string[] { "green", null }, new Pcg32(1UL)));
        }
    }
}