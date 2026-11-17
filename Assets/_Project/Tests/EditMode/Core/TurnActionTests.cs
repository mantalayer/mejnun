using NUnit.Framework;
using Mejnun.Core.Tests.TestUtil;

namespace Mejnun.Core.Tests
{
    public sealed class TurnActionTests
    {
        [Test]
        public void Turn_CannotEndWithoutValidBoardAndHandCardOnBoard()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H", "4H", "5C");

            Assert.That(game.CanEndTurn(), Is.False);
            var result = game.Apply(GameCommand.EndTurn(0));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.CannotEndTurn));
            Assert.That(game.State.CurrentSeat, Is.EqualTo(0));
        }

        [Test]
        public void Turn_EndingWithValidHandOnlySetImplicitlyGoesDown()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H", "4H", "5C");
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "3H", "4H"),
                CardTarget.NewMeld(0))).Ok, Is.True);
            var wentDownSeat = -1;
            game.Events.WentDown += seat => wentDownSeat = seat;

            var result = game.Apply(GameCommand.EndTurn(0));

            Assert.That(result.Ok, Is.True);
            Assert.That(wentDownSeat, Is.EqualTo(0));
            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.Down));
            Assert.That(game.State.CurrentSeat, Is.EqualTo(1));
            Assert.That(game.State.TurnNumber, Is.EqualTo(2));
        }

        [Test]
        public void Turn_EndTurnRejectsInvalidBoardWithoutChangingTurn()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "4H", "5H");
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "4H", "5H"),
                CardTarget.NewMeld(0))).Ok, Is.True);

            var result = game.Apply(GameCommand.EndTurn(0));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.CannotEndTurn));
            Assert.That(game.State.CurrentSeat, Is.EqualTo(0));
            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.NotDown));
        }

        [Test]
        public void Turn_UndoReplaysMovesOneAtATime()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H", "4H", "5C");
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "3H"),
                CardTarget.NewMeld(0))).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("4H"), CardTarget.Meld(1, 2))).Ok, Is.True);
            Assert.That(game.Turn.Moves[0].Type, Is.EqualTo(CommandType.MoveCards));

            Assert.That(game.Apply(GameCommand.Undo(0)).Ok, Is.True);
            Assert.That(game.State.Board[0].Cards.Count, Is.EqualTo(2));
            Assert.That(game.State.Hands[0].Count, Is.EqualTo(2));

            Assert.That(game.Apply(GameCommand.Undo(0)).Ok, Is.True);
            Assert.That(game.State.Board, Is.Empty);
            Assert.That(game.State.Hands[0].Count, Is.EqualTo(4));
        }

        [Test]
        public void Turn_UndoWithNoMovesReturnsNothingToUndo()
        {
            var game = NewGame();

            var result = game.Apply(GameCommand.Undo(0));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.NothingToUndo));
        }

        [Test]
        public void Turn_RevertRestoresStartOfTurnBoardAndHand()
        {
            var game = NewGame();
            GameBuilder.SetBoard(game, new[] { "2H", "3H", "4H" });
            GameBuilder.SetHand(game, 0, "5C");
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("3H"), CardTarget.NewMeld(0))).Ok, Is.True);

            var result = game.Apply(GameCommand.Revert(0));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.Board.Count, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { "2H", "3H", "4H" },
                game.State.Board[0].Cards.ConvertAll(card => Notation(card)));
            Assert.That(game.State.Hands[0].Count, Is.EqualTo(1));
            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.Down));
            Assert.That(game.Turn.Moves, Is.Empty);
        }

        [Test]
        public void Turn_RevertOnUntouchedTurnAlwaysSucceeds()
        {
            var game = NewGame();

            var result = game.Apply(GameCommand.Revert(0));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.Turn.Moves, Is.Empty);
        }

        [Test]
        public void Turn_DrawRevertsBoardBeforeDrawingAndAdvancing()
        {
            var game = NewGame();
            GameBuilder.SetBoard(game, new[] { "2H", "3H", "4H" });
            GameBuilder.SetHand(game, 0, "5C");
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);
            GameBuilder.SetDrawPile(game, "AH");
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("3H"), CardTarget.NewMeld(0))).Ok, Is.True);

            var result = game.Apply(GameCommand.DrawOrPass(0));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.Board.Count, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { "2H", "3H", "4H" },
                game.State.Board[0].Cards.ConvertAll(card => Notation(card)));
            Assert.That(game.State.Hands[0].Count, Is.EqualTo(2));
            Assert.That(game.State.Hands[0], Does.Contain(GameBuilder.ParseCard("AH")));
            Assert.That(game.State.DrawPile, Is.Empty);
            Assert.That(game.State.CurrentSeat, Is.EqualTo(1));
        }

        [Test]
        public void Turn_EmptyPileDrawBecomesPassAndRevertsBoard()
        {
            var game = NewGame();
            GameBuilder.SetBoard(game, new[] { "2H", "3H", "4H" });
            GameBuilder.SetHand(game, 0, "5C");
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);
            GameBuilder.SetDrawPile(game);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("3H"), CardTarget.NewMeld(0))).Ok, Is.True);
            var passedSeat = -1;
            game.Events.Passed += seat => passedSeat = seat;

            var result = game.Apply(GameCommand.DrawOrPass(0));

            Assert.That(result.Ok, Is.True);
            Assert.That(passedSeat, Is.EqualTo(0));
            Assert.That(game.State.ConsecutivePasses, Is.EqualTo(1));
            Assert.That(game.State.Board.Count, Is.EqualTo(1));
            Assert.That(game.State.Board[0].Cards.Count, Is.EqualTo(3));
            Assert.That(game.State.CurrentSeat, Is.EqualTo(1));
        }

        [Test]
        public void Turn_CommandsFromAnotherSeatAreRejectedWithoutStateChanges()
        {
            var game = NewGame();
            var handCount = game.State.Hands[0].Count;
            var result = game.Apply(GameCommand.DrawOrPass(1));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.NotYourTurn));
            Assert.That(game.State.CurrentSeat, Is.EqualTo(0));
            Assert.That(game.State.Hands[0].Count, Is.EqualTo(handCount));
        }

        [Test]
        public void Turn_MalformedMoveRequestsReturnErrorsWithoutChangingState()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H");
            var handCount = game.State.Hands[0].Count;
            var cardId = GameBuilder.ParseCard("2H").Id;

            AssertMoveError(game, GameCommand.MoveCards(0, new byte[0], CardTarget.NewMeld(0)),
                RuleError.EmptyMove);
            AssertMoveError(game, GameCommand.MoveCards(0, new[] { cardId, cardId }, CardTarget.NewMeld(0)),
                RuleError.DuplicateCard);
            AssertMoveError(game, GameCommand.MoveCards(0, new byte[] { byte.MaxValue }, CardTarget.NewMeld(0)),
                RuleError.UnknownCard);
            AssertMoveError(game, GameCommand.MoveCards(0, new[] { cardId }, CardTarget.NewMeld(1)),
                RuleError.InvalidTarget);
            AssertMoveError(game, GameCommand.MoveCards(0, new[] { cardId }, CardTarget.Meld(99, 0)),
                RuleError.UnknownMeld);
            AssertMoveError(game, GameCommand.MoveCards(0, new[] { cardId }, CardTarget.Hand(-1)),
                RuleError.InvalidTarget);
            AssertMoveError(game, GameCommand.MoveCards(0, new[] { cardId }, new CardTarget((TargetKind)99, 0, 0)),
                RuleError.InvalidTarget);
            Assert.That(game.State.Hands[0].Count, Is.EqualTo(handCount));
            Assert.That(game.State.Board, Is.Empty);
        }

        [Test]
        public void Turn_GameCommandCardIdsAreDefensivelyCopied()
        {
            var ids = new byte[] { 1, 2 };
            var command = GameCommand.MoveCards(0, ids, CardTarget.NewMeld(0));
            ids[0] = 9;
            var exposedIds = command.CardIds;
            exposedIds[1] = 8;

            Assert.That(command.CardIds, Is.EqualTo(new byte[] { 1, 2 }));
            Assert.That(default(GameCommand).CardIds, Is.Empty);
        }

        [Test]
        public void Turn_UnsupportedCommandReturnsReasonCode()
        {
            var game = NewGame();

            var result = game.Apply(new GameCommand(CommandType.Invalid, 0));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.UnsupportedCommand));
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

        private static string Notation(Card card)
        {
            var rank = card.Rank == Rank.Ace ? "A" :
                card.Rank == Rank.Jack ? "J" :
                card.Rank == Rank.Queen ? "Q" :
                card.Rank == Rank.King ? "K" : ((int)card.Rank + 1).ToString();
            var suit = card.Suit == Suit.Clubs ? "C" :
                card.Suit == Suit.Diamonds ? "D" :
                card.Suit == Suit.Hearts ? "H" : "S";
            return rank + suit;
        }

        private static void AssertMoveError(MejnunGame game, GameCommand command, RuleError error)
        {
            var result = game.Apply(command);
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(error));
        }
    }
}