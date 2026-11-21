using NUnit.Framework;

namespace Mejnun.Net.Tests
{
    public sealed class FirstPlayerVoteTests
    {
        [Test]
        public void Resolve_MajorityVoteWins()
        {
            var winner = FirstPlayerVote.Resolve(new[] { 1, 1, 2 },
                new[] { true, true, true }, new[] { true, true, true }, new System.Random(1));

            Assert.That(winner, Is.EqualTo(1));
        }

        [Test]
        public void Resolve_TieSelectsOneOfTheTiedCandidates()
        {
            var winner = FirstPlayerVote.Resolve(new[] { 1, 0 },
                new[] { true, true }, new[] { true, true }, new System.Random(2));

            Assert.That(winner == 0 || winner == 1, Is.True);
        }

        [Test]
        public void Resolve_AllSelfVotesChooseRandomCandidate()
        {
            var seed = 17;
            var expected = new System.Random(seed).Next(3);

            var winner = FirstPlayerVote.Resolve(new[] { 0, 1, 2 },
                new[] { true, true, true }, new[] { true, true, true }, new System.Random(seed));

            Assert.That(winner, Is.EqualTo(expected));
        }

        [Test]
        public void Resolve_RandomVoteIsCountedAsRandomCandidateVote()
        {
            var winner = FirstPlayerVote.Resolve(new[] { FirstPlayerVote.RandomVote, 1, 1 },
                new[] { true, true, true }, new[] { true, true, true }, new System.Random(3));

            Assert.That(winner, Is.EqualTo(1));
        }

        [Test]
        public void Resolve_SingleCandidateWins()
        {
            var winner = FirstPlayerVote.Resolve(new[]
                {
                    FirstPlayerVote.NoVote,
                    1,
                    FirstPlayerVote.NoVote
                },
                new[] { false, true, false }, new[] { false, true, false }, new System.Random(4));

            Assert.That(winner, Is.EqualTo(1));
        }

        [Test]
        public void NetCodec_RoundTripsFirstPlayerVoteStatus()
        {
            var message = new FirstPlayerVoteStatusMessage(true, "dealer.question.example",
                new[] { true, false, true }, new[] { 2, FirstPlayerVote.NoVote, FirstPlayerVote.RandomVote });

            var decoded = (FirstPlayerVoteStatusMessage)NetCodec.Decode(NetCodec.Encode(message));

            Assert.That(decoded.IsOpen, Is.True);
            Assert.That(decoded.QuestionKey, Is.EqualTo("dealer.question.example"));
            CollectionAssert.AreEqual(new[] { true, false, true }, decoded.Candidates);
            CollectionAssert.AreEqual(new[] { 2, FirstPlayerVote.NoVote, FirstPlayerVote.RandomVote }, decoded.Votes);
        }
    }
}