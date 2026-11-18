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
        public void Turn_PlacingValidHandOnlySetGoesDownAndEndTurnRevealsIt()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H", "4H", "5C");
            var wentDownSeat = -1;
            var revealedSeat = -1;
            game.Events.WentDown += seat => wentDownSeat = seat;
            game.Events.DownRevealed += (seat, meldIds) => revealedSeat = seat;
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "3H", "4H"),
                CardTarget.NewMeld(0))).Ok, Is.True);
            Assert.That(wentDownSeat, Is.EqualTo(0));
            Assert.That(revealedSeat, Is.EqualTo(-1));

            var result = game.Apply(GameCommand.EndTurn(0));

            Assert.That(result.Ok, Is.True);
            Assert.That(revealedSeat, Is.EqualTo(0));
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
            var reverted = 0;
            game.Events.BoardReverted += seat => reverted++;
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
            Assert.That(reverted, Is.EqualTo(1));
        }

        [Test]
        public void Turn_DrawOnUntouchedTurnDoesNotRevertBoard()
        {
            var game = NewGame();
            GameBuilder.SetDrawPile(game, "AH");
            var reverted = 0;
            game.Events.BoardReverted += seat => reverted++;

            var result = game.Apply(GameCommand.DrawOrPass(0));

            Assert.That(result.Ok, Is.True);
            Assert.That(reverted, Is.Zero);
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
        public void Turn_CannotMoveDoubleIntoMeld()
        {
            var game = NewGame();
            GameBuilder.SetBoard(game, new[] { "2H", "3H", "4H" });
            GameBuilder.SetHand(game, 0, "9C");
            var doubleCard = Card.FromId(GameBuilder.ParseCard("3H").Id + Card.CardsPerDeck);
            game.State.Hands[0].Add(doubleCard);
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);
            var originalMeld = game.State.Board[0].Cards.ToArray();
            var originalHand = game.State.Hands[0].ToArray();

            var result = game.Apply(GameCommand.MoveCards(0, new[] { doubleCard.Id }, CardTarget.Meld(1, 3)));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.DoubleInSet));
            CollectionAssert.AreEqual(originalMeld, game.State.Board[0].Cards);
            CollectionAssert.AreEqual(originalHand, game.State.Hands[0]);
            Assert.That(game.Turn.Moves, Is.Empty);
        }

        [Test]
        public void Turn_CannotCreateNewMeldContainingDouble()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "3H");
            var doubleCard = Card.FromId(GameBuilder.ParseCard("3H").Id + Card.CardsPerDeck);
            game.State.Hands[0].Add(doubleCard);
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);
            var originalHand = game.State.Hands[0].ToArray();

            var result = game.Apply(GameCommand.MoveCards(0,
                new[] { GameBuilder.ParseCard("3H").Id, doubleCard.Id }, CardTarget.NewMeld(0)));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.DoubleInSet));
            Assert.That(game.State.Board, Is.Empty);
            CollectionAssert.AreEqual(originalHand, game.State.Hands[0]);
            Assert.That(game.Turn.Moves, Is.Empty);
        }

        [Test]
        public void Turn_CannotMergeMeldsContainingDouble()
        {
            var game = NewGame();
            GameBuilder.SetBoard(game, new[] { "2H", "3H", "4H" }, new[] { "3H", "3C", "3D" });
            game.State.Board[1].Cards[0] = Card.FromId(GameBuilder.ParseCard("3H").Id + Card.CardsPerDeck);
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);
            var originalFirst = game.State.Board[0].Cards.ToArray();
            var originalSecond = game.State.Board[1].Cards.ToArray();

            var result = game.Apply(GameCommand.MergeMelds(0, 1, 2));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.DoubleInSet));
            Assert.That(game.State.Board.Count, Is.EqualTo(2));
            CollectionAssert.AreEqual(originalFirst, game.State.Board[0].Cards);
            CollectionAssert.AreEqual(originalSecond, game.State.Board[1].Cards);
            Assert.That(game.Turn.Moves, Is.Empty);
        }

        [Test]
        public void Turn_CannotGrowMeldBeyondThirteenCards()
        {
            var game = NewGame();
            var hearts = new[]
            {
                "AH", "2H", "3H", "4H", "5H", "6H", "7H", "8H", "9H", "10H", "JH", "QH", "KH"
            };
            GameBuilder.SetBoard(game, hearts);
            GameBuilder.SetHand(game, 0, "2C");
            var extraCard = Card.FromId(GameBuilder.ParseCard("2C").Id + Card.CardsPerDeck);
            game.State.Hands[0].Add(extraCard);
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);

            var result = game.Apply(GameCommand.MoveCards(0, new[] { extraCard.Id }, CardTarget.Meld(1, 13)));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.SetTooLarge));
            Assert.That(game.State.Board[0].Cards.Count, Is.EqualTo(13));
            Assert.That(game.State.Hands[0], Does.Contain(extraCard));
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