using System.Collections;
using System.Collections.Generic;
using Mejnun.AI;
using Mejnun.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Mejnun.Game.Tests
{
    public sealed class BotMatchTests
    {
        private const int FrameLimit = 900;

        [UnityTest]
        public IEnumerator SoloMatchSkipsHandoffAndBotsPlayUntilTheHumansTurn()
        {
            LoadTable();
            yield return null;
            var flow = Object.FindAnyObjectByType<MatchFlow>();
            MakeBotsInstant(flow);

            Assert.That(flow.StartMatch(new[]
            {
                SeatConfig.Human("Sam"),
                SeatConfig.Bot("Leyla", BotDifficulty.Hard),
                SeatConfig.Bot("Kian", BotDifficulty.Easy)
            }, 4242UL), Is.True);
            Assert.That(flow.Screen, Is.EqualTo(MatchFlowScreen.DealerQuestion));
            Assert.That(flow.ChooseDealer(0).Ok, Is.True);

            Assert.That(flow.Game.State.CurrentSeat, Is.EqualTo(1));
            Assert.That(flow.Screen, Is.EqualTo(MatchFlowScreen.Turn));
            Assert.That(flow.Controller.InputEnabled, Is.False);
            Assert.That(flow.Table.ViewerSeat, Is.EqualTo(0));
            Assert.That(flow.Table.HandVisible, Is.True);
            Assert.That(flow.Controller.HandInputEnabled, Is.True);
            Assert.That(flow.Controller.HandSeat, Is.EqualTo(0));
            Assert.That(flow.GetSeatLabel(1), Is.EqualTo("Leyla (Hard bot)"));

            var hand = flow.Game.State.Hands[0];
            var initialHandOrder = new List<byte>(flow.Table.HandOrder.GetCardIds(0));
            var reorderedCardId = initialHandOrder[0];
            var reorder = flow.Controller.ExecuteHandGesture(new GestureInput(GestureType.Drag,
                GestureHit.HandCard(reorderedCardId, 0), GestureHit.HandArea(initialHandOrder.Count - 1)));
            Assert.That(reorder.Ok, Is.True);
            Assert.That(flow.Table.HandOrder.GetCardIds(0)[initialHandOrder.Count - 1], Is.EqualTo(reorderedCardId));

            var handOrderBeforeBoardDrop = new List<byte>(flow.Table.HandOrder.GetCardIds(0));
            var boardCountBeforeDrop = flow.Game.State.Board.Count;
            var handCardIndex = handOrderBeforeBoardDrop.Count - 1;
            var handCardId = handOrderBeforeBoardDrop[handCardIndex];
            var boardDrop = flow.Controller.ExecuteHandGesture(new GestureInput(GestureType.Drag,
                GestureHit.HandCard(handCardId, handCardIndex), GestureHit.EmptyBoard()));
            Assert.That(boardDrop.Ok, Is.True);
            Assert.That(flow.Game.State.Board.Count, Is.EqualTo(boardCountBeforeDrop));
            Assert.That(flow.Game.State.Hands[0].Count, Is.EqualTo(hand.Count));
            CollectionAssert.AreEqual(handOrderBeforeBoardDrop, flow.Table.HandOrder.GetCardIds(0));

            var frames = 0;
            while (flow.Game.State.Phase == GamePhase.Playing && flow.Game.State.CurrentSeat != 0 &&
                   frames++ < FrameLimit)
            {
                yield return null;
            }

            Assert.That(frames, Is.LessThan(FrameLimit), "The bots never finished their turns.");
            if (flow.Game.State.Phase != GamePhase.Playing)
            {
                Assert.That(flow.Screen, Is.EqualTo(MatchFlowScreen.Results));
                yield break;
            }

            Assert.That(flow.Game.State.TurnNumber, Is.EqualTo(3));
            Assert.That(flow.Screen, Is.EqualTo(MatchFlowScreen.Turn));
            Assert.That(flow.Controller.InputEnabled, Is.True);
            Assert.That(flow.Table.ViewerSeat, Is.EqualTo(0));

            flow.Controller.SetHintsEnabled(true);
            var expected = PlayableHints.Find(flow.Game.State);
            CollectionAssert.AreEquivalent(expected, flow.Controller.HintedCardIds);
            foreach (var pair in flow.Table.ActiveCards)
            {
                Assert.That(pair.Value.IsHinted, Is.EqualTo(expected.Contains(pair.Key)));
            }
        }

        [UnityTest]
        public IEnumerator DrawingAfterSortingKeepsExistingHandOrderAndCardViews()
        {
            LoadTable();
            yield return null;
            var flow = Object.FindAnyObjectByType<MatchFlow>();
            MakeBotsInstant(flow);

            Assert.That(flow.StartMatch(new[]
            {
                SeatConfig.Human("Sam"),
                SeatConfig.Bot("Leyla", BotDifficulty.Hard),
                SeatConfig.Bot("Kian", BotDifficulty.Easy)
            }, 4242UL), Is.True);
            Assert.That(flow.ChooseDealer(0).Ok, Is.True);

            var frames = 0;
            while (flow.Game.State.Phase == GamePhase.Playing && flow.Game.State.CurrentSeat != 0 &&
                   frames++ < FrameLimit)
            {
                yield return null;
            }

            Assert.That(frames, Is.LessThan(FrameLimit), "The bots never finished their turns.");
            Assert.That(flow.Game.State.Phase, Is.EqualTo(GamePhase.Playing));
            Assert.That(flow.Controller.InputEnabled, Is.True);
            Assert.That(flow.Table.ViewerSeat, Is.EqualTo(0));

            var hand = flow.Game.State.Hands[0];
            flow.Table.HandOrder.SortBySuit(0, hand);
            flow.Table.Sync(flow.Game.State, flow.Game.Turn);
            var sortedOrder = new List<byte>(flow.Table.HandOrder.GetCardIds(0));
            var existingViews = new Dictionary<byte, CardView>();
            for (var index = 0; index < sortedOrder.Count; index++)
            {
                existingViews.Add(sortedOrder[index], flow.Table.GetCardView(sortedOrder[index]));
            }

            Assert.That(flow.Controller.ApplyCommand(GameCommand.DrawOrPass(0)).Ok, Is.True);

            var drawnHand = flow.Game.State.Hands[0];
            var orderAfterDraw = flow.Table.HandOrder.GetCardIds(0);
            Assert.That(orderAfterDraw.Count, Is.EqualTo(sortedOrder.Count + 1));
            for (var index = 0; index < sortedOrder.Count; index++)
            {
                Assert.That(orderAfterDraw[index], Is.EqualTo(sortedOrder[index]));
                Assert.That(flow.Table.GetCardView(sortedOrder[index]), Is.SameAs(existingViews[sortedOrder[index]]));
            }

            var drawnCardId = orderAfterDraw[orderAfterDraw.Count - 1];
            CollectionAssert.DoesNotContain(sortedOrder, drawnCardId);
            Assert.That(drawnHand.Exists(card => card.Id == drawnCardId), Is.True);
            Assert.That(flow.Table.ViewerSeat, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator PassAndPlayShowsHandoffOnlyBetweenDifferentHumans()
        {
            LoadTable();
            yield return null;
            var flow = Object.FindAnyObjectByType<MatchFlow>();
            MakeBotsInstant(flow);

            Assert.That(flow.StartMatch(new[]
            {
                SeatConfig.Human("Sam"),
                SeatConfig.Bot("Leyla", BotDifficulty.Medium),
                SeatConfig.Human("Alex")
            }, 777UL), Is.True);
            Assert.That(flow.ChooseDealer(2).Ok, Is.True);
            Assert.That(flow.Screen, Is.EqualTo(MatchFlowScreen.Handoff));
            Assert.That(flow.RevealHand(), Is.True);
            Assert.That(flow.Controller.ApplyCommand(GameCommand.DrawOrPass(0)).Ok, Is.True);

            Assert.That(flow.Game.State.CurrentSeat, Is.EqualTo(1));
            Assert.That(flow.Screen, Is.EqualTo(MatchFlowScreen.Turn));
            Assert.That(flow.Table.HandVisible, Is.False, "No hand is shown during a bot turn with two humans.");
            Assert.That(flow.Controller.InputEnabled, Is.False);
            Assert.That(flow.Controller.HandInputEnabled, Is.False);
            Assert.That(flow.Controller.HandSeat, Is.EqualTo(-1));

            var frames = 0;
            while (flow.Game.State.Phase == GamePhase.Playing && flow.Game.State.CurrentSeat == 1 &&
                   frames++ < FrameLimit)
            {
                yield return null;
            }

            Assert.That(frames, Is.LessThan(FrameLimit));
            if (flow.Game.State.Phase == GamePhase.Playing)
            {
                Assert.That(flow.Game.State.CurrentSeat, Is.EqualTo(2));
                Assert.That(flow.Screen, Is.EqualTo(MatchFlowScreen.Handoff));
            }
        }

        [UnityTest]
        public IEnumerator AMatchNeedsAtLeastOneHuman()
        {
            LoadTable();
            yield return null;
            var flow = Object.FindAnyObjectByType<MatchFlow>();

            Assert.That(flow.StartMatch(new[]
            {
                SeatConfig.Bot("Leyla", BotDifficulty.Easy),
                SeatConfig.Bot("Kian", BotDifficulty.Hard)
            }), Is.False);
        }

        private static void LoadTable()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
        }

        private static void MakeBotsInstant(MatchFlow flow)
        {
            Assert.That(flow, Is.Not.Null);
            Assert.That(flow.BotDriver, Is.Not.Null);
            flow.BotDriver.ThinkSeconds = 0f;
            flow.BotDriver.StepSeconds = 0f;
        }
    }
}
