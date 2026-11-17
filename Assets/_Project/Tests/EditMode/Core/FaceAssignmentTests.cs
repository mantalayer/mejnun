using System.Linq;
using NUnit.Framework;

namespace Mejnun.Core.Tests
{
    public sealed class FaceAssignmentTests
    {
        [Test]
        public void Faces_AssignmentIsDistinctAndSelectedFromPool()
        {
            var pool = new[] { "red", "blue", "green", "gold" };
            var faces = DeckFaceAssigner.Assign(pool, new Pcg32(77UL));

            Assert.That(faces.Length, Is.EqualTo(2));
            Assert.That(faces[0], Is.Not.EqualTo(faces[1]));
            Assert.That(pool.Contains(faces[0]), Is.True);
            Assert.That(pool.Contains(faces[1]), Is.True);
        }

        [Test]
        public void Faces_AssignmentIsDeterministicForTheSameSeed()
        {
            var pool = new[] { "red", "blue", "green", "gold" };

            CollectionAssert.AreEqual(DeckFaceAssigner.Assign(pool, new Pcg32(99UL)),
                DeckFaceAssigner.Assign(pool, new Pcg32(99UL)));
        }

        [Test]
        public void Faces_EmptyPoolFallsBackToBothBaseFaces()
        {
            var faces = DeckFaceAssigner.Assign(new string[0], new Pcg32(5UL));

            CollectionAssert.AreEquivalent(
                new[] { DeckFaceAssigner.ClassicFace, DeckFaceAssigner.ClassicIvoryFace }, faces);
        }

        [Test]
        public void Faces_OneFacePoolIsToppedUpWithBaseFaces()
        {
            var faces = DeckFaceAssigner.Assign(new[] { "custom" }, new Pcg32(5UL));

            Assert.That(faces.Length, Is.EqualTo(2));
            Assert.That(faces[0], Is.Not.EqualTo(faces[1]));
            Assert.That(faces.All(face => face == "custom" ||
                                          face == DeckFaceAssigner.ClassicFace ||
                                          face == DeckFaceAssigner.ClassicIvoryFace), Is.True);
        }

        [Test]
        public void Faces_AssignedFacesAreStoredInGameState()
        {
            var game = new MejnunGame(new GameSetup(2, 13UL, dealer: 0,
                facePool: new[] { "red", "blue" }));

            Assert.That(game.State.DeckFaces.Length, Is.EqualTo(2));
            Assert.That(game.State.DeckFaces[0], Is.Not.EqualTo(game.State.DeckFaces[1]));
            Assert.That(game.State.DeckFaces.All(face => face == "red" || face == "blue"), Is.True);
        }

        [Test]
        public void Faces_DuplicatePoolEntriesDoNotProduceDuplicateDeckFaces()
        {
            var faces = DeckFaceAssigner.Assign(new[] { "red", "red", "blue" }, new Pcg32(17UL));

            Assert.That(faces[0], Is.Not.EqualTo(faces[1]));
            Assert.That(faces.All(face => face == "red" || face == "blue"), Is.True);
        }

        [Test]
        public void Faces_AssignmentRejectsNullInputsAndNullFaceIds()
        {
            Assert.Throws<System.ArgumentNullException>(() => DeckFaceAssigner.Assign(null, new Pcg32(1UL)));
            Assert.Throws<System.ArgumentNullException>(() => DeckFaceAssigner.Assign(new string[0], null));
            Assert.Throws<System.ArgumentException>(() => DeckFaceAssigner.Assign(new string[] { null },
                new Pcg32(1UL)));
        }
    }
}