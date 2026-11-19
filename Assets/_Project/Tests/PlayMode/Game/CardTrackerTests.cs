using System.Collections.Generic;
using Mejnun.AI;
using Mejnun.Core;
using NUnit.Framework;

namespace Mejnun.Game.Tests
{
    public sealed class CardTrackerTests
    {
        [Test]
        public void TracksCardsMovedFromBoardIntoAnOpponentHand()
        {
            var state = CreateState();
            var tracker = new CardTracker();
            tracker.Observe(state);

            state.Board.Clear();
            state.Hands[0].Add(Card.FromId(5));
            tracker.Observe(state);
            var knowledge = tracker.CreateKnowledge(state);
            var board = new List<IReadOnlyList<Card>>
            {
                new List<Card> { Card.FromId(2), Card.FromId(3), Card.FromId(4) }
            };

            Assert.That(knowledge.WouldEnableKnownExtender(board, 1), Is.True);
        }

        [Test]
        public void RecordsDrawWhileDownAndLowersInferenceConfidence()
        {
            var state = CreateState();
            state.PlayerPhases[0] = PlayerPhase.Down;
            var tracker = new CardTracker();
            tracker.Observe(state);

            state.Board.Clear();
            state.Hands[0].Add(Card.FromId(5));
            tracker.Observe(state);
            var knowledge = tracker.CreateKnowledge(state);
            var board = new List<IReadOnlyList<Card>>
            {
                new List<Card> { Card.FromId(2), Card.FromId(3), Card.FromId(4) }
            };

            Assert.That(knowledge.DrewWhileDown(0), Is.True);
            Assert.That(knowledge.WouldEnableKnownExtender(board, 1), Is.False);
        }

        [Test]
        public void BoardArrangementChangesPresentationOrderOnly()
        {
            var game = new MejnunGame(new GameSetup(2, 1UL, 1));
            game.State.Board.Clear();
            game.State.Board.Add(new Meld(1, new List<Card> { Card.FromId(2), Card.FromId(3), Card.FromId(4) }));
            game.State.Board.Add(new Meld(2, new List<Card> { Card.FromId(15), Card.FromId(16), Card.FromId(17) }));
            var before = SnapshotSerializer.Write(game);
            var arrangement = new BoardArrangement();

            arrangement.Arrange(game.State.Board);
            Assert.That(arrangement.Move(1, 2), Is.True);
            var ordered = arrangement.Arrange(game.State.Board);

            Assert.That(ordered[0].Id, Is.EqualTo(2));
            Assert.That(ordered[1].Id, Is.EqualTo(1));
            CollectionAssert.AreEqual(before, SnapshotSerializer.Write(game));
        }

        [Test]
        public void HandOrderReordersASelectedGroupAndKeepsItsRelativeOrder()
        {
            var order = new HandOrder();
            var hand = new List<Card>
            {
                Card.FromId(10), Card.FromId(11), Card.FromId(12), Card.FromId(13), Card.FromId(14)
            };
            order.Synchronize(0, hand);

            order.ReorderGroup(0, new byte[] { 11, 12 }, 4);

            CollectionAssert.AreEqual(new byte[] { 10, 13, 14, 11, 12 }, order.GetCardIds(0));
        }

        [Test]
        public void PortraitHandUsesTwoRowsWhenItHasManyCards()
        {
            Assert.That(HandView.CalculateRowCount(720, 1280, 14), Is.EqualTo(2));
            Assert.That(HandView.CalculateRowCount(1280, 720, 14), Is.EqualTo(1));
            Assert.That(HandView.CalculateRowCount(720, 1280, 8), Is.EqualTo(1));
        }

        [Test]
        public void HandLayoutFitsOneRowAndOnlyFallsBackWhenPortraitIsTooNarrow()
        {
            var rows = HandView.CalculateRowLayout(true, 10, 100f, 60f, 1000f, out var fittedStep);
            Assert.That(rows, Is.EqualTo(1));
            Assert.That(fittedStep, Is.EqualTo(60f));

            rows = HandView.CalculateRowLayout(true, 10, 100f, 80f, 800f, out fittedStep);
            Assert.That(rows, Is.EqualTo(1));
            Assert.That(fittedStep, Is.EqualTo(700f / 9f).Within(0.001f));

            rows = HandView.CalculateRowLayout(true, 10, 100f, 80f, 350f, out fittedStep);
            Assert.That(rows, Is.EqualTo(2));
        }

        [Test]
        public void HandArcAmountControlsCardLiftAndTilt()
        {
            HandView.CalculateArcPose(-1f, 0f, out var edgeOffset, out var edgeAngle);
            HandView.CalculateArcPose(0.5f, 0f, out var flatOffset, out var flatAngle);

            Assert.That(edgeOffset, Is.Zero);
            Assert.That(edgeAngle, Is.Zero);
            Assert.That(flatOffset, Is.Zero);
            Assert.That(flatAngle, Is.Zero);

            HandView.CalculateArcPose(0.5f, 0.5f, out var liftedOffset, out var tiltedAngle);

            Assert.That(liftedOffset, Is.GreaterThan(0f));
            Assert.That(tiltedAngle, Is.Not.Zero);
        }

        private static GameState CreateState()
        {
            var game = new MejnunGame(new GameSetup(2, 1UL, 1));
            game.State.Board.Clear();
            game.State.Board.Add(new Meld(1,
                new List<Card> { Card.FromId(2), Card.FromId(3), Card.FromId(4), Card.FromId(5) }));
            return game.State;
        }
    }
}