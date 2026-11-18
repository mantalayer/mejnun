using Mejnun.Core;
using NUnit.Framework;

namespace Mejnun.AI.Tests
{
    public sealed class BoardFixerTests
    {
        [Test]
        public void FixBoard_ReturnsInvalidHandMeldAndKeepsValidNewMeld()
        {
            var game = AiTestUtil.Game(new[] { "2C", "3C", "4C", "6D", "8H" }, new string[0][], false);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2C", "3C", "4C"), CardTarget.NewMeld(0))).Ok,
                Is.True);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("6D", "8H"), CardTarget.NewMeld(0))).Ok, Is.True);

            ApplyFix(game);

            Assert.That(game.State.Board.Count, Is.EqualTo(1));
            Assert.That(game.State.Board[0].Cards.ConvertAll(Notation), Is.EqualTo(new[] { "2C", "3C", "4C" }));
            Assert.That(game.State.Hands[0].Count, Is.EqualTo(2));
            AssertBoardValid(game);
        }

        [Test]
        public void FixBoard_ReturnsStrayCardInTemporaryMeld()
        {
            var game = AiTestUtil.Game(new[] { "5H", "9C" }, new[] { new[] { "2H", "3H", "4H" } }, true);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("5H"), CardTarget.Meld(1, 3))).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("9C"), CardTarget.NewMeld(0))).Ok, Is.True);

            ApplyFix(game);

            Assert.That(game.State.Board.Count, Is.EqualTo(1));
            Assert.That(game.State.Board[0].Cards.ConvertAll(Notation),
                Is.EqualTo(new[] { "2H", "3H", "4H", "5H" }));
            Assert.That(game.State.Hands[0], Does.Contain(AiTestUtil.Parse("9C")));
            AssertBoardValid(game);
        }

        [Test]
        public void FixBoard_GlobalPassRehomesOrphanedStartBoardCard()
        {
            var game = AiTestUtil.Game(new[] { "10S" }, new[] { new[] { "2H", "3H", "4H", "5H" } }, true);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("5H"), CardTarget.NewMeld(0))).Ok, Is.True);

            ApplyFix(game);

            Assert.That(game.State.Board.Count, Is.EqualTo(1));
            Assert.That(game.State.Board[0].Cards.ConvertAll(Notation),
                Is.EqualTo(new[] { "2H", "3H", "4H", "5H" }));
            AssertBoardValid(game);
        }

        [Test]
        public void FixBoard_MovesDoubleConflictBeforeAddingTargetCard()
        {
            var game = AiTestUtil.Game(new string[0], new[]
            {
                new[] { "2H", "3H", "4H", "5H'", "5C" },
                new[] { "5H", "5D", "5S", "6H" }
            }, true);

            ApplyFix(game);

            AssertBoardValid(game);
        }

        [Test]
        public void Timeout_AfterFixingValidHandPlacementEndsWithoutDrawing()
        {
            var game = AiTestUtil.Game(new[] { "5H", "9C" }, new[] { new[] { "2H", "3H", "4H" } }, true,
                "AH");
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("5H"), CardTarget.Meld(1, 3))).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("9C"), CardTarget.NewMeld(0))).Ok, Is.True);

            ApplyFix(game);
            var result = game.Apply(GameCommand.TimeoutTurn(0));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.CurrentSeat, Is.EqualTo(1));
            Assert.That(game.State.DrawPile.Count, Is.EqualTo(1));
            Assert.That(game.State.Hands[0], Does.Contain(AiTestUtil.Parse("9C")));
            AssertBoardValid(game);
        }

        [Test]
        public void Timeout_WithNoPlacedCardDrawsAndEndsTurn()
        {
            var game = AiTestUtil.Game(new[] { "5C" }, new string[0][], true, "AH");

            var result = game.Apply(GameCommand.TimeoutTurn(0));

            Assert.That(result.Ok, Is.True);
            Assert.That(game.State.Hands[0], Does.Contain(AiTestUtil.Parse("AH")));
            Assert.That(game.State.CurrentSeat, Is.EqualTo(1));
        }

        [Test]
        public void FixBoard_HintsOffOnlyReturnsCardsFromInvalidSets()
        {
            var game = AiTestUtil.Game(new[] { "5H", "9C" }, new[] { new[] { "2H", "3H", "4H" } }, true);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("5H"), CardTarget.Meld(1, 3))).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("9C"), CardTarget.NewMeld(0))).Ok, Is.True);

            ApplyFix(game, false);

            Assert.That(game.State.Board.Count, Is.EqualTo(1));
            Assert.That(game.State.Board[0].Cards.ConvertAll(Notation),
                Is.EqualTo(new[] { "2H", "3H", "4H", "5H" }));
            Assert.That(game.State.Hands[0], Does.Contain(AiTestUtil.Parse("9C")));
            AssertBoardValid(game);
        }

        [Test]
        public void FixBoard_HintsOffDoesNotSolveADumpedHand()
        {
            var hand = new[] { "2C", "3C", "4C", "9H", "KD" };
            var game = AiTestUtil.Game(hand, new[] { new[] { "7S", "8S", "9S" } }, true);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids(hand), CardTarget.NewMeld(0))).Ok, Is.True);

            var withHints = SnapshotSerializer.Read(SnapshotSerializer.Write(game));
            ApplyFix(withHints);
            ApplyFix(game, false);

            Assert.That(withHints.State.Hands[0], Is.EquivalentTo(AiTestUtil.Cards("9H", "KD")));
            Assert.That(game.State.Hands[0].Count, Is.EqualTo(hand.Length));
            Assert.That(game.State.Board.Count, Is.EqualTo(1));
            AssertBoardValid(game);
        }

        [Test]
        public void FixBoard_HintsOffRevertsWhenOnlyBoardCardsAreMisplaced()
        {
            var game = AiTestUtil.Game(new[] { "10S" }, new[] { new[] { "2H", "3H", "4H", "5H" } }, true);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("5H"), CardTarget.NewMeld(0))).Ok, Is.True);

            var commands = BoardFixer.Plan(game, false);

            Assert.That(commands.Count, Is.EqualTo(1));
            Assert.That(commands[0].Type, Is.EqualTo(CommandType.Revert));
        }

        [Test]
        public void FixBoard_KeepsGoDownCardsOnTheBoard()
        {
            var game = AiTestUtil.Game(new[] { "2H", "3H", "4H", "9C" }, new[] { new[] { "7S", "8S", "9S" } }, false);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "3H", "4H"), CardTarget.NewMeld(0))).Ok,
                Is.True);
            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.Down));
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("4H", "9C"), CardTarget.NewMeld(0))).Ok, Is.True);

            ApplyFix(game);

            Assert.That(game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.Down));
            Assert.That(game.State.Hands[0], Is.EquivalentTo(AiTestUtil.Cards("9C")));
            Assert.That(AiTestUtil.BoardCardCount(game.State), Is.EqualTo(6));
            AssertBoardValid(game);
        }

        [Test]
        public void FixBoard_HintsOffRevertsRatherThanReturningGoDownCards()
        {
            var game = AiTestUtil.Game(new[] { "2H", "3H", "4H", "9C" }, new string[0][], false);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "3H", "4H"), CardTarget.NewMeld(0))).Ok,
                Is.True);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("4H"), CardTarget.NewMeld(0))).Ok, Is.True);

            var commands = BoardFixer.Plan(game, false);

            Assert.That(commands.Count, Is.EqualTo(1));
            Assert.That(commands[0].Type, Is.EqualTo(CommandType.Revert));
        }

        private static void ApplyFix(MejnunGame game, bool allowRearrange = true)
        {
            var commands = BoardFixer.Plan(game, allowRearrange);
            for (var index = 0; index < commands.Count; index++)
            {
                Assert.That(game.Apply(commands[index]).Ok, Is.True, "Fix command " + index + " failed.");
                AssertMeldLimits(game);
            }
        }

        private static void AssertMeldLimits(MejnunGame game)
        {
            for (var meldIndex = 0; meldIndex < game.State.Board.Count; meldIndex++)
            {
                var cards = game.State.Board[meldIndex].Cards;
                Assert.That(cards.Count, Is.LessThanOrEqualTo(13));
                for (var cardIndex = 0; cardIndex < cards.Count; cardIndex++)
                {
                    for (var otherIndex = 0; otherIndex < cardIndex; otherIndex++)
                    {
                        Assert.That(cards[cardIndex].IsDoubleOf(cards[otherIndex]), Is.False,
                            "A fix command put a double in meld " + game.State.Board[meldIndex].Id + ".");
                    }
                }
            }
        }

        private static void AssertBoardValid(MejnunGame game)
        {
            Assert.That(MeldValidator.IsBoardValid(game.State.Board), Is.True);
        }

        private static byte[] Ids(params string[] cards)
        {
            var ids = new byte[cards.Length];
            for (var index = 0; index < cards.Length; index++)
            {
                ids[index] = AiTestUtil.Parse(cards[index]).Id;
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