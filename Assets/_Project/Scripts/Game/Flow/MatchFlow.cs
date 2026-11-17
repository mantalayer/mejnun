using System;
using System.Collections.Generic;
using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Game
{
    public enum MatchFlowScreen
    {
        Setup,
        DealerQuestion,
        Handoff,
        Turn,
        Results
    }

    public sealed class MatchFlow : MonoBehaviour
    {
        private static readonly string[] FacePool = { "classic", "classic_ivory" };

        [SerializeField] private TableController tableController;
        [SerializeField] private TableView tableView;
        [SerializeField] private DealerQuestions dealerQuestions;

        private string[] playerNames = Array.Empty<string>();
        private System.Random questionRandom = new System.Random();
        private int handoffSeat = -1;

        public MejnunGame Game { get; private set; }
        public TableController Controller => tableController;
        public TableView Table => tableView;
        public GameResult LastResult { get; private set; }
        public MatchFlowScreen Screen { get; private set; } = MatchFlowScreen.Setup;
        public int SeatCount => playerNames.Length;
        public string DealerQuestion { get; private set; }
        public int CurrentSeat => Game == null ? -1 : Game.State.CurrentSeat;
        public ulong RoundSeed { get; private set; }

        public event Action<MatchFlowScreen> ScreenChanged;

        public string HandoffMessage => CurrentSeat < 0
            ? string.Empty
            : "Pass to " + GetPlayerName(CurrentSeat) + " — tap to reveal";

        public string ResultTitle
        {
            get
            {
                if (LastResult == null)
                {
                    return string.Empty;
                }

                if (LastResult.Kind == GameEndKind.Stalemate)
                {
                    return "Stalemate";
                }

                return LastResult.Winner.HasValue ? GetPlayerName(LastResult.Winner.Value) + " wins!" : "Game over";
            }
        }

        public string ResultMessage
        {
            get
            {
                if (LastResult == null)
                {
                    return string.Empty;
                }

                if (LastResult.Kind == GameEndKind.EmptyHand)
                {
                    return GetPlayerName(LastResult.Winner.Value) + " emptied their hand.";
                }

                if (LastResult.Kind == GameEndKind.FewestCards)
                {
                    return GetPlayerName(LastResult.Winner.Value) + " had the fewest cards when the draw pile ran out.";
                }

                return "The fewest-card score was tied. Pick a new dealer.";
            }
        }

        private void Awake()
        {
            if (tableController == null)
            {
                tableController = FindAnyObjectByType<TableController>();
            }

            if (tableView == null)
            {
                tableView = FindAnyObjectByType<TableView>();
            }
        }

        private void OnEnable()
        {
            if (tableController != null)
            {
                tableController.Synchronized += OnSynchronized;
                tableController.GameWon += OnGameWon;
                tableController.GameStalemate += OnGameStalemate;
            }
        }

        private void OnDisable()
        {
            if (tableController != null)
            {
                tableController.Synchronized -= OnSynchronized;
                tableController.GameWon -= OnGameWon;
                tableController.GameStalemate -= OnGameStalemate;
            }
        }

        public bool StartMatch(int seatCount, IReadOnlyList<string> names, ulong? seed = null)
        {
            if (seatCount < 2 || seatCount > 6)
            {
                return false;
            }

            playerNames = new string[seatCount];
            for (var seat = 0; seat < seatCount; seat++)
            {
                var name = names != null && seat < names.Count ? names[seat] : null;
                playerNames[seat] = string.IsNullOrWhiteSpace(name) ? "Player " + (seat + 1) : name.Trim();
            }

            return StartRound(null, seed ?? CreateSeed());
        }

        public CommandResult ChooseDealer(int seat)
        {
            if (Game == null || Game.State.Phase != GamePhase.ChoosingDealer || tableController == null)
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            return tableController.ApplyCommand(GameCommand.ChooseDealer(seat));
        }

        public CommandResult ChooseRandomDealer()
        {
            if (Game == null || Game.State.Phase != GamePhase.ChoosingDealer || tableController == null)
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            return tableController.ApplyCommand(GameCommand.ChooseRandomDealer());
        }

        public bool RevealHand()
        {
            if (Screen != MatchFlowScreen.Handoff || tableView == null)
            {
                return false;
            }

            tableView.SetHandVisible(true);
            SetScreen(MatchFlowScreen.Turn);
            return true;
        }

        public bool Rematch()
        {
            if (LastResult == null)
            {
                return false;
            }

            return StartRound(LastResult.NextDealer, CreateSeed());
        }

        public string GetPlayerName(int seat)
        {
            if (seat < 0 || seat >= playerNames.Length)
            {
                return "Player " + (seat + 1);
            }

            return playerNames[seat];
        }

        private bool StartRound(int? dealer, ulong seed)
        {
            if (tableController == null)
            {
                return false;
            }

            RoundSeed = seed;
            Debug.Log("Mejnun round seed: " + RoundSeed);
            questionRandom = new System.Random(unchecked((int)(seed ^ (seed >> 32))));
            DealerQuestion = dealerQuestions == null
                ? "Who should deal this game?"
                : dealerQuestions.GetRandomQuestion(questionRandom);
            LastResult = null;
            handoffSeat = -1;
            if (tableView != null)
            {
                tableView.SetHandVisible(false);
            }

            Game = new MejnunGame(new GameSetup(playerNames.Length, seed, dealer, facePool: FacePool));
            SetScreen(dealer.HasValue ? MatchFlowScreen.Turn : MatchFlowScreen.DealerQuestion);
            tableController.Initialize(Game);
            return true;
        }

        private void OnSynchronized(GameState state, TurnSession turn)
        {
            if (state.Phase != GamePhase.Playing || state.CurrentSeat == handoffSeat)
            {
                return;
            }

            handoffSeat = state.CurrentSeat;
            if (tableView != null)
            {
                tableView.SetHandVisible(false);
            }

            SetScreen(MatchFlowScreen.Handoff);
        }

        private void OnGameWon(GameResult result)
        {
            ShowResults(result);
        }

        private void OnGameStalemate(GameResult result)
        {
            ShowResults(result);
        }

        private void ShowResults(GameResult result)
        {
            LastResult = result;
            if (tableView != null)
            {
                tableView.SetHandVisible(false);
            }

            SetScreen(MatchFlowScreen.Results);
        }

        private void SetScreen(MatchFlowScreen screen)
        {
            if (Screen == screen)
            {
                return;
            }

            Screen = screen;
            ScreenChanged?.Invoke(screen);
        }

        private static ulong CreateSeed()
        {
            return unchecked((ulong)DateTime.UtcNow.Ticks);
        }
    }
}