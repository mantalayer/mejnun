using Mejnun.Core;
using Mejnun.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class HudPresenter : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private TableController tableController;
        [SerializeField] private TableView tableView;

        private VisualElement root;
        private VisualElement opponentsStrip;
        private Label turnLabel;
        private Label drawPileLabel;
        private Button endTurnButton;
        private Button drawPassButton;
        private Button undoButton;
        private Button revertButton;
        private Button goDownButton;
        private Button sortSuitButton;
        private Button sortRankButton;
        private Button fitBoardButton;

        private void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (tableController == null)
            {
                tableController = FindAnyObjectByType<TableController>();
            }

            if (tableView == null)
            {
                tableView = FindAnyObjectByType<TableView>();
            }

            CacheElements();
            RegisterButtons();
        }

        private void OnEnable()
        {
            if (tableController != null)
            {
                tableController.Synchronized += OnSynchronized;
                tableController.GameWon += OnGameResult;
                tableController.GameStalemate += OnGameResult;
            }
        }

        private void OnDisable()
        {
            if (tableController != null)
            {
                tableController.Synchronized -= OnSynchronized;
                tableController.GameWon -= OnGameResult;
                tableController.GameStalemate -= OnGameResult;
            }
        }

        private void Update()
        {
            HandleKeyboardShortcuts();
        }

        private void CacheElements()
        {
            if (uiDocument == null)
            {
                return;
            }

            root = uiDocument.rootVisualElement;
            turnLabel = root.Q<Label>("turn-label");
            drawPileLabel = root.Q<Label>("draw-pile-label");
            opponentsStrip = root.Q<VisualElement>("opponents-strip");
            endTurnButton = root.Q<Button>("end-turn-button");
            drawPassButton = root.Q<Button>("draw-pass-button");
            undoButton = root.Q<Button>("undo-button");
            revertButton = root.Q<Button>("revert-button");
            goDownButton = root.Q<Button>("go-down-button");
            sortSuitButton = root.Q<Button>("sort-suit-button");
            sortRankButton = root.Q<Button>("sort-rank-button");
            fitBoardButton = root.Q<Button>("fit-board-button");
        }

        private void RegisterButtons()
        {
            if (endTurnButton != null) endTurnButton.clicked += EndTurn;
            if (drawPassButton != null) drawPassButton.clicked += DrawOrPass;
            if (undoButton != null) undoButton.clicked += Undo;
            if (revertButton != null) revertButton.clicked += Revert;
            if (goDownButton != null) goDownButton.clicked += GoDown;
            if (sortSuitButton != null) sortSuitButton.clicked += SortBySuit;
            if (sortRankButton != null) sortRankButton.clicked += SortByRank;
            if (fitBoardButton != null) fitBoardButton.clicked += FitBoard;
        }

        private void OnSynchronized(GameState state, TurnSession turn)
        {
            Refresh(state, turn);
        }

        private void OnGameResult(GameResult result)
        {
            if (tableController != null && tableController.Game != null)
            {
                Refresh(tableController.Game.State, tableController.Game.Turn);
            }
        }

        private void Refresh(GameState state, TurnSession turn)
        {
            if (tableController == null || tableController.Game == null || state == null ||
                state.CurrentSeat < 0 || state.CurrentSeat >= state.Hands.Count)
            {
                return;
            }

            var game = tableController.Game;
            var activeSeat = state.CurrentSeat;
            if (turnLabel != null)
            {
                turnLabel.text = "Seat " + (activeSeat + 1) + "'s turn";
            }

            if (drawPileLabel != null)
            {
                drawPileLabel.text = "Draw pile: " + state.DrawPile.Count;
            }

            if (drawPassButton != null)
            {
                drawPassButton.text = state.DrawPile.Count == 0 ? "Pass" : "Draw";
            }

            if (endTurnButton != null)
            {
                endTurnButton.SetEnabled(game.CanEndTurn());
            }

            if (undoButton != null)
            {
                undoButton.SetEnabled(tableController.UndoGroupCount > 0);
            }

            if (revertButton != null)
            {
                revertButton.SetEnabled(turn != null && turn.Moves.Count > 0);
            }

            if (goDownButton != null)
            {
                var canGoDown = game.CanGoDown();
                goDownButton.style.display = canGoDown ? DisplayStyle.Flex : DisplayStyle.None;
                goDownButton.SetEnabled(canGoDown);
            }

            var hasCards = state.Hands[activeSeat].Count > 0;
            if (sortSuitButton != null) sortSuitButton.SetEnabled(hasCards);
            if (sortRankButton != null) sortRankButton.SetEnabled(hasCards);
            RefreshSeats(state);
        }

        private void RefreshSeats(GameState state)
        {
            if (opponentsStrip == null)
            {
                return;
            }

            opponentsStrip.Clear();
            for (var seat = 0; seat < state.Hands.Count; seat++)
            {
                var label = new Label("Seat " + (seat + 1) + "  ·  " + state.Hands[seat].Count);
                label.AddToClassList("seat-chip");
                if (seat == state.CurrentSeat)
                {
                    label.text += "  •  Active";
                    label.AddToClassList("active-seat");
                }

                opponentsStrip.Add(label);
            }
        }

        private void EndTurn()
        {
            if (tableController != null && tableController.Game != null)
            {
                tableController.ApplyCommand(GameCommand.EndTurn(tableController.Game.State.CurrentSeat));
            }
        }

        private void DrawOrPass()
        {
            if (tableController != null && tableController.Game != null)
            {
                tableController.ApplyCommand(GameCommand.DrawOrPass(tableController.Game.State.CurrentSeat));
            }
        }

        private void Undo()
        {
            tableController?.UndoGesture();
        }

        private void Revert()
        {
            tableController?.RevertTurn();
        }

        private void GoDown()
        {
            if (tableController != null && tableController.Game != null)
            {
                tableController.ApplyCommand(GameCommand.GoDown(tableController.Game.State.CurrentSeat));
            }
        }

        private void SortBySuit()
        {
            SortHand(true);
        }

        private void SortByRank()
        {
            SortHand(false);
        }

        private void SortHand(bool bySuit)
        {
            if (tableController == null || tableController.Game == null || tableView == null)
            {
                return;
            }

            var state = tableController.Game.State;
            var seat = state.CurrentSeat;
            if (bySuit)
            {
                tableView.HandOrder.SortBySuit(seat, state.Hands[seat]);
            }
            else
            {
                tableView.HandOrder.SortByRank(seat, state.Hands[seat]);
            }

            tableView.Sync(state, tableController.Game.Turn);
            Refresh(state, tableController.Game.Turn);
        }

        private void FitBoard()
        {
            tableView?.FitBoard();
        }

        private void HandleKeyboardShortcuts()
        {
            if (tableController == null || tableController.Game == null || Keyboard.current == null ||
                IsTextFieldFocused())
            {
                return;
            }

            var keyboard = Keyboard.current;
            var commandOrControl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed ||
                                   keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed;
            if (commandOrControl && keyboard.zKey.wasPressedThisFrame)
            {
                Undo();
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                Revert();
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                EndTurn();
            }

            if (keyboard.dKey.wasPressedThisFrame)
            {
                DrawOrPass();
            }
        }

        private bool IsTextFieldFocused()
        {
            if (root == null || root.panel == null)
            {
                return false;
            }

            return root.panel.focusController.focusedElement is TextField;
        }
    }
}