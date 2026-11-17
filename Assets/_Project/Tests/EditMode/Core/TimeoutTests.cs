using NUnit.Framework;
using Mejnun.Core.Tests.TestUtil;

namespace Mejnun.Core.Tests
{
    public sealed class TimeoutTests
    {
        [Test]
        public void Timeout_NoHandCardPlacedAndValidBoardKeepsBoardThenDraws()
        {
            var game = NewGame();
            GameBuilder.SetBoard(game, new[] { "2H", "3H", "4H", "5H", "6H", "7H" });
            GameBuilder.SetHand(game, 0, "5C");
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);
            GameBuilder.SetDrawPile(game, "AH");
            Assert.That(game.Apply(GameCommand.SplitMeld(0, 1, 3)).Ok, Is.True);

            var result = game.Apply(GameCommand.TimeoutTurn(0));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.Board.Count, Is.EqualTo(2));
            Assert.That(game.State.Board.TrueForAll(meld => MeldValidator.Classify(meld.Cards).IsValid), Is.True);
            Assert.That(game.State.Hands[0], Does.Contain(GameBuilder.ParseCard("AH")));
            Assert.That(game.State.DrawPile, Is.Empty);
            Assert.That(game.State.CurrentSeat, Is.EqualTo(1));
        }

        [Test]
        public void Timeout_NoHandCardPlacedAndInvalidBoardRevertsThenDraws()
        {
            var game = NewGame();
            GameBuilder.SetBoard(game, new[] { "2H", "3H", "4H", "5H" });
            GameBuilder.SetHand(game, 0, "5C");
            GameBuilder.SetPlayerPhase(game, 0, PlayerPhase.Down);
            GameBuilder.SetDrawPile(game, "AH");
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("3H"), CardTarget.NewMeld(0))).Ok, Is.True);

            var result = game.Apply(GameCommand.TimeoutTurn(0));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.Board.Count, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { "2H", "3H", "4H", "5H" },
                game.State.Board[0].Cards.ConvertAll(Notation));
            Assert.That(game.State.Hands[0], Does.Contain(GameBuilder.ParseCard("AH")));
            Assert.That(game.State.CurrentSeat, Is.EqualTo(1));
        }

        [Test]
        public void Timeout_HandCardPlacedAndValidBoardEndsWithoutDrawing()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "3H", "4H", "5C");
            GameBuilder.SetDrawPile(game, "AH");
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "3H", "4H"),
                CardTarget.NewMeld(0))).Ok, Is.True);

            var result = game.Apply(GameCommand.TimeoutTurn(0));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.Down));
            Assert.That(game.State.Hands[0].Count, Is.EqualTo(1));
            Assert.That(game.State.DrawPile.Count, Is.EqualTo(1));
            Assert.That(game.State.CurrentSeat, Is.EqualTo(1));
        }

        [Test]
        public void Timeout_HandCardPlacedAndInvalidBoardRevertsThenDraws()
        {
            var game = NewGame();
            GameBuilder.SetHand(game, 0, "2H", "4H", "5H", "6C");
            GameBuilder.SetDrawPile(game, "AH");
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "4H", "5H"),
                CardTarget.NewMeld(0))).Ok, Is.True);

            var result = game.Apply(GameCommand.TimeoutTurn(0));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.Board, Is.Empty);
            Assert.That(game.State.Hands[0].Count, Is.EqualTo(5));
            Assert.That(game.State.Hands[0], Does.Contain(GameBuilder.ParseCard("2H")));
            Assert.That(game.State.Hands[0], Does.Contain(GameBuilder.ParseCard("AH")));
            Assert.That(game.State.CurrentSeat, Is.EqualTo(1));
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
    }
}