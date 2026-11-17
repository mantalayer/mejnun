using NUnit.Framework;
using Mejnun.Core.Tests.TestUtil;

namespace Mejnun.Core.Tests
{
    public sealed class NotDownTests
    {
        [Test]
        public void NotDown_CreatesNewSetsUsingOnlyHandCards()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H", "4H", "5C");

            var result = game.Apply(GameCommand.MoveCards(0, Ids("2H", "3H", "4H"), CardTarget.NewMeld(0)));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.Hands[0].Count, Is.EqualTo(1));
            Assert.That(game.State.Board.Count, Is.EqualTo(1));
            Assert.That(MeldValidator.Classify(game.State.Board[0].Cards).Kind, Is.EqualTo(MeldKind.StraightLow));
            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.Down));
        }

        [Test]
        public void NotDown_CannotTouchMeldThatStartedOnBoard()
        {
            var game = NewGame();
            GameBuilder.SetBoard(game, new[] { "2H", "3H", "4H" });
            GameBuilder.SetHand(game, 0, "5H");
            var boardBefore = BoardSignature(game);

            var result = game.Apply(GameCommand.MoveCards(0, Ids("2H"), CardTarget.NewMeld(0)));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.NotDownBoardAccess));
            Assert.That(BoardSignature(game), Is.EqualTo(boardBefore));
        }

        [Test]
        public void NotDown_CannotAddHandCardToMeldThatStartedOnBoard()
        {
            var game = NewGame();
            GameBuilder.SetBoard(game, new[] { "2H", "3H", "4H" });
            GameBuilder.SetHand(game, 0, "5H");

            var result = game.Apply(GameCommand.MoveCards(0, Ids("5H"), CardTarget.Meld(1, 3)));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.NotDownBoardAccess));
            Assert.That(game.State.Board[0].Cards.Count, Is.EqualTo(3));
            Assert.That(game.State.Hands[0].Count, Is.EqualTo(1));
        }

        [Test]
        public void NotDown_GoDownRequiresAtLeastOneValidNewSet()
        {
            var game = NewGame();

            var result = game.Apply(GameCommand.GoDown(0));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.GoDownInvalid));
            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.NotDown));
        }

        [Test]
        public void NotDown_GoDownRejectsAnInvalidNewSet()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "4H", "5H");
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "4H", "5H"),
                CardTarget.NewMeld(0))).Ok, Is.True);

            var result = game.Apply(GameCommand.GoDown(0));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.GoDownInvalid));
            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.NotDown));
        }

        [Test]
        public void NotDown_MultipleValidHandOnlySetsAutomaticallyGoDown()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H", "4H", "5C", "6C", "7C", "8D");
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "3H", "4H"),
                CardTarget.NewMeld(0))).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("5C", "6C", "7C"),
                CardTarget.NewMeld(0))).Ok, Is.True);

            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.Down));
            Assert.That(game.CanGoDown(), Is.False);
            Assert.That(game.State.Board.Count, Is.EqualTo(2));
        }

        [Test]
        public void NotDown_AutomaticGoDownAllowsExistingBoardAccessInSameTurn()
        {
            var game = NewGame();
            GameBuilder.SetBoard(game, new[] { "8C", "9C", "10C" });
            GameBuilder.SetHand(game, 0, "2H", "3H", "4H", "5C");
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "3H", "4H"),
                CardTarget.NewMeld(0))).Ok, Is.True);
            var result = game.Apply(GameCommand.MoveCards(0, Ids("8C"), CardTarget.NewMeld(0)));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.Down));
            Assert.That(game.State.Board.Count, Is.EqualTo(3));
        }

        [Test]
        public void NotDown_UndoingTheSetReturnsPlayerToNotDown()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H", "4H", "5C");
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "3H"),
                CardTarget.NewMeld(0))).Ok, Is.True);
            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.NotDown));
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("4H"), CardTarget.Meld(1, 2))).Ok, Is.True);
            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.Down));

            Assert.That(game.Apply(GameCommand.Undo(0)).Ok, Is.True);

            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.NotDown));
            Assert.That(game.State.Board.Count, Is.EqualTo(1));
            Assert.That(game.Turn.Moves.Count, Is.EqualTo(1));
            Assert.That(game.Apply(GameCommand.Undo(0)).Ok, Is.True);
            Assert.That(game.State.Board, Is.Empty);
            Assert.That(game.State.Hands[0].Count, Is.EqualTo(4));
        }

        [Test]
        public void NotDown_UndoAndRedoRaisesWentDownAgain()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H", "4H", "5C");
            var wentDown = 0;
            game.Events.WentDown += seat => wentDown++;

            var move = GameCommand.MoveCards(0, Ids("2H", "3H", "4H"), CardTarget.NewMeld(0));
            Assert.That(game.Apply(move).Ok, Is.True);
            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.Down));
            Assert.That(wentDown, Is.EqualTo(1));

            Assert.That(game.Apply(GameCommand.Undo(0)).Ok, Is.True);
            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.NotDown));
            Assert.That(game.Apply(move).Ok, Is.True);

            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.Down));
            Assert.That(wentDown, Is.EqualTo(2));
        }

        [Test]
        public void NotDown_InvalidHandSetDoesNotGoDown()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "4H", "5H", "9C");
            var wentDown = 0;
            game.Events.WentDown += seat => wentDown++;

            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "4H", "5H"),
                CardTarget.NewMeld(0))).Ok, Is.True);

            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.NotDown));
            Assert.That(wentDown, Is.EqualTo(0));
        }

        [Test]
        public void NotDown_EndTurnRevealsTheNewSetsToEveryone()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H", "4H", "5C");
            var revealedSeat = -1;
            System.Collections.Generic.IReadOnlyList<int> revealedMelds = null;
            game.Events.DownRevealed += (seat, meldIds) =>
            {
                revealedSeat = seat;
                revealedMelds = meldIds;
            };

            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "3H", "4H"),
                CardTarget.NewMeld(0))).Ok, Is.True);
            Assert.That(revealedMelds, Is.Null);
            var meldId = game.State.Board[0].Id;

            Assert.That(game.Apply(GameCommand.EndTurn(0)).Ok, Is.True);

            Assert.That(revealedSeat, Is.EqualTo(0));
            Assert.That(revealedMelds, Is.EqualTo(new[] { meldId }));
        }

        [Test]
        public void Down_EndTurnDoesNotRevealAgainForPlayerAlreadyDown()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H", "4H", "5C");
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);
            var revealed = 0;
            game.Events.DownRevealed += (seat, meldIds) => revealed++;

            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "3H", "4H"),
                CardTarget.NewMeld(0))).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.EndTurn(0)).Ok, Is.True);

            Assert.That(revealed, Is.EqualTo(0));
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

        private static string BoardSignature(MejnunGame game)
        {
            var signature = string.Empty;
            for (var meldIndex = 0; meldIndex < game.State.Board.Count; meldIndex++)
            {
                signature += game.State.Board[meldIndex].Id + ":";
                for (var cardIndex = 0; cardIndex < game.State.Board[meldIndex].Cards.Count; cardIndex++)
                {
                    signature += game.State.Board[meldIndex].Cards[cardIndex].Id + ",";
                }
            }

            return signature;
        }
    }
}