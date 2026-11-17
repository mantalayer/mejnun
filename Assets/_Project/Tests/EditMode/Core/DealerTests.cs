using NUnit.Framework;

namespace Mejnun.Core.Tests
{
    public sealed class DealerTests
    {
        [Test]
        public void Dealer_GivenPreviousWinnerDealsImmediately()
        {
            var game = new MejnunGame(new GameSetup(4, 12UL, dealer: 2));

            Assert.That(game.State.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(game.State.Dealer, Is.EqualTo(2));
            Assert.That(game.State.CurrentSeat, Is.EqualTo(3));
            Assert.That(game.State.Hands[0].Count, Is.EqualTo(11));
        }

        [Test]
        public void Dealer_WithoutPreviousWinnerWaitsForChoice()
        {
            var game = new MejnunGame(new GameSetup(3, 77UL));

            Assert.That(game.State.Phase, Is.EqualTo(GamePhase.ChoosingDealer));
            Assert.That(game.State.Dealer, Is.Null);
            Assert.That(game.State.Hands.TrueForAll(hand => hand.Count == 0), Is.True);
            Assert.That(game.State.DrawPile, Is.Empty);
        }

        [Test]
        public void Dealer_ChooseDealerDealsAndStartsToTheLeft()
        {
            var game = new MejnunGame(new GameSetup(3, 77UL));
            var chosenDealer = -1;
            game.Events.DealerChosen += seat => chosenDealer = seat;

            var result = game.Apply(GameCommand.ChooseDealer(2));

            Assert.That(result.Ok, Is.True);
            Assert.That(chosenDealer, Is.EqualTo(2));
            Assert.That(game.State.Dealer, Is.EqualTo(2));
            Assert.That(game.State.CurrentSeat, Is.EqualTo(0));
            Assert.That(game.State.Hands[0].Count, Is.EqualTo(11));
        }

        [Test]
        public void Dealer_ChooseRandomIsDeterministicForTheSameSeed()
        {
            var first = new MejnunGame(new GameSetup(6, 1234UL));
            var second = new MejnunGame(new GameSetup(6, 1234UL));

            Assert.That(first.Apply(GameCommand.ChooseRandomDealer()).Ok, Is.True);
            Assert.That(second.Apply(GameCommand.ChooseRandomDealer()).Ok, Is.True);

            Assert.That(first.State.Dealer, Is.EqualTo(second.State.Dealer));
            for (var seat = 0; seat < 6; seat++)
            {
                CollectionAssert.AreEqual(first.State.Hands[seat], second.State.Hands[seat]);
            }

            CollectionAssert.AreEqual(first.State.DrawPile, second.State.DrawPile);
            CollectionAssert.AreEqual(first.State.DeckFaces, second.State.DeckFaces);
        }

        [Test]
        public void Dealer_InvalidSeatIsRejectedWithoutChangingState()
        {
            var game = new MejnunGame(new GameSetup(3, 8UL));
            var stateBefore = game.State.Rng.State;

            var result = game.Apply(GameCommand.ChooseDealer(3));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.InvalidSeat));
            Assert.That(game.State.Phase, Is.EqualTo(GamePhase.ChoosingDealer));
            Assert.That(game.State.Dealer, Is.Null);
            Assert.That(game.State.Rng.State, Is.EqualTo(stateBefore));
        }

        [Test]
        public void Dealer_ChoiceCommandIsRejectedAfterDealing()
        {
            var game = new MejnunGame(new GameSetup(3, 8UL, dealer: 1));
            var dealerBefore = game.State.Dealer;

            var result = game.Apply(GameCommand.ChooseDealer(2));

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.WrongPhase));
            Assert.That(game.State.Dealer, Is.EqualTo(dealerBefore));
        }

        [Test]
        public void Dealer_RandomChoiceAfterDealingDoesNotAdvanceRandomState()
        {
            var game = new MejnunGame(new GameSetup(3, 8UL, dealer: 1));
            var rngState = game.State.Rng.State;

            var result = game.Apply(GameCommand.ChooseRandomDealer());

            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(RuleError.WrongPhase));
            Assert.That(game.State.Rng.State, Is.EqualTo(rngState));
        }

        [Test]
        public void Setup_RejectsNullGameSetup()
        {
            Assert.Throws<System.ArgumentNullException>(() => new MejnunGame(null));
        }

        [TestCase(1)]
        [TestCase(7)]
        public void Dealer_SetupRejectsSeatCountsOutsideTwoThroughSix(int seats)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new GameSetup(seats, 0UL));
        }

        [Test]
        public void Dealer_SetupRejectsDealerOutsideSeatRange()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new GameSetup(2, 0UL, dealer: 2));
        }

        [Test]
        public void Setup_RejectsNegativeTurnSeconds()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new GameSetup(2, 0UL, turnSeconds: -1));
        }

        [Test]
        public void Setup_RejectsNullFaceIds()
        {
            Assert.Throws<System.ArgumentException>(() => new GameSetup(2, 0UL,
                facePool: new string[] { null }));
        }
    }
}