using System;
using System.Collections.Generic;
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
        private readonly GestureInterpreter gestureInterpreter = new GestureInterpreter();
        private MejnunGame game;
        private IFeedbackSink feedbackSink;
        private GestureHit dragSource;
        private bool dragActive;
        private bool handScrollActive;

        public MejnunGame Game => game;
        public int UndoGroupCount => undoGroups.Count;
        public IReadOnlyList<byte> SelectedCardIds => gestureInterpreter.SelectedCardIds;

        public event Action<GameState, TurnSession> Synchronized;
        public event Action<RuleError> InvalidGesture;
        public event Action<GameResult> GameWon;
        public event Action<GameResult> GameStalemate;

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
        }

        public void Initialize(MejnunGame activeGame)
        {
            if (game != activeGame)
            {
                UnsubscribeFromGame();
                game = activeGame;
                SubscribeToGame();
            }

            undoGroups.Clear();
            gestureInterpreter.ClearSelection();
            SynchronizeViews();
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
                    tableView.HandOrder.Reorder(game.State.CurrentSeat, interpretation.ReorderedCardId,
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
                Haptics.Drop();
            }
            return result;
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
                gestureInterpreter.ClearSelection();
            }
            else if (command.Type == CommandType.EndTurn || command.Type == CommandType.DrawOrPass ||
                     command.Type == CommandType.TimeoutTurn)
            {
                undoGroups.Clear();
                gestureInterpreter.ClearSelection();
            }
            else if (command.Type == CommandType.MoveCards || command.Type == CommandType.SplitMeld ||
                     command.Type == CommandType.MergeMelds || command.Type == CommandType.GoDown)
            {
                undoGroups.Push(1);
            }

            SynchronizeViews();
            return result;
        }

        public CommandResult UndoGesture()
        {
            if (game == null || undoGroups.Count == 0)
            {
                return CommandResult.Failure(RuleError.NothingToUndo);
            }

            var commandCount = undoGroups.Pop();
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
            SynchronizeViews();
            return CommandResult.Success();
        }

        public CommandResult RevertTurn()
        {
            if (game == null)
            {
                return CommandResult.Failure(RuleError.WrongPhase);
            }

            return ApplyCommand(GameCommand.Revert(game.State.CurrentSeat));
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

            if (tableView != null)
            {
                tableView.Sync(game.State, game.Turn);
                RefreshSelection();
            }

            Synchronized?.Invoke(game.State, game.Turn);
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
            }
        }

        private void HandlePointerGesture(PointerGesture pointerGesture)
        {
            if (game == null || tableView == null)
            {
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
                    break;
                case PointerGestureKind.DragStarted:
                    dragSource = tableView.HitTest(pointerGesture.StartPosition);
                    handScrollActive = dragSource.Kind == GestureHitKind.HandArea;
                    dragActive = dragSource.Kind == GestureHitKind.HandCard ||
                                 dragSource.Kind == GestureHitKind.BoardCard;
                    if (dragActive)
                    {
                        Haptics.Pickup();
                    }

                    break;
                case PointerGestureKind.DragUpdated:
                    if (handScrollActive)
                    {
                        tableView.ScrollHandByScreenDelta(pointerGesture.Delta.x);
                    }
                    else
                    {
                        UpdateDropPreview(pointerGesture.Position, pointerGesture.IsLongPress);
                    }
                    break;
                case PointerGestureKind.DragEnded:
                    if (handScrollActive)
                    {
                        handScrollActive = false;
                        break;
                    }

                    if (dragActive)
                    {
                        var target = tableView.HitTest(pointerGesture.Position);
                        var type = pointerGesture.IsLongPress ? GestureType.LongPressDrag : GestureType.Drag;
                        ExecuteGesture(new GestureInput(type, dragSource, target));
                    }

                    dragActive = false;
                    if (tableView != null && tableView.CurrentBoardLayout != null)
                    {
                        var board = GetComponentInChildren<BoardView>();
                        if (board != null)
                        {
                            board.ClearDropPreview();
                        }
                    }

                    break;
                case PointerGestureKind.Cancelled:
                    dragActive = false;
                    handScrollActive = false;
                    var boardView = GetComponentInChildren<BoardView>();
                    if (boardView != null)
                    {
                        boardView.ClearDropPreview();
                    }

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
            var boardView = GetComponentInChildren<BoardView>();
            if (boardView != null)
            {
                boardView.ShowDropPreview(target, !preview.Denied && preview.Batch.Count > 0);
            }
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
        }

        private void UnsubscribeFromGame()
        {
            if (game == null)
            {
                return;
            }

            game.Events.Won -= HandleWon;
            game.Events.Stalemate -= HandleStalemate;
        }

        private void HandleWon(GameResult result)
        {
            GameWon?.Invoke(result);
        }

        private void HandleStalemate(GameResult result)
        {
            GameStalemate?.Invoke(result);
        }
    }
}