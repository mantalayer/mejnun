using System;
using System.Collections.Generic;
using Mejnun.AI;
using Mejnun.Cosmetics;
using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Game
{
    public enum MatchFlowScreen
    {
        Idle,
        DealerQuestion,
        Handoff,
        Turn,
        Results
    }

    public sealed class MatchFlow : MonoBehaviour
    {
        [SerializeField] private TableController tableController;
        [SerializeField] private TableView tableView;
        [SerializeField] private DealerQuestions dealerQuestions;
        [SerializeField] private CosmeticCatalog cosmeticCatalog;
        [SerializeField] private DeckSkinLoader deckSkinLoader;

        private string[] playerNames = Array.Empty<string>();
        private SeatConfig[] seats = Array.Empty<SeatConfig>();
        private System.Random questionRandom = new System.Random();
        private BotTurnDriver botDriver;
        private int handoffSeat = -1;
        private int revealedSeat = -1;
        private TurnTimer turnTimer;
        private MejnunGame lastSavedGame;
        private int lastSavedTurnNumber = -1;
        private bool resultRecorded;
        private bool matchPaused;
        private bool applicationPaused;
        private float elapsedMatchSeconds;
        private MatchRequest activeRequest;
        private readonly CardTracker cardTracker = new CardTracker();

        public MejnunGame Game { get; private set; }
        public TableController Controller => tableController;
        public TableView Table => tableView;
        public GameResult LastResult { get; private set; }
        public ProgressionReport LastProgression { get; private set; }
        public bool IsInitializingRound { get; private set; }
        public MatchFlowScreen Screen { get; private set; } = MatchFlowScreen.Idle;
        public int SeatCount => playerNames.Length;
        public string DealerQuestion { get; private set; }
        public int CurrentSeat => Game == null ? -1 : Game.State.CurrentSeat;
        public ulong RoundSeed { get; private set; }
        public BotTurnDriver BotDriver => botDriver;
        public TurnTimer TurnTimer => turnTimer;
        public int TurnSeconds { get; private set; }
        public IReadOnlyList<SeatConfig> Seats => seats;
        public bool FastForward
        {
            get => GameSettings.FastForward;
            set => GameSettings.FastForward = value;
        }

        public int HumanCount
        {
            get
            {
                var count = 0;
                for (var seat = 0; seat < seats.Length; seat++)
                {
                    if (!seats[seat].IsBot)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

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

                return "The fewest-card score was tied. Pick who goes first.";
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

            botDriver = GetComponent<BotTurnDriver>();
            if (botDriver == null)
            {
                botDriver = gameObject.AddComponent<BotTurnDriver>();
            }

            if (tableController != null)
            {
                tableController.SetSeatInputFilter(seat => !IsBotSeat(seat));
            }

            tableView?.SetCpuSeatPredicate(IsBotSeat);

            turnTimer = GetComponent<TurnTimer>();
            if (turnTimer == null)
            {
                turnTimer = gameObject.AddComponent<TurnTimer>();
            }

            turnTimer.Configure(this, tableController, TurnSeconds);
            if (deckSkinLoader == null)
            {
                deckSkinLoader = GetComponent<DeckSkinLoader>();
            }

            if (deckSkinLoader != null && tableView != null)
            {
                deckSkinLoader.Configure(tableView.SkinCatalog);
            }

            if (cosmeticCatalog != null)
            {
                ProgressionService.Initialize(cosmeticCatalog);
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

        private void Start()
        {
            if (MatchLaunch.TryTake(out var request))
            {
                if (request.Resume)
                {
                    ResumeSavedGame();
                }
                else
                {
                    StartMatch(request);
                }

                return;
            }

            SetScreen(MatchFlowScreen.Idle);
        }

        private void Update()
        {
            if (!matchPaused && !applicationPaused && Game != null && Game.State.Phase == GamePhase.Playing &&
                !IsBotSeat(Game.State.CurrentSeat))
            {
                elapsedMatchSeconds += Time.unscaledDeltaTime;
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

            var humans = new SeatConfig[seatCount];
            for (var seat = 0; seat < seatCount; seat++)
            {
                humans[seat] = SeatConfig.Human(names != null && seat < names.Count ? names[seat] : null);
            }

            return StartMatch(humans, seed);
        }

        public bool StartMatch(MatchRequest request)
        {
            if (request == null || request.Resume || request.Seats == null)
            {
                return false;
            }

            return StartMatch(request.Seats, request.Seed, request.TurnSeconds, request);
        }

        /// <summary>Starts a match with any mix of local humans and bots (at least one human).</summary>
        public bool StartMatch(IReadOnlyList<SeatConfig> seatConfigs, ulong? seed = null, int turnSeconds = 0)
        {
            return StartMatch(seatConfigs, seed, turnSeconds, null);
        }

        public bool DebugWinCurrentGame()
        {
            if (Game == null || Game.State.Phase != GamePhase.Playing || Screen == MatchFlowScreen.Results ||
                resultRecorded)
            {
                return false;
            }

            for (var seat = 0; seat < seats.Length; seat++)
            {
                if (seats[seat].IsBot)
                {
                    continue;
                }

                ShowResults(new GameResult(GameEndKind.EmptyHand, seat, seat));
                return true;
            }

            return false;
        }

        private bool StartMatch(IReadOnlyList<SeatConfig> seatConfigs, ulong? seed, int turnSeconds,
            MatchRequest request)
        {
            if (seatConfigs == null || seatConfigs.Count < 2 || seatConfigs.Count > 6)
            {
                return false;
            }

            var hasHuman = false;
            for (var seat = 0; seat < seatConfigs.Count; seat++)
            {
                hasHuman |= !seatConfigs[seat].IsBot;
            }

            if (!hasHuman)
            {
                return false;
            }

            seats = new SeatConfig[seatConfigs.Count];
            TurnSeconds = Mathf.Max(0, turnSeconds);
            turnTimer?.SetDuration(TurnSeconds);
            playerNames = new string[seatConfigs.Count];
            for (var seat = 0; seat < seats.Length; seat++)
            {
                var config = seatConfigs[seat];
                var name = config.Name;
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = (config.IsBot ? "Bot " : "Player ") + (seat + 1);
                }

                playerNames[seat] = name.Trim();
                seats[seat] = new SeatConfig(playerNames[seat], config.IsBot, config.Difficulty, config.Style);
            }

            if (botDriver != null)
            {
                botDriver.Configure(tableController, seats, cardTracker);
            }

            var humanCount = 0;
            for (var seat = 0; seat < seats.Length; seat++)
            {
                if (!seats[seat].IsBot)
                {
                    humanCount++;
                }
            }

            activeRequest = request ?? new MatchRequest(seats, TurnSeconds,
                humanCount >= 2 ? MatchMode.PassAndPlay : MatchMode.Solo, seed: seed);

            return StartRound(null, seed ?? CreateSeed(seats.Length));
        }

        public bool ResumeSavedGame()
        {
            if (tableController == null || !SavedGame.TryLoad(out var data))
            {
                return false;
            }

            seats = new SeatConfig[data.Seats.Count];
            playerNames = new string[seats.Length];
            for (var seat = 0; seat < seats.Length; seat++)
            {
                seats[seat] = data.Seats[seat];
                playerNames[seat] = seats[seat].Name;
            }

            var humanCount = 0;
            for (var seat = 0; seat < seats.Length; seat++)
            {
                if (!seats[seat].IsBot)
                {
                    humanCount++;
                }
            }

            activeRequest = new MatchRequest(seats, data.TurnSeconds,
                humanCount >= 2 ? MatchMode.PassAndPlay : MatchMode.Solo, resume: true, seed: data.RoundSeed);

            GameSettings.FastForward = false;
            Game = SnapshotSerializer.Read(data.Snapshot);
            RoundSeed = data.RoundSeed;
            TurnSeconds = data.TurnSeconds;
            turnTimer?.SetDuration(TurnSeconds);
            questionRandom = new System.Random(unchecked((int)(RoundSeed ^ (RoundSeed >> 32))));
            DealerQuestion = dealerQuestions == null
                ? "Who should go first?"
                : dealerQuestions.GetRandomQuestion(questionRandom);
            LastResult = null;
            LastProgression = null;
            resultRecorded = false;
            handoffSeat = -1;
            revealedSeat = -1;
            lastSavedGame = null;
            lastSavedTurnNumber = -1;
            cardTracker.Reset();
            if (botDriver != null)
            {
                botDriver.Configure(tableController, seats, cardTracker);
            }

            ProgressionService.Instance.SetLastUsedFelt(Game.State.TableFeltId);

            SetPaused(false);
            if (tableView != null)
            {
                tableView.ClearCpuPlacementViews();
                tableView.SetViewerSeat(-1);
                tableView.SetHandVisible(false);
                tableView.ApplyFelt(Game.State.TableFeltId);
            }

            IsInitializingRound = true;
            if (deckSkinLoader != null && deckSkinLoader.Preload(Game.State.DeckFaces,
                    InitializeResumedGame))
            {
                return true;
            }

            InitializeResumedGame();
            return true;
        }

        public bool IsBotSeat(int seat)
        {
            return seat >= 0 && seat < seats.Length && seats[seat].IsBot;
        }

        /// <summary>The player's name, with the bot difficulty for bot seats (e.g. "Riley (Hard bot)").</summary>
        public string GetSeatLabel(int seat)
        {
            var name = GetPlayerName(seat);
            return IsBotSeat(seat) ? name + " (" + seats[seat].Difficulty + " bot)" : name;
        }

        public CommandResult ChooseDealer(int seat)
        {
            if (Game == null || Game.State.Phase != GamePhase.ChoosingDealer || tableController == null)
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            return tableController.ApplyCommand(GameCommand.ChooseDealer(seat));
        }

        public CommandResult ChooseFirstPlayer(int seat)
        {
            if (SeatCount <= 0 || seat < 0 || seat >= SeatCount)
            {
                return ChooseDealer(seat);
            }

            var dealerSeat = (seat - 1 + SeatCount) % SeatCount;
            return ChooseDealer(dealerSeat);
        }

        public CommandResult ChooseRandomDealer()
        {
            if (Game == null || Game.State.Phase != GamePhase.ChoosingDealer || tableController == null)
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            return tableController.ApplyCommand(GameCommand.ChooseRandomDealer());
        }

        public CommandResult ChooseRandomFirstPlayer()
        {
            return ChooseRandomDealer();
        }

        public bool RevealHand()
        {
            if (Screen != MatchFlowScreen.Handoff || tableView == null)
            {
                return false;
            }

            revealedSeat = CurrentSeat;
            tableView.SetViewerSeat(CurrentSeat);
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

            var firstPlayer = LastResult.NextFirstPlayer;
            int? dealer = firstPlayer.HasValue && firstPlayer.Value >= 0 && firstPlayer.Value < seats.Length
                ? (firstPlayer.Value - 1 + seats.Length) % seats.Length
                : (int?)null;
            return StartRound(dealer, CreateSeed(seats.Length));
        }

        public bool RestartMatch()
        {
            if (LastResult != null)
            {
                return Rematch();
            }

            return Game != null && StartRound(Game.Setup.Dealer, CreateSeed(seats.Length));
        }

        public void SetPaused(bool paused)
        {
            matchPaused = paused;
            if (turnTimer != null)
            {
                turnTimer.Paused = paused;
            }

            if (tableController != null)
            {
                tableController.SetInputPaused(paused);
            }

            if (botDriver != null)
            {
                botDriver.SetPaused(paused);
            }

            if (tableView != null)
            {
                tableView.SetHandConcealed(paused);
            }
        }

        public void ReturnToMenu(MenuDestination destination = default)
        {
            SaveCurrentGame();
            SetPaused(false);
            if (botDriver != null)
            {
                botDriver.Stop();
            }

            if (tableView != null)
            {
                tableView.SetHandVisible(false);
                tableView.SetViewerSeat(-1);
            }

            SceneFlow.LoadMainMenu(destination);
        }

        public bool SaveCurrentGame()
        {
            if (Game == null || Game.State.Phase != GamePhase.Playing)
            {
                return false;
            }

            if (!SavedGame.Save(this))
            {
                return false;
            }

            lastSavedGame = Game;
            lastSavedTurnNumber = Game.State.TurnNumber;
            return true;
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

            GameSettings.FastForward = false;
            elapsedMatchSeconds = 0f;
            matchPaused = false;
            RoundSeed = seed;
            Debug.Log("Mejnun round seed: " + RoundSeed);
            questionRandom = new System.Random(unchecked((int)(seed ^ (seed >> 32))));
            DealerQuestion = dealerQuestions == null
                ? "Who should go first?"
                : dealerQuestions.GetRandomQuestion(questionRandom);
            LastResult = null;
            LastProgression = null;
            resultRecorded = false;
            handoffSeat = -1;
            revealedSeat = -1;
            cardTracker.Reset();
            if (botDriver != null)
            {
                botDriver.Stop();
            }

            var feltId = MejnunGame.AssignTableFelt(activeRequest?.FeltPool, seed);
            ProgressionService.Instance.SetLastUsedFelt(feltId);

            if (tableView != null)
            {
                tableView.ClearCpuPlacementViews();
                tableView.SetViewerSeat(-1);
                tableView.SetHandVisible(false);
                tableView.ApplyFelt(feltId);
            }

            var faceCandidates = GetPreloadCandidates();
            IsInitializingRound = true;
            if (deckSkinLoader != null && deckSkinLoader.Preload(faceCandidates,
                    () => StartRoundWithAvailableSkins(dealer, seed)))
            {
                return true;
            }

            return StartRoundWithAvailableSkins(dealer, seed);
        }

        private bool StartRoundWithAvailableSkins(int? dealer, ulong seed)
        {
            var facePool = activeRequest != null && activeRequest.RandomFacePool != null
                ? CopyStrings(activeRequest.RandomFacePool)
                : tableView == null || tableView.SkinCatalog == null
                    ? new[] { DeckFaceAssigner.ClassicFace, DeckFaceAssigner.ClassicIvoryFace }
                    : tableView.SkinCatalog.GetAvailableSkinIds();
            Game = new MejnunGame(new GameSetup(playerNames.Length, seed, dealer, TurnSeconds, facePool,
                CopyStrings(activeRequest?.DeckPicks), CopyStrings(activeRequest?.FeltPool)));
            IsInitializingRound = false;
            SetScreen(dealer.HasValue ? MatchFlowScreen.Turn : MatchFlowScreen.DealerQuestion);
            tableController.Initialize(Game);
            return true;
        }

        private string[] GetPreloadCandidates()
        {
            if (activeRequest != null &&
                (activeRequest.RandomFacePool != null || activeRequest.DeckPicks != null))
            {
                var candidates = new List<string>();
                AddFaceCandidates(candidates, activeRequest.DeckPicks);
                AddFaceCandidates(candidates, activeRequest.RandomFacePool);
                return candidates.ToArray();
            }

            return tableView == null || tableView.SkinCatalog == null
                ? new[] { DeckFaceAssigner.ClassicFace, DeckFaceAssigner.ClassicIvoryFace }
                : tableView.SkinCatalog.GetSkinIds();
        }

        private static void AddFaceCandidates(List<string> destination, IReadOnlyList<string> source)
        {
            if (source == null)
            {
                return;
            }

            for (var index = 0; index < source.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(source[index]))
                {
                    destination.Add(source[index]);
                }
            }
        }

        private void InitializeResumedGame()
        {
            IsInitializingRound = false;
            tableController.Initialize(Game);
        }

        private static string[] CopyStrings(IReadOnlyList<string> values)
        {
            if (values == null)
            {
                return null;
            }

            var copy = new string[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                copy[index] = values[index];
            }

            return copy;
        }

        private void OnSynchronized(GameState state, TurnSession turn)
        {
            cardTracker.Observe(state);
            if (state.Phase == GamePhase.Playing &&
                (lastSavedGame != Game || lastSavedTurnNumber != state.TurnNumber))
            {
                SaveCurrentGame();
            }

            if (state.Phase != GamePhase.Playing || state.CurrentSeat == handoffSeat)
            {
                return;
            }

            var seat = state.CurrentSeat;
            handoffSeat = seat;
            if (IsBotSeat(seat))
            {
                ShowBotTurn();
                return;
            }

            if (botDriver != null)
            {
                botDriver.FastForward = false;
            }

            // The handoff screen only appears when the device must pass to a different human.
            if (HumanCount <= 1 || seat == revealedSeat)
            {
                revealedSeat = seat;
                if (tableView != null)
                {
                    tableView.SetViewerSeat(seat);
                    tableView.SetHandVisible(true);
                }

                SetScreen(MatchFlowScreen.Turn);
                return;
            }

            if (tableView != null)
            {
                tableView.SetHandVisible(false);
            }

            SetScreen(MatchFlowScreen.Handoff);
        }

        private void ShowBotTurn()
        {
            if (tableView != null)
            {
                // With a single human, they keep watching their own hand while bots play.
                // With several humans, no hand is shown, so nobody's cards are revealed to the wrong person.
                var human = SoleHumanSeat();
                tableView.SetViewerSeat(human);
                tableView.SetHandVisible(human >= 0);
            }

            SetScreen(MatchFlowScreen.Turn);
            if (botDriver != null)
            {
                botDriver.Evaluate();
            }
        }

        private int SoleHumanSeat()
        {
            if (HumanCount != 1)
            {
                return -1;
            }

            for (var seat = 0; seat < seats.Length; seat++)
            {
                if (!seats[seat].IsBot)
                {
                    return seat;
                }
            }

            return -1;
        }

        private void OnGameWon(GameResult result)
        {
            ShowResults(result);
        }

        private void OnGameStalemate(GameResult result)
        {
            ShowResults(result);
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused)
            {
                SaveCurrentGame();
            }
        }

        private void OnApplicationQuit()
        {
            SaveCurrentGame();
        }

        private void ShowResults(GameResult result)
        {
            SavedGame.Delete();
            lastSavedGame = null;
            lastSavedTurnNumber = -1;
            LastResult = result;
            if (!resultRecorded && seats.Length >= 2 && HumanCount >= 1)
            {
                resultRecorded = true;
                var localWin = result.Winner.HasValue && result.Winner.Value >= 0 &&
                               result.Winner.Value < seats.Length && !seats[result.Winner.Value].IsBot;
                var mode = activeRequest == null
                    ? (HumanCount >= 2 ? MatchMode.PassAndPlay : MatchMode.Solo)
                    : activeRequest.Mode;
                var beatHardBot = false;
                var hasBot = false;
                var cpuDifficulty = 0;
                for (var seat = 0; seat < seats.Length; seat++)
                {
                    if (!seats[seat].IsBot)
                    {
                        continue;
                    }

                    hasBot = true;
                    cpuDifficulty = Math.Max(cpuDifficulty, (int)seats[seat].Difficulty);
                }

                if (!hasBot)
                {
                    cpuDifficulty = 2;
                }

                if (mode == MatchMode.Solo && localWin)
                {
                    for (var seat = 0; seat < seats.Length; seat++)
                    {
                        if (seats[seat].IsBot && seats[seat].Difficulty == BotDifficulty.Hard)
                        {
                            beatHardBot = true;
                            break;
                        }
                    }
                }

                var summary = new MatchSummary(mode, seats.Length, HumanCount, localWin,
                    localWin && result.Kind == GameEndKind.EmptyHand,
                    result.Kind == GameEndKind.Stalemate, beatHardBot, cpuDifficulty,
                    Mathf.FloorToInt(elapsedMatchSeconds));
                LastProgression = ProgressionService.Instance.RecordMatch(summary);
            }

            if (botDriver != null)
            {
                botDriver.Stop();
            }

            if (tableView != null)
            {
                tableView.SetViewerSeat(-1);
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

        private static ulong CreateSeed(int seatCount)
        {
            var seed = unchecked((ulong)DateTime.UtcNow.Ticks);
            return GameSettings.EasyOpeningDeal
                ? OpeningSeedFinder.Find(seed, seatCount)
                : seed;
        }
    }
}