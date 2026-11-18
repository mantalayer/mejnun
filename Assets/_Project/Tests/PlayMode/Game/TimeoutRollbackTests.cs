using Mejnun.Core;
using NUnit.Framework;
using UnityEngine;

namespace Mejnun.Game.Tests
{
    public sealed class TimeoutRollbackTests
    {
        [Test]
        public void GetTimeoutUndoCount_StopsAtTheLastValidBoardPrefixWithoutChangingTheLiveGame()
        {
            var game = GestureTestGameFactory.CreateGameWithStraightInHand(4, out var straight);
            var seat = game.State.CurrentSeat;
            Assert.That(game.Apply(GameCommand.MoveCards(seat,
                new[] { straight[0], straight[1], straight[2] }, CardTarget.NewMeld(0))).Ok, Is.True);
            Assert.That(game.Apply(GameCommand.MoveCards(seat, new[] { straight[3] }, CardTarget.NewMeld(0))).Ok,
                Is.True);
            Assert.That(MeldValidator.IsBoardValid(game.State.Board), Is.False);

            var gameObject = new GameObject("Timeout rollback test");
            try
            {
                var controller = gameObject.AddComponent<TableController>();
                controller.Initialize(game);

                Assert.That(controller.GetTimeoutUndoCount(), Is.EqualTo(1));
                Assert.That(game.Turn.Moves.Count, Is.EqualTo(2));
                Assert.That(MeldValidator.IsBoardValid(game.State.Board), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}