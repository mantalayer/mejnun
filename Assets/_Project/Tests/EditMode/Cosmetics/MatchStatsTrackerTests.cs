using Mejnun.Core;
using Mejnun.Cosmetics;
using NUnit.Framework;

namespace Mejnun.Cosmetics.Tests
{
    public sealed class MatchStatsTrackerTests
    {
        [Test]
        public void TracksCardsPlayedBigEntranceCleanSweepAndArchitect()
        {
            var tracker = new MatchStatsTracker();
            tracker.Reset(30);
            var hand = new byte[] { 0, 13, 26, 1, 14, 27, 2, 15, 28 };
            tracker.Observe(Snapshot(0, 1, hand), false);

            var final = Snapshot(1, 2, new byte[0], true, 10, 0,
                Meld(1, MeldKind.SameKind, 0, 13, 26),
                Meld(2, MeldKind.SameKind, 1, 14, 27),
                Meld(3, MeldKind.SameKind, 2, 15, 28));
            var details = tracker.Finish(new GameResult(GameEndKind.EmptyHand, 0, 0), final);

            Assert.That(details.TurnsPlayed, Is.EqualTo(1));
            Assert.That(details.LocalTurns, Is.EqualTo(1));
            Assert.That(details.CardsPlayedFromHand, Is.EqualTo(9));
            Assert.That(details.MostCardsInOneTurn, Is.EqualTo(9));
            Assert.That(details.SetsCreated, Is.EqualTo(3));
            Assert.That(details.SameKindsCreated, Is.EqualTo(3));
            Assert.That(details.GoDowns, Is.EqualTo(1));
            Assert.That(details.TurnsToGoDown, Is.EqualTo(1));
            Assert.That(details.SetsAtGoDown, Is.EqualTo(3));
            Assert.That(details.BigEntrance, Is.True);
            Assert.That(details.Architect, Is.True);
            Assert.That(details.CleanSweep, Is.True);
        }

        [Test]
        public void TracksRoyalRoadFromAThirteenCardStraight()
        {
            var tracker = new MatchStatsTracker();
            var hand = new byte[14];
            for (var index = 0; index < 13; index++)
            {
                hand[index] = (byte)index;
            }
            hand[13] = 50;
            tracker.Observe(Snapshot(0, 1, hand), false);

            var final = Snapshot(1, 2, new byte[] { 50 }, false, 10, 0,
                Meld(1, MeldKind.StraightLow, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12));
            var details = tracker.Finish(null, final);

            Assert.That(details.LongestStraight, Is.EqualTo(13));
            Assert.That(details.StraightsCreated, Is.EqualTo(1));
            Assert.That(details.RoyalRoad, Is.True);
            Assert.That(details.CardsPlayedFromHand, Is.EqualTo(13));
        }

        [Test]
        public void TracksLastGaspWhenHandIsEmptiedWithNoDrawCards()
        {
            var tracker = new MatchStatsTracker();
            tracker.Observe(Snapshot(0, 1, new byte[] { 0, 1, 2 }, drawPileCount: 0), false);

            var final = Snapshot(1, 2, new byte[0], true, 0, 0,
                Meld(1, MeldKind.StraightLow, 0, 1, 2));
            var details = tracker.Finish(new GameResult(GameEndKind.EmptyHand, 0, 0), final);

            Assert.That(details.LastGasp, Is.True);
        }

        [Test]
        public void CountsDrawsAndPassesAcrossTwoLocalTurns()
        {
            var tracker = new MatchStatsTracker();
            tracker.Observe(Snapshot(0, 1, new byte[] { 0, 1 }, drawPileCount: 5), false);
            tracker.Observe(Snapshot(0, 1, new byte[] { 0, 1, 2 }, drawPileCount: 4), false);
            tracker.Observe(Snapshot(1, 2, new byte[] { 0, 1, 2 }, drawPileCount: 4), false);
            tracker.Observe(Snapshot(0, 3, new byte[] { 0, 1, 2 }, drawPileCount: 4), false);

            var final = Snapshot(1, 4, new byte[] { 0, 1, 2 }, drawPileCount: 4, consecutivePasses: 1);
            var details = tracker.Finish(null, final);

            Assert.That(details.TurnsPlayed, Is.EqualTo(2));
            Assert.That(details.LocalTurns, Is.EqualTo(2));
            Assert.That(details.Draws, Is.EqualTo(1));
            Assert.That(details.Passes, Is.EqualTo(1));
        }

        [Test]
        public void TracksDoubleSurgeonBorrowedCardAndTimerPressure()
        {
            var doubleTracker = new MatchStatsTracker();
            doubleTracker.Observe(Snapshot(0, 1, new byte[] { 52 }, board: new[]
            {
                Meld(1, MeldKind.SameKind, 0, 1, 2)
            }), false);
            var doubleDetails = doubleTracker.Finish(null, Snapshot(1, 2, new byte[0], board: new[]
            {
                Meld(1, MeldKind.SameKind, 0, 1), Meld(2, MeldKind.SameKind, 52, 2)
            }));
            Assert.That(doubleDetails.SeeingDouble, Is.True);

            var boardTracker = new MatchStatsTracker();
            boardTracker.Observe(Snapshot(0, 1, new byte[] { 40, 41 }, board: new[]
            {
                Meld(10, MeldKind.StraightLow, 1, 2, 3, 4),
                Meld(11, MeldKind.SameKind, 0, 13, 26, 39)
            }), false);
            boardTracker.NoteRemainingSeconds(2.5f);
            var boardDetails = boardTracker.Finish(null, Snapshot(1, 2, new byte[0], board: new[]
            {
                Meld(10, MeldKind.StraightLow, 1, 2),
                Meld(12, MeldKind.StraightLow, 3, 4),
                Meld(11, MeldKind.SameKind, 0, 13, 26),
                Meld(13, MeldKind.StraightLow, 39, 40, 41)
            }));

            Assert.That(boardDetails.Surgeon, Is.True);
            Assert.That(boardDetails.BorrowedTime, Is.True);
            Assert.That(boardDetails.CoolUnderPressure, Is.True);
        }

        [Test]
        public void CountsOnlyExplicitAssistAndTimeoutCalls()
        {
            var tracker = new MatchStatsTracker();
            tracker.RecordUndo();
            tracker.RecordRevert();
            tracker.RecordFixBoard();
            tracker.RecordTimeout();

            var details = tracker.Finish(null, (TableSnapshot)null);

            Assert.That(details.UndosUsed, Is.EqualTo(1));
            Assert.That(details.RevertsUsed, Is.EqualTo(1));
            Assert.That(details.FixBoardsUsed, Is.EqualTo(1));
            Assert.That(details.Timeouts, Is.EqualTo(1));
        }

        private static TableSnapshot Snapshot(int currentSeat, int turnNumber, byte[] hand,
            bool localIsDown = false, int drawPileCount = 10, int consecutivePasses = 0,
            params TableMeldSnapshot[] board)
        {
            return new TableSnapshot(0, currentSeat, turnNumber, hand, drawPileCount, localIsDown, board,
                consecutivePasses: consecutivePasses);
        }

        private static TableMeldSnapshot Meld(int id, MeldKind kind, params byte[] cardIds)
        {
            return new TableMeldSnapshot(id, kind, cardIds);
        }
    }
}