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
            var document = presenter.GetComponent<UIDocument>();
            Assert.That(document, Is.Not.Null);
            var root = document.rootVisualElement;
            var endTurn = root.Q<Button>("end-turn-button");
            var drawPass = root.Q<Button>("draw-pass-button");
            var undo = root.Q<Button>("undo-button");
            var goDown = root.Q<Button>("go-down-button");
            var opponents = root.Q<VisualElement>("opponents-strip");

            Assert.That(endTurn, Is.Not.Null);
            Assert.That(endTurn.enabledInHierarchy, Is.False);
            Assert.That(drawPass.text, Is.EqualTo("Draw"));
            Assert.That(undo.enabledInHierarchy, Is.False);
            Assert.That(goDown.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(opponents.childCount, Is.EqualTo(3));

            matchFlow.Game.State.DrawPile.Clear();
            Assert.That(matchFlow.Controller.ApplyCommand(
                GameCommand.DrawOrPass(matchFlow.Game.State.CurrentSeat)).Ok, Is.True);
            Assert.That(drawPass.text, Is.EqualTo("Pass"));
        }
    }
}