using NUnit.Framework;

namespace Mejnun.Net.Tests
{
    public sealed class PendingJoinGateTests
    {
        [Test]
        public void Evaluate_AcceptsPendingJoinAfterMembershipAppears()
        {
            var gate = new PendingJoinGate();
            gate.Add(4, new JoinMessage("player", "Player"), 0);

            var result = gate.Evaluate(2, request => request.AuthenticationId == "player");

            Assert.That(result.Accepted.Count, Is.EqualTo(1));
            Assert.That(result.Accepted[0].ClientId, Is.EqualTo(4));
            Assert.That(result.Expired, Is.Empty);
            Assert.That(gate.Count, Is.Zero);
        }

        [Test]
        public void Evaluate_ExpiresJoinWhenMembershipNeverAppears()
        {
            var gate = new PendingJoinGate();
            gate.Add(4, new JoinMessage("player", "Player"), 0);

            var result = gate.Evaluate(PendingJoinGate.DefaultTimeoutSeconds, request => false);

            Assert.That(result.Accepted, Is.Empty);
            Assert.That(result.Expired.Count, Is.EqualTo(1));
            Assert.That(result.Expired[0].AuthenticationId, Is.EqualTo("player"));
            Assert.That(gate.Count, Is.Zero);
        }

        [Test]
        public void Add_ReplacesPendingJoinForSameClient()
        {
            var gate = new PendingJoinGate();
            gate.Add(4, new JoinMessage("old-player", "Old"), 0);
            gate.Add(4, new JoinMessage("new-player", "New"), 5);

            var result = gate.Evaluate(6, request => true);

            Assert.That(result.Accepted.Count, Is.EqualTo(1));
            Assert.That(result.Accepted[0].AuthenticationId, Is.EqualTo("new-player"));
            Assert.That(result.Accepted[0].DisplayName, Is.EqualTo("New"));
        }

        [Test]
        public void Remove_DropsPendingJoinWhenClientDisconnects()
        {
            var gate = new PendingJoinGate();
            gate.Add(4, new JoinMessage("player", "Player"), 0);

            Assert.That(gate.Remove(4), Is.True);
            Assert.That(gate.Remove(4), Is.False);
            Assert.That(gate.Evaluate(1, request => true).Accepted, Is.Empty);
        }

        [Test]
        public void Clear_RemovesAllPendingJoinsForANewNetworkSession()
        {
            var gate = new PendingJoinGate();
            gate.Add(4, new JoinMessage("player-a", "A"), 0);
            gate.Add(5, new JoinMessage("player-b", "B"), 0);

            gate.Clear();

            Assert.That(gate.Count, Is.Zero);
            Assert.That(gate.Evaluate(1, request => true).Accepted, Is.Empty);
        }

        [Test]
        public void ContainsAuthentication_ReportsPendingJoinForPlayer()
        {
            var gate = new PendingJoinGate();
            gate.Add(4, new JoinMessage("player", "Player"), 0);

            Assert.That(gate.ContainsAuthentication("player"), Is.True);
            Assert.That(gate.ContainsAuthentication("other-player"), Is.False);
            gate.Remove(4);
            Assert.That(gate.ContainsAuthentication("player"), Is.False);
        }
    }
}