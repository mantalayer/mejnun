using System;
using System.Collections;
using System.Collections.Generic;
using Mejnun.AI;
using Mejnun.Core;
using UnityEngine;

namespace Mejnun.Game
{
    public sealed class TableController : MonoBehaviour
    {
        [SerializeField] private TableView tableView;
        [SerializeField] private PointerRouter pointerRouter;
        [SerializeField] private MonoBehaviour feedbackSinkBehaviour;

        private readonly Stack<int> undoGroups = new Stack<int>();
        private readonly Stack<List<GameCommand>> redoGroups = new Stack<List<GameCommand>>();
        private readonly GestureInterpreter gestureInterpreter = new GestureInterpreter();
        private HashSet<byte> hintedCardIds = new HashSet<byte>();
        private Func<int, bool> seatInputFilter;
        private bool hintsEnabled;
        private MejnunGame game;
        private IFeedbackSink feedbackSink;
        private GestureHit dragSource;
        private bool dragActive;
        private bool handScrollActive;
        private bool panActive;
        private bool inputPaused;
        private bool meldReorderActive;
        private bool sweepSelectActive;
        private bool autoFixing;
        private bool markModeActive;
        [SerializeField, Min(0f)] private float timeoutFixStepSeconds = 0.6f;
        private readonly SetMarkers setMarkers = new SetMarkers();

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

        /// <summary>True when the local player may act: a game is in progress and the current seat accepts input.</summary>
        public bool InputEnabled => game != null && game.State.Phase == GamePhase.Playing &&
                                    !inputPaused && !autoFixing &&
                                    (seatInputFilter == null || seatInputFilter(game.State.CurrentSeat));

        public bool HandInputEnabled => game != null && game.State.Phase == GamePhase.Playing && !inputPaused &&
                                        !autoFixing && HandSeat >= 0 && HandSeat < game.State.Hands.Count &&
                                        (seatInputFilter == null || seatInputFilter(HandSeat));

        public event Action<GameState, TurnSession> Synchronized;
        public event Action<int> WentDown;
        public event Action<int, IReadOnlyList<int>> DownRevealed;
        public event Action<RuleError> InvalidGesture;
        public event Action<GameResult> GameWon;
        public event Action<GameResult> GameStalemate;
        public event Action<MarkedSet> MarkedSetBroken;
        public event Action<bool> MarkModeChanged;

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
        }

        private void OnDisable()
        {
            if (pointerRouter != null)
            {
                pointerRouter.Gesture -= HandlePointerGesture;
            }
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
            MarkModeChanged?.Invoke(markModeActive);
        }

        public void CycleMarkedSet(int meldId)
        {
            if (game != null)
            {
                setMarkers.Cycle(meldId, game.State.Board);
            }
        }

        public void CycleMarkedCard(byte cardId)
        {
            setMarkers.CycleCard(cardId);
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
                    tableView.HandOrder.ReorderGroup(game.State.CurrentSeat, reorderedCards,
                        interpretation.ReorderTargetIndex);
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

            var result = ExecuteBatch(interpretation.Batch);
            gestureInterpreter.Complete(interpretation, result.Ok);
            RefreshSelection();
            if (result.Ok)
            {
                SetMarkMode(false);
                Haptics.Drop();
            }
            return result;
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
                tableView.HandOrder.ReorderGroup(seat, reorderedCards, interpretation.ReorderTargetIndex);
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
            if (game == null)
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            if (batch == null)
            {
                return CommandResult.Failure(RuleError.EmptyMove);
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
            if (game == null)
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            var result = game.Apply(command);
            if (!result.Ok)
            {
                return result;
            }

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

            SynchronizeViews();
            return result;
        }

        public CommandResult UndoGesture()
        {
            SetMarkMode(false);
            if (game == null || undoGroups.Count == 0)
            {
                return CommandResult.Failure(RuleError.NothingToUndo);
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

            gestureInterpreter.ClearSelection();
            redoGroups.Push(redoCommands);
            SynchronizeViews();
            return CommandResult.Success();
        }

        public CommandResult RedoGesture()
        {
            SetMarkMode(false);
            if (game == null || redoGroups.Count == 0)
            {
                return CommandResult.Failure(RuleError.NothingToUndo);
            }

            var commands = redoGroups.Pop();
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

            return ApplyCommand(GameCommand.Revert(game.State.CurrentSeat));
        }

        public CommandResult FixBoard()
        {
            SetMarkMode(false);
            if (game == null)
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            var commands = BoardFixer.Plan(game);
            if (commands.Count == 0)
            {
                return CommandResult.Success();
            }

            if (commands.Count == 1 && commands[0].Type == CommandType.Revert)
            {
                return ApplyCommand(commands[0]);
            }

            var batch = new CommandBatch();
            for (var index = 0; index < commands.Count; index++)
            {
                batch.Add(commands[index]);
            }

            return ExecuteBatch(batch);
        }

        public bool FixBoardWouldReturnCards()
        {
            if (game == null)
            {
                return false;
            }

            var commands = BoardFixer.Plan(game);
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

        private CommandResult ApplyBatch(CommandBatch batch)
        {
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

            foreach (var pair in tableView.ActiveCards)
            {
                pair.Value.SetSelected(gestureInterpreter.IsSelected(pair.Key));
                pair.Value.SetHinted(hintedCardIds.Contains(pair.Key));
            }
        }

        private void HighlightLongPress(GestureHit hit)
        {
            var cardIds = gestureInterpreter.GetLongPressCardIds(hit, game.State);
            for (var index = 0; index < cardIds.Length; index++)
            {
                var cardView = tableView.GetCardView(cardIds[index]);
                if (cardView != null)
                {
                    cardView.SetSelected(true);
                }
            }
        }

        private void UpdateSweepSelection(Vector2 position)
        {
            var hit = tableView.HitTest(position);
            if (hit.Kind != GestureHitKind.HandCard || HandSeat < 0)
            {
                return;
            }

            gestureInterpreter.SelectHandRange(tableView.HandOrder.GetCardIds(HandSeat), dragSource.Index, hit.Index);
            RefreshSelection();
        }

        private void HandlePointerGesture(PointerGesture pointerGesture)
        {
            if (game == null || tableView == null)
            {
                return;
            }

            if (markModeActive && (pointerGesture.Kind == PointerGestureKind.Tap ||
                                   pointerGesture.Kind == PointerGestureKind.DoubleTap))
            {
                var markHit = tableView.HitTest(pointerGesture.Position);
                if (markHit.Kind == GestureHitKind.HandCard)
                {
                    CycleMarkedCard(markHit.CardId);
                }
                else if (markHit.Kind == GestureHitKind.BoardCard)
                {
                    if (pointerGesture.Kind == PointerGestureKind.DoubleTap)
                    {
                        CycleMarkedSet(markHit.MeldId);
                    }
                    else
                    {
                        CycleMarkedCard(markHit.CardId);
                    }
                }
                else if (markHit.Kind == GestureHitKind.MeldGap || markHit.Kind == GestureHitKind.MeldGrip)
                {
                    CycleMarkedSet(markHit.MeldId);
                }

                return;
            }

            if (pointerGesture.Kind == PointerGestureKind.DragStarted)
            {
                SetMarkMode(false);
                dragSource = tableView.HitTest(pointerGesture.StartPosition, true);
                sweepSelectActive = dragSource.Kind == GestureHitKind.HandCard && pointerGesture.IsLongPress &&
                                    (InputEnabled || HandInputEnabled);
                var boardDragWhileDisabled = !InputEnabled &&
                                             (dragSource.Kind == GestureHitKind.BoardCard ||
                                              dragSource.Kind == GestureHitKind.MeldGrip);
                handScrollActive = !boardDragWhileDisabled && dragSource.Kind == GestureHitKind.HandArea;
                meldReorderActive = !boardDragWhileDisabled && dragSource.Kind == GestureHitKind.MeldGrip;
                dragActive = !sweepSelectActive && !boardDragWhileDisabled &&
                             (dragSource.Kind == GestureHitKind.HandCard || dragSource.Kind == GestureHitKind.BoardCard);
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
                    var liftedCardIds = pointerGesture.IsLongPress
                        ? gestureInterpreter.GetLongPressCardIds(dragSource, game.State)
                        : new[] { dragSource.CardId };
                    tableView.BeginBoardLift(dragSource.CardId, liftedCardIds, pointerGesture.StartPosition);
                }
                if (panActive)
                {
                    return;
                }
            }

            if (panActive)
            {
                if (pointerGesture.Kind == PointerGestureKind.DragUpdated)
                {
                    tableView.PanCamera(pointerGesture.Delta, pointerGesture.Position);
                }
                else if (pointerGesture.Kind == PointerGestureKind.DragEnded ||
                         pointerGesture.Kind == PointerGestureKind.Cancelled)
                {
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

                dragActive = false;
                handScrollActive = false;
                meldReorderActive = false;
                sweepSelectActive = false;
                tableView.EndHandLift();
                tableView.EndMeldLift();
                tableView.EndBoardLift();
                SynchronizeViews();
                return;
            }

            switch (pointerGesture.Kind)
            {
                case PointerGestureKind.Tap:
                {
                    var hit = tableView.HitTest(pointerGesture.Position);
                    var source = hit.Kind == GestureHitKind.HandCard ? hit : GestureHit.None;
                    var target = hit.Kind == GestureHitKind.HandCard ? GestureHit.None : hit;
                    ExecuteGesture(new GestureInput(GestureType.Tap, source, target));
                    break;
                }
                case PointerGestureKind.DoubleTap:
                {
                    var hit = tableView.HitTest(pointerGesture.Position);
                    ExecuteGesture(new GestureInput(GestureType.DoubleTap, hit, GestureHit.None));
                    break;
                }
                case PointerGestureKind.LongPress:
                    Haptics.LongPress();
                    HighlightLongPress(tableView.HitTest(pointerGesture.Position));
                    break;
                case PointerGestureKind.DragStarted:
                    if (meldReorderActive)
                    {
                        tableView.BeginMeldLift(dragSource.MeldId, pointerGesture.StartPosition);
                    }

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
                        var target = tableView.HitTest(pointerGesture.Position);
                        var type = pointerGesture.IsLongPress ? GestureType.LongPressDrag : GestureType.Drag;
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
                    SynchronizeViews();
                    var boardView = GetComponentInChildren<BoardView>();
                    if (boardView != null)
                    {
                        boardView.ClearDropPreview();
                    }

                    break;
            }
        }

        private void HandleHandPointerGesture(PointerGesture pointerGesture)
        {
            switch (pointerGesture.Kind)
            {
                case PointerGestureKind.Tap:
                {
                    var hit = tableView.HitTest(pointerGesture.Position);
                    if (hit.Kind == GestureHitKind.HandCard)
                    {
                        ExecuteHandGesture(new GestureInput(GestureType.Tap, hit, GestureHit.None));
                    }

                    break;
                }
                case PointerGestureKind.LongPress:
                    HighlightLongPress(tableView.HitTest(pointerGesture.Position));
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
                        var target = tableView.HitTest(pointerGesture.Position);
                        var type = pointerGesture.IsLongPress ? GestureType.LongPressDrag : GestureType.Drag;
                        tableView.EndHandLift();
                        ExecuteHandGesture(new GestureInput(type, dragSource, target));
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
            var type = isLongPress ? GestureType.LongPressDrag : GestureType.Drag;
            var preview = gestureInterpreter.Interpret(new GestureInput(type, dragSource, target), game.State,
                game.Turn);
            IReadOnlyList<byte> draggedIds;
            if (dragSource.Kind == GestureHitKind.BoardCard)
            {
                draggedIds = isLongPress
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
            tableView.ShowDropPreview(target, !preview.Denied && preview.Batch.Count > 0, draggedIds,
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