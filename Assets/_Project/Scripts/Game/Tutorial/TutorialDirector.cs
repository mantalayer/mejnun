using System;
using System.Collections.Generic;
using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Game
{
    /// <summary>
    /// Walks the tutorial's steps in order. Each step restricts input through <see cref="TutorialGate"/>, sets the
    /// card focus on <see cref="TableController"/>, holds the scripted CPU when asked, and advances when the game
    /// state satisfies the step (or when the player acknowledges a popup / modal, or an expected move is refused).
    /// The UI listens to <see cref="StepStarted"/>, <see cref="Nudge"/> and <see cref="Finished"/>.
    /// </summary>
    public sealed class TutorialDirector : MonoBehaviour
    {
        private TableController controller;
        private BotTurnDriver bots;
        private TutorialGate gate;
        private int stepIndex = -1;
        private int pendingIndex = -1;
        private bool advancing;

        public MejnunTutorial Tutorial { get; private set; }
        public bool IsRunning { get; private set; }
        public int StepIndex => stepIndex;

        public TutorialStep CurrentStep => IsRunning && Tutorial != null && stepIndex >= 0 &&
                                           stepIndex < Tutorial.Steps.Count
            ? Tutorial.Steps[stepIndex]
            : null;

        public event Action<TutorialStep> StepStarted;
        public event Action<RuleError> Nudge;
        public event Action Finished;
        public event Action Skipped;

        /// <summary>Starts the tutorial. Call before the table is initialised so the CPU is already held.</summary>
        public void Begin(MejnunTutorial tutorial, TableController tableController, BotTurnDriver botDriver)
        {
            Stop();
            Tutorial = tutorial ?? throw new ArgumentNullException(nameof(tutorial));
            controller = tableController ?? throw new ArgumentNullException(nameof(tableController));
            bots = botDriver;
            gate = new TutorialGate(MejnunTutorial.PlayerSeat);
            controller.SetCommandGate(gate);
            controller.Synchronized += HandleSynchronized;
            controller.InvalidGesture += HandleInvalidGesture;
            if (bots != null)
            {
                bots.SetScriptedTurns(tutorial.CpuTurns);
            }

            IsRunning = true;
            StartStep(0);
        }

        /// <summary>Acknowledges the current popup / modal (Next, Got it). Ignored for steps the player must do.</summary>
        public void Advance()
        {
            var step = CurrentStep;
            if (step != null && step.CompletesOnAcknowledge)
            {
                StartStep(stepIndex + 1);
            }
        }

        /// <summary>Leaves the tutorial early. It still counts as completed, so the first-Play prompt stops.</summary>
        public void Skip()
        {
            if (!IsRunning)
            {
                return;
            }

            GameSettings.TutorialCompleted = true;
            Stop();
            Skipped?.Invoke();
        }

        /// <summary>Removes the gate, focus, CPU script and hold without marking completion.</summary>
        public void Stop()
        {
            if (controller != null)
            {
                controller.Synchronized -= HandleSynchronized;
                controller.InvalidGesture -= HandleInvalidGesture;
                if (ReferenceEquals(controller.CommandGate, gate))
                {
                    controller.SetCommandGate(null);
                }

                controller.ClearTutorialFocus();
            }

            if (bots != null)
            {
                bots.SetScriptedTurns(null);
                bots.Held = false;
            }

            IsRunning = false;
            stepIndex = -1;
            gate = null;
        }

        private void OnDestroy()
        {
            Stop();
        }

        private void StartStep(int index)
        {
            if (advancing)
            {
                pendingIndex = index;
                return;
            }

            advancing = true;
            try
            {
                pendingIndex = index;
                while (pendingIndex >= 0)
                {
                    var next = pendingIndex;
                    pendingIndex = -1;
                    EnterStep(next);
                }
            }
            finally
            {
                advancing = false;
            }
        }

        private void EnterStep(int index)
        {
            if (!IsRunning)
            {
                return;
            }

            if (index >= Tutorial.Steps.Count)
            {
                Complete();
                return;
            }

            stepIndex = index;
            var step = Tutorial.Steps[index];
            gate.Step = step;
            if (bots != null)
            {
                bots.Held = step.HoldCpu || step.Ui == TutorialUiKind.Modal;
            }

            ApplyFocus();
            StepStarted?.Invoke(step);
            CheckCompletion();
        }

        private void Complete()
        {
            GameSettings.TutorialCompleted = true;
            Stop();
            Finished?.Invoke();
        }

        private void ApplyFocus()
        {
            var step = CurrentStep;
            if (step == null || controller == null)
            {
                return;
            }

            int? targetMeldId = null;
            var state = controller.Game == null ? null : controller.Game.State;
            if (step.Target.Kind == TutorialTargetKind.MeldContaining && state != null)
            {
                var meld = CpuIntent.FindMeldContaining(state, step.Target.Anchor, out _);
                if (meld != null)
                {
                    targetMeldId = meld.Id;
                }
            }

            controller.SetTutorialFocus(step.FocusCardIds(), targetMeldId,
                step.Target.Kind == TutorialTargetKind.EmptyBoard);
        }

        private void CheckCompletion()
        {
            var step = CurrentStep;
            var game = controller == null ? null : controller.Game;
            if (step?.IsComplete == null || game == null)
            {
                return;
            }

            if (step.IsComplete(game.State, game.Turn))
            {
                StartStep(stepIndex + 1);
            }
        }

        private void HandleSynchronized(GameState state, TurnSession turn)
        {
            if (!IsRunning)
            {
                return;
            }

            ApplyFocus();
            CheckCompletion();
        }

        private void HandleInvalidGesture(RuleError error)
        {
            var step = CurrentStep;
            if (step == null)
            {
                return;
            }

            if (step.ExpectRejection && error != RuleError.TutorialBlocked)
            {
                StartStep(stepIndex + 1);
                return;
            }

            Nudge?.Invoke(error);
        }

        /// <summary>For tests and debug tools: the ids of the steps in order.</summary>
        public IReadOnlyList<string> StepIds()
        {
            var ids = new List<string>();
            if (Tutorial != null)
            {
                for (var index = 0; index < Tutorial.Steps.Count; index++)
                {
                    ids.Add(Tutorial.Steps[index].Id);
                }
            }

            return ids;
        }
    }
}
