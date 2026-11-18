using System.Collections;
using Mejnun.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Mejnun.Game.Tests
{
    public sealed class TableSmokeTests
    {
        [UnityTest]
        public IEnumerator EmptyPilePassesEndInStalemateAndAskForADealerAgain()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var flow = Object.FindAnyObjectByType<MatchFlow>();
            Assert.That(flow.StartMatch(3, new[] { "Sam", "Alex", "Lee" }, 34567), Is.True);
            yield return WaitForRoundInitialization(flow);
            Assert.That(flow.ChooseDealer(0).Ok, Is.True);
            Assert.That(flow.RevealHand(), Is.True);
            flow.Game.State.DrawPile.Clear();

            for (var pass = 0; pass < 3; pass++)
            {
                var seat = flow.Game.State.CurrentSeat;
                Assert.That(flow.Controller.ApplyCommand(GameCommand.DrawOrPass(seat)).Ok, Is.True);
                if (pass < 2)
                {
                    Assert.That(flow.Table.HandVisible, Is.False);
                }
            }

            Assert.That(flow.Game.State.Phase, Is.EqualTo(GamePhase.Finished));
            Assert.That(flow.Game.State.Result.Kind, Is.EqualTo(GameEndKind.Stalemate));
            Assert.That(flow.Game.State.Result.NextFirstPlayer, Is.Null);
            Assert.That(flow.Screen, Is.EqualTo(MatchFlowScreen.Results));
            Assert.That(flow.Rematch(), Is.True);
            Assert.That(flow.Game.State.Phase, Is.EqualTo(GamePhase.ChoosingDealer));
            Assert.That(flow.Game.Setup.Dealer, Is.Null);
            Assert.That(flow.Screen, Is.EqualTo(MatchFlowScreen.DealerQuestion));
        }

        [UnityTest]
        public IEnumerator SixSeatsDealSixtySixCardsAndLeaveThirtyEightInThePile()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var flow = Object.FindAnyObjectByType<MatchFlow>();
            Assert.That(flow.StartMatch(6,
                new[] { "Sam", "Alex", "Lee", "Jo", "Morgan", "Riley" }, 98765), Is.True);
            yield return WaitForRoundInitialization(flow);
            Assert.That(flow.ChooseDealer(0).Ok, Is.True);

            var handCount = 0;
            for (var seat = 0; seat < flow.Game.State.Hands.Count; seat++)
            {
                handCount += flow.Game.State.Hands[seat].Count;
            }

            Assert.That(handCount, Is.EqualTo(66));
            Assert.That(flow.Game.State.DrawPile.Count, Is.EqualTo(38));
        }

        [UnityTest]
        public IEnumerator WinnerGoesFirstInTheRematch()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var flow = Object.FindAnyObjectByType<MatchFlow>();
            byte[] straight = null;
            var seat = -1;
            for (ulong seed = 1; seed < 512 && straight == null; seed++)
            {
                Assert.That(flow.StartMatch(2, new[] { "Sam", "Alex" }, seed), Is.True);
                yield return WaitForRoundInitialization(flow);
                Assert.That(flow.ChooseDealer(0).Ok, Is.True);
                seat = flow.Game.State.CurrentSeat;
                GestureTestGameFactory.TryFindStraight(flow.Game.State.Hands[seat], 3, out straight);
            }

            Assert.That(straight, Is.Not.Null);
            Assert.That(flow.RevealHand(), Is.True);
            var hand = flow.Game.State.Hands[seat];
            for (var index = hand.Count - 1; index >= 0; index--)
            {
                if (hand[index].Id != straight[0] && hand[index].Id != straight[1] && hand[index].Id != straight[2])
                {
                    hand.RemoveAt(index);
                }
            }

            var batch = new CommandBatch();
            batch.Add(GameCommand.MoveCards(seat, straight, CardTarget.NewMeld(0)));
            Assert.That(flow.Controller.ExecuteBatch(batch).Ok, Is.True);
            Assert.That(flow.Game.State.Result.Kind, Is.EqualTo(GameEndKind.EmptyHand));
            Assert.That(flow.Game.State.Result.Winner, Is.EqualTo(seat));
            Assert.That(flow.Game.State.Result.NextFirstPlayer, Is.EqualTo(seat));
            Assert.That(flow.Rematch(), Is.True);
            Assert.That(flow.Game.State.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(flow.Game.State.CurrentSeat, Is.EqualTo(seat));
            Assert.That(flow.Screen, Is.EqualTo(MatchFlowScreen.Handoff));
        }

        private static IEnumerator WaitForRoundInitialization(MatchFlow flow)
        {
            var frames = 0;
            while (flow.IsInitializingRound && frames++ < 300)
            {
                yield return null;
            }

            Assert.That(flow.IsInitializingRound, Is.False);
            Assert.That(flow.Game, Is.Not.Null);
        }
    }
}