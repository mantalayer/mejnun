using System.Collections;
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
            Assert.That(matchFlow.ChooseDealer(0).Ok, Is.True);
            Assert.That(matchFlow.RevealHand(), Is.True);

            var presenter = Object.FindAnyObjectByType<HudPresenter>();
            var tableView = Object.FindAnyObjectByType<TableView>();
            Assert.That(tableView, Is.Not.Null);
            var document = presenter.GetComponent<UIDocument>();
            Assert.That(document, Is.Not.Null);
            var root = document.rootVisualElement;
            var endTurn = root.Q<Button>("end-turn-button");
            var drawPass = root.Q<Button>("draw-pass-button");
            var undo = root.Q<Button>("undo-button");
            var fixBoard = root.Q<Button>("fix-board-button");
            var turnPopup = root.Q<Label>("turn-popup");
            var goneDownPopup = root.Q<Label>("gone-down-popup");
            var goneDownTag = root.Q<Label>("gone-down-tag");
            var opponents = root.Q<VisualElement>("opponents-strip");
            var topOpponents = root.Q<VisualElement>("top-opponents");
            var leftOpponents = root.Q<VisualElement>("left-opponents");
            var rightOpponents = root.Q<VisualElement>("right-opponents");

            Assert.That(endTurn, Is.Not.Null);
            Assert.That(endTurn.enabledInHierarchy, Is.False);
            Assert.That(drawPass.text, Is.EqualTo("Draw"));
            Assert.That(undo.enabledInHierarchy, Is.False);
            Assert.That(fixBoard, Is.Not.Null);
            Assert.That(fixBoard.enabledInHierarchy, Is.False);
            Assert.That(root.Q<Button>("go-down-button"), Is.Null);
            Assert.That(turnPopup, Is.Not.Null);
            Assert.That(goneDownPopup, Is.Not.Null);
            Assert.That(goneDownTag, Is.Not.Null);
            Assert.That(opponents.childCount, Is.EqualTo(2));
            Assert.That(topOpponents, Is.Not.Null);
            Assert.That(root.Q<Slider>("opponent-card-scale-slider"), Is.Not.Null);
            Assert.That(root.Q<Slider>("zoom-sensitivity-slider"), Is.Not.Null);
            Assert.That(leftOpponents.childCount, Is.EqualTo(1));
            Assert.That(rightOpponents.childCount, Is.EqualTo(1));
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

            matchFlow.Game.State.DrawPile.Clear();
            Assert.That(matchFlow.Controller.ApplyCommand(
                GameCommand.DrawOrPass(matchFlow.Game.State.CurrentSeat)).Ok, Is.True);
            Assert.That(drawPass.text, Is.EqualTo("Pass"));
        }

        [UnityTest]
        public IEnumerator TurnTimerTimesOutAndRaisesHalasEvent()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var matchFlow = Object.FindAnyObjectByType<MatchFlow>();
            var seats = new[] { SeatConfig.Human("Sam"), SeatConfig.Human("Alex") };
            Assert.That(matchFlow.StartMatch(seats, 20261117UL, 10), Is.True);
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
    }
}