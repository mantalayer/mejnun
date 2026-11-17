using NUnit.Framework;
using Mejnun.Core.Tests.TestUtil;

namespace Mejnun.Core.Tests
{
    public sealed class DownTests
    {
        [Test]
        public void Down_CanSplitStraightIntoTwoMelds()
        {
            var game = NewGame();
            GameBuilder.SetBoard(game, new[] { "2H", "3H", "4H", "5H" });
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);

            var result = game.Apply(GameCommand.SplitMeld(0, 1, 2));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.Board.Count, Is.EqualTo(2));
            Assert.That(game.State.Board[0].Cards.Count, Is.EqualTo(2));
            Assert.That(game.State.Board[1].Cards.Count, Is.EqualTo(2));
            Assert.That(game.State.NextMeldId, Is.EqualTo(3));
        }

        [Test]
        public void Down_CannotSplitNonStraightMeld()
        {
            var game = NewGame();
            GameBuilder.SetBoard(game, new[] { "2H", "2D", "2C" });
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);

            var result = game.Apply(GameCommand.SplitMeld(0, 1, 1));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.InvalidSplit));
            Assert.That(game.State.Board.Count, Is.EqualTo(1));
        }

        [Test]
        public void CanGoDown_IsTrueOnlyForValidHandOnlyNewMeldsWhileNotDown()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H", "4H", "5C");
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.NotDown);

            Assert.That(game.CanGoDown(), Is.False);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "3H", "4H"), CardTarget.NewMeld(0))).Ok,
                Is.True);
            Assert.That(game.CanGoDown(), Is.True);

            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);
            Assert.That(game.CanGoDown(), Is.False);
        }

        [Test]
        public void Down_CanMergeMelds()
        {
            var game = NewGame();
            GameBuilder.SetBoard(game, new[] { "2H", "3H" }, new[] { "4H", "5H" });
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);

            var result = game.Apply(GameCommand.MergeMelds(0, 1, 2));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.Board.Count, Is.EqualTo(1));
            Assert.That(MeldValidator.Classify(game.State.Board[0].Cards).Kind, Is.EqualTo(MeldKind.StraightLow));
        }

        [Test]
        public void Down_CanTakeCardFromFourCardSameKindSet()
        {
            var game = NewGame();
            GameBuilder.SetBoard(game, new[] { "8C", "8D", "8H", "8S" });
            GameBuilder.SetHand(game, 0, "2C");
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);

            var result = game.Apply(GameCommand.MoveCards(0, Ids("8S"), CardTarget.NewMeld(0)));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.Board.Count, Is.EqualTo(2));
            Assert.That(MeldValidator.Classify(game.State.Board[0].Cards).Kind, Is.EqualTo(MeldKind.SameKind));
            Assert.That(MeldValidator.Classify(game.State.Board[1].Cards).IsValid, Is.False);
        }

        [Test]
        public void Down_CanCreateTemporaryInvalidGroups()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "9D");
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);

            var result = game.Apply(GameCommand.MoveCards(0, Ids("9D"), CardTarget.NewMeld(0)));

            Assert.That(result.Ok, Is.True);
            Assert.That(MeldValidator.IsBoardValid(game.State.Board), Is.False);
            Assert.That(game.CanEndTurn(), Is.False);
        }

        [Test]
        public void Down_CanMoveAnyBoardCardBetweenMelds()
        {
            var game = NewGame();
            GameBuilder.SetBoard(game, new[] { "2H", "3H", "4H" }, new[] { "7C", "8C", "9C" });
            GameBuilder.SetHand(game, 0, "5D");
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);

            var result = game.Apply(GameCommand.MoveCards(0, Ids("4H"), CardTarget.Meld(2, 0)));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.Board[0].Cards.Count, Is.EqualTo(2));
            Assert.That(game.State.Board[1].Cards.Count, Is.EqualTo(4));
            Assert.That(MeldValidator.IsBoardValid(game.State.Board), Is.False);
        }

        [Test]
        public void Down_CannotReturnCardThatStartedOnBoardToHand()
        {
            var game = NewGame();
            GameBuilder.SetBoard(game, new[] { "2H", "3H", "4H" });
            GameBuilder.SetHand(game, 0, "5C");
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);

            var result = game.Apply(GameCommand.MoveCards(0, Ids("3H"), CardTarget.Hand(0)));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.BoardCardToHand));
            Assert.That(game.State.Hands[0], Does.Contain(GameBuilder.ParseCard("5C")));
            Assert.That(game.State.Board[0].Cards.Count, Is.EqualTo(3));
        }

        [Test]
        public void Down_CanReturnCardThatStartedInHand()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H", "4H", "5C");
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "3H", "4H"),
                CardTarget.NewMeld(0))).Ok, Is.True);

            var result = game.Apply(GameCommand.MoveCards(0, Ids("3H"), CardTarget.Hand(0)));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.Hands[0], Does.Contain(GameBuilder.ParseCard("3H")));
            Assert.That(game.State.Board[0].Cards.Count, Is.EqualTo(2));
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
    }
}