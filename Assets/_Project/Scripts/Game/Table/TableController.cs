using System;
using System.Collections;
using System.Collections.Generic;
using Mejnun.AI;
using Mejnun.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mejnun.Game
{
    public enum TableAssistKind
    {
        Undo,
        Revert,
        FixBoard
    }

    public enum GoodMoveKind
    {
        NewSet,
        CardAdded
    }

    public sealed class TableController : MonoBehaviour
    {
        private const float BoardTailHighlightStaggerSeconds = 0.035f;
        [SerializeField] private TableView tableView;
        [SerializeField] private PointerRouter pointerRouter;
        [SerializeField] private MonoBehaviour feedbackSinkBehaviour;

        private readonly Stack<int> undoGroups = new Stack<int>();
        private readonly Stack<List<GameCommand>> redoGroups = new Stack<List<GameCommand>>();
        private readonly GestureInterpreter gestureInterpreter = new GestureInterpreter();
        private HashSet<byte> hintedCardIds = new HashSet<byte>();
        private readonly HashSet<byte> tutorialFocusIds = new HashSet<byte>();
        private readonly HashSet<byte> tutorialTargetIds = new HashSet<byte>();
        private ICommandGate commandGate;
        private ICommandRelay commandRelay;
        private int lastSynchronizedTurn = -1;
        private int lastSynchronizedSeat = -1;
        private bool tutorialFocusActive;
        private int tutorialTargetMeldId = -1;
        private bool tutorialEmptyTarget;
        private Func<int, bool> seatInputFilter;
        private bool hintsEnabled;
        private MejnunGame game;
        private IFeedbackSink feedbackSink;
        private GestureHit dragSource;
        private readonly List<byte> boardTailCardIds = new List<byte>();
        private bool dragActive;
        private Vector2 lastDragPosition;
        private bool lastDragIsLongPress;
        private bool edgePanArmed;
        private bool boardHoldLift;
        private bool boardTailLifted;
        private float boardHoldStartedAt;
        private bool handScrollActive;
        private bool panActive;
        private bool inputPaused;
        private bool meldReorderActive;
        private bool sweepSelectActive;
        private bool autoFixing;
        private bool markModeActive;
        private byte? hoveredCardId;
        private TapEffect lastTapEffect;
        private SelectionSnapshot lastTapSelectionSnapshot;
        [SerializeField, Min(0f)] private float timeoutFixStepSeconds = 0.6f;
        private readonly SetMarkers setMarkers = new SetMarkers();

        private enum TapEffect
        {
            None,
            SelectionToggle,
            SuccessfulMove,
            RemoteTurnBlocked
        }

        public MejnunGame Game => game;
        public int UndoGroupCount => undoGroups.Count;
        public int RedoGroupCount => redoGroups.Count;
        public bool HasPlacedHandCard => HasPlacedHandCardOnBoard();
        public IReadOnlyList<byte> SelectedCardIds => gestureInterpreter.SelectedCardIds;
        public bool HintsEnabled => hintsEnabled;
        public IReadOnlyCollection<byte> HintedCardIds => hintedCardIds;
        public int HandSeat => tableView == null ? -1 : tableView.HandSeat;
        public IReadOnlyList<MarkedSet> MarkedSets => setMarkers.Marks;
        public IReadOnlyList<MarkedCard> MarkedCards => setMarkers.CardMarks;
        public bool MarkModeActive => markModeActive;
        public bool IsAutoFixing => autoFixing;
        public ICommandGate CommandGate => commandGate;
        public bool TutorialFocusActive => tutorialFocusActive;
        public IReadOnlyCollection<byte> TutorialFocusCardIds => tutorialFocusIds;
        public int TutorialTargetMeldId => tutorialTargetMeldId;
        public bool TutorialEmptyTarget => tutorialEmptyTarget;

        /// <summary>True when the local player may act: a game is in progress and the current seat accepts input.</summary>
        public bool InputEnabled => game != null && game.State.Phase == GamePhase.Playing &&
                                    !inputPaused && !autoFixing && !(tableView != null && tableView.IsDealing) &&
                                    (seatInputFilter == null || seatInputFilter(game.State.CurrentSeat)) &&
                                    (commandRelay == null || !commandRelay.IsRemoteAuthority ||
                                     commandRelay.CanControlSeat(game.State.CurrentSeat));

        public bool HandInputEnabled => game != null && game.State.Phase == GamePhase.Playing && !inputPaused &&
                                        !autoFixing && !(tableView != null && tableView.IsDealing) &&
                                        HandSeat >= 0 && HandSeat < game.State.Hands.Count &&
                                        (seatInputFilter == null || seatInputFilter(HandSeat)) &&
                                        (commandRelay == null || !commandRelay.IsRemoteAuthority ||
                                         commandRelay.CanControlSeat(HandSeat));

        public event Action<GameState, TurnSession> Synchronized;
        public event Action<int> WentDown;
        public event Action<GoodMoveKind, int> GoodMove;
        public event Action<int, IReadOnlyList<int>> DownRevealed;
        public event Action<RuleError> InvalidGesture;
        public event Action<GameResult> GameWon;
        public event Action<GameResult> GameStalemate;
        public event Action<MarkedSet> MarkedSetBroken;
        public event Action<bool> MarkModeChanged;
        public event Action<TableAssistKind> AssistUsed;

        private void Awake()
        {
            if (tableView == null)
            {
                tableView = GetComponent<TableView>();
            }

            if (pointerRouter == null)
            {
                pointerRouter = GetComponent<PointerRouter>();
            }

            feedbackSink = feedbackSinkBehaviour as IFeedbackSink ?? new NoOpFeedbackSink();
            setMarkers.Broken += HandleMarkedSetBroken;
        }

        private void OnEnable()
        {
            if (pointerRouter != null)
            {
                pointerRouter.Gesture += HandlePointerGesture;
            }

            if (tableView != null)
            {
                tableView.DealCompleted += HandleDealCompleted;
            }
        }

        private void OnDisable()
        {
            SetHoveredCard(null);
            ResetEdgePanDrag();
            if (pointerRouter != null)
            {
                pointerRouter.Gesture -= HandlePointerGesture;
            }

            if (tableView != null)
            {
                tableView.DealCompleted -= HandleDealCompleted;
            }
        }

        private void Update()
        {
            UpdateEdgePan();
            var mouse = Mouse.current;
            if (!GameSettings.CardHoverHighlight || tableView == null || mouse == null ||
                mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed ||
                dragActive || handScrollActive || panActive || meldReorderActive || sweepSelectActive)
            {
                SetHoveredCard(null);
                return;
            }

            var position = mouse.position.ReadValue();
            if (UiPointerBlocker.IsOverUi(position))
            {
                SetHoveredCard(null);
                return;
            }

            var hit = tableView.HitTest(position);
            SetHoveredCard(hit.Kind == GestureHitKind.HandCard || hit.Kind == GestureHitKind.BoardCard
                ? hit.CardId
                : (byte?)null);
        }

        private void UpdateEdgePan()
        {
            if (!dragActive || sweepSelectActive || (!InputEnabled && !HandInputEnabled) || !edgePanArmed ||
                tableView == null || GameSettings.EdgePanWidth <= 0f)
            {
                return;
            }

            var direction = PointerRouter.GetEdgePanDirection(lastDragPosition, Screen.width, Screen.height,
                GameSettings.EdgePanWidth, tableView.EdgePanBottomInset * Screen.height);
            if (direction.sqrMagnitude <= 0f)
            {
                return;
            }

            var screenDelta = -direction * (PointerRouter.EdgePanSpeedScreensPerSecond * GameSettings.EdgePanSpeed *
                                            Mathf.Min(Screen.width, Screen.height) * Time.unscaledDeltaTime);
            tableView.PanCamera(screenDelta, lastDragPosition, false);
            if (dragSource.Kind == GestureHitKind.BoardCard)
            {
                tableView.UpdateBoardLift(lastDragPosition);
            }
            else if (dragSource.Kind == GestureHitKind.HandCard)
            {
                tableView.UpdateHandLift(lastDragPosition);
            }

            UpdateDropPreview(lastDragPosition, lastDragIsLongPress);
        }

        private void TrackEdgePanGesture(PointerGesture pointerGesture)
        {
            switch (pointerGesture.Kind)
            {
                case PointerGestureKind.DragStarted:
                    lastDragPosition = pointerGesture.Position;
                    lastDragIsLongPress = pointerGesture.IsLongPress;
                    edgePanArmed = !IsInDragEdgePanZone(pointerGesture.StartPosition) ||
                                   !IsInDragEdgePanZone(pointerGesture.Position);
                    break;
                case PointerGestureKind.DragUpdated:
                    lastDragPosition = pointerGesture.Position;
                    lastDragIsLongPress = pointerGesture.IsLongPress;
                    if (!edgePanArmed && !IsInDragEdgePanZone(pointerGesture.Position))
                    {
                        edgePanArmed = true;
                    }

                    break;
                case PointerGestureKind.DragEnded:
                case PointerGestureKind.Cancelled:
                    ResetEdgePanDrag();
                    break;
            }
        }

        private bool IsInDragEdgePanZone(Vector2 position)
        {
            var bottomInset = tableView == null ? 0f : tableView.EdgePanBottomInset * Screen.height;
            return PointerRouter.GetEdgePanDirection(position, Screen.width, Screen.height,
                GameSettings.EdgePanWidth, bottomInset).sqrMagnitude > 0f;
        }

        private void ResetEdgePanDrag()
        {
            lastDragPosition = Vector2.zero;
            lastDragIsLongPress = false;
            edgePanArmed = false;
        }

        private void SetHoveredCard(byte? cardId)
        {
            if (hoveredCardId == cardId)
            {
                return;
            }

            if (hoveredCardId.HasValue)
            {
                tableView?.GetCardView(hoveredCardId.Value)?.SetHovered(false);
            }

            hoveredCardId = cardId;
            if (hoveredCardId.HasValue)
            {
                tableView?.GetCardView(hoveredCardId.Value)?.SetHovered(true);
            }
        }

        private void HandleDealCompleted()
        {
            SynchronizeViews();
        }

        private void OnDestroy()
        {
            UnsubscribeFromGame();
            setMarkers.Broken -= HandleMarkedSetBroken;
        }

        public void Initialize(MejnunGame activeGame)
        {
            if (game != activeGame)
            {
                UnsubscribeFromGame();
                game = activeGame;
                setMarkers.Clear();
                autoFixing = false;
                SubscribeToGame();
            }

            undoGroups.Clear();
            redoGroups.Clear();
            gestureInterpreter.ClearSelection();
            SynchronizeViews();
        }

        /// <summary>Decides which seats take local input (bots don't). Null means every seat does.</summary>
        public void SetSeatInputFilter(Func<int, bool> filter)
        {
            seatInputFilter = filter;
            RefreshHints();
            RefreshSelection();
            if (game != null)
            {
                SynchronizeViews();
            }
        }

        /// <summary>Filters every command before it reaches the engine (the tutorial uses this). Null allows all.</summary>
        public void SetCommandGate(ICommandGate gate)
        {
            commandGate = gate;
        }

        public void SetCommandRelay(ICommandRelay relay)
        {
            commandRelay = relay;
            if (game != null)
            {
                SynchronizeViews();
            }
        }

        public void Resync(MejnunGame replacement)
        {
            if (replacement == null)
            {
                throw new ArgumentNullException(nameof(replacement));
            }

            if (lastSynchronizedTurn != replacement.State.TurnNumber ||
                lastSynchronizedSeat != replacement.State.CurrentSeat)
            {
                undoGroups.Clear();
                redoGroups.Clear();
            }

            if (game != replacement)
            {
                UnsubscribeFromGame();
                game = replacement;
                SubscribeToGame();
            }

            gestureInterpreter.ClearSelection();
            SynchronizeViews();
        }

        /// <summary>
        /// Pulses the given cards, glows the target set or empty-table slot, and dims every other card.
        /// Passing no cards and no target clears the focus.
        /// </summary>
        public void SetTutorialFocus(IReadOnlyCollection<byte> cardIds, int? targetMeldId, bool emptyTarget)
        {
            tutorialFocusIds.Clear();
            if (cardIds != null)
            {
                tutorialFocusIds.UnionWith(cardIds);
            }

            tutorialTargetMeldId = targetMeldId ?? -1;
            tutorialEmptyTarget = emptyTarget;
            tutorialFocusActive = tutorialFocusIds.Count > 0 || tutorialTargetMeldId >= 0 || emptyTarget;
            if (tableView != null)
            {
                tableView.SetTutorialTarget(tutorialTargetMeldId, tutorialEmptyTarget);
            }

            RefreshSelection();
        }

        public void ClearTutorialFocus()
        {
            SetTutorialFocus(null, null, false);
        }

        public void SetInputPaused(bool paused)
        {
            if (inputPaused == paused)
            {
                return;
            }

            inputPaused = paused;
            SynchronizeViews();
        }

        public void SetMarkMode(bool active)
        {
            if (markModeActive == active)
            {
                return;
            }

            markModeActive = active;
            ClearLastTapEffect();
            MarkModeChanged?.Invoke(markModeActive);
        }

        public void CycleMarkedSet(int meldId)
        {
            if (game != null)
            {
                setMarkers.Cycle(meldId, game.State.Board);
                FeedbackCues.Raise(FeedbackCue.Mark);
            }
        }

        public void CycleMarkedCard(byte cardId)
        {
            setMarkers.CycleCard(cardId);
            FeedbackCues.Raise(FeedbackCue.Mark);
        }

        public bool RemoveMarkedSet(int meldId)
        {
            return setMarkers.Remove(meldId);
        }

        public bool RemoveMarkedCard(byte cardId)
        {
            return setMarkers.RemoveCard(cardId);
        }

        public void ClearSelection()
        {
            gestureInterpreter.ClearSelection();
            RefreshSelection();
        }

        public bool CancelActiveGesture()
        {
            var wasActive = dragActive || handScrollActive || panActive || meldReorderActive;
            dragActive = false;
            handScrollActive = false;
            panActive = false;
            meldReorderActive = false;
            if (tableView != null)
            {
                tableView.EndHandLift();
                tableView.EndMeldLift();
                tableView.EndBoardLift();
                SynchronizeViews();
                tableView.ClearDropPreview();
            }

            return wasActive;
        }

        public void SetHintsEnabled(bool enabled)
        {
            if (hintsEnabled == enabled)
            {
                return;
            }

            hintsEnabled = enabled;
            RefreshHints();
            RefreshSelection();
            if (game != null)
            {
                Synchronized?.Invoke(game.State, game.Turn);
            }
        }

        public void RefreshHintOptions()
        {
            RefreshHints();
            RefreshSelection();
        }

        public CommandResult ExecuteGesture(GestureInput input)
        {
            if (game == null)
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            var interpretation = gestureInterpreter.Interpret(input, game.State, game.Turn);
            if (interpretation.FitBoardRequested)
            {
                SetMarkMode(false);
                if (tableView != null)
                {
                    tableView.FitBoard();
                }

                return CommandResult.Success();
            }

            if (interpretation.Denied)
            {
                NotifyInvalid(RuleError.BoardCardToHand);
                return CommandResult.Failure(RuleError.BoardCardToHand);
            }

            if (interpretation.HandOrderChanged)
            {
                if (tableView != null)
                {
                    var reorderedCards = interpretation.ReorderedCardIds ??
                                         new[] { interpretation.ReorderedCardId };
                    if (interpretation.ReorderTargetIsSlot)
                    {
                        tableView.HandOrder.MoveGroupToSlot(game.State.CurrentSeat, reorderedCards,
                            interpretation.ReorderTargetIndex);
                    }
                    else
                    {
                        tableView.HandOrder.ReorderGroup(game.State.CurrentSeat, reorderedCards,
                            interpretation.ReorderTargetIndex);
                    }
                    SynchronizeViews();
                }

                return CommandResult.Success();
            }

            if (interpretation.Batch.Count == 0)
            {
                gestureInterpreter.Complete(interpretation, true);
                if (interpretation.SelectionChanged)
                {
                    Haptics.Pickup();
                }

                RefreshSelection();
                return CommandResult.Success();
            }

            var validMeldCardCounts = ValidMeldCardCounts(game.State);
            var result = ExecuteBatch(interpretation.Batch);
            if (result.Ok && TryClassifyGoodMove(game.State, validMeldCardCounts, out var goodMoveKind,
                    out var meldId))
            {
                GoodMove?.Invoke(goodMoveKind, meldId);
            }

            gestureInterpreter.Complete(interpretation, result.Ok);
            RefreshSelection();
            if (result.Ok)
            {
                SetMarkMode(false);
                Haptics.Drop();
            }
            return result;
        }

        private static Dictionary<int, int> ValidMeldCardCounts(GameState state)
        {
            var validMeldCardCounts = new Dictionary<int, int>();
            for (var index = 0; index < state.Board.Count; index++)
            {
                var meld = state.Board[index];
                if (MeldValidator.Classify(meld.Cards).IsValid)
                {
                    validMeldCardCounts[meld.Id] = meld.Cards.Count;
                }
            }

            return validMeldCardCounts;
        }

        private static bool TryClassifyGoodMove(GameState state, Dictionary<int, int> previousValidMelds,
            out GoodMoveKind kind, out int meldId)
        {
            for (var index = 0; index < state.Board.Count; index++)
            {
                var meld = state.Board[index];
                if (!MeldValidator.Classify(meld.Cards).IsValid)
                {
                    continue;
                }

                if (!previousValidMelds.TryGetValue(meld.Id, out var previousCardCount))
                {
                    kind = GoodMoveKind.NewSet;
                    meldId = meld.Id;
                    return true;
                }

                if (meld.Cards.Count > previousCardCount)
                {
                    kind = GoodMoveKind.CardAdded;
                    meldId = meld.Id;
                    return true;
                }
            }

            kind = default;
            meldId = -1;
            return false;
        }

        public CommandResult ExecuteHandGesture(GestureInput input)
        {
            if (game == null || tableView == null || !HandInputEnabled)
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            var seat = HandSeat;
            var interpretation = gestureInterpreter.InterpretHandGesture(input, game.State.Hands[seat].Count);
            if (interpretation.HandOrderChanged)
            {
                var reorderedCards = interpretation.ReorderedCardIds ??
                                     new[] { interpretation.ReorderedCardId };
                if (interpretation.ReorderTargetIsSlot)
                {
                    tableView.HandOrder.MoveGroupToSlot(seat, reorderedCards, interpretation.ReorderTargetIndex);
                }
                else
                {
                    tableView.HandOrder.ReorderGroup(seat, reorderedCards, interpretation.ReorderTargetIndex);
                }
            }

            SynchronizeViews();
            if (interpretation.SelectionChanged)
            {
                Haptics.Pickup();
            }

            RefreshSelection();
            return CommandResult.Success();
        }

        public CommandResult ExecuteBatch(CommandBatch batch)
        {
            return ExecuteBatch(batch, CommandOrigin.Gesture);
        }

        private CommandResult ExecuteBatch(CommandBatch batch, CommandOrigin origin)
        {
            if (game == null)
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            if (IsRemoteTurnBlocked())
            {
                NotifyInvalid(RuleError.NotYourTurn);
                return CommandResult.Failure(RuleError.NotYourTurn);
            }

            if (batch == null)
            {
                return CommandResult.Failure(RuleError.EmptyMove);
            }

            for (var index = 0; index < batch.Count; index++)
            {
                if (!GateAllows(batch.Commands[index], origin))
                {
                    return Blocked();
                }
            }

            var result = ApplyBatch(batch);
            if (!result.Ok)
            {
                NotifyInvalid(result.Error);
            }

            SynchronizeViews();
            return result;
        }

        public CommandResult ApplyCommand(GameCommand command)
        {
            return ApplyCommand(command, CommandOrigin.Command);
        }

        private CommandResult ApplyCommand(GameCommand command, CommandOrigin origin)
        {
            if (game == null)
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            if (IsRemoteTurnBlocked())
            {
                NotifyInvalid(RuleError.NotYourTurn);
                return CommandResult.Failure(RuleError.NotYourTurn);
            }

            if (!GateAllows(command, origin))
            {
                return Blocked();
            }

            var isRelayed = commandRelay != null && commandRelay.IsRemoteAuthority;
            if (isRelayed && command.Type != CommandType.ChooseRandomDealer &&
                !commandRelay.CanControlSeat(command.Seat))
            {
                NotifyInvalid(RuleError.NotYourTurn);
                return CommandResult.Failure(RuleError.NotYourTurn);
            }

            if (isRelayed && (!commandRelay.ApplyCommandsLocally || IsRemoteOnly(command)))
            {
                if (!commandRelay.Submit(new[] { command }))
                {
                    return CommandResult.Failure(RuleError.WrongPhase);
                }

                if (!commandRelay.ApplyCommandsLocally)
                {
                    TrackCommand(command);
                    SynchronizeViews();
                }

                return CommandResult.Success();
            }

            var result = game.Apply(command);
            if (!result.Ok)
            {
                return result;
            }

            if (isRelayed && !commandRelay.Submit(new[] { command }))
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            TrackCommand(command);
            SynchronizeViews();
            return result;
        }

        private void TrackCommand(GameCommand command)
        {
            if (command.Type == CommandType.Revert)
            {
                undoGroups.Clear();
                redoGroups.Clear();
                gestureInterpreter.ClearSelection();
            }
            else if (command.Type == CommandType.EndTurn || command.Type == CommandType.DrawOrPass ||
                     command.Type == CommandType.TimeoutTurn)
            {
                undoGroups.Clear();
                redoGroups.Clear();
                gestureInterpreter.ClearSelection();
            }
            else if (command.Type == CommandType.MoveCards || command.Type == CommandType.SplitMeld ||
                     command.Type == CommandType.MergeMelds || command.Type == CommandType.GoDown)
            {
                redoGroups.Clear();
                undoGroups.Push(1);
            }
        }

        private static bool IsRemoteOnly(GameCommand command)
        {
            return command.Type == CommandType.DrawOrPass || command.Type == CommandType.TimeoutTurn ||
                   command.Type == CommandType.EndTurn;
        }

        public CommandResult UndoGesture()
        {
            SetMarkMode(false);
            if (game == null || undoGroups.Count == 0)
            {
                return CommandResult.Failure(RuleError.NothingToUndo);
            }

            if (!GateAllows(GameCommand.Undo(game.State.CurrentSeat), CommandOrigin.Undo))
            {
                return Blocked();
            }

            var commandCount = undoGroups.Pop();
            var moves = game.Turn.Moves;
            var firstIndex = moves.Count - commandCount;
            if (firstIndex < 0)
            {
                undoGroups.Push(commandCount);
                return CommandResult.Failure(RuleError.NothingToUndo);
            }

            var redoCommands = new List<GameCommand>(commandCount);
            for (var index = firstIndex; index < moves.Count; index++)
            {
                redoCommands.Add(moves[index].Command);
            }

            var undoCommands = new GameCommand[commandCount];
            for (var index = 0; index < undoCommands.Length; index++)
            {
                undoCommands[index] = GameCommand.Undo(game.State.CurrentSeat);
            }

            if (commandRelay != null && commandRelay.IsRemoteAuthority && !commandRelay.ApplyCommandsLocally)
            {
                if (!commandRelay.Submit(undoCommands))
                {
                    undoGroups.Push(commandCount);
                    return CommandResult.Failure(RuleError.WrongPhase);
                }

                gestureInterpreter.ClearSelection();
                redoGroups.Push(redoCommands);
                SynchronizeViews();
                AssistUsed?.Invoke(TableAssistKind.Undo);
                return CommandResult.Success();
            }

            var undoneCount = 0;
            for (; undoneCount < commandCount; undoneCount++)
            {
                var result = game.Apply(GameCommand.Undo(game.State.CurrentSeat));
                if (!result.Ok)
                {
                    if (commandCount - undoneCount > 0)
                    {
                        undoGroups.Push(commandCount - undoneCount);
                    }

                    SynchronizeViews();
                    return result;
                }
            }

            if (commandRelay != null && commandRelay.IsRemoteAuthority && !commandRelay.Submit(undoCommands))
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            gestureInterpreter.ClearSelection();
            redoGroups.Push(redoCommands);
            SynchronizeViews();
            AssistUsed?.Invoke(TableAssistKind.Undo);
            return CommandResult.Success();
        }

        public CommandResult RedoGesture()
        {
            SetMarkMode(false);
            if (game == null || redoGroups.Count == 0)
            {
                return CommandResult.Failure(RuleError.NothingToUndo);
            }

            var pending = redoGroups.Peek();
            for (var index = 0; index < pending.Count; index++)
            {
                if (!GateAllows(pending[index], CommandOrigin.Redo))
                {
                    return Blocked();
                }
            }

            var commands = redoGroups.Pop();
            if (commandRelay != null && commandRelay.IsRemoteAuthority && !commandRelay.ApplyCommandsLocally)
            {
                if (!commandRelay.Submit(commands))
                {
                    redoGroups.Push(commands);
                    return CommandResult.Failure(RuleError.WrongPhase);
                }

                undoGroups.Push(commands.Count);
                gestureInterpreter.ClearSelection();
                SynchronizeViews();
                return CommandResult.Success();
            }

            var applied = 0;
            for (; applied < commands.Count; applied++)
            {
                var result = game.Apply(commands[applied]);
                if (result.Ok)
                {
                    continue;
                }

                while (applied > 0)
                {
                    game.Apply(GameCommand.Undo(game.State.CurrentSeat));
                    applied--;
                }

                redoGroups.Clear();
                SynchronizeViews();
                return result;
            }

            if (commandRelay != null && commandRelay.IsRemoteAuthority && !commandRelay.Submit(commands))
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            undoGroups.Push(commands.Count);
            gestureInterpreter.ClearSelection();
            SynchronizeViews();
            return CommandResult.Success();
        }

        public CommandResult RevertTurn()
        {
            SetMarkMode(false);
            if (game == null)
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            var result = ApplyCommand(GameCommand.Revert(game.State.CurrentSeat));
            if (result.Ok)
            {
                AssistUsed?.Invoke(TableAssistKind.Revert);
            }

            return result;
        }

        public CommandResult FixBoard()
        {
            SetMarkMode(false);
            if (game == null)
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            var commands = BoardFixer.Plan(game, hintsEnabled);
            if (commands.Count == 0)
            {
                return CommandResult.Success();
            }

            if (commands.Count == 1 && commands[0].Type == CommandType.Revert)
            {
                var result = ApplyCommand(commands[0], CommandOrigin.FixBoard);
                if (result.Ok)
                {
                    AssistUsed?.Invoke(TableAssistKind.FixBoard);
                }

                return result;
            }

            var batch = new CommandBatch();
            for (var index = 0; index < commands.Count; index++)
            {
                batch.Add(commands[index]);
            }

            var batchResult = ExecuteBatch(batch, CommandOrigin.FixBoard);
            if (batchResult.Ok)
            {
                AssistUsed?.Invoke(TableAssistKind.FixBoard);
            }

            return batchResult;
        }

        public bool FixBoardWouldReturnCards()
        {
            if (game == null)
            {
                return false;
            }

            var commands = BoardFixer.Plan(game, hintsEnabled);
            for (var index = 0; index < commands.Count; index++)
            {
                if (commands[index].Type == CommandType.Revert)
                {
                    return HasPlacedHandCardOnBoard();
                }

                if (commands[index].Type == CommandType.MoveCards && commands[index].Target.Kind == TargetKind.Hand)
                {
                    return true;
                }
            }

            return false;
        }

        public CommandResult TimeoutTurn()
        {
            SetMarkMode(false);
            if (game == null)
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            if (commandRelay != null && commandRelay.IsRemoteAuthority)
            {
                return ApplyCommand(GameCommand.TimeoutTurn(game.State.CurrentSeat));
            }

            var undoCount = GetTimeoutUndoCount();
            for (var index = 0; index < undoCount; index++)
            {
                var undoResult = game.Apply(GameCommand.Undo(game.State.CurrentSeat));
                if (!undoResult.Ok)
                {
                    return undoResult;
                }
            }

            return ApplyCommand(GameCommand.TimeoutTurn(game.State.CurrentSeat));
        }

        public int GetTimeoutUndoCount()
        {
            if (game == null || game.State.Phase != GamePhase.Playing ||
                MeldValidator.IsBoardValid(game.State.Board) || game.Turn == null)
            {
                return 0;
            }

            var scratch = SnapshotSerializer.Read(SnapshotSerializer.Write(game));
            var seat = scratch.State.CurrentSeat;
            var undoCount = 0;
            while (scratch.State.Phase == GamePhase.Playing &&
                   !MeldValidator.IsBoardValid(scratch.State.Board))
            {
                var result = scratch.Apply(GameCommand.Undo(seat));
                if (!result.Ok)
                {
                    break;
                }

                undoCount++;
            }

            return undoCount;
        }

        public IEnumerator BeginTimeoutFix()
        {
            if (game == null)
            {
                yield break;
            }

            if (commandRelay != null && commandRelay.IsRemoteAuthority)
            {
                TimeoutTurn();
                yield break;
            }

            var activeGame = game;
            var seat = activeGame.State.CurrentSeat;
            var turnNumber = activeGame.State.TurnNumber;
            var undoCount = GetTimeoutUndoCount();
            if (undoCount == 0)
            {
                TimeoutTurn();
                yield break;
            }

            autoFixing = true;
            RefreshSelection();
            for (var index = 0; index < undoCount; index++)
            {
                var waited = 0f;
                var delay = Mathf.Max(0.6f, timeoutFixStepSeconds) * GameSettings.CpuDelayMultiplier;
                while (inputPaused || (!GameSettings.FastForward && waited < delay))
                {
                    if (!IsTimeoutTurn(activeGame, seat, turnNumber))
                    {
                        autoFixing = false;
                        SynchronizeViews();
                        yield break;
                    }

                    if (!inputPaused)
                    {
                        waited += Time.deltaTime;
                    }

                    yield return null;
                }

                if (!IsTimeoutTurn(activeGame, seat, turnNumber))
                {
                    autoFixing = false;
                    SynchronizeViews();
                    yield break;
                }

                var result = activeGame.Apply(GameCommand.Undo(seat));
                if (!result.Ok)
                {
                    autoFixing = false;
                    SynchronizeViews();
                    yield break;
                }

                undoGroups.Clear();
                redoGroups.Clear();
                SynchronizeViews();
            }

            if (IsTimeoutTurn(activeGame, seat, turnNumber))
            {
                ApplyCommand(GameCommand.TimeoutTurn(seat));
            }

            autoFixing = false;
            SynchronizeViews();
        }

        private bool HasPlacedHandCardOnBoard()
        {
            if (game == null || game.Turn == null)
            {
                return false;
            }

            var startHand = new HashSet<byte>();
            for (var index = 0; index < game.Turn.StartSnapshot.Hand.Count; index++)
            {
                startHand.Add(game.Turn.StartSnapshot.Hand[index].Id);
            }

            for (var meldIndex = 0; meldIndex < game.State.Board.Count; meldIndex++)
            {
                var cards = game.State.Board[meldIndex].Cards;
                for (var cardIndex = 0; cardIndex < cards.Count; cardIndex++)
                {
                    if (startHand.Contains(cards[cardIndex].Id))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool GateAllows(in GameCommand command, CommandOrigin origin)
        {
            return commandGate == null || commandGate.Allows(command, origin, game.State);
        }

        private CommandResult Blocked()
        {
            NotifyInvalid(RuleError.TutorialBlocked);
            SynchronizeViews();
            return CommandResult.Failure(RuleError.TutorialBlocked);
        }

        private bool IsRemoteTurnBlocked()
        {
            return game != null && game.State.Phase == GamePhase.Playing && commandRelay != null &&
                   commandRelay.IsRemoteAuthority &&
                   !commandRelay.CanControlSeat(game.State.CurrentSeat);
        }

        private CommandResult ApplyBatch(CommandBatch batch)
        {
            if (commandRelay != null && commandRelay.IsRemoteAuthority && !commandRelay.ApplyCommandsLocally)
            {
                if (!commandRelay.Submit(batch.Commands))
                {
                    return CommandResult.Failure(RuleError.WrongPhase);
                }

                if (batch.Count > 0)
                {
                    redoGroups.Clear();
                    undoGroups.Push(batch.Count);
                }

                return CommandResult.Success();
            }

            var appliedCount = 0;
            for (var index = 0; index < batch.Count; index++)
            {
                var result = game.Apply(batch.Commands[index]);
                if (!result.Ok)
                {
                    RollBack(appliedCount);
                    return result;
                }

                appliedCount++;
            }

            if (appliedCount > 0)
            {
                if (commandRelay != null && commandRelay.IsRemoteAuthority &&
                    !commandRelay.Submit(batch.Commands))
                {
                    RollBack(appliedCount);
                    return CommandResult.Failure(RuleError.WrongPhase);
                }

                redoGroups.Clear();
                undoGroups.Push(appliedCount);
            }

            return CommandResult.Success();
        }

        private void RollBack(int appliedCount)
        {
            for (var index = 0; index < appliedCount; index++)
            {
                var result = game.Apply(GameCommand.Undo(game.State.CurrentSeat));
                if (!result.Ok)
                {
                    Debug.LogError("A failed gesture could not be fully rolled back: " + result.Error, this);
                    break;
                }
            }
        }

        private void SynchronizeViews()
        {
            if (game == null)
            {
                return;
            }

            lastSynchronizedTurn = game.State.TurnNumber;
            lastSynchronizedSeat = game.State.CurrentSeat;
            setMarkers.Update(game.State.Board);

            if (tableView != null)
            {
                tableView.Sync(game.State, game.Turn);
                RefreshHints();
                RefreshSelection();
            }

            Synchronized?.Invoke(game.State, game.Turn);
        }

        private void RefreshHints()
        {
            hintedCardIds = hintsEnabled && InputEnabled ? PlayableHints.Find(game.State) : new HashSet<byte>();
            if (hintsEnabled && InputEnabled && GameSettings.HintBoardMoves)
            {
                hintedCardIds.UnionWith(PlayableHints.FindBoardMoves(game.State));
            }
        }

        private void RefreshSelection()
        {
            if (tableView == null)
            {
                return;
            }

            tutorialTargetIds.Clear();
            if (tutorialFocusActive && tutorialTargetMeldId >= 0 && game != null)
            {
                for (var meldIndex = 0; meldIndex < game.State.Board.Count; meldIndex++)
                {
                    var meld = game.State.Board[meldIndex];
                    if (meld.Id != tutorialTargetMeldId)
                    {
                        continue;
                    }

                    for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                    {
                        tutorialTargetIds.Add(meld.Cards[cardIndex].Id);
                    }
                }
            }

            foreach (var pair in tableView.ActiveCards)
            {
                pair.Value.SetSelected(gestureInterpreter.IsSelected(pair.Key));
                pair.Value.SetHovered(hoveredCardId == pair.Key);
                pair.Value.SetHinted(hintedCardIds.Contains(pair.Key));
                var focused = tutorialFocusActive && tutorialFocusIds.Contains(pair.Key);
                pair.Value.SetTutorialFocus(focused);
                pair.Value.SetDimmed(tutorialFocusActive && !focused && !tutorialTargetIds.Contains(pair.Key));
            }
        }

        private void HandleLongPress(Vector2 position, bool playCue)
        {
            var cardIds = gestureInterpreter.GetLongPressCardIds(tableView.HitTest(position), game.State);
            if (cardIds.Length == 0)
            {
                return;
            }

            if (playCue)
            {
                Haptics.LongPress();
            }

            for (var index = 0; index < cardIds.Length; index++)
            {
                var cardView = tableView.GetCardView(cardIds[index]);
                if (cardView != null)
                {
                    cardView.SetSelected(true);
                }
            }
        }

        private void BeginBoardHoldLift(GestureHit source, Vector2 position, Vector2 startPosition)
        {
            if (PointerRouter.IsInEdgePanZone(startPosition, Screen.width, Screen.height, GameSettings.EdgePanWidth))
            {
                return;
            }

            dragSource = source;
            dragActive = true;
            boardHoldLift = true;
            boardTailLifted = false;
            boardHoldStartedAt = Time.unscaledTime;
            boardTailCardIds.Clear();
            var selectedBoardCardIds = gestureInterpreter.IsBoardSelection &&
                                        gestureInterpreter.IsSelected(source.CardId)
                ? gestureInterpreter.GetSelectedBoardCardIds(source, game.State)
                : new byte[0];
            var liftSelection = selectedBoardCardIds.Length > 1;
            if (!liftSelection && gestureInterpreter.IsBoardSelection)
            {
                gestureInterpreter.ClearSelection();
                RefreshSelection();
            }

            if (!liftSelection)
            {
                var tailCardIds = gestureInterpreter.GetLongPressCardIds(source, game.State);
                for (var index = 0; index < tailCardIds.Length; index++)
                {
                    if (tailCardIds[index] != source.CardId)
                    {
                        boardTailCardIds.Add(tailCardIds[index]);
                    }
                }
            }

            var liftCardIds = liftSelection ? selectedBoardCardIds : new[] { source.CardId };
            tableView.BeginBoardLift(source.CardId, liftCardIds, position);
            Haptics.Pickup();
        }

        private void UpdateBoardTailPickupCharge()
        {
            if (!boardHoldLift || boardTailLifted || boardTailCardIds.Count == 0)
            {
                return;
            }

            var heldAfterLongPress = Time.unscaledTime - boardHoldStartedAt;
            for (var index = 0; index < boardTailCardIds.Count; index++)
            {
                var delay = PointerRouter.TailHighlightDelaySeconds +
                            index * BoardTailHighlightStaggerSeconds;
                var progress = PointerRouter.GetTailHighlightProgress(heldAfterLongPress, delay,
                    PointerRouter.TailHoldSeconds);
                tableView.GetCardView(boardTailCardIds[index])?.SetPickupCharge(progress);
            }
        }

        private void CompleteBoardTailHold(Vector2 position)
        {
            if (!boardHoldLift || boardTailLifted || boardTailCardIds.Count == 0)
            {
                return;
            }

            var liftCardIds = gestureInterpreter.GetLongPressCardIds(dragSource, game.State);
            tableView.BeginBoardLift(dragSource.CardId, liftCardIds, position);
            boardTailLifted = true;
            Haptics.Pickup();
            ClearBoardPickupCharge();
        }

        private void ClearBoardPickupCharge()
        {
            for (var index = 0; index < boardTailCardIds.Count; index++)
            {
                tableView.GetCardView(boardTailCardIds[index])?.SetPickupCharge(0f);
            }
        }

        private void ResetBoardHoldLift()
        {
            ClearBoardPickupCharge();
            boardTailCardIds.Clear();
            boardHoldLift = false;
            boardTailLifted = false;
            boardHoldStartedAt = 0f;
        }

        private void UpdateSweepSelection(Vector2 position)
        {
            var hit = tableView.HitTest(position);
            if (hit.Kind != GestureHitKind.HandCard || HandSeat < 0)
            {
                return;
            }

            var previousSelection = new HashSet<byte>(gestureInterpreter.SelectedCardIds);
            gestureInterpreter.SelectHandRange(tableView.HandOrder.GetCardIds(HandSeat), dragSource.Index, hit.Index);
            var selectedCardIds = gestureInterpreter.SelectedCardIds;
            for (var index = 0; index < selectedCardIds.Count; index++)
            {
                if (selectedCardIds[index] != dragSource.CardId && !previousSelection.Contains(selectedCardIds[index]))
                {
                    Haptics.Pickup();
                }
            }

            RefreshSelection();
        }

        private void HandlePointerGesture(PointerGesture pointerGesture)
        {
            if (game == null || tableView == null)
            {
                return;
            }

            TrackEdgePanGesture(pointerGesture);

            if (pointerGesture.Kind == PointerGestureKind.Tap)
            {
                ClearLastTapEffect();
            }
            else if (pointerGesture.Kind != PointerGestureKind.DoubleTap)
            {
                ClearLastTapEffect();
            }

            if (pointerGesture.Kind == PointerGestureKind.DoubleTap &&
                lastTapEffect == TapEffect.SuccessfulMove)
            {
                ClearLastTapEffect();
                return;
            }

            var dragHit = GestureHit.None;
            if (pointerGesture.Kind == PointerGestureKind.DragStarted)
            {
                dragHit = tableView.HitTest(pointerGesture.StartPosition, true);
                if (PointerRouter.IsInEdgePanZone(pointerGesture.StartPosition, Screen.width, Screen.height,
                        GameSettings.EdgePanWidth) &&
                    (dragHit.Kind == GestureHitKind.BoardCard || dragHit.Kind == GestureHitKind.MeldGrip ||
                     dragHit.Kind == GestureHitKind.MeldGap))
                {
                    dragHit = GestureHit.EmptyBoard();
                }

                var isSelectedBoardCard = dragHit.Kind == GestureHitKind.BoardCard &&
                                          gestureInterpreter.IsBoardSelection &&
                                          gestureInterpreter.IsSelected(dragHit.CardId);
                if (PointerRouter.ShouldPanBoardDrag(pointerGesture.IsTouch, pointerGesture.IsLongPress,
                        isSelectedBoardCard) &&
                    (dragHit.Kind == GestureHitKind.BoardCard || dragHit.Kind == GestureHitKind.MeldGrip))
                {
                    dragHit = GestureHit.EmptyBoard();
                }
            }

            if (boardHoldLift && !InputEnabled)
            {
                dragActive = false;
                handScrollActive = false;
                meldReorderActive = false;
                sweepSelectActive = false;
                panActive = false;
                ResetEdgePanDrag();
                tableView.EndBoardLift();
                ResetBoardHoldLift();
                SynchronizeViews();
                return;
            }

            if (IsRemoteTurnBlocked() && (pointerGesture.Kind == PointerGestureKind.Tap ||
                                          pointerGesture.Kind == PointerGestureKind.DoubleTap))
            {
                if (pointerGesture.Kind == PointerGestureKind.DoubleTap &&
                    lastTapEffect == TapEffect.RemoteTurnBlocked)
                {
                    ClearLastTapEffect();
                    return;
                }

                var blockedHit = tableView.HitTest(pointerGesture.Position);
                if (blockedHit.Kind != GestureHitKind.HandCard && blockedHit.Kind != GestureHitKind.HandArea)
                {
                    if (pointerGesture.Kind == PointerGestureKind.Tap)
                    {
                        lastTapEffect = TapEffect.RemoteTurnBlocked;
                    }

                    NotifyInvalid(RuleError.NotYourTurn);
                    return;
                }
            }

            if (IsRemoteTurnBlocked() && pointerGesture.Kind == PointerGestureKind.DragStarted)
            {
                var blockedHit = dragHit;
                if (blockedHit.Kind == GestureHitKind.BoardCard || blockedHit.Kind == GestureHitKind.MeldGrip)
                {
                    ResetEdgePanDrag();
                    NotifyInvalid(RuleError.NotYourTurn);
                    return;
                }
            }

            if (markModeActive && (pointerGesture.Kind == PointerGestureKind.LongPress ||
                                   pointerGesture.Kind == PointerGestureKind.Tap ||
                                   pointerGesture.Kind == PointerGestureKind.DoubleTap))
            {
                var markHit = tableView.HitTest(pointerGesture.Position);
                if (pointerGesture.Kind == PointerGestureKind.LongPress)
                {
                    var removed = false;
                    if (markHit.Kind == GestureHitKind.HandCard)
                    {
                        removed = RemoveMarkedCard(markHit.CardId);
                    }
                    else if (markHit.Kind == GestureHitKind.BoardCard)
                    {
                        removed = RemoveMarkedCard(markHit.CardId) || RemoveMarkedSet(markHit.MeldId);
                    }
                    else if (markHit.Kind == GestureHitKind.MeldGap || markHit.Kind == GestureHitKind.MeldGrip)
                    {
                        removed = RemoveMarkedSet(markHit.MeldId);
                    }

                    if (removed)
                    {
                        Haptics.LongPress();
                    }
                }
                else if (markHit.Kind == GestureHitKind.HandCard)
                {
                    CycleMarkedCard(markHit.CardId);
                }
                else if (markHit.Kind == GestureHitKind.BoardCard)
                {
                    CycleMarkedCard(markHit.CardId);
                }
                else if (markHit.Kind == GestureHitKind.MeldGap || markHit.Kind == GestureHitKind.MeldGrip)
                {
                    CycleMarkedSet(markHit.MeldId);
                }

                if (pointerGesture.Kind == PointerGestureKind.DoubleTap)
                {
                    ClearLastTapEffect();
                }

                return;
            }

            if (pointerGesture.Kind == PointerGestureKind.DragStarted)
            {
                SetMarkMode(false);
                if (boardHoldLift && dragSource.Kind == GestureHitKind.BoardCard)
                {
                    ClearBoardPickupCharge();
                }
                else
                {
                    dragSource = dragHit;
                    sweepSelectActive = dragSource.Kind == GestureHitKind.HandCard && pointerGesture.IsLongPress &&
                                        (InputEnabled || HandInputEnabled);
                    var boardDragWhileDisabled = !InputEnabled &&
                                                 (dragSource.Kind == GestureHitKind.BoardCard ||
                                                  dragSource.Kind == GestureHitKind.MeldGrip);
                    handScrollActive = !boardDragWhileDisabled && dragSource.Kind == GestureHitKind.HandArea;
                    meldReorderActive = !boardDragWhileDisabled && dragSource.Kind == GestureHitKind.MeldGrip;
                    dragActive = !sweepSelectActive && !boardDragWhileDisabled &&
                                 (dragSource.Kind == GestureHitKind.HandCard ||
                                  dragSource.Kind == GestureHitKind.BoardCard);
                    panActive = !sweepSelectActive && (boardDragWhileDisabled ||
                                (!dragActive && !handScrollActive && !meldReorderActive));
                    if (sweepSelectActive)
                    {
                        UpdateSweepSelection(pointerGesture.StartPosition);
                    }
                    else if (dragSource.Kind == GestureHitKind.HandCard && (InputEnabled || HandInputEnabled))
                    {
                        tableView.BeginHandLift(dragSource.CardId, gestureInterpreter.SelectedCardIds,
                            pointerGesture.StartPosition);
                    }
                    else if (dragSource.Kind == GestureHitKind.BoardCard && InputEnabled)
                    {
                        var selectedBoardCardIds = gestureInterpreter.IsBoardSelection &&
                                                    gestureInterpreter.IsSelected(dragSource.CardId)
                            ? gestureInterpreter.GetSelectedBoardCardIds(dragSource, game.State)
                            : new byte[0];
                        var liftCardIds = selectedBoardCardIds.Length > 0
                            ? selectedBoardCardIds
                            : new[] { dragSource.CardId };
                        tableView.BeginBoardLift(dragSource.CardId, liftCardIds, pointerGesture.StartPosition);
                    }
                }

                if (panActive)
                {
                    tableView.BeginCameraPan();
                    return;
                }
            }

            if (panActive)
            {
                if (pointerGesture.Kind == PointerGestureKind.DragUpdated)
                {
                    tableView.PanCamera(pointerGesture.Delta, pointerGesture.Position);
                }
                else if (pointerGesture.Kind == PointerGestureKind.DragEnded)
                {
                    tableView.ReleaseCameraPan();
                    panActive = false;
                }
                else if (pointerGesture.Kind == PointerGestureKind.Cancelled)
                {
                    tableView.CancelCameraPan();
                    panActive = false;
                }

                return;
            }

            if (!InputEnabled)
            {
                if (HandInputEnabled)
                {
                    HandleHandPointerGesture(pointerGesture);
                    return;
                }

                if (pointerGesture.Kind == PointerGestureKind.DoubleTap)
                {
                    ClearLastTapEffect();
                }

                dragActive = false;
                handScrollActive = false;
                meldReorderActive = false;
                sweepSelectActive = false;
                ResetEdgePanDrag();
                tableView.EndHandLift();
                tableView.EndMeldLift();
                tableView.EndBoardLift();
                ResetBoardHoldLift();
                SynchronizeViews();
                return;
            }

            switch (pointerGesture.Kind)
            {
                case PointerGestureKind.Tap:
                {
                    var hit = tableView.HitTest(pointerGesture.Position);
                    var isBoardSelectionTap = hit.Kind == GestureHitKind.BoardCard &&
                                               (gestureInterpreter.SelectedCardIds.Count == 0 ||
                                                gestureInterpreter.IsBoardSelection);
                    if (hit.Kind == GestureHitKind.HandCard || isBoardSelectionTap)
                    {
                        lastTapEffect = TapEffect.SelectionToggle;
                        lastTapSelectionSnapshot = gestureInterpreter.CaptureSelection();
                        ExecuteGesture(new GestureInput(GestureType.Tap, hit, GestureHit.None));
                    }
                    else
                    {
                        var source = GestureHit.None;
                        var target = hit;
                        var selectedCount = gestureInterpreter.SelectedCardIds.Count;
                        var result = ExecuteGesture(new GestureInput(GestureType.Tap, source, target));
                        if (selectedCount > 0 && result.Ok && gestureInterpreter.SelectedCardIds.Count == 0)
                        {
                            lastTapEffect = TapEffect.SuccessfulMove;
                        }
                    }

                    break;
                }
                case PointerGestureKind.DoubleTap:
                {
                    var hit = tableView.HitTest(pointerGesture.Position);
                    if (lastTapEffect == TapEffect.SelectionToggle)
                    {
                        gestureInterpreter.RestoreSelection(lastTapSelectionSnapshot);
                        RefreshSelection();
                    }

                    ExecuteGesture(new GestureInput(GestureType.DoubleTap, hit, GestureHit.None));
                    ClearLastTapEffect();
                    break;
                }
                case PointerGestureKind.LongPress:
                {
                    var hit = tableView.HitTest(pointerGesture.Position);
                    if (hit.Kind == GestureHitKind.BoardCard)
                    {
                        BeginBoardHoldLift(hit, pointerGesture.Position, pointerGesture.StartPosition);
                    }
                    else
                    {
                        HandleLongPress(pointerGesture.Position, true);
                    }

                    break;
                }
                case PointerGestureKind.HoldProgress:
                    UpdateBoardTailPickupCharge();
                    break;
                case PointerGestureKind.TailHold:
                    CompleteBoardTailHold(pointerGesture.Position);
                    break;
                case PointerGestureKind.DragStarted:
                    if (meldReorderActive)
                    {
                        tableView.BeginMeldLift(dragSource.MeldId, pointerGesture.StartPosition);
                    }

                    if (dragActive && !boardHoldLift)
                    {
                        Haptics.Pickup();
                    }

                    break;
                case PointerGestureKind.DragUpdated:
                    if (sweepSelectActive)
                    {
                        UpdateSweepSelection(pointerGesture.Position);
                    }
                    else if (handScrollActive)
                    {
                        tableView.ScrollHandByScreenDelta(pointerGesture.Delta.x);
                    }
                    else if (dragSource.Kind == GestureHitKind.HandCard)
                    {
                        tableView.UpdateHandLift(pointerGesture.Position);
                        UpdateDropPreview(pointerGesture.Position, pointerGesture.IsLongPress);
                    }
                    else if (dragSource.Kind == GestureHitKind.BoardCard && dragActive)
                    {
                        tableView.UpdateBoardLift(pointerGesture.Position);
                        UpdateDropPreview(pointerGesture.Position, pointerGesture.IsLongPress);
                    }
                    else if (meldReorderActive)
                    {
                        tableView.UpdateMeldLift(pointerGesture.Position);
                    }
                    else
                    {
                        UpdateDropPreview(pointerGesture.Position, pointerGesture.IsLongPress);
                    }
                    break;
                case PointerGestureKind.DragEnded:
                    if (sweepSelectActive)
                    {
                        sweepSelectActive = false;
                        dragActive = false;
                        tableView.EndHandLift();
                        RefreshSelection();
                        break;
                    }

                    if (handScrollActive)
                    {
                        handScrollActive = false;
                        break;
                    }

                    if (meldReorderActive)
                    {
                        tableView.EndMeldLift();
                        tableView.ReorderMeldByScreenPosition(dragSource.MeldId, pointerGesture.Position);
                        meldReorderActive = false;
                        break;
                    }

                    if (dragActive)
                    {
                        if (dragSource.Kind == GestureHitKind.HandCard)
                        {
                            tableView.UpdateHandLift(pointerGesture.Position);
                        }

                        var target = dragSource.Kind == GestureHitKind.HandCard &&
                                     tableView.TryGetHandDropTarget(out var handDropTarget)
                            ? handDropTarget
                            : tableView.HitTest(pointerGesture.Position);
                        var isLongPressDrag = dragSource.Kind == GestureHitKind.BoardCard
                            ? boardTailLifted
                            : pointerGesture.IsLongPress;
                        var type = isLongPressDrag ? GestureType.LongPressDrag : GestureType.Drag;
                        if (target.Kind == GestureHitKind.EmptyBoard)
                        {
                            tableView.SetPendingNewMeldPoint(pointerGesture.Position);
                        }

                        if (dragSource.Kind == GestureHitKind.HandCard)
                        {
                            tableView.EndHandLift();
                        }
                        else if (dragSource.Kind == GestureHitKind.BoardCard)
                        {
                            tableView.EndBoardLift();
                        }

                        var result = ExecuteGesture(new GestureInput(type, dragSource, target));
                        if (target.Kind == GestureHitKind.EmptyBoard)
                        {
                            tableView.ClearPendingNewMeldPoint();
                        }
                        if (dragSource.Kind == GestureHitKind.BoardCard)
                        {
                            SynchronizeViews();
                        }

                        if (pointerGesture.IsLongPress && result.Ok)
                        {
                            RefreshSelection();
                        }
                    }

                    dragActive = false;
                    tableView.EndHandLift();
                    tableView.EndBoardLift();
                    tableView.ClearDropPreview();
                    ResetBoardHoldLift();

                    break;
                case PointerGestureKind.Cancelled:
                    if (sweepSelectActive)
                    {
                        sweepSelectActive = false;
                        dragActive = false;
                        tableView.EndHandLift();
                        RefreshSelection();
                        break;
                    }

                    dragActive = false;
                    handScrollActive = false;
                    meldReorderActive = false;
                    tableView.EndHandLift();
                    tableView.EndMeldLift();
                    tableView.EndBoardLift();
                    ResetBoardHoldLift();
                    SynchronizeViews();
                    var boardView = GetComponentInChildren<BoardView>();
                    if (boardView != null)
                    {
                        boardView.ClearDropPreview();
                    }

                    break;
            }
        }

        private void ClearLastTapEffect()
        {
            lastTapEffect = TapEffect.None;
            lastTapSelectionSnapshot = default;
        }

        private void HandleHandPointerGesture(PointerGesture pointerGesture)
        {
            switch (pointerGesture.Kind)
            {
                case PointerGestureKind.Tap:
                case PointerGestureKind.DoubleTap:
                {
                    var hit = tableView.HitTest(pointerGesture.Position);
                    if (hit.Kind == GestureHitKind.HandCard)
                    {
                        if (pointerGesture.Kind == PointerGestureKind.Tap)
                        {
                            lastTapEffect = TapEffect.SelectionToggle;
                            lastTapSelectionSnapshot = gestureInterpreter.CaptureSelection();
                        }

                        ExecuteHandGesture(new GestureInput(GestureType.Tap, hit, GestureHit.None));
                    }

                    if (pointerGesture.Kind == PointerGestureKind.DoubleTap)
                    {
                        ClearLastTapEffect();
                    }

                    break;
                }
                case PointerGestureKind.LongPress:
                    HandleLongPress(pointerGesture.Position, false);
                    break;
                case PointerGestureKind.DragStarted:
                    if (dragActive)
                    {
                        Haptics.Pickup();
                    }

                    break;
                case PointerGestureKind.DragUpdated:
                    if (sweepSelectActive)
                    {
                        UpdateSweepSelection(pointerGesture.Position);
                    }
                    else if (handScrollActive)
                    {
                        tableView.ScrollHandByScreenDelta(pointerGesture.Delta.x);
                    }
                    else if (dragActive && dragSource.Kind == GestureHitKind.HandCard)
                    {
                        tableView.UpdateHandLift(pointerGesture.Position);
                        UpdateDropPreview(pointerGesture.Position, pointerGesture.IsLongPress);
                    }

                    break;
                case PointerGestureKind.DragEnded:
                    if (sweepSelectActive)
                    {
                        sweepSelectActive = false;
                        dragActive = false;
                        tableView.EndHandLift();
                        RefreshSelection();
                        break;
                    }

                    if (handScrollActive)
                    {
                        handScrollActive = false;
                        break;
                    }

                    if (dragActive && dragSource.Kind == GestureHitKind.HandCard)
                    {
                        tableView.UpdateHandLift(pointerGesture.Position);
                        var target = tableView.TryGetHandDropTarget(out var handDropTarget)
                            ? handDropTarget
                            : tableView.HitTest(pointerGesture.Position);
                        var type = pointerGesture.IsLongPress ? GestureType.LongPressDrag : GestureType.Drag;
                        tableView.EndHandLift();
                        if (IsRemoteTurnBlocked() && target.Kind != GestureHitKind.HandCard &&
                            target.Kind != GestureHitKind.HandArea && target.Kind != GestureHitKind.HandSlot)
                        {
                            NotifyInvalid(RuleError.NotYourTurn);
                        }
                        else
                        {
                            ExecuteHandGesture(new GestureInput(type, dragSource, target));
                        }
                    }

                    dragActive = false;
                    tableView.EndHandLift();
                    SynchronizeViews();
                    tableView.ClearDropPreview();
                    break;
                case PointerGestureKind.Cancelled:
                    if (sweepSelectActive)
                    {
                        sweepSelectActive = false;
                        dragActive = false;
                        tableView.EndHandLift();
                        RefreshSelection();
                        break;
                    }

                    dragActive = false;
                    handScrollActive = false;
                    tableView.EndHandLift();
                    tableView.EndMeldLift();
                    SynchronizeViews();
                    tableView.ClearDropPreview();
                    break;
                default:
                    dragActive = false;
                    handScrollActive = false;
                    meldReorderActive = false;
                    tableView.EndHandLift();
                    tableView.EndMeldLift();
                    SynchronizeViews();
                    break;
            }
        }

        private void UpdateDropPreview(Vector2 position, bool isLongPress)
        {
            if (!dragActive || tableView == null)
            {
                return;
            }

            var target = tableView.HitTest(position);
            var isLongPressDrag = dragSource.Kind == GestureHitKind.BoardCard ? boardTailLifted : isLongPress;
            var type = isLongPressDrag ? GestureType.LongPressDrag : GestureType.Drag;
            var preview = gestureInterpreter.Interpret(new GestureInput(type, dragSource, target), game.State,
                game.Turn);
            IReadOnlyList<byte> draggedIds;
            if (dragSource.Kind == GestureHitKind.BoardCard)
            {
                draggedIds = boardTailLifted
                    ? gestureInterpreter.GetLongPressCardIds(dragSource, game.State)
                    : new[] { dragSource.CardId };
            }
            else if (dragSource.Kind == GestureHitKind.HandCard)
            {
                draggedIds = gestureInterpreter.GetLongPressCardIds(dragSource, game.State);
            }
            else
            {
                draggedIds = new[] { dragSource.CardId };
            }
            var previewValid = !preview.Denied && preview.Batch.Count > 0;
            if (previewValid && preview.Batch.Count == 1)
            {
                var command = preview.Batch.Commands[0];
                if (command.Type == CommandType.MoveCards && command.Target.Kind == TargetKind.Meld)
                {
                    Meld targetMeld = null;
                    for (var index = 0; index < game.State.Board.Count; index++)
                    {
                        if (game.State.Board[index].Id == command.Target.MeldId)
                        {
                            targetMeld = game.State.Board[index];
                            break;
                        }
                    }

                    if (targetMeld == null)
                    {
                        previewValid = false;
                    }
                    else
                    {
                        var movedCardIds = command.CardIds;
                        var resultingCards = new List<Card>(targetMeld.Cards.Count + movedCardIds.Length);
                        for (var cardIndex = 0; cardIndex < targetMeld.Cards.Count; cardIndex++)
                        {
                            var card = targetMeld.Cards[cardIndex];
                            var moved = false;
                            for (var movedIndex = 0; movedIndex < movedCardIds.Length; movedIndex++)
                            {
                                if (movedCardIds[movedIndex] == card.Id)
                                {
                                    moved = true;
                                    break;
                                }
                            }

                            if (!moved)
                            {
                                resultingCards.Add(card);
                            }
                        }

                        for (var index = 0; index < movedCardIds.Length; index++)
                        {
                            resultingCards.Add(Card.FromId(movedCardIds[index]));
                        }

                        previewValid = MeldValidator.CanPlaceInto(targetMeld.Cards, resultingCards);
                    }
                }
            }

            tableView.ShowDropPreview(target, previewValid, draggedIds,
                draggedIds, position);
        }

        private void NotifyInvalid(RuleError error)
        {
            Haptics.Invalid();
            feedbackSink?.Invalid();
            InvalidGesture?.Invoke(error);
        }

        private void SubscribeToGame()
        {
            if (game == null)
            {
                return;
            }

            game.Events.Won += HandleWon;
            game.Events.Stalemate += HandleStalemate;
            game.Events.WentDown += HandleWentDown;
            game.Events.DownRevealed += HandleDownRevealed;
        }

        private void UnsubscribeFromGame()
        {
            if (game == null)
            {
                return;
            }

            game.Events.Won -= HandleWon;
            game.Events.Stalemate -= HandleStalemate;
            game.Events.WentDown -= HandleWentDown;
            game.Events.DownRevealed -= HandleDownRevealed;
        }

        private void HandleWentDown(int seat)
        {
            WentDown?.Invoke(seat);
        }

        private void HandleDownRevealed(int seat, IReadOnlyList<int> meldIds)
        {
            DownRevealed?.Invoke(seat, meldIds);
        }

        private void HandleWon(GameResult result)
        {
            GameWon?.Invoke(result);
        }

        private void HandleStalemate(GameResult result)
        {
            GameStalemate?.Invoke(result);
        }

        private bool IsTimeoutTurn(MejnunGame activeGame, int seat, int turnNumber)
        {
            return game == activeGame && activeGame.State.Phase == GamePhase.Playing &&
                   activeGame.State.CurrentSeat == seat && activeGame.State.TurnNumber == turnNumber;
        }

        private void HandleMarkedSetBroken(MarkedSet markedSet)
        {
            FeedbackCues.Raise(FeedbackCue.MarkedSetBroken);
            MarkedSetBroken?.Invoke(markedSet);
        }
    }
}