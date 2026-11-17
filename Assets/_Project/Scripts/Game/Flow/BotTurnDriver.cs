using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mejnun.AI;
using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Game
{
    /// <summary>
    /// Plays bot seats: plans the turn on a worker thread (from a snapshot, so the live game is never touched off
    /// the main thread), waits a short "thinking" delay, then applies the commands one by one so every move is
    /// visible on the table. Commands go through <see cref="TableController.ApplyCommand"/> like a human's.
    /// </summary>
    public sealed class BotTurnDriver : MonoBehaviour
    {
        [SerializeField] private float thinkSeconds = 0.7f;
        [SerializeField] private float stepSeconds = 0.45f;

        private TableController controller;
        private IReadOnlyList<SeatConfig> seats = new SeatConfig[0];
        private Coroutine routine;
        private MejnunGame playingGame;
        private int playingTurn = -1;

        public float ThinkSeconds
        {
            get => thinkSeconds;
            set => thinkSeconds = Mathf.Max(0f, value);
        }

        public float StepSeconds
        {
            get => stepSeconds;
            set => stepSeconds = Mathf.Max(0f, value);
        }

        public bool IsPlaying => routine != null;

        public void Configure(TableController tableController, IReadOnlyList<SeatConfig> seatConfigs)
        {
            Stop();
            controller = tableController;
            seats = seatConfigs ?? new SeatConfig[0];
        }

        public void Stop()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            playingGame = null;
            playingTurn = -1;
        }

        /// <summary>Starts the bot's turn if the current seat is a bot and it isn't already playing.</summary>
        public void Evaluate()
        {
            if (routine != null || controller == null || controller.Game == null || !isActiveAndEnabled)
            {
                return;
            }

            var game = controller.Game;
            var state = game.State;
            if (state.Phase != GamePhase.Playing || state.CurrentSeat < 0 || state.CurrentSeat >= seats.Count ||
                !seats[state.CurrentSeat].IsBot)
            {
                return;
            }

            if (playingGame == game && playingTurn == state.TurnNumber)
            {
                return;
            }

            playingGame = game;
            playingTurn = state.TurnNumber;
            routine = StartCoroutine(PlayTurn(game, state.CurrentSeat, state.TurnNumber,
                seats[state.CurrentSeat].Difficulty));
        }

        private void OnDisable()
        {
            Stop();
        }

        private IEnumerator PlayTurn(MejnunGame game, int seat, int turnNumber, BotDifficulty difficulty)
        {
            var snapshot = SnapshotSerializer.Write(game);
            var seed = unchecked(game.Setup.Seed * 6364136223846793005UL + (ulong)turnNumber * 1442695040888963407UL +
                                 (ulong)seat);
            var planning = Task.Run(() => BotBrain.PlanTurn(snapshot, difficulty, seed));
            var waited = 0f;
            while (!planning.IsCompleted || waited < thinkSeconds)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            IReadOnlyList<GameCommand> commands;
            if (planning.IsFaulted || planning.Result == null)
            {
                Debug.LogError("Bot planning failed: " + planning.Exception, this);
                commands = new[] { GameCommand.DrawOrPass(seat) };
            }
            else
            {
                if (planning.Result.FallbackReason != null)
                {
                    Debug.LogWarning("Bot fell back to drawing: " + planning.Result.FallbackReason, this);
                }

                commands = planning.Result.Commands;
            }

            for (var index = 0; index < commands.Count; index++)
            {
                if (!IsStillTurn(game, seat, turnNumber))
                {
                    break;
                }

                if (index > 0 && stepSeconds > 0f)
                {
                    yield return new WaitForSeconds(stepSeconds);
                    if (!IsStillTurn(game, seat, turnNumber))
                    {
                        break;
                    }
                }

                var result = controller.ApplyCommand(commands[index]);
                if (!result.Ok)
                {
                    Debug.LogWarning("Bot command " + commands[index].Type + " was rejected: " + result.Error, this);
                    if (IsStillTurn(game, seat, turnNumber))
                    {
                        controller.ApplyCommand(GameCommand.DrawOrPass(seat));
                    }

                    break;
                }
            }

            routine = null;
            Evaluate();
        }

        private bool IsStillTurn(MejnunGame game, int seat, int turnNumber)
        {
            return controller != null && controller.Game == game && game.State.Phase == GamePhase.Playing &&
                   game.State.CurrentSeat == seat && game.State.TurnNumber == turnNumber;
        }
    }
}
