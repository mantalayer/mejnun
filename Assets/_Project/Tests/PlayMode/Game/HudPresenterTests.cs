using System.Collections;
using System.Reflection;
using Mejnun.Core;
using Mejnun.Game;
using Mejnun.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Mejnun.Game.Tests
{
    public sealed class HudPresenterTests
    {
        [Test]
        public void OpponentSidesScaleFromTopToLeftAndRightAsSeatsIncrease()
        {
            AssertOpponentSides(2, HudPresenter.OpponentSide.Top);
            AssertOpponentSides(3, HudPresenter.OpponentSide.Left, HudPresenter.OpponentSide.Right);
            AssertOpponentSides(4, HudPresenter.OpponentSide.Left, HudPresenter.OpponentSide.Top,
                HudPresenter.OpponentSide.Right);
            AssertOpponentSides(5, HudPresenter.OpponentSide.Left, HudPresenter.OpponentSide.Top,
                HudPresenter.OpponentSide.Top, HudPresenter.OpponentSide.Right);
        }

        [UnityTest]
        public IEnumerator TableSceneHudReflectsTheActiveTurnState()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var matchFlow = Object.FindAnyObjectByType<MatchFlow>();
            Assert.That(matchFlow, Is.Not.Null);
            Assert.That(matchFlow.StartMatch(3, new[] { "Sam", "Alex", "Lee" }, 202601), Is.True);
            yield return WaitForRoundInitialization(matchFlow);
            Assert.That(matchFlow.ChooseDealer(0).Ok, Is.True);
            Assert.That(matchFlow.RevealHand(), Is.True);

            var presenter = Object.FindAnyObjectByType<HudPresenter>();
            var tableView = Object.FindAnyObjectByType<TableView>();
            Assert.That(tableView, Is.Not.Null);
            var document = presenter.GetComponent<UIDocument>();
            Assert.That(document, Is.Not.Null);
            var root = document.rootVisualElement;
            var drawPileStack = root.Q<VisualElement>("draw-pile-stack");
            var drawPileLabel = root.Q<Label>("draw-pile-label");
            var endTurn = root.Q<Button>("end-turn-button");
            var exitButton = root.Q<Button>("menu-button");
            var pauseButton = root.Q<Button>("pause-button");
            var hintButton = root.Q<Button>("hint-button");
            var helpButton = root.Q<Button>("help-button");
            var pausePanel = root.Q<VisualElement>("pause-panel");
            var drawPass = root.Q<Button>("draw-pass-button");
            var undo = root.Q<Button>("undo-button");
            var fixBoard = root.Q<Button>("fix-board-button");
            var turnPopup = root.Q<Label>("turn-popup");
            var goneDownPopup = root.Q<Label>("gone-down-popup");
            var goneDownTag = root.Q<Label>("gone-down-tag");
            var opponents = root.Q<VisualElement>("opponents-strip");
            var topOpponents = root.Q<VisualElement>("top-opponents");
            var bottomArea = root.Q<VisualElement>(className: "bottom-area");
            var statusPanel = root.Q<VisualElement>("status-panel");
            var turnPanel = root.Q<VisualElement>("turn-panel");
            var turnLabel = root.Q<Label>("turn-label");
            var timerLabel = root.Q<Label>("timer-label");
            var bottomBar = root.Q<VisualElement>("bottom-bar");
            var leftOpponents = root.Q<VisualElement>("left-opponents");
            var rightOpponents = root.Q<VisualElement>("right-opponents");

            Assert.That(endTurn, Is.Not.Null);
            Assert.That(exitButton, Is.Not.Null);
            Assert.That(exitButton.text, Is.EqualTo("Main menu"));
            Assert.That(pauseButton, Is.Not.Null);
            Assert.That(pauseButton.text, Is.EqualTo("☰"));
            Assert.That(pauseButton.parent, Is.Not.SameAs(bottomBar));
            Assert.That(pausePanel, Is.Not.Null);
            Assert.That(exitButton.parent, Is.SameAs(pausePanel));
            Assert.That(hintButton, Is.Not.Null);
            Assert.That(hintButton.parent, Is.SameAs(pausePanel));
            Assert.That(helpButton, Is.Not.Null);
            Assert.That(helpButton.parent, Is.SameAs(pausePanel));
            Assert.That(drawPileStack, Is.Not.Null);
            Assert.That(drawPileStack.childCount, Is.EqualTo(Mathf.Min(6,
                Mathf.CeilToInt(matchFlow.Game.State.DrawPile.Count / 6f))));
            Assert.That(endTurn.enabledInHierarchy, Is.False);
            Assert.That(drawPass.text, Is.EqualTo("Draw"));
            Assert.That(undo.enabledInHierarchy, Is.False);
            Assert.That(fixBoard, Is.Not.Null);
            Assert.That(fixBoard.enabledInHierarchy, Is.False);
            Assert.That(root.Q<Button>("go-down-button"), Is.Null);
            Assert.That(turnPopup, Is.Not.Null);
            Assert.That(goneDownPopup, Is.Not.Null);
            Assert.That(goneDownTag, Is.Not.Null);
            Assert.That(root.Q<VisualElement>("top-bar"), Is.Null);
            Assert.That(bottomArea, Is.Not.Null);
            Assert.That(statusPanel, Is.Not.Null);
            Assert.That(turnPanel, Is.Not.Null);
            Assert.That(turnLabel, Is.Not.Null);
            Assert.That(timerLabel, Is.Not.Null);
            Assert.That(drawPileLabel, Is.Not.Null);
            Assert.That(statusPanel.parent, Is.SameAs(bottomArea));
            Assert.That(turnPanel.parent, Is.SameAs(root));
            Assert.That(turnLabel.parent, Is.SameAs(turnPanel));
            Assert.That(timerLabel.parent, Is.SameAs(turnPanel));
            Assert.That(turnLabel.parent, Is.Not.SameAs(statusPanel));
            Assert.That(bottomBar.parent, Is.SameAs(bottomArea));
            Assert.That(opponents.childCount, Is.EqualTo(2));
            Assert.That(topOpponents, Is.Not.Null);
            Assert.That(root.Q<Slider>("opponent-card-scale-slider"), Is.Not.Null);
            Assert.That(root.Q<Slider>("zoom-sensitivity-slider"), Is.Not.Null);
            Assert.That(root.Q<Toggle>("organic-board-toggle"), Is.Not.Null);
            Assert.That(root.Q<Slider>("card-messiness-slider"), Is.Not.Null);
            Assert.That(leftOpponents.childCount, Is.EqualTo(1));
            Assert.That(rightOpponents.childCount, Is.EqualTo(1));

            var statusPanelWidth = statusPanel.resolvedStyle.width;
            var drawPileLabelWidth = drawPileLabel.resolvedStyle.width;
            var drawPileStackWidth = drawPileStack.resolvedStyle.width;
            var drawPassWidth = drawPass.resolvedStyle.width;
            Assert.That(statusPanelWidth, Is.GreaterThan(0f));
            Assert.That(drawPileLabelWidth, Is.GreaterThan(0f));
            Assert.That(drawPileStackWidth, Is.GreaterThan(0f));
            Assert.That(drawPassWidth, Is.GreaterThan(0f));
            drawPileLabel.text = "Draw pile: 108";
            yield return null;
            Assert.That(statusPanel.resolvedStyle.width, Is.EqualTo(statusPanelWidth).Within(0.1f));
            Assert.That(drawPileLabel.resolvedStyle.width, Is.EqualTo(drawPileLabelWidth).Within(0.1f));
            Assert.That(drawPileStack.resolvedStyle.width, Is.EqualTo(drawPileStackWidth).Within(0.1f));
            Assert.That(drawPass.resolvedStyle.width, Is.EqualTo(drawPassWidth).Within(0.1f));
            drawPileLabel.text = "Draw pile: 0";
            yield return null;
            Assert.That(statusPanel.resolvedStyle.width, Is.EqualTo(statusPanelWidth).Within(0.1f));
            Assert.That(drawPileLabel.resolvedStyle.width, Is.EqualTo(drawPileLabelWidth).Within(0.1f));
            Assert.That(drawPileStack.resolvedStyle.width, Is.EqualTo(drawPileStackWidth).Within(0.1f));
            Assert.That(drawPass.resolvedStyle.width, Is.EqualTo(drawPassWidth).Within(0.1f));

            for (var seat = 0; seat < matchFlow.Game.State.Hands.Count; seat++)
            {
                if (seat == matchFlow.Game.State.CurrentSeat) continue;
                var cards = root.Q<VisualElement>("opponent-cards-" + seat);
                Assert.That(cards.ClassListContains("opponent-cards-vertical"), Is.True);
                var backs = cards.Query<VisualElement>(className: "opponent-card-back").ToList();
                Assert.That(backs.Count, Is.EqualTo(matchFlow.Game.State.Hands[seat].Count));
                for (var cardIndex = 0; cardIndex < backs.Count; cardIndex++)
                {
                    var expectedBack = tableView.GetCardBack(matchFlow.Game.State,
                        matchFlow.Game.State.Hands[seat][cardIndex].Deck);
                    Assert.That(expectedBack, Is.Not.Null);
                    Assert.That(backs[cardIndex].style.backgroundImage.value.sprite, Is.SameAs(expectedBack));
                }
            }

            matchFlow.SetPaused(true);
            Assert.That(drawPass.enabledInHierarchy, Is.False);
            matchFlow.SetPaused(false);
            Assert.That(drawPass.enabledInHierarchy, Is.True);

            matchFlow.Game.State.DrawPile.Clear();
            Assert.That(matchFlow.Controller.ApplyCommand(
                GameCommand.DrawOrPass(matchFlow.Game.State.CurrentSeat)).Ok, Is.True);
            Assert.That(drawPass.text, Is.EqualTo("Pass"));
            Assert.That(drawPileStack.childCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TurnTimerTimesOutAndRaisesHalasEvent()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var matchFlow = Object.FindAnyObjectByType<MatchFlow>();
            var seats = new[] { SeatConfig.Human("Sam"), SeatConfig.Human("Alex") };
            Assert.That(matchFlow.StartMatch(seats, 20261117UL, 10), Is.True);
            yield return WaitForRoundInitialization(matchFlow);
            Assert.That(matchFlow.ChooseDealer(0).Ok, Is.True);
            Assert.That(matchFlow.RevealHand(), Is.True);
            var startingSeat = matchFlow.Game.State.CurrentSeat;
            var timeoutRaised = false;
            matchFlow.TurnTimer.TimedOut += () => timeoutRaised = true;

            matchFlow.TurnTimer.Tick(11f);

            Assert.That(timeoutRaised, Is.True);
            Assert.That(matchFlow.TurnTimer.IsRunning, Is.False);
            Assert.That(matchFlow.Game.State.CurrentSeat,
                Is.EqualTo((startingSeat + 1) % matchFlow.Game.State.Hands.Count));
        }

        [UnityTest]
        public IEnumerator MatchTimeOnlyAccumulatesDuringHumanTurns()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var matchFlow = Object.FindAnyObjectByType<MatchFlow>();
            Assert.That(matchFlow, Is.Not.Null);
            var botDriver = matchFlow.GetComponent<BotTurnDriver>();
            Assert.That(botDriver, Is.Not.Null);
            botDriver.enabled = false;
            var seats = new[] { SeatConfig.Human("Sam"), SeatConfig.Bot("CPU", Mejnun.AI.BotDifficulty.Medium) };

            Assert.That(matchFlow.StartMatch(seats, 20261118UL), Is.True);
            yield return WaitForRoundInitialization(matchFlow);
            Assert.That(matchFlow.ChooseDealer(0).Ok, Is.True);
            Assert.That(matchFlow.Game.State.CurrentSeat, Is.EqualTo(1));
            Assert.That(matchFlow.IsBotSeat(matchFlow.Game.State.CurrentSeat), Is.True);
            yield return null;
            yield return null;
            Assert.That(GetElapsedMatchSeconds(matchFlow), Is.Zero);

            Assert.That(matchFlow.StartMatch(seats, 20261119UL), Is.True);
            yield return WaitForRoundInitialization(matchFlow);
            Assert.That(matchFlow.ChooseDealer(1).Ok, Is.True);
            Assert.That(matchFlow.Game.State.CurrentSeat, Is.Zero);
            Assert.That(matchFlow.IsBotSeat(matchFlow.Game.State.CurrentSeat), Is.False);
            yield return null;
            yield return null;
            Assert.That(GetElapsedMatchSeconds(matchFlow), Is.GreaterThan(0f));
        }

        [Test]
        public void PhoneLayoutAppliesToPhoneShapedOrSmallMobileScreens()
        {
            Assert.That(HudPresenter.IsPhoneScreen(true, 2532, 1170, 460f), Is.True);
            Assert.That(HudPresenter.IsPhoneScreen(true, 1170, 2532, 460f), Is.True);
            Assert.That(HudPresenter.IsPhoneScreen(true, 2732, 2048, 264f), Is.False);
            Assert.That(HudPresenter.IsPhoneScreen(false, 1320, 2868, 96f), Is.True);
            Assert.That(HudPresenter.IsPhoneScreen(false, 2868, 1320, 96f), Is.True);
            Assert.That(HudPresenter.IsPhoneScreen(false, 1920, 1080, 96f), Is.False);
            Assert.That(HudPresenter.IsPhoneScreen(false, 2560, 1600, 96f), Is.False);
            Assert.That(HudPresenter.IsPhoneScreen(true, 1920, 1080, 0f), Is.True);
            Assert.That(HudPresenter.IsPhoneScreen(true, 2560, 1600, 0f), Is.False);
        }

        private static void AssertOpponentSides(int seatCount, params HudPresenter.OpponentSide[] expected)
        {
            var opponentCount = seatCount - 1;
            Assert.That(expected.Length, Is.EqualTo(opponentCount));
            for (var relativeSeat = 1; relativeSeat <= opponentCount; relativeSeat++)
            {
                Assert.That(HudPresenter.GetOpponentSide(relativeSeat, opponentCount),
                    Is.EqualTo(expected[relativeSeat - 1]));
            }
        }

        private static float GetElapsedMatchSeconds(MatchFlow matchFlow)
        {
            var field = typeof(MatchFlow).GetField("elapsedMatchSeconds",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (float)field.GetValue(matchFlow);
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