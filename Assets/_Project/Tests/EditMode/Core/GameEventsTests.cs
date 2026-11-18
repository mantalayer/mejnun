using NUnit.Framework;
using Mejnun.Core.Tests.TestUtil;

namespace Mejnun.Core.Tests
{
    public sealed class GameEventsTests
    {
        [Test]
        public void Events_AllSetupAndTurnEventsReachSubscribers()
        {
            var game = new MejnunGame(new GameSetup(2, 91UL));
            var dealerChosen = 0;
            var facesAssigned = 0;
            var feltAssigned = 0;
            var cardsDealt = 0;
            var turnStarted = 0;
            var cardsMoved = 0;
            var validMelds = 0;
            var invalidMelds = 0;
            var wentDown = 0;
            var undone = 0;
            var reverted = 0;
            var drew = 0;
            var passed = 0;
            var turnEnded = 0;

            game.Events.DealerChosen += seat => dealerChosen++;
            game.Events.DeckFacesAssigned += (first, second) => facesAssigned++;
            game.Events.TableFeltAssigned += feltId => feltAssigned++;
            game.Events.CardDealt += (seat, card) => cardsDealt++;
            game.Events.TurnStarted += (seat, turnNumber) => turnStarted++;
            game.Events.CardMoved += (card, from, to) => cardsMoved++;
            game.Events.MeldBecameValid += (meldId, kind) => validMelds++;
            game.Events.MeldBecameInvalid += (meldId, reason) => invalidMelds++;
            game.Events.WentDown += seat => wentDown++;
            game.Events.Undone += seat => undone++;
            game.Events.BoardReverted += seat => reverted++;
            game.Events.Drew += (seat, card) => drew++;
            game.Events.Passed += seat => passed++;
            game.Events.TurnEnded += (seat, nextSeat) => turnEnded++;

            Assert.That(game.Apply(GameCommand.ChooseDealer(1)).Ok, Is.True);
            GameBuilder.SetHand(game, 0, "2H", "3H", "4H", "5C");
            GameBuilder.SetDrawPile(game, "AH");
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H", "3H"),
                CardTarget.NewMeld(0))).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("4H"), CardTarget.Meld(1, 2))).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("2H"), CardTarget.Hand(0))).Error,
                Is.EqualTo(RuleError.GoDownCardToHand));
            Assert.That(game.Apply(GameCommand.MoveCards(0, Ids("5C"), CardTarget.NewMeld(0))).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.Undo(0)).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.Revert(0)).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.DrawOrPass(0)).Ok, Is.True);
            GameBuilder.SetDrawPile(game);
            Assert.That(game.Apply(GameCommand.DrawOrPass(1)).Ok, Is.True);

            Assert.That(dealerChosen, Is.EqualTo(1));
            Assert.That(facesAssigned, Is.EqualTo(1));
            Assert.That(feltAssigned, Is.EqualTo(1));
            Assert.That(cardsDealt, Is.EqualTo(22));
            Assert.That(turnStarted, Is.EqualTo(3));
            Assert.That(cardsMoved, Is.EqualTo(4));
            Assert.That(validMelds, Is.GreaterThanOrEqualTo(1));
            Assert.That(invalidMelds, Is.GreaterThanOrEqualTo(2));
            Assert.That(wentDown, Is.EqualTo(1));
            Assert.That(undone, Is.EqualTo(1));
            Assert.That(reverted, Is.EqualTo(1));
            Assert.That(drew, Is.EqualTo(1));
            Assert.That(passed, Is.EqualTo(1));
            Assert.That(turnEnded, Is.EqualTo(2));
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