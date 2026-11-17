using System.Collections.Generic;
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
        [SerializeField] private MatchFlow matchFlow;

        private VisualElement root;
        private VisualElement opponentsStrip;
        private Label turnLabel;
        private Label drawPileLabel;
        private Button endTurnButton;
        private Button drawPassButton;
        private Button undoButton;
        private Button revertButton;
        private Button sortSuitButton;
        private Button sortRankButton;
        private Button fitBoardButton;
        private Button hintButton;
        private Label turnPopup;
        private Label goneDownPopup;
        private Label goneDownTag;
        private IVisualElementScheduledItem turnPopupHide;
        private IVisualElementScheduledItem goneDownPopupHide;
        private IReadOnlyList<int> revealedDownMeldIds;
        private int revealedDownSeat = -1;
        private int revealedDownTurnNumber = -1;
        private MejnunGame announcedGame;
        private int announcedTurnNumber = -1;
        private bool hasAnnouncedTurn;

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

            if (matchFlow == null)
            {
                matchFlow = FindAnyObjectByType<MatchFlow>();
            }

            CacheElements();
            RegisterButtons();
        }

        private void OnEnable()
        {
            if (tableController != null)
            {
                tableController.Synchronized += OnSynchronized;
                tableController.WentDown += OnWentDown;
                tableController.DownRevealed += OnDownRevealed;
                tableController.GameWon += OnGameResult;
                tableController.GameStalemate += OnGameResult;
            }

            if (matchFlow != null)
            {
                matchFlow.ScreenChanged += OnMatchFlowScreenChanged;
            }
        }

        private void OnDisable()
        {
            if (tableController != null)
            {
                tableController.Synchronized -= OnSynchronized;
                tableController.WentDown -= OnWentDown;
                tableController.DownRevealed -= OnDownRevealed;
                tableController.GameWon -= OnGameResult;
                tableController.GameStalemate -= OnGameResult;
            }

            if (matchFlow != null)
            {
                matchFlow.ScreenChanged -= OnMatchFlowScreenChanged;
            }

            turnPopupHide?.Pause();
            goneDownPopupHide?.Pause();
        }

        private void Update()
        {
            HandleKeyboardShortcuts();
            UpdateDownRevealTag();
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
            sortSuitButton = root.Q<Button>("sort-suit-button");
            sortRankButton = root.Q<Button>("sort-rank-button");
            fitBoardButton = root.Q<Button>("fit-board-button");
            hintButton = root.Q<Button>("hint-button");
            turnPopup = root.Q<Label>("turn-popup");
            goneDownPopup = root.Q<Label>("gone-down-popup");
            goneDownTag = root.Q<Label>("gone-down-tag");
        }

        private void RegisterButtons()
        {
            if (endTurnButton != null) endTurnButton.clicked += EndTurn;
            if (drawPassButton != null) drawPassButton.clicked += DrawOrPass;
            if (undoButton != null) undoButton.clicked += Undo;
            if (revertButton != null) revertButton.clicked += Revert;
            if (sortSuitButton != null) sortSuitButton.clicked += SortBySuit;
            if (sortRankButton != null) sortRankButton.clicked += SortByRank;
            if (fitBoardButton != null) fitBoardButton.clicked += FitBoard;
            if (hintButton != null) hintButton.clicked += ToggleHints;
        }

        private void OnSynchronized(GameState state, TurnSession turn)
        {
            Refresh(state, turn);
            TryAnnounceTurn(state);
        }

        private void OnWentDown(int seat)
        {
            if (tableController != null && tableController.InputEnabled &&
                tableController.Game.State.CurrentSeat == seat)
            {
                goneDownPopupHide = ShowPopup(goneDownPopup, goneDownPopupHide);
            }
        }

        private void OnDownRevealed(int seat, IReadOnlyList<int> meldIds)
        {
            if (tableController == null || tableController.Game == null)
            {
                return;
            }

            revealedDownSeat = seat;
            revealedDownMeldIds = meldIds;
            revealedDownTurnNumber = tableController.Game.State.TurnNumber + 1;
        }

        private void OnMatchFlowScreenChanged(MatchFlowScreen screen)
        {
            if (screen == MatchFlowScreen.Turn && tableController != null && tableController.Game != null)
            {
                TryAnnounceTurn(tableController.Game.State);
            }
        }

        private void TryAnnounceTurn(GameState state)
        {
            if (tableController == null || tableController.Game == null || state == null ||
                state.Phase != GamePhase.Playing || state.CurrentSeat < 0 || state.CurrentSeat >= state.Hands.Count ||
                IsBotSeat(state.CurrentSeat) || (matchFlow != null && matchFlow.Screen != MatchFlowScreen.Turn))
            {
                return;
            }

            var game = tableController.Game;
            if (hasAnnouncedTurn && announcedGame == game && announcedTurnNumber == state.TurnNumber)
            {
                return;
            }

            announcedGame = game;
            announcedTurnNumber = state.TurnNumber;
            hasAnnouncedTurn = true;
            if (turnPopup != null)
            {
                turnPopup.text = GetPlayerName(state.CurrentSeat) + "'s Turn";
                turnPopupHide = ShowPopup(turnPopup, turnPopupHide);
            }
        }

        private void UpdateDownRevealTag()
        {
            if (revealedDownMeldIds == null || tableController == null || tableController.Game == null)
            {
                return;
            }

            var state = tableController.Game.State;
            if (state.TurnNumber != revealedDownTurnNumber)
            {
                ClearDownRevealTag();
                return;
            }

            if (goneDownTag == null || root == null || root.panel == null || tableView == null ||
                tableView.ViewCamera == null)
            {
                return;
            }

            if (!tableView.TryGetMeldsAnchor(revealedDownMeldIds, out var world))
            {
                ClearDownRevealTag();
                return;
            }

            var panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, tableView.ViewCamera);
            goneDownTag.text = GetPlayerName(revealedDownSeat) + " · Gone Down";
            goneDownTag.style.left = panelPosition.x - 100f;
            goneDownTag.style.top = panelPosition.y - 36f;
            goneDownTag.AddToClassList("popup-visible");
        }

        private void ClearDownRevealTag()
        {
            revealedDownMeldIds = null;
            revealedDownSeat = -1;
            revealedDownTurnNumber = -1;
            if (goneDownTag != null)
            {
                goneDownTag.RemoveFromClassList("popup-visible");
            }
        }

        private static IVisualElementScheduledItem ShowPopup(Label popup,
            IVisualElementScheduledItem scheduledHide)
        {
            if (popup == null)
            {
                return scheduledHide;
            }

            scheduledHide?.Pause();
            popup.AddToClassList("popup-visible");
            return popup.schedule.Execute(() => popup.RemoveFromClassList("popup-visible")).StartingIn(1600);
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
            var interactive = tableController.InputEnabled;
            if (turnLabel != null)
            {
                turnLabel.text = IsBotSeat(activeSeat)
                    ? GetSeatLabel(activeSeat) + " is thinking…"
                    : GetSeatLabel(activeSeat) + "'s turn";
            }

            if (drawPileLabel != null)
            {
                drawPileLabel.text = "Draw pile: " + state.DrawPile.Count;
            }

            if (drawPassButton != null)
            {
                drawPassButton.text = state.DrawPile.Count == 0 ? "Pass" : "Draw";
                drawPassButton.SetEnabled(interactive);
            }

            if (endTurnButton != null)
            {
                endTurnButton.SetEnabled(interactive && game.CanEndTurn());
            }

            if (undoButton != null)
            {
                undoButton.SetEnabled(interactive && tableController.UndoGroupCount > 0);
            }

            if (revertButton != null)
            {
                revertButton.SetEnabled(interactive && turn != null && turn.Moves.Count > 0);
            }

            var hasCards = interactive && state.Hands[activeSeat].Count > 0;
            if (sortSuitButton != null) sortSuitButton.SetEnabled(hasCards);
            if (sortRankButton != null) sortRankButton.SetEnabled(hasCards);
            if (hintButton != null)
            {
                hintButton.text = tableController.HintsEnabled ? "Hint: on" : "Hint: off";
                hintButton.SetEnabled(interactive);
            }

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
                var label = new Label(GetPlayerName(seat) + (IsBotSeat(seat) ? " (bot)" : string.Empty) + "  ·  " +
                                      state.Hands[seat].Count);
                label.AddToClassList("seat-chip");
                if (seat == state.CurrentSeat)
                {
                    label.text += "  •  Active";
                    label.AddToClassList("active-seat");
                }

                opponentsStrip.Add(label);
            }
        }

        private bool IsBotSeat(int seat)
        {
            return matchFlow != null && matchFlow.IsBotSeat(seat);
        }

        private string GetPlayerName(int seat)
        {
            return matchFlow != null && matchFlow.SeatCount > 0 ? matchFlow.GetPlayerName(seat) : "Seat " + (seat + 1);
        }

        private string GetSeatLabel(int seat)
        {
            return matchFlow != null && matchFlow.SeatCount > 0 ? matchFlow.GetSeatLabel(seat) : "Seat " + (seat + 1);
        }

        private void ToggleHints()
        {
            if (tableController != null)
            {
                tableController.SetHintsEnabled(!tableController.HintsEnabled);
            }
        }

        private void EndTurn()
        {
            if (tableController != null && tableController.InputEnabled)
            {
                tableController.ApplyCommand(GameCommand.EndTurn(tableController.Game.State.CurrentSeat));
            }
        }

        private void DrawOrPass()
        {
            if (tableController != null && tableController.InputEnabled)
            {
                tableController.ApplyCommand(GameCommand.DrawOrPass(tableController.Game.State.CurrentSeat));
            }
        }

        private void Undo()
        {
            if (tableController != null && tableController.InputEnabled)
            {
                tableController.UndoGesture();
            }
        }

        private void Revert()
        {
            if (tableController != null && tableController.InputEnabled)
            {
                tableController.RevertTurn();
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
            if (tableController == null || !tableController.InputEnabled || tableView == null)
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