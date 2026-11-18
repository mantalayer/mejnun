using NUnit.Framework;
using Mejnun.Core.Tests.TestUtil;

namespace Mejnun.Core.Tests
{
    public sealed class EndConditionTests
    {
        [Test]
        public void Win_EmptyHandAndValidBoardEndsImmediately()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H", "4H");
            GameResult won = null;
            game.Events.Won += result => won = result;

            var result = game.Apply(GameCommand.MoveCards(0, Ids("2H", "3H", "4H"), CardTarget.NewMeld(0)));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.Phase, Is.EqualTo(GamePhase.Finished));
            Assert.That(game.State.Result.Kind, Is.EqualTo(GameEndKind.EmptyHand));
            Assert.That(game.State.Result.Winner, Is.EqualTo(0));
            Assert.That(game.State.Result.NextFirstPlayer, Is.EqualTo(0));
            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.Down));
            Assert.That(won, Is.SameAs(game.State.Result));
        }

        [Test]
        public void Win_EmptyHandAndInvalidBoardDoesNotEndGame()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "4H", "5H");

            var result = game.Apply(GameCommand.MoveCards(0, Ids("2H", "4H", "5H"), CardTarget.NewMeld(0)));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.Hands[0], Is.Empty);
            Assert.That(game.State.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(game.State.Result, Is.Null);
        }

        [Test]
        public void Win_EmptyHandWinsWhenBoardBecomesValidLaterInTheTurn()
        {
            var game = NewGame();
            GameBuilder.SetBoard(game, new[] { "3H", "4H", "5H" });
            GameBuilder.SetHand(game, 0, "2H");
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H"), CardTarget.NewMeld(0))).Ok, Is.True);
            Assert.That(game.State.Result, Is.Null);

            var result = game.Apply(GameCommand.MoveCards(0, Ids("2H"), CardTarget.Meld(1, 0)));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.Phase, Is.EqualTo(GamePhase.Finished));
            Assert.That(game.State.Result.Kind, Is.EqualTo(GameEndKind.EmptyHand));
        }

        [Test]
        public void EmptyPile_PassesWithUniqueFewestCardsWinAndWinnerDealsNext()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H");
            GameBuilder.SetHand(game, 1, "4C", "5C", "6C");
            GameBuilder.SetDrawPile(game);
            GameResult won = null;
            game.Events.Won += result => won = result;

            Assert.That(game.Apply(GameCommand.DrawOrPass(0)).Ok, Is.True);
            var result = game.Apply(GameCommand.DrawOrPass(1));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.Phase, Is.EqualTo(GamePhase.Finished));
            Assert.That(game.State.Result.Kind, Is.EqualTo(GameEndKind.FewestCards));
            Assert.That(game.State.Result.Winner, Is.EqualTo(0));
            Assert.That(game.State.Result.NextFirstPlayer, Is.EqualTo(0));
            Assert.That(won, Is.SameAs(game.State.Result));
        }

        [Test]
        public void EmptyPile_TiedFewestCardsEndsInStalemateAndRequestsDealerPrompt()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H");
            GameBuilder.SetHand(game, 1, "4C", "5C");
            GameBuilder.SetDrawPile(game);
            GameResult stalemate = null;
            game.Events.Stalemate += result => stalemate = result;

            Assert.That(game.Apply(GameCommand.DrawOrPass(0)).Ok, Is.True);
            var result = game.Apply(GameCommand.DrawOrPass(1));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.Phase, Is.EqualTo(GamePhase.Finished));
            Assert.That(game.State.Result.Kind, Is.EqualTo(GameEndKind.Stalemate));
            Assert.That(game.State.Result.Winner, Is.Null);
            Assert.That(game.State.Result.NextFirstPlayer, Is.Null);
            Assert.That(stalemate, Is.SameAs(game.State.Result));
        }

        [Test]
        public void EmptyPile_PlacingAHandCardResetsConsecutivePasses()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 1, "2H", "3H", "4H", "5C");
            GameBuilder.SetDrawPile(game);
            Assert.That(game.Apply(GameCommand.DrawOrPass(0)).Ok, Is.True);
            Assert.That(game.State.ConsecutivePasses, Is.EqualTo(1));
            Assert.That(game.Apply(GameCommand.MoveCards(1, Ids("2H", "3H", "4H"),
                CardTarget.NewMeld(0))).Ok, Is.True);

            var result = game.Apply(GameCommand.EndTurn(1));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.ConsecutivePasses, Is.EqualTo(0));
            Assert.That(game.State.Phase, Is.EqualTo(GamePhase.Playing));
        }

        [Test]
        public void Game_RejectsCommandsAfterItHasFinished()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H", "4H");
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "3H", "4H"),
                CardTarget.NewMeld(0))).Ok, Is.True);
            var handCount = game.State.Hands[0].Count;

            var result = game.Apply(GameCommand.DrawOrPass(0));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.GameFinished));
            Assert.That(game.State.Hands[0].Count, Is.EqualTo(handCount));
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