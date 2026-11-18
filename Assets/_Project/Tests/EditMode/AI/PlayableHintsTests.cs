using Mejnun.Core;
using NUnit.Framework;
using static Mejnun.AI.Tests.AiTestUtil;

namespace Mejnun.AI.Tests
{
    public sealed class PlayableHintsTests
    {
        [Test]
        public void NotDownHintsOnlyCardsThatFormHandSets()
        {
            var game = Game(new[] { "3H", "4H", "5H", "6S", "QD" }, new[] { new[] { "7S", "8S", "9S" } }, false);

            var hints = PlayableHints.Find(game.State);

            CollectionAssert.AreEquivalent(new[] { Parse("3H").Id, Parse("4H").Id, Parse("5H").Id }, hints);
        }

        [Test]
        public void DownHintsAlsoIncludeSimpleExtensions()
        {
            var game = Game(new[] { "6S", "QD", "10S" }, new[] { new[] { "7S", "8S", "9S" } }, true);

            var hints = PlayableHints.Find(game.State);

            CollectionAssert.AreEquivalent(new[] { Parse("6S").Id, Parse("10S").Id }, hints);
        }

        [Test]
        public void BoardMoveHintsExcludeCardsThatLeaveAnotherBoardCardStranded()
        {
            var game = Game(new[] { "4H" }, new[] { new[] { "5H", "6H", "7H", "8H" } }, true);

            var hints = PlayableHints.FindBoardMoves(game.State);

            CollectionAssert.DoesNotContain(hints, Parse("8H").Id);
        }

        [Test]
        public void BoardMoveHintsKeepMovesThatCanResolveTheDisplacedCard()
        {
            var game = Game(new[] { "6S", "10H", "10D" }, new[] { new[] { "7S", "8S", "9S", "10S" } }, true);

            var hints = PlayableHints.FindBoardMoves(game.State);

            CollectionAssert.Contains(hints, Parse("10S").Id);
        }

        [Test]
        public void NoHintsOutsideAGameInProgress()
        {
            var game = new MejnunGame(new GameSetup(2, 3UL));

            Assert.That(PlayableHints.Find(game.State), Is.Empty);
            Assert.That(PlayableHints.Find(null), Is.Empty);
        }
    }
}
