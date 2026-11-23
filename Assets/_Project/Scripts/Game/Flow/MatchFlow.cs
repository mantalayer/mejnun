using System;
using System.Collections.Generic;
using Mejnun.AI;
using Mejnun.Cosmetics;
using Mejnun.Core;
using Mejnun.Localization;
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
        private TutorialDirector tutorialDirector;
        private GameResult pendingTutorialResult;
        private int handoffSeat = -1;
        private int revealedSeat = -1;
        private TurnTimer turnTimer;
        private MejnunGame lastSavedGame;
        private int lastSavedTurnNumber = -1;
        private bool resultRecorded;
        private bool matchPaused;
        private bool onlineSessionPaused;
        private bool applicationPaused;
        private bool remoteAuthority;
        private bool onlineRematchAccepted;
        private int onlineLocalSeat = -1;
        private bool onlineFirstPlayerVoteOpen;
        private bool[] onlineFirstPlayerVoteCandidates = Array.Empty<bool>();
        private int[] onlineFirstPlayerVotes = Array.Empty<int>();
        private bool onlineDeckPreloadPending;
        private MejnunGame pendingOnlineGame;
        private MejnunGame deferredOnlineGame;
        private string onlineWaitingMessage = string.Empty;
        private float elapsedMatchSeconds;
        private float localTurnSeconds;
        private MatchRequest activeRequest;
        private readonly CardTracker cardTracker = new CardTracker();
        private readonly MatchStatsTracker matchStatsTracker = new MatchStatsTracker();
        private bool chosenFirstByQuestion;
        private bool allBotsHard;

        public MejnunGame Game { get; private set; }
        public TableController Controller => tableController;
        public TableView Table => tableView;
        public GameResult LastResult { get; private set; }
        public ProgressionReport LastProgression { get; private set; }
        public bool IsInitializingRound { get; private set; }
        public MatchFlowScreen Screen { get; private set; } = MatchFlowScreen.Idle;
        public int SeatCount => playerNames.Length;
        public float MatchElapsedSeconds => elapsedMatchSeconds;
        public float LocalTurnSeconds => localTurnSeconds;

        /// <summary>The DealerQuestions table key of this round's question; null uses the generic question.</summary>
        public string DealerQuestionKey { get; private set; }

        /// <summary>This round's question in the current language.</summary>
        public string DealerQuestion => DealerQuestions.Resolve(DealerQuestionKey);
        public int CurrentSeat => Game == null ? -1 : Game.State.CurrentSeat;
        public ulong RoundSeed { get; private set; }
        public BotTurnDriver BotDriver => botDriver;
        public TutorialDirector Tutorial => tutorialDirector;
        public bool IsTutorial => activeRequest != null && activeRequest.IsTutorial;
        public MatchMode Mode => activeRequest == null
            ? (HumanCount >= 2 ? MatchMode.PassAndPlay : MatchMode.Solo)
            : activeRequest.Mode;
        public TurnTimer TurnTimer => turnTimer;
        public int TurnSeconds { get; private set; }
        public static string FormatDuration(float durationSeconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(durationSeconds));
            var zero = Loc.Number(0)[0];
            var seconds = Loc.Number(totalSeconds % 60).PadLeft(2, zero);
            if (totalSeconds >= 3600)
            {
                var minutes = Loc.Number(totalSeconds / 60 % 60).PadLeft(2, zero);
                return $"{Loc.Number(totalSeconds / 3600)}:{minutes}:{seconds}";
            }

            return $"{Loc.Number(totalSeconds / 60)}:{seconds}";
        }

        public bool IsOnline => remoteAuthority && Mode == MatchMode.Online;
        public bool OnlineIsHost { get; set; }
        public IReadOnlyList<SeatConfig> Seats => seats;
        public MatchRequest ActiveRequest => activeRequest;
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
        public event Action OnlineSeatsChanged;
        public event Action<string> OnlineNotice;
        public event Action OnlineRematchRequested;
        public event Action OnlineFirstPlayerVoteChanged;
        public event Action<int> OnlineFirstPlayerVoteCast;
        public event Action<Reaction> OnlineReactionRequested;
        public event Action<int, Reaction> ReactionReceived;

        public string OnlineWaitingMessage => onlineWaitingMessage;
        public bool CanShowReactions => IsOnline && Game != null && Game.State.Phase == GamePhase.Playing &&
                                        onlineLocalSeat >= 0 && !IsBotSeat(onlineLocalSeat) && !matchPaused &&
                                        !applicationPaused && string.IsNullOrEmpty(onlineWaitingMessage);
        public bool IsOnlineFirstPlayerVoteOpen => IsOnline && onlineFirstPlayerVoteOpen;
        public bool CanVoteOnlineFirstPlayer => IsOnlineFirstPlayerVoteOpen && !matchPaused &&
                                                IsOnlineFirstPlayerVoteCandidate(onlineLocalSeat);
        public int OnlineFirstPlayerLocalVote => onlineLocalSeat >= 0 && onlineLocalSeat < onlineFirstPlayerVotes.Length
            ? onlineFirstPlayerVotes[onlineLocalSeat]
            : -2;

        public string HandoffMessage => CurrentSeat < 0
            ? string.Empty
            : Loc.Format("match.handoff.message", GetPlayerName(CurrentSeat));

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
                    return Loc.Get("match.result.stalemate");
                }

                return LastResult.Winner.HasValue
                    ? Loc.Format("match.result.winner", GetPlayerName(LastResult.Winner.Value))
                    : Loc.Get("match.result.game_over");
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
                    return Loc.Format("match.result.empty_hand", GetPlayerName(LastResult.Winner.Value));
                }

                if (LastResult.Kind == GameEndKind.FewestCards)
                {
                    return Loc.Format("match.result.fewest_cards", GetPlayerName(LastResult.Winner.Value));
                }

                return Loc.Get("match.result.tied");
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

            botDriver.TurnBlocker = () => tableView != null && tableView.IsDealing;

            tutorialDirector = GetComponent<TutorialDirector>();
            if (tutorialDirector == null)
            {
                tutorialDirector = gameObject.AddComponent<TutorialDirector>();
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
                tableController.AssistUsed += OnAssistUsed;
            }

            if (turnTimer != null)
            {
                turnTimer.TimedOut += OnTurnTimedOut;
            }

            if (tutorialDirector != null)
            {
                tutorialDirector.Finished += OnTutorialFinished;
            }
        }

        private void Start()
        {
            if (MatchLaunch.TryTake(out var request))
            {
                if (tableView != null)
                {
                    tableView.DealAnimationEnabled = true;
                }

                if (request.Resume)
                {
                    ResumeSavedGame();
                }
                else if (request.Mode == MatchMode.Online)
                {
                    activeRequest = request;
                    SetScreen(MatchFlowScreen.Idle);
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
            if (ShouldRunMatchClock(Mode == MatchMode.Online, matchPaused, onlineSessionPaused, applicationPaused) &&
                Game != null && Game.State.Phase == GamePhase.Playing)
            {
                var deltaTime = Time.unscaledDeltaTime;
                elapsedMatchSeconds += deltaTime;
                var currentSeat = Game.State.CurrentSeat;
                if (Mode == MatchMode.Online
                        ? currentSeat == onlineLocalSeat && !IsBotSeat(currentSeat)
                        : !IsBotSeat(currentSeat))
                {
                    localTurnSeconds += deltaTime;
                }
            }

            if (TurnSeconds > 0 && turnTimer != null)
            {
                var trackingSeat = StatsTrackingSeat();
                if (trackingSeat >= 0 && Game != null && Game.State.CurrentSeat == trackingSeat)
                {
                    matchStatsTracker.NoteRemainingSeconds(turnTimer.RemainingSeconds);
                }
            }
        }

        /// <summary>
        /// Online matches keep running while the local pause menu is open; only a session-wide pause stops the clocks.
        /// </summary>
        public static bool ShouldRunMatchClock(bool online, bool matchPaused, bool onlineSessionPaused,
            bool applicationPaused)
        {
            return !applicationPaused && !(online ? onlineSessionPaused : matchPaused);
        }

        private void OnDisable()
        {
            if (tableController != null)
            {
                tableController.Synchronized -= OnSynchronized;
                tableController.GameWon -= OnGameWon;
                tableController.GameStalemate -= OnGameStalemate;
                tableController.AssistUsed -= OnAssistUsed;
            }

            if (turnTimer != null)
            {
                turnTimer.TimedOut -= OnTurnTimedOut;
            }

            if (tutorialDirector != null)
            {
                tutorialDirector.Finished -= OnTutorialFinished;
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

            return StartMatch(request.Seats, request.Seed, request.IsTutorial ? 0 : request.TurnSeconds, request);
        }

        public string OnlineSessionCode { get; set; }

        public void SetOnlineAuthority(bool enabled, int localSeat)
        {
            remoteAuthority = enabled;
            onlineLocalSeat = localSeat;
            if (!enabled)
            {
                onlineSessionPaused = false;
                onlineFirstPlayerVoteOpen = false;
                onlineFirstPlayerVoteCandidates = Array.Empty<bool>();
                onlineFirstPlayerVotes = Array.Empty<int>();
            }
            if (turnTimer != null)
            {
                turnTimer.enabled = true;
                turnTimer.SetDisplayOnly(enabled);
                if (enabled)
                {
                    TurnSeconds = Mathf.Max(0, activeRequest == null ? 0 : activeRequest.TurnSeconds);
                    turnTimer.SetDuration(TurnSeconds);
                    turnTimer.Paused = matchPaused;
                }
            }

            if (tableController != null)
            {
                tableController.SetSeatInputFilter(enabled
                    ? seat => seat == onlineLocalSeat
                    : seat => !IsBotSeat(seat));
            }

            ApplyOnlineViewer();
        }

        public bool IsLocalSeat(int seat)
        {
            return IsOnline && seat >= 0 && seat == onlineLocalSeat;
        }

        public void SetOnlineSeatStatus(int seat, string name, bool isBot)
        {
            if (!remoteAuthority || seat < 0 || seat >= seats.Length)
            {
                return;
            }

            // Online names are nicknames; only an empty or default name becomes an ID.
            var seatName = SeatNames.ToId(name, false, seat);
            playerNames[seat] = seatName;
            seats[seat] = isBot
                ? SeatConfig.Bot(seatName, BotDifficulty.Medium)
                : SeatConfig.Human(seatName);
            if (isBot && seat == onlineLocalSeat)
            {
                matchStatsTracker.Reset(TurnSeconds);
            }

            tableView?.SetCpuSeatPredicate(IsBotSeat);
            OnlineSeatsChanged?.Invoke();
        }

        public void PostOnlineNotice(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                OnlineNotice?.Invoke(message);
            }
        }

        public void SetOnlineWaiting(string message)
        {
            onlineWaitingMessage = message ?? string.Empty;
            tableController?.SetInputPaused(matchPaused || !string.IsNullOrEmpty(onlineWaitingMessage));
        }

        public void SetOnlineFirstPlayerVote(bool isOpen, string questionKey, bool[] candidates, int[] votes)
        {
            if (!remoteAuthority)
            {
                return;
            }

            onlineFirstPlayerVoteOpen = isOpen;
            onlineFirstPlayerVoteCandidates = candidates == null ? Array.Empty<bool>() : (bool[])candidates.Clone();
            onlineFirstPlayerVotes = votes == null ? Array.Empty<int>() : (int[])votes.Clone();
            if (isOpen)
            {
                DealerQuestionKey = questionKey;
            }

            if (Game != null && Game.State.Phase == GamePhase.ChoosingDealer && Screen != MatchFlowScreen.Results)
            {
                SetScreen(isOpen ? MatchFlowScreen.DealerQuestion : MatchFlowScreen.Turn);
            }

            OnlineFirstPlayerVoteChanged?.Invoke();
        }

        public bool IsOnlineFirstPlayerVoteCandidate(int seat)
        {
            return IsOnlineFirstPlayerVoteOpen && seat >= 0 && seat < onlineFirstPlayerVoteCandidates.Length &&
                   onlineFirstPlayerVoteCandidates[seat];
        }

        public int GetOnlineFirstPlayerVoteCount(int choice)
        {
            var count = 0;
            for (var seat = 0; seat < onlineFirstPlayerVotes.Length; seat++)
            {
                if (onlineFirstPlayerVotes[seat] == choice)
                {
                    count++;
                }
            }

            return count;
        }

        public void ApplyOnlineGame(MejnunGame authoritativeGame)
        {
            if (!remoteAuthority || authoritativeGame == null)
            {
                return;
            }

            if (Screen == MatchFlowScreen.Results && authoritativeGame.State.Phase != GamePhase.Finished &&
                !onlineRematchAccepted)
            {
                deferredOnlineGame = authoritativeGame;
                return;
            }

            var duration = authoritativeGame.Setup.TurnSeconds;
            if (TurnSeconds != duration)
            {
                TurnSeconds = duration;
                turnTimer?.SetDuration(duration);
            }

            if (turnTimer != null)
            {
                turnTimer.SetDisplayOnly(true);
                turnTimer.Paused = matchPaused;
            }
            if (onlineDeckPreloadPending)
            {
                pendingOnlineGame = authoritativeGame;
                return;
            }

            if (deckSkinLoader != null && authoritativeGame.State.DeckFaces != null)
            {
                onlineDeckPreloadPending = true;
                pendingOnlineGame = authoritativeGame;
                if (deckSkinLoader.Preload(authoritativeGame.State.DeckFaces, OnOnlineDecksPreloaded))
                {
                    return;
                }

                onlineDeckPreloadPending = false;
                pendingOnlineGame = null;
            }

            ApplyOnlineGameNow(authoritativeGame);
        }

        private void OnOnlineDecksPreloaded()
        {
            var game = pendingOnlineGame;
            pendingOnlineGame = null;
            onlineDeckPreloadPending = false;
            if (game != null)
            {
                ApplyOnlineGameNow(game);
            }
        }

        private void ApplyOnlineGameNow(MejnunGame authoritativeGame)
        {
            if (!remoteAuthority || authoritativeGame == null)
            {
                return;
            }

            if (Screen == MatchFlowScreen.Results && authoritativeGame.State.Phase != GamePhase.Finished &&
                !onlineRematchAccepted)
            {
                deferredOnlineGame = authoritativeGame;
                return;
            }

            var startingRematch = Game != null && Game.State.Phase == GamePhase.Finished &&
                                  authoritativeGame.State.Phase != GamePhase.Finished;
            if (startingRematch)
            {
                GameSettings.FastForward = false;
                matchPaused = false;
                elapsedMatchSeconds = 0;
                localTurnSeconds = 0;
                RoundSeed = authoritativeGame.Setup.Seed;
                questionRandom = new System.Random(unchecked((int)(RoundSeed ^ (RoundSeed >> 32))));
                LastResult = null;
                LastProgression = null;
                resultRecorded = false;
                chosenFirstByQuestion = false;
                pendingTutorialResult = null;
                handoffSeat = -1;
                revealedSeat = -1;
                lastSavedGame = null;
                lastSavedTurnNumber = -1;
                DealerQuestionKey = PickDealerQuestionKey();
                cardTracker.Reset();
                allBotsHard = AreAllOpponentsHardBots();
                matchStatsTracker.Reset(TurnSeconds);
                ProgressionService.Instance.SetLastUsedFelt(authoritativeGame.State.TableFeltId);
                tableView?.ResetForNewGame();
                tableView?.ClearCpuPlacementViews();
                tableView?.ApplyFelt(authoritativeGame.State.TableFeltId);
                deferredOnlineGame = null;
            }

            Game = authoritativeGame;
            tableController?.Resync(authoritativeGame);
            ApplyOnlineViewer();
            if (Screen != MatchFlowScreen.Results && authoritativeGame.State.Phase == GamePhase.ChoosingDealer)
            {
                SetScreen(IsOnlineFirstPlayerVoteOpen ? MatchFlowScreen.DealerQuestion : MatchFlowScreen.Turn);
            }
            else if (startingRematch)
            {
                SetScreen(MatchFlowScreen.Turn);
            }

            if (authoritativeGame.State.Phase == GamePhase.Finished && authoritativeGame.State.Result != null &&
                Screen != MatchFlowScreen.Results)
            {
                ShowResults(authoritativeGame.State.Result);
            }
        }

        public void SetOnlinePaused(bool paused)
        {
            if (!remoteAuthority)
            {
                return;
            }

            matchPaused = paused;
            onlineSessionPaused = paused;
            if (turnTimer != null)
            {
                turnTimer.Paused = paused;
            }

            tableController?.SetInputPaused(paused || !string.IsNullOrEmpty(onlineWaitingMessage));
            OnlineFirstPlayerVoteChanged?.Invoke();
        }

        public bool CanSendReaction(Reaction reaction)
        {
            if (!CanShowReactions || !ReactionRules.CanSend(reaction, Game.State, onlineLocalSeat))
            {
                return false;
            }

            return !ReactionRules.IsTargeted(reaction) || !IsBotSeat(Game.State.CurrentSeat);
        }

        public void RequestReaction(Reaction reaction)
        {
            if (CanSendReaction(reaction))
            {
                OnlineReactionRequested?.Invoke(reaction);
            }
        }

        public void ReceiveReaction(int fromSeat, Reaction reaction)
        {
            if (!IsOnline || Game == null || Game.State.Phase != GamePhase.Playing ||
                onlineLocalSeat < 0 || fromSeat < 0 || fromSeat >= seats.Length || fromSeat == onlineLocalSeat ||
                matchPaused || applicationPaused || !string.IsNullOrEmpty(onlineWaitingMessage) ||
                !Enum.IsDefined(typeof(Reaction), reaction))
            {
                return;
            }

            if (ReactionRules.IsTargeted(reaction) && Game.State.CurrentSeat != onlineLocalSeat)
            {
                return;
            }

            ReactionReceived?.Invoke(fromSeat, reaction);
        }

        /// <summary>Leaves the tutorial: it counts as completed, nothing is saved, and the menu is shown.</summary>
        public void SkipTutorial()
        {
            if (tutorialDirector != null)
            {
                tutorialDirector.Skip();
            }

            ReturnToMenu();
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
                playerNames[seat] = SeatNames.ToId(config.Name, config.IsBot, seat);
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
            allBotsHard = AreAllOpponentsHardBots();

            return StartRound(request?.Dealer, seed ?? CreateSeed(seats.Length));
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
                var saved = data.Seats[seat];
                playerNames[seat] = SeatNames.ToId(saved.Name, saved.IsBot, seat);
                seats[seat] = new SeatConfig(playerNames[seat], saved.IsBot, saved.Difficulty, saved.Style);
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
            allBotsHard = AreAllOpponentsHardBots();

            GameSettings.FastForward = false;
            Game = SnapshotSerializer.Read(data.Snapshot);
            RoundSeed = data.RoundSeed;
            TurnSeconds = data.TurnSeconds;
            turnTimer?.SetDuration(TurnSeconds);
            questionRandom = new System.Random(unchecked((int)(RoundSeed ^ (RoundSeed >> 32))));
            DealerQuestionKey = PickDealerQuestionKey();
            LastResult = null;
            LastProgression = null;
            resultRecorded = false;
            chosenFirstByQuestion = false;
            handoffSeat = -1;
            revealedSeat = -1;
            lastSavedGame = null;
            lastSavedTurnNumber = -1;
            cardTracker.Reset();
            matchStatsTracker.Reset(TurnSeconds);
            if (botDriver != null)
            {
                botDriver.Configure(tableController, seats, cardTracker);
            }

            ProgressionService.Instance.SetLastUsedFelt(Game.State.TableFeltId);

            SetPaused(false);
            if (tableView != null)
            {
                tableView.ClearCpuPlacementViews();
                if (remoteAuthority && onlineLocalSeat >= 0)
                {
                    ApplyOnlineViewer();
                }
                else
                {
                    tableView.SetViewerSeat(-1);
                    tableView.SetHandVisible(false);
                }

                tableView.ApplyFelt(Game.State.TableFeltId);
                tableView.PrepareBoardRestore();
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
            if (!IsBotSeat(seat))
            {
                return name;
            }

            switch (seats[seat].Difficulty)
            {
                case BotDifficulty.Easy:
                    return Loc.Format("match.seat.bot_label.easy", name);
                case BotDifficulty.Hard:
                    return Loc.Format("match.seat.bot_label.hard", name);
                default:
                    return Loc.Format("match.seat.bot_label.medium", name);
            }
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
                if (IsOnline)
                {
                    return CommandResult.Failure(RuleError.WrongPhase);
                }

                return ChooseDealer(seat);
            }

            if (IsOnline)
            {
                if (!CanVoteOnlineFirstPlayer)
                {
                    return CommandResult.Failure(RuleError.WrongPhase);
                }

                OnlineFirstPlayerVoteCast?.Invoke(seat);
                if (seat == StatsTrackingSeat())
                {
                    chosenFirstByQuestion = true;
                }

                return CommandResult.Success();
            }

            var dealerSeat = (seat - 1 + SeatCount) % SeatCount;
            var result = ChooseDealer(dealerSeat);
            if (result.Ok && seat == StatsTrackingSeat())
            {
                chosenFirstByQuestion = true;
            }

            return result;
        }

        public CommandResult ChooseRandomDealer()
        {
            if (IsOnline)
            {
                return ChooseRandomFirstPlayer();
            }

            if (Game == null || Game.State.Phase != GamePhase.ChoosingDealer || tableController == null)
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            return tableController.ApplyCommand(GameCommand.ChooseRandomDealer());
        }

        public CommandResult ChooseRandomFirstPlayer()
        {
            if (IsOnline)
            {
                if (!CanVoteOnlineFirstPlayer)
                {
                    return CommandResult.Failure(RuleError.WrongPhase);
                }

                OnlineFirstPlayerVoteCast?.Invoke(-1);
                return CommandResult.Success();
            }

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

            if (IsOnline)
            {
                if (onlineRematchAccepted)
                {
                    return true;
                }

                onlineRematchAccepted = true;
                var deferredGame = deferredOnlineGame;
                deferredOnlineGame = null;
                OnlineRematchRequested?.Invoke();
                if (deferredGame != null && Screen == MatchFlowScreen.Results)
                {
                    ApplyOnlineGame(deferredGame);
                }

                return true;
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
                tableController.SetInputPaused(paused || !string.IsNullOrEmpty(onlineWaitingMessage));
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
            if (botDriver != null)
            {
                botDriver.Stop();
            }

            SceneFlow.LoadMainMenu(destination);
        }

        public bool SaveCurrentGame()
        {
            if (Game == null || Game.State.Phase != GamePhase.Playing || IsTutorial || Mode == MatchMode.Online)
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
                return Loc.Format("match.seat.player", seat + 1);
            }

            // Seats store IDs for generated names, so this resolves in the current language on every call.
            return SeatNames.Display(playerNames[seat], seat);
        }

        private string PickDealerQuestionKey()
        {
            return dealerQuestions != null
                ? dealerQuestions.GetRandomQuestionKey(questionRandom)
                : DealerQuestions.PickKey(questionRandom);
        }

        private bool StartRound(int? dealer, ulong seed)
        {
            if (tableController == null)
            {
                return false;
            }

            GameSettings.FastForward = false;
            elapsedMatchSeconds = 0f;
            localTurnSeconds = 0f;
            matchPaused = false;
            onlineFirstPlayerVoteOpen = false;
            onlineFirstPlayerVoteCandidates = Array.Empty<bool>();
            onlineFirstPlayerVotes = Array.Empty<int>();
            RoundSeed = seed;
            Debug.Log("Mejnun round seed: " + RoundSeed);
            questionRandom = new System.Random(unchecked((int)(seed ^ (seed >> 32))));
            DealerQuestionKey = PickDealerQuestionKey();
            LastResult = null;
            LastProgression = null;
            resultRecorded = false;
            chosenFirstByQuestion = false;
            pendingTutorialResult = null;
            handoffSeat = -1;
            revealedSeat = -1;
            cardTracker.Reset();
            allBotsHard = AreAllOpponentsHardBots();
            matchStatsTracker.Reset(TurnSeconds);
            if (botDriver != null)
            {
                botDriver.Stop();
            }

            if (tutorialDirector != null)
            {
                tutorialDirector.Stop();
            }

            var feltId = MejnunGame.AssignTableFelt(activeRequest?.FeltPool, seed);
            ProgressionService.Instance.SetLastUsedFelt(feltId);

            if (tableView != null)
            {
                tableView.ClearCpuPlacementViews();
                if (remoteAuthority && onlineLocalSeat >= 0)
                {
                    ApplyOnlineViewer();
                }
                else
                {
                    tableView.SetViewerSeat(-1);
                    tableView.SetHandVisible(false);
                }

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
            tableView?.ResetForNewGame();
            var facePool = activeRequest != null && activeRequest.RandomFacePool != null
                ? CopyStrings(activeRequest.RandomFacePool)
                : tableView == null || tableView.SkinCatalog == null
                    ? new[] { DeckFaceAssigner.ClassicFace, DeckFaceAssigner.ClassicIvoryFace }
                    : tableView.SkinCatalog.GetAvailableSkinIds();
            Game = new MejnunGame(new GameSetup(playerNames.Length, seed, dealer, TurnSeconds, facePool,
                CopyStrings(activeRequest?.DeckPicks), CopyStrings(activeRequest?.FeltPool),
                activeRequest?.ShoeOrder));
            tableView?.ArmDeal();
            IsInitializingRound = false;
            if (IsTutorial)
            {
                tableController.SetHintsEnabled(false);
                // Started before the table syncs so the scripted CPU is held behind the opening modals.
                tutorialDirector.Begin(MejnunTutorial.Create(), tableController, botDriver);
            }

            SetScreen(dealer.HasValue || Mode == MatchMode.Online
                ? MatchFlowScreen.Turn
                : MatchFlowScreen.DealerQuestion);
            tableController.Initialize(Game);
            ApplyOnlineViewer();
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
            ApplyOnlineViewer();
            if (tableView != null)
            {
                tableView.PlayBoardDeal(0f);
                tableView.PlayHandIntro(0.7f, 0.08f);
            }
        }

        private void ApplyOnlineViewer()
        {
            if (!remoteAuthority || onlineLocalSeat < 0 || tableView == null)
            {
                return;
            }

            tableView.SetViewerSeat(onlineLocalSeat);
            tableView.SetHandVisible(true);
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
            var trackingSeat = StatsTrackingSeat();
            if (trackingSeat >= 0)
            {
                matchStatsTracker.Observe(state, trackingSeat, tableController != null && tableController.HintsEnabled);
            }

            if (Mode == MatchMode.Online)
            {
                if (state.Phase == GamePhase.Finished && state.Result != null && Screen != MatchFlowScreen.Results)
                {
                    ShowResults(state.Result);
                }
                else if (state.Phase == GamePhase.ChoosingDealer)
                {
                    SetScreen(IsOnlineFirstPlayerVoteOpen ? MatchFlowScreen.DealerQuestion : MatchFlowScreen.Turn);
                }
                else if (state.Phase == GamePhase.Playing && Screen != MatchFlowScreen.Results)
                {
                    SetScreen(MatchFlowScreen.Turn);
                }

                return;
            }

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

        private int StatsTrackingSeat()
        {
            if (Mode == MatchMode.Online)
            {
                return onlineLocalSeat >= 0 && onlineLocalSeat < seats.Length && !seats[onlineLocalSeat].IsBot
                    ? onlineLocalSeat
                    : -1;
            }

            return SoleHumanSeat();
        }

        private bool AreAllOpponentsHardBots()
        {
            var localSeat = SoleHumanSeat();
            if (SeatCount != 6 || localSeat < 0)
            {
                return false;
            }

            for (var seat = 0; seat < seats.Length; seat++)
            {
                if (seat != localSeat && (!seats[seat].IsBot || seats[seat].Difficulty != BotDifficulty.Hard))
                {
                    return false;
                }
            }

            return true;
        }

        private void OnAssistUsed(TableAssistKind assistKind)
        {
            switch (assistKind)
            {
                case TableAssistKind.Undo:
                    matchStatsTracker.RecordUndo();
                    break;
                case TableAssistKind.Revert:
                    matchStatsTracker.RecordRevert();
                    break;
                case TableAssistKind.FixBoard:
                    matchStatsTracker.RecordFixBoard();
                    break;
            }
        }

        private void OnTurnTimedOut()
        {
            var trackingSeat = StatsTrackingSeat();
            if (Game != null && trackingSeat >= 0 && Game.State.CurrentSeat == trackingSeat)
            {
                matchStatsTracker.RecordTimeout();
            }
        }

        private void OnGameWon(GameResult result)
        {
            if (IsTutorial && tutorialDirector != null && tutorialDirector.IsRunning)
            {
                // The tutorial shows its closing "Good to know" pages before the results screen.
                pendingTutorialResult = result;
                if (botDriver != null)
                {
                    botDriver.Stop();
                }

                return;
            }

            ShowResults(result);
        }

        private void OnTutorialFinished()
        {
            if (pendingTutorialResult == null)
            {
                return;
            }

            var result = pendingTutorialResult;
            pendingTutorialResult = null;
            ShowResults(result);
        }

        private void OnGameStalemate(GameResult result)
        {
            ShowResults(result);
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused && Mode != MatchMode.Online)
            {
                SaveCurrentGame();
            }
        }

        private void OnApplicationQuit()
        {
            if (Mode != MatchMode.Online)
            {
                SaveCurrentGame();
            }
        }

        private void ShowResults(GameResult result)
        {
            if (!IsTutorial)
            {
                // The tutorial never saves, so it must not discard a real game the player left to resume.
                SavedGame.Delete();
            }

            lastSavedGame = null;
            lastSavedTurnNumber = -1;
            LastResult = result;
            onlineRematchAccepted = false;
            if (IsTutorial)
            {
                resultRecorded = true;
            }

            var resultMode = activeRequest == null
                ? (HumanCount >= 2 ? MatchMode.PassAndPlay : MatchMode.Solo)
                : activeRequest.Mode;
            var onlineSeatIsHuman = resultMode != MatchMode.Online ||
                                    (onlineLocalSeat >= 0 && onlineLocalSeat < seats.Length &&
                                     !seats[onlineLocalSeat].IsBot);
            if (!onlineSeatIsHuman)
            {
                resultRecorded = true;
            }

            if (!resultRecorded && seats.Length >= 2 && HumanCount >= 1)
            {
                resultRecorded = true;
                var mode = resultMode;
                var localWin = result.Winner.HasValue && result.Winner.Value >= 0 &&
                               result.Winner.Value < seats.Length &&
                               (mode == MatchMode.Online
                                   ? result.Winner.Value == onlineLocalSeat && onlineSeatIsHuman
                                   : !seats[result.Winner.Value].IsBot);
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

                var trackingSeat = StatsTrackingSeat();
                if (Game != null && trackingSeat >= 0)
                {
                    matchStatsTracker.Observe(Game.State, trackingSeat,
                        tableController != null && tableController.HintsEnabled);
                }

                var details = trackingSeat >= 0
                    ? matchStatsTracker.Finish(result, Game == null ? null : Game.State)
                    : new MatchDetails();
                details.FastestWinSeconds = localWin ? Mathf.FloorToInt(elapsedMatchSeconds) : 0;
                details.TurnSeconds = TurnSeconds;
                details.IsHost = mode == MatchMode.Online && OnlineIsHost;
                details.ChosenFirstByQuestion = chosenFirstByQuestion;
                details.AllBotsHard = allBotsHard;

                var summary = new MatchSummary(mode, seats.Length, HumanCount, localWin,
                    localWin && result.Kind == GameEndKind.EmptyHand,
                    result.Kind == GameEndKind.Stalemate, beatHardBot, cpuDifficulty,
                    Mathf.FloorToInt(elapsedMatchSeconds), details);
                LastProgression = ProgressionService.Instance.RecordMatch(summary);
            }

            if (botDriver != null)
            {
                botDriver.Stop();
            }

            if (tableView != null)
            {
                tableView.CollectTableToPile();
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