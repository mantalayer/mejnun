using System.Linq;
using NUnit.Framework;

namespace Mejnun.Core.Tests
{
    public sealed class PickAssignmentTests
    {
        [Test]
        public void Picks_ExplicitFacesMapToTheirDeckSlots()
        {
            var faces = DeckFaceAssigner.AssignFromPicks(new[] { "first", "second" },
                new[] { "random" }, new Pcg32(7UL));

            CollectionAssert.AreEqual(new[] { "first", "second" }, faces);
        }

        [Test]
        public void Picks_RandomSlotsUsePoolAndResolveDistinctSkins()
        {
            var pool = new[] { "red:blue_back", "red:gold_back", "blue" };

            var faces = DeckFaceAssigner.AssignFromPicks(new string[] { null, null }, pool,
                new Pcg32(71UL));

            Assert.That(pool.Contains(faces[0]), Is.True);
            Assert.That(pool.Contains(faces[1]), Is.True);
            Assert.That(DeckFaceId.GetSkinId(faces[0]), Is.Not.EqualTo(DeckFaceId.GetSkinId(faces[1])));
        }

        [Test]
        public void Picks_ExplicitSkinIsExcludedFromRandomSlot()
        {
            var faces = DeckFaceAssigner.AssignFromPicks(new[] { "chosen", null },
                new[] { "chosen", "other" }, new Pcg32(19UL));

            CollectionAssert.AreEqual(new[] { "chosen", "other" }, faces);
        }

        [Test]
        public void Picks_DuplicateExplicitPickMakesSecondSlotRandom()
        {
            var faces = DeckFaceAssigner.AssignFromPicks(new[] { "same:blue_back", "same:gold_back" },
                new[] { "same", "different" }, new Pcg32(13UL));

            Assert.That(faces[0], Is.EqualTo("same:blue_back"));
            Assert.That(faces[1], Is.EqualTo("different"));
        }

        [Test]
        public void Picks_EmptyPoolBackfillsWithDistinctBaseFaces()
        {
            var faces = DeckFaceAssigner.AssignFromPicks(new string[] { null, null },
                new string[0], new Pcg32(5UL));

            CollectionAssert.AreEquivalent(
                new[] { DeckFaceAssigner.ClassicFace, DeckFaceAssigner.ClassicIvoryFace }, faces);
        }

        [Test]
        public void Picks_AreDeterministicForTheSameSeed()
        {
            var picks = new string[] { null, "chosen" };
            var pool = new[] { "red:blue_back", "red:gold_back", "blue" };

            CollectionAssert.AreEqual(
                DeckFaceAssigner.AssignFromPicks(picks, pool, new Pcg32(31UL)),
                DeckFaceAssigner.AssignFromPicks(picks, pool, new Pcg32(31UL)));
        }

        [Test]
        public void Picks_RejectInvalidArguments()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                DeckFaceAssigner.AssignFromPicks(null, new string[0], new Pcg32(1UL)));
            Assert.Throws<System.ArgumentException>(() =>
                DeckFaceAssigner.AssignFromPicks(new[] { "one" }, new string[0], new Pcg32(1UL)));
            Assert.Throws<System.ArgumentNullException>(() =>
                DeckFaceAssigner.AssignFromPicks(new string[] { null, null }, null, new Pcg32(1UL)));
            Assert.Throws<System.ArgumentNullException>(() =>
                DeckFaceAssigner.AssignFromPicks(new string[] { null, null }, new string[0], null));
        }
    }
}