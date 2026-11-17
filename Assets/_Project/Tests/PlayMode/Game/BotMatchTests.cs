using System.Collections;
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
            Assert.That(flow.GetSeatLabel(1), Is.EqualTo("Leyla (Hard bot)"));

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
            Assert.That(flow.Table.ViewerSeat, Is.EqualTo(-1));

            flow.Controller.SetHintsEnabled(true);
            var expected = PlayableHints.Find(flow.Game.State);
            CollectionAssert.AreEquivalent(expected, flow.Controller.HintedCardIds);
            foreach (var pair in flow.Table.ActiveCards)
            {
                Assert.That(pair.Value.IsHinted, Is.EqualTo(expected.Contains(pair.Key)));
            }
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
