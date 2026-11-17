using NUnit.Framework;

namespace Mejnun.Core.Tests
{
    public sealed class ReplayTests
    {
        [Test]
        public void Replay_SameSeedAndCommandsProduceIdenticalSnapshots()
        {
            CollectionAssert.AreEqual(PlayDeterministicSequence(), PlayDeterministicSequence());
        }

        [Test]
        public void Replay_DifferentSeedsProduceDifferentSnapshots()
        {
            CollectionAssert.AreNotEqual(PlaySequence(100UL), PlaySequence(101UL));
        }

        private static byte[] PlayDeterministicSequence()
        {
            return PlaySequence(8675309UL);
        }

        private static byte[] PlaySequence(ulong seed)
        {
            var game = new MejnunGame(new GameSetup(4, seed,
                facePool: new[] { "classic", "classic_ivory", "face_extra" }));
            Assert.That(game.Apply(GameCommand.ChooseRandomDealer()).Ok, Is.True);

            var seat = game.State.CurrentSeat;
            var cardId = game.State.Hands[seat][0].Id;
            Assert.That(game.Apply(GameCommand.MoveCards(seat, new[] { cardId }, CardTarget.NewMeld(0))).Ok,
                Is.True);
            Assert.That(game.Apply(GameCommand.Undo(seat)).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.DrawOrPass(seat)).Ok, Is.True);

            return SnapshotSerializer.Write(game);
        }
    }
}