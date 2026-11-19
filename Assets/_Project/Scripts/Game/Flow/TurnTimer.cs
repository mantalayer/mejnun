using System;
using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Game
{
    public sealed class TurnTimer : MonoBehaviour
    {
        private MatchFlow matchFlow;
        private TableController controller;
        private MejnunGame timedGame;
        private int timedSeat = -1;
        private int timedTurn = -1;
        private bool timedOut;
        private bool displayOnly;

        public int DurationSeconds { get; private set; }
        public float RemainingSeconds { get; private set; }
        public bool IsRunning => DurationSeconds > 0 && timedGame != null && RemainingSeconds > 0f && !timedOut;
        public bool Paused { get; set; }

        public event Action TimedOut;

        public void Configure(MatchFlow flow, TableController tableController, int durationSeconds)
        {
            matchFlow = flow;
            controller = tableController;
            SetDuration(durationSeconds);
        }

        public void SetDuration(int seconds)
        {
            DurationSeconds = Mathf.Max(0, seconds);
            ResetTurn();
        }

        public void SetDisplayOnly(bool enabled)
        {
            if (displayOnly == enabled)
            {
                return;
            }

            displayOnly = enabled;
            ResetTurn();
        }

        public void Tick(float deltaTime)
        {
            if (DurationSeconds <= 0 || matchFlow == null || controller == null || controller.Game == null ||
                matchFlow.Screen != MatchFlowScreen.Turn)
            {
                ResetTurn();
                return;
            }

            if (Paused)
            {
                return;
            }

            var game = controller.Game;
            var state = game.State;
            if (state.Phase != GamePhase.Playing || matchFlow.IsBotSeat(state.CurrentSeat))
            {
                ResetTurn();
                return;
            }

            if ((!displayOnly && timedGame != game) || timedSeat != state.CurrentSeat ||
                timedTurn != state.TurnNumber)
            {
                timedGame = game;
                timedSeat = state.CurrentSeat;
                timedTurn = state.TurnNumber;
                RemainingSeconds = DurationSeconds;
                timedOut = false;
            }

            if (timedOut)
            {
                return;
            }

            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Mathf.Max(0f, deltaTime));
            if (RemainingSeconds > 0f)
            {
                return;
            }

            timedOut = true;
            TimedOut?.Invoke();
            if (displayOnly)
            {
                return;
            }

            if (controller.GetTimeoutUndoCount() > 0)
            {
                StartCoroutine(controller.BeginTimeoutFix());
            }
            else
            {
                controller.TimeoutTurn();
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void ResetTurn()
        {
            timedGame = null;
            timedSeat = -1;
            timedTurn = -1;
            timedOut = false;
            RemainingSeconds = DurationSeconds;
        }
    }
}