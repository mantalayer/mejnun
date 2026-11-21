using Mejnun.AI;
using NUnit.Framework;

namespace Mejnun.Net.Tests
{
    public sealed class SeatRosterTests
    {
        [Test]
        public void Claim_ReconnectsTheSamePlayerDuringGrace()
        {
            var roster = new SeatRoster(1);
            var seat = roster.Claim("player-a", "A", 0, out var pendingHandBack);

            roster.MarkDropped("player-a");
            roster.Tick(5);
            var reclaimedSeat = roster.Claim("player-a", "A again", 0, out pendingHandBack);

            Assert.That(seat, Is.Zero);
            Assert.That(reclaimedSeat, Is.Zero);
            Assert.That(pendingHandBack, Is.False);
            Assert.That(roster.Get(0).Presence, Is.EqualTo(SeatPresence.Connected));
            Assert.That(roster.HasBlockingAbsence, Is.False);
        }

        [Test]
        public void Claim_ReservesGraceSeatUntilExpiry()
        {
            var roster = new SeatRoster(1);
            roster.Claim("player-a", "A", 0, out _);
            roster.MarkAway("player-a");

            Assert.That(roster.Claim("player-b", "B", 0, out _), Is.EqualTo(-1));

            var expired = roster.Tick(SeatRoster.DefaultGraceSeconds);
            Assert.That(expired, Is.EquivalentTo(new[] { 0 }));
            Assert.That(roster.Get(0).Presence, Is.EqualTo(SeatPresence.Disconnected));
            Assert.That(roster.Claim("player-b", "B", 0, out _), Is.Zero);
            Assert.That(roster.Get(0).AuthenticationId, Is.EqualTo("player-b"));
            Assert.That(roster.HasBlockingAbsence, Is.False);
        }

        [Test]
        public void Claim_PrefersDisconnectedSeatsToEmptySeats()
        {
            var roster = new SeatRoster(2);
            roster.Claim("player-a", "A", 0, out _);
            roster.MarkDropped("player-a");
            roster.Tick(SeatRoster.DefaultGraceSeconds);

            var seat = roster.Claim("player-b", "B", 0, out _);

            Assert.That(seat, Is.Zero);
            Assert.That(roster.Get(1).Presence, Is.EqualTo(SeatPresence.Empty));
        }

        [Test]
        public void ClaimFromBotControl_WaitsForCurrentTurnThenHandsBack()
        {
            var roster = new SeatRoster(2);
            roster.Claim("player-a", "A", 0, out _);
            roster.MarkDropped("player-a");
            roster.Tick(SeatRoster.DefaultGraceSeconds);
            roster.SetBotControl(0, BotDifficulty.Hard);

            var seat = roster.Claim("player-b", "B", 0, out var pendingHandBack);

            Assert.That(seat, Is.Zero);
            Assert.That(pendingHandBack, Is.True);
            Assert.That(roster.Get(0).Presence, Is.EqualTo(SeatPresence.BotControlled));
            Assert.That(roster.Get(0).PendingHandBack, Is.True);

            roster.CompleteHandBacks(1);

            Assert.That(roster.Get(0).Presence, Is.EqualTo(SeatPresence.Connected));
            Assert.That(roster.Get(0).PendingHandBack, Is.False);
        }

        [Test]
        public void ClaimFromBotControl_HandsBackImmediatelyWhenItIsNotTheCurrentSeat()
        {
            var roster = new SeatRoster(2);
            roster.Claim("player-a", "A", 0, out _);
            roster.MarkDropped("player-a");
            roster.Tick(SeatRoster.DefaultGraceSeconds);
            roster.SetBotControl(0, BotDifficulty.Medium);

            roster.Claim("player-b", "B", 1, out var pendingHandBack);

            Assert.That(pendingHandBack, Is.False);
            Assert.That(roster.Get(0).Presence, Is.EqualTo(SeatPresence.Connected));
        }

        [Test]
        public void Tick_RejectsInvalidElapsedTime()
        {
            var roster = new SeatRoster(2);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => roster.Tick(-1));
        }
    }
}