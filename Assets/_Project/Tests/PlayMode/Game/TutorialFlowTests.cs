using System.Collections;
using System.Linq;
using Mejnun.AI;
using Mejnun.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Mejnun.Game.Tests
{
    public sealed class TutorialFlowTests
    {
        private const int FrameLimit = 900;
        private bool originalTutorialCompleted;

        [SetUp]
        public void RememberSettings()
        {
            originalTutorialCompleted = GameSettings.TutorialCompleted;
        }

        [TearDown]
        public void RestoreSettings()
        {
            GameSettings.TutorialCompleted = originalTutorialCompleted;
        }

        [UnityTest]
        public IEnumerator TutorialSkipsTheDealerQuestionAndLaylaPlaysHerScriptedOpening()
        {
            var flow = default(MatchFlow);
            yield return StartTutorial(result => flow = result);

            Assert.That(flow.IsTutorial, Is.True);
            Assert.That(flow.Screen, Is.EqualTo(MatchFlowScreen.Turn), "The tutorial dealer skips the question.");
            Assert.That(flow.Tutorial.IsRunning, Is.True);
            Assert.That(flow.Tutorial.CurrentStep.Id, Is.EqualTo("welcome"));
            Assert.That(flow.Game.State.CurrentSeat, Is.EqualTo(MejnunTutorial.CpuSeat));
            Assert.That(flow.Controller.HintsEnabled, Is.False);
            Assert.That(flow.TurnSeconds, Is.EqualTo(0));
            CollectionAssert.AreEqual(MejnunTutorial.Cards("7H", "8H", "9H", "5C", "5D", "5S", "AS", "9H'", "3H",
                "4H", "AH"), flow.Game.State.Hands[MejnunTutorial.PlayerSeat]);

            for (var frame = 0; frame < 10; frame++)
            {
                yield return null;
            }

            Assert.That(flow.Game.State.Board, Is.Empty, "Layla waits while the opening modals are shown.");

            flow.Tutorial.Advance();
            flow.Tutorial.Advance();
            yield return WaitFor(() => flow.Game.State.CurrentSeat == MejnunTutorial.PlayerSeat);

            Assert.That(flow.Game.State.Board.Count, Is.EqualTo(1));
            CollectionAssert.AreEquivalent(MejnunTutorial.Cards("JS", "QS", "KS"), flow.Game.State.Board[0].Cards);
            Assert.That(flow.Game.State.PlayerPhases[MejnunTutorial.CpuSeat], Is.EqualTo(PlayerPhase.Down));
            Assert.That(flow.Tutorial.CurrentStep.Id, Is.EqualTo("c1-down"));
        }

        [UnityTest]
        public IEnumerator FirstTurnExplainsNotDownThenAllowsCardsToBePlacedOneAtATime()
        {
            var flow = default(MatchFlow);
            yield return StartTutorial(result => flow = result);
            flow.Tutorial.Advance();
            flow.Tutorial.Advance();
            yield return WaitFor(() => flow.Tutorial.CurrentStep.Id == "c1-down");
            flow.Tutorial.Advance();
            Assert.That(flow.Tutorial.CurrentStep.Id, Is.EqualTo("h1a-not-down"));
            yield return null;
            yield return null;

            var popup = FindCoachPopup();
            Assert.That(popup, Is.Not.Null);
            Assert.That(popup.Q<Label>("coach-body").text, Does.Contain("can't use the table until you're Down"));

            // Real presses arrive in bottom-left screen space; the coach popup must not swallow presses on the hand.
            var camera = flow.Table.ViewCamera;
            foreach (var handCard in flow.Game.State.Hands[MejnunTutorial.PlayerSeat])
            {
                var centre = (Vector2)camera.WorldToScreenPoint(flow.Table.GetCardView(handCard.Id).transform.position);
                Assert.That(UiPointerBlocker.IsOverUi(centre), Is.False, "Hand card " + handCard.Id + " is blocked.");
                Assert.That(flow.Table.HitTest(centre, true).Kind, Is.EqualTo(GestureHitKind.HandCard));
            }

            var panelSize = popup.panel.visualTree.worldBound.size;
            var popupCentre = popup.worldBound.center;
            var popupScreen = new Vector2(popupCentre.x * Screen.width / panelSize.x,
                Screen.height - popupCentre.y * Screen.height / panelSize.y);
            Assert.That(UiPointerBlocker.IsOverUi(popupScreen), Is.True, "Presses on the coach popup stay in the UI.");

            flow.Tutorial.Advance();
            Assert.That(flow.Tutorial.CurrentStep.Id, Is.EqualTo("h1b-go-down"));
            Assert.That(popup.Q<Label>("coach-body").text, Is.EqualTo(flow.Tutorial.CurrentStep.Body));

            Assert.That(flow.Table.GetCardView(Id("7H")).IsTutorialFocused, Is.True);
            Assert.That(flow.Table.GetCardView(Id("9H")).IsTutorialFocused, Is.True);
            Assert.That(flow.Table.GetCardView(Id("5C")).IsDimmed, Is.True);
            Assert.That(flow.Table.GetCardView(Id("5C")).IsTutorialFocused, Is.False);
            Assert.That(flow.Controller.TutorialEmptyTarget, Is.True);

            var before = SnapshotSerializer.Write(flow.Game);
            var offScript = flow.Controller.ExecuteGesture(new GestureInput(GestureType.Drag, HandHit(flow, "5C"),
                GestureHit.EmptyBoard()));
            Assert.That(offScript.Error, Is.EqualTo(RuleError.TutorialBlocked));
            Assert.That(flow.Controller.ApplyCommand(GameCommand.EndTurn(0)).Error, Is.EqualTo(RuleError.TutorialBlocked));
            CollectionAssert.AreEqual(before, SnapshotSerializer.Write(flow.Game), "Blocked commands change nothing.");
            Assert.That(flow.Tutorial.CurrentStep.Id, Is.EqualTo("h1b-go-down"));

            Assert.That(flow.Controller.ExecuteGesture(new GestureInput(GestureType.Drag, HandHit(flow, "7H"),
                GestureHit.EmptyBoard())).Ok, Is.True);
            var partialBody = flow.Tutorial.CurrentStep.PartialBody;
            Assert.That(flow.Tutorial.CurrentBody, Is.EqualTo(partialBody));
            Assert.That(popup.Q<Label>("coach-body").text, Is.EqualTo(partialBody));
            var partialMeld = CpuIntent.FindMeldContaining(flow.Game.State, MejnunTutorial.C("7H"), out _);
            Assert.That(flow.Controller.TutorialTargetMeldId, Is.EqualTo(partialMeld.Id));
            Assert.That(flow.Controller.TutorialEmptyTarget, Is.False);

            Assert.That(flow.Controller.ExecuteGesture(new GestureInput(GestureType.Drag, HandHit(flow, "8H"),
                GestureHit.MeldGap(partialMeld.Id, partialMeld.Cards.Count))).Ok, Is.True);
            Assert.That(flow.Tutorial.CurrentBody, Is.EqualTo(partialBody));
            partialMeld = CpuIntent.FindMeldContaining(flow.Game.State, MejnunTutorial.C("7H"), out _);
            Assert.That(flow.Controller.ExecuteGesture(new GestureInput(GestureType.Drag, HandHit(flow, "9H"),
                GestureHit.MeldGap(partialMeld.Id, partialMeld.Cards.Count))).Ok, Is.True);
            Assert.That(flow.Game.State.PlayerPhases[0], Is.EqualTo(PlayerPhase.Down));
            Assert.That(flow.Tutorial.CurrentStep.Id, Is.EqualTo("h1b-board-open"));

            flow.Tutorial.Advance();
            Assert.That(flow.Tutorial.CurrentStep.Id, Is.EqualTo("h1c-ace-high"));
            var spades = flow.Game.State.Board[0];
            Assert.That(flow.Controller.TutorialTargetMeldId, Is.EqualTo(spades.Id));

            // Drag the ace onto Layla's set.
            Assert.That(flow.Controller.ExecuteGesture(new GestureInput(GestureType.Drag, HandHit(flow, "AS"),
                GestureHit.MeldGap(spades.Id, spades.Cards.Count))).Ok, Is.True);
            Assert.That(flow.Tutorial.CurrentStep.Id, Is.EqualTo("h1c-ace-rule"));
        }

        [UnityTest]
        public IEnumerator FinishingTheTutorialMarksItCompletedWithoutSavingOrProgress()
        {
            GameSettings.TutorialCompleted = false;
            var hadSave = SavedGame.HasSave;
            var xpBefore = ProgressionService.Instance.Profile.Xp;
            var flow = default(MatchFlow);
            yield return StartTutorial(result => flow = result);
            var director = flow.Tutorial;
            var sawGoodToKnow = false;

            var frames = 0;
            while (director.IsRunning && frames++ < FrameLimit * 4)
            {
                var step = director.CurrentStep;
                sawGoodToKnow |= step.Id == "good-to-know";
                if (step.CompletesOnAcknowledge)
                {
                    if (step.Id == "good-to-know")
                    {
                        Assert.That(flow.Screen, Is.Not.EqualTo(MatchFlowScreen.Results),
                            "Results wait for the closing pages.");
                        Assert.That(SavedGame.HasSave, Is.EqualTo(hadSave));
                    }

                    director.Advance();
                }
                else if (flow.Game.State.CurrentSeat == MejnunTutorial.PlayerSeat &&
                         (step.ExpectRejection || step.Solution.Count > 0))
                {
                    PlaySolution(flow, step);
                }

                yield return null;
            }

            Assert.That(director.IsRunning, Is.False, "The tutorial never finished.");
            Assert.That(sawGoodToKnow, Is.True);
            Assert.That(GameSettings.TutorialCompleted, Is.True);
            Assert.That(flow.Game.State.Result.Kind, Is.EqualTo(GameEndKind.EmptyHand));
            Assert.That(flow.Game.State.Result.Winner, Is.EqualTo(MejnunTutorial.PlayerSeat));
            Assert.That(flow.Screen, Is.EqualTo(MatchFlowScreen.Results));
            Assert.That(flow.LastProgression, Is.Null);
            Assert.That(ProgressionService.Instance.Profile.Xp, Is.EqualTo(xpBefore));
            Assert.That(SavedGame.HasSave, Is.EqualTo(hadSave));
            Assert.That(flow.Controller.CommandGate, Is.Null);
        }

        [UnityTest]
        public IEnumerator SkippingClearsTheTutorialAndANormalMatchHasNoGateOrFocus()
        {
            GameSettings.TutorialCompleted = false;
            var flow = default(MatchFlow);
            yield return StartTutorial(result => flow = result);
            Assert.That(flow.Controller.CommandGate, Is.Not.Null);

            flow.Tutorial.Skip();

            Assert.That(GameSettings.TutorialCompleted, Is.True);
            Assert.That(flow.Tutorial.IsRunning, Is.False);
            Assert.That(flow.Controller.CommandGate, Is.Null);
            Assert.That(flow.Controller.TutorialFocusActive, Is.False);
            Assert.That(flow.BotDriver.Held, Is.False);

            Assert.That(flow.StartMatch(new[]
            {
                SeatConfig.Human("Sam"),
                SeatConfig.Bot("Kian", BotDifficulty.Easy)
            }, 4242UL), Is.True);
            yield return WaitFor(() => !flow.IsInitializingRound && flow.Game != null);

            Assert.That(flow.IsTutorial, Is.False);
            Assert.That(flow.Tutorial.IsRunning, Is.False);
            Assert.That(flow.Controller.CommandGate, Is.Null);
            Assert.That(flow.Controller.TutorialFocusActive, Is.False);
            Assert.That(flow.Screen, Is.EqualTo(MatchFlowScreen.DealerQuestion));
            Assert.That(flow.ChooseDealer(1).Ok, Is.True);
            Assert.That(flow.Table.ActiveCards.Values.Any(view => view.IsDimmed || view.IsTutorialFocused), Is.False);
        }

        private static void PlaySolution(MatchFlow flow, TutorialStep step)
        {
            if ((step.Allowed & TutorialAction.Undo) != 0)
            {
                Assert.That(flow.Controller.UndoGesture().Ok, Is.True, step.Id);
                return;
            }

            if ((step.Allowed & TutorialAction.Redo) != 0)
            {
                Assert.That(flow.Controller.RedoGesture().Ok, Is.True, step.Id);
                return;
            }

            if (step.ExpectRejection)
            {
                Assert.That(step.Solution[0].TryResolve(flow.Game.State, 0, out var refused), Is.True, step.Id);
                var batch = new CommandBatch();
                batch.Add(refused);
                Assert.That(flow.Controller.ExecuteBatch(batch).Ok, Is.False, step.Id);
                return;
            }

            Assert.That(CpuIntent.TryResolveAll(flow.Game, 0, step.Solution, out var commands), Is.True, step.Id);
            foreach (var command in commands)
            {
                CommandResult result;
                if (command.Type == CommandType.EndTurn || command.Type == CommandType.DrawOrPass)
                {
                    result = flow.Controller.ApplyCommand(command);
                }
                else
                {
                    var batch = new CommandBatch();
                    batch.Add(command);
                    result = flow.Controller.ExecuteBatch(batch);
                }

                Assert.That(result.Ok, Is.True, step.Id + " " + command.Type + ": " + result.Error);
            }
        }

        private static IEnumerator StartTutorial(System.Action<MatchFlow> onReady)
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;
            var flow = Object.FindAnyObjectByType<MatchFlow>();
            Assert.That(flow, Is.Not.Null);
            flow.BotDriver.ThinkSeconds = 0f;
            flow.BotDriver.StepSeconds = 0f;
            Assert.That(flow.StartMatch(MejnunTutorial.CreateMatchRequest()), Is.True);
            yield return WaitFor(() => !flow.IsInitializingRound && flow.Game != null);
            onReady(flow);
        }

        private static IEnumerator WaitFor(System.Func<bool> condition)
        {
            var frames = 0;
            while (!condition() && frames++ < FrameLimit)
            {
                yield return null;
            }

            Assert.That(condition(), Is.True, "Timed out waiting for the tutorial.");
        }

        private static VisualElement FindCoachPopup()
        {
            foreach (var document in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var popup = document.rootVisualElement?.Q<VisualElement>("coach-popup");
                if (popup != null && popup.panel != null)
                {
                    return popup;
                }
            }

            return null;
        }

        private static byte Id(string notation)
        {
            return MejnunTutorial.C(notation).Id;
        }

        private static GestureHit HandHit(MatchFlow flow, string notation)
        {
            var id = Id(notation);
            var order = flow.Table.HandOrder.GetCardIds(MejnunTutorial.PlayerSeat);
            var index = -1;
            for (var position = 0; position < order.Count; position++)
            {
                if (order[position] == id)
                {
                    index = position;
                    break;
                }
            }

            Assert.That(index, Is.GreaterThanOrEqualTo(0), notation + " is not in the hand");
            return GestureHit.HandCard(id, index);
        }
    }
}
