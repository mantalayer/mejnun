using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Mejnun.Core.Tests.TestUtil;

namespace Mejnun.Core.Tests
{
    public sealed class DealTests
    {
        [TestCase(2, 82)]
        [TestCase(3, 71)]
        [TestCase(4, 60)]
        [TestCase(5, 49)]
        [TestCase(6, 38)]
        public void Deal_GivesEverySeatElevenCardsAndLeavesExpectedPile(int seats, int pileSize)
        {
            var game = GameBuilder.Create(seatCount: seats, seed: 56UL);

            Assert.That(game.State.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(game.State.Hands.Count, Is.EqualTo(seats));
            Assert.That(game.State.Hands.All(hand => hand.Count == 11), Is.True);
            Assert.That(game.State.DrawPile.Count, Is.EqualTo(pileSize));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void Deal_StartsLeftOfDealerAndDealsClockwise(int seats)
        {
            var game = new MejnunGame(new GameSetup(seats, 99UL));
            var dealtSeats = new List<int>();
            game.Events.CardDealt += (seat, card) => dealtSeats.Add(seat);

            var result = game.Apply(GameCommand.ChooseDealer(1));

            Assert.That(result.Ok, Is.True);
            var expected = new List<int>(seats * 11);
            for (var round = 0; round < 11; round++)
            {
                for (var offset = 1; offset <= seats; offset++)
                {
                    expected.Add((1 + offset) % seats);
                }
            }

            CollectionAssert.AreEqual(expected, dealtSeats);
            Assert.That(game.State.CurrentSeat, Is.EqualTo(2 % seats));
            Assert.That(game.State.TurnNumber, Is.EqualTo(1));
        }

        [Test]
        public void Deal_UsesEveryCardExactlyOnceAcrossHandsAndPile()
        {
            var game = GameBuilder.Create(seatCount: 6, seed: 314UL, dealer: 3);
            var allCards = game.State.Hands.SelectMany(hand => hand).Concat(game.State.DrawPile).ToArray();

            Assert.That(allCards.Length, Is.EqualTo(Card.CardCount));
            Assert.That(allCards.Select(card => card.Id).Distinct().Count(), Is.EqualTo(Card.CardCount));
        }

        [Test]
        public void Deal_EmitsDealerFacesCardsAndTurnEvents()
        {
            var game = new MejnunGame(new GameSetup(2, 7UL));
            var eventOrder = new List<string>();
            game.Events.DealerChosen += seat => eventOrder.Add("dealer");
            game.Events.DeckFacesAssigned += (first, second) => eventOrder.Add("faces");
            game.Events.CardDealt += (seat, card) => eventOrder.Add("card");
            game.Events.TurnStarted += (seat, turn) => eventOrder.Add("turn");

            game.Apply(GameCommand.ChooseDealer(0));

            Assert.That(eventOrder[0], Is.EqualTo("dealer"));
            Assert.That(eventOrder[1], Is.EqualTo("faces"));
            Assert.That(eventOrder.Skip(2).Take(22).All(item => item == "card"), Is.True);
            Assert.That(eventOrder.Last(), Is.EqualTo("turn"));
        }
    }
}