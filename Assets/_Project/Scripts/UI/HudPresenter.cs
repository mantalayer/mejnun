using System;
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
        private static readonly string[] CpuSpeedLabels = { "Glacial", "Very slow", "Slow", "Normal", "Fast", "Very fast" };

        public enum OpponentSide
        {
            Top,
            Left,
            Right
        }

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private TableController tableController;
        [SerializeField] private TableView tableView;
        [SerializeField] private MatchFlow matchFlow;

        private VisualElement root;
        private VisualElement topOpponents;
        private VisualElement leftOpponents;
        private VisualElement rightOpponents;
        private Label turnLabel;
        private Label drawPileLabel;
        private VisualElement drawPileStack;
        private Button endTurnButton;
        private Button drawPassButton;
        private Button undoButton;
        private Button redoButton;
        private Button revertButton;
        private Button fixBoardButton;
        private Button sortSuitButton;
        private Button sortRankButton;
        private Button fitBoardButton;
        private Button hintButton;
        private Button fastForwardButton;
        private Button markSetButton;
        private Button pauseButton;
        private Button menuButton;
        private Button helpButton;
        private Label turnPopup;
        private Label halasPopup;
        private Label timerLabel;
        private Label goneDownPopup;
        private Label goneDownTag;
        private IVisualElementScheduledItem turnPopupHide;
        private IVisualElementScheduledItem goneDownPopupHide;
        private IVisualElementScheduledItem goneDownTagHide;
        private IVisualElementScheduledItem halasHide;
        private IReadOnlyList<int> revealedDownMeldIds;
        private int revealedDownSeat = -1;
        private int revealedDownTurnNumber = -1;
        private MejnunGame announcedGame;
        private int announcedTurnNumber = -1;
        private bool hasAnnouncedTurn;
        private bool goneDownTagShown;
        private VisualElement confirmDialog;
        private Label confirmMessage;
        private Toggle confirmDontAsk;
        private VisualElement pausePanel;
        private VisualElement settingsPanel;
        private VisualElement helpPanel;
        private VisualElement bottomBar;
        private VisualElement topBar;
        private VisualElement markerLayer;
        private Toggle hintBoardMovesToggle;
        private Toggle hintJiggleToggle;
        private Toggle reducedMotionToggle;
        private Toggle hapticsToggle;
        private Slider handArcAmountSlider;
        private Slider handSpreadSlider;
        private Slider handCardScaleSlider;
        private Slider handLiftSlider;
        private Slider opponentCardScaleSlider;
        private Slider zoomSensitivitySlider;
        private DropdownField cpuSpeedDropdown;
        private Toggle showFastForwardToggle;
        private Toggle overlapSetsToggle;
        private Toggle organicBoardToggle;
        private Slider boardCardSpreadSlider;
        private Slider cardMessinessSlider;
        private Toggle easyOpeningDealToggle;
        private Action pendingConfirmAction;
        private Action<bool> pendingConfirmSetting;
        private float nextBadgePulse;
        private bool badgePulse;
        private bool menuPaused;
        private bool confirmPaused;
        private int displayedDrawPileCount = -1;
        private MejnunGame displayedDrawPileGame;

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
                tableController.MarkModeChanged += OnMarkModeChanged;
                tableController.GameWon += OnGameResult;
                tableController.GameStalemate += OnGameResult;
            }

            if (matchFlow != null)
            {
                matchFlow.ScreenChanged += OnMatchFlowScreenChanged;
                if (matchFlow.TurnTimer != null)
                {
                    matchFlow.TurnTimer.TimedOut += OnTimedOut;
                }
            }
        }

        private void OnDisable()
        {
            if (tableController != null)
            {
                tableController.Synchronized -= OnSynchronized;
                tableController.WentDown -= OnWentDown;
                tableController.DownRevealed -= OnDownRevealed;
                tableController.MarkModeChanged -= OnMarkModeChanged;
                tableController.GameWon -= OnGameResult;
                tableController.GameStalemate -= OnGameResult;
            }

            if (matchFlow != null)
            {
                matchFlow.ScreenChanged -= OnMatchFlowScreenChanged;
                if (matchFlow.TurnTimer != null)
                {
                    matchFlow.TurnTimer.TimedOut -= OnTimedOut;
                }
            }

            turnPopupHide?.Pause();
            goneDownPopupHide?.Pause();
            goneDownTagHide?.Pause();
            halasHide?.Pause();
        }

        private void Update()
        {
            HandleKeyboardShortcuts();
            UpdateDownRevealTag();
            UpdateTimerLabel();
            UpdateLastCardBadges();
            UpdateDrawPilePosition();
            if (root != null)
            {
                root.EnableInClassList("portrait-layout", Screen.height > Screen.width);
                UpdateHandBottomInset();
                UpdateBoardInsets();
                UpdateFastForwardButton();
                UpdateSetMarkers();
            }
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
            drawPileStack = root.Q<VisualElement>("draw-pile-stack");
            timerLabel = root.Q<Label>("timer-label");
            topOpponents = root.Q<VisualElement>("top-opponents");
            leftOpponents = root.Q<VisualElement>("left-opponents");
            rightOpponents = root.Q<VisualElement>("right-opponents");
            endTurnButton = root.Q<Button>("end-turn-button");
            drawPassButton = root.Q<Button>("draw-pass-button");
            undoButton = root.Q<Button>("undo-button");
            redoButton = root.Q<Button>("redo-button");
            revertButton = root.Q<Button>("revert-button");
            fixBoardButton = root.Q<Button>("fix-board-button");
            sortSuitButton = root.Q<Button>("sort-suit-button");
            sortRankButton = root.Q<Button>("sort-rank-button");
            fitBoardButton = root.Q<Button>("fit-board-button");
            hintButton = root.Q<Button>("hint-button");
            pauseButton = root.Q<Button>("pause-button");
            menuButton = root.Q<Button>("menu-button");
            helpButton = root.Q<Button>("help-button");
            turnPopup = root.Q<Label>("turn-popup");
            halasPopup = root.Q<Label>("halas-popup");
            goneDownPopup = root.Q<Label>("gone-down-popup");
            goneDownTag = root.Q<Label>("gone-down-tag");
            confirmDialog = root.Q<VisualElement>("confirm-dialog");
            confirmMessage = root.Q<Label>("confirm-message");
            confirmDontAsk = root.Q<Toggle>("confirm-dont-ask");
            pausePanel = root.Q<VisualElement>("pause-panel");
            settingsPanel = root.Q<VisualElement>("settings-panel");
            helpPanel = root.Q<VisualElement>("help-panel");
            bottomBar = root.Q<VisualElement>("bottom-bar");
            topBar = root.Q<VisualElement>("top-bar");
            markerLayer = root.Q<VisualElement>("marker-layer");
            hintBoardMovesToggle = root.Q<Toggle>("hint-board-moves-toggle");
            hintJiggleToggle = root.Q<Toggle>("hint-jiggle-toggle");
            reducedMotionToggle = root.Q<Toggle>("reduced-motion-toggle");
            hapticsToggle = root.Q<Toggle>("haptics-toggle");
            handArcAmountSlider = root.Q<Slider>("hand-arc-amount-slider");
            handSpreadSlider = root.Q<Slider>("hand-spread-slider");
            handCardScaleSlider = root.Q<Slider>("hand-card-scale-slider");
            handLiftSlider = root.Q<Slider>("hand-lift-slider");
            opponentCardScaleSlider = root.Q<Slider>("opponent-card-scale-slider");
            zoomSensitivitySlider = root.Q<Slider>("zoom-sensitivity-slider");
            fastForwardButton = root.Q<Button>("fast-forward-button");
            markSetButton = root.Q<Button>("mark-set-button");
            cpuSpeedDropdown = root.Q<DropdownField>("cpu-speed-dropdown");
            showFastForwardToggle = root.Q<Toggle>("show-fast-forward-toggle");
            overlapSetsToggle = root.Q<Toggle>("overlap-sets-toggle");
            organicBoardToggle = root.Q<Toggle>("organic-board-toggle");
            boardCardSpreadSlider = root.Q<Slider>("board-card-spread-slider");
            cardMessinessSlider = root.Q<Slider>("card-messiness-slider");
            easyOpeningDealToggle = root.Q<Toggle>("easy-opening-deal-toggle");
        }

        private void RegisterButtons()
        {
            if (endTurnButton != null) endTurnButton.clicked += EndTurn;
            if (drawPassButton != null) drawPassButton.clicked += DrawOrPass;
            if (undoButton != null) undoButton.clicked += Undo;
            if (redoButton != null) redoButton.clicked += Redo;
            if (revertButton != null) revertButton.clicked += Revert;
            if (fixBoardButton != null) fixBoardButton.clicked += FixBoard;
            if (sortSuitButton != null) sortSuitButton.clicked += SortBySuit;
            if (sortRankButton != null) sortRankButton.clicked += SortByRank;
            if (fitBoardButton != null) fitBoardButton.clicked += FitBoard;
            if (hintButton != null) hintButton.clicked += ToggleHints;
            if (fastForwardButton != null) fastForwardButton.clicked += ToggleFastForward;
            if (markSetButton != null) markSetButton.clicked += ToggleMarkMode;
            if (pauseButton != null) pauseButton.clicked += TogglePause;
            if (menuButton != null) menuButton.clicked += RequestLeave;
            if (helpButton != null) helpButton.clicked += ToggleHelp;
            root.Q<Button>("confirm-cancel-button").clicked += CancelConfirmation;
            root.Q<Button>("confirm-accept-button").clicked += AcceptConfirmation;
            root.Q<Button>("resume-button").clicked += Resume;
            root.Q<Button>("restart-button").clicked += RequestRestart;
            root.Q<Button>("settings-button").clicked += OpenSettings;
            root.Q<Button>("settings-close-button").clicked += CloseSettings;
            root.Q<Button>("help-close-button").clicked += ToggleHelp;
            hintBoardMovesToggle.RegisterValueChangedCallback(changeEvent =>
            {
                GameSettings.HintBoardMoves = changeEvent.newValue;
                tableController?.RefreshHintOptions();
            });
            hintJiggleToggle.RegisterValueChangedCallback(changeEvent => GameSettings.HintJiggle = changeEvent.newValue);
            reducedMotionToggle.RegisterValueChangedCallback(changeEvent => GameSettings.ReducedMotion = changeEvent.newValue);
            hapticsToggle.RegisterValueChangedCallback(changeEvent => GameSettings.HapticsEnabled = changeEvent.newValue);
            RegisterHandSlider(handArcAmountSlider, GameSettings.HandArcAmount,
                value => GameSettings.HandArcAmount = value);
            RegisterHandSlider(handSpreadSlider, GameSettings.HandSpread,
                value => GameSettings.HandSpread = value);
            RegisterHandSlider(handCardScaleSlider, GameSettings.HandCardScale,
                value => GameSettings.HandCardScale = value);
            RegisterHandSlider(handLiftSlider, GameSettings.HandLift,
                value => GameSettings.HandLift = value);
            if (opponentCardScaleSlider != null)
            {
                opponentCardScaleSlider.RegisterValueChangedCallback(changeEvent =>
                {
                    GameSettings.OpponentCardScale = changeEvent.newValue;
                    if (tableController != null && tableController.Game != null)
                    {
                        RefreshSeats(tableController.Game.State);
                    }
                });
                opponentCardScaleSlider.SetValueWithoutNotify(GameSettings.OpponentCardScale);
            }
            if (zoomSensitivitySlider != null)
            {
                zoomSensitivitySlider.RegisterValueChangedCallback(changeEvent =>
                    GameSettings.ZoomSensitivity = changeEvent.newValue);
                zoomSensitivitySlider.SetValueWithoutNotify(GameSettings.ZoomSensitivity);
            }

            if (cpuSpeedDropdown != null)
            {
                cpuSpeedDropdown.RegisterValueChangedCallback(changeEvent =>
                {
                    var index = Array.IndexOf(CpuSpeedLabels, changeEvent.newValue);
                    if (index >= 0) GameSettings.CpuSpeed = index;
                });
                cpuSpeedDropdown.SetValueWithoutNotify(CpuSpeedLabels[GameSettings.CpuSpeed]);
            }

            if (showFastForwardToggle != null)
            {
                showFastForwardToggle.RegisterValueChangedCallback(changeEvent =>
                    GameSettings.ShowFastForward = changeEvent.newValue);
                showFastForwardToggle.SetValueWithoutNotify(GameSettings.ShowFastForward);
            }

            if (overlapSetsToggle != null)
            {
                overlapSetsToggle.RegisterValueChangedCallback(changeEvent =>
                {
                    GameSettings.OverlapSets = changeEvent.newValue;
                    if (tableController?.Game != null)
                    {
                        tableView?.Sync(tableController.Game.State, tableController.Game.Turn);
                    }
                });
                overlapSetsToggle.SetValueWithoutNotify(GameSettings.OverlapSets);
            }

            if (organicBoardToggle != null)
            {
                organicBoardToggle.RegisterValueChangedCallback(changeEvent =>
                {
                    GameSettings.OrganicBoard = changeEvent.newValue;
                    if (tableController?.Game != null)
                    {
                        tableView?.Sync(tableController.Game.State, tableController.Game.Turn);
                    }
                });
                organicBoardToggle.SetValueWithoutNotify(GameSettings.OrganicBoard);
            }

            if (cardMessinessSlider != null)
            {
                cardMessinessSlider.RegisterValueChangedCallback(changeEvent =>
                {
                    GameSettings.CardMessiness = changeEvent.newValue;
                    if (tableController?.Game != null)
                    {
                        tableView?.Sync(tableController.Game.State, tableController.Game.Turn);
                    }
                });
                cardMessinessSlider.SetValueWithoutNotify(GameSettings.CardMessiness);
            }

            if (boardCardSpreadSlider != null)
            {
                boardCardSpreadSlider.RegisterValueChangedCallback(changeEvent =>
                {
                    GameSettings.BoardCardSpread = changeEvent.newValue;
                    if (tableController?.Game != null)
                    {
                        tableView?.Sync(tableController.Game.State, tableController.Game.Turn);
                    }
                });
                boardCardSpreadSlider.SetValueWithoutNotify(GameSettings.BoardCardSpread);
            }

            if (easyOpeningDealToggle != null)
            {
                easyOpeningDealToggle.RegisterValueChangedCallback(changeEvent =>
                    GameSettings.EasyOpeningDeal = changeEvent.newValue);
                easyOpeningDealToggle.SetValueWithoutNotify(GameSettings.EasyOpeningDeal);
            }

            hintBoardMovesToggle.SetValueWithoutNotify(GameSettings.HintBoardMoves);
            hintJiggleToggle.SetValueWithoutNotify(GameSettings.HintJiggle);
            reducedMotionToggle.SetValueWithoutNotify(GameSettings.ReducedMotion);
            hapticsToggle.SetValueWithoutNotify(GameSettings.HapticsEnabled);
        }

        private void RegisterHandSlider(Slider slider, float value, Action<float> setValue)
        {
            if (slider == null)
            {
                return;
            }

            slider.RegisterValueChangedCallback(changeEvent =>
            {
                setValue(changeEvent.newValue);
                tableView?.RefreshHandLayout();
            });
            slider.SetValueWithoutNotify(value);
        }

        private void UpdateHandBottomInset()
        {
            if (tableView == null || root == null || bottomBar == null ||
                bottomBar.resolvedStyle.display == DisplayStyle.None || root.worldBound.height <= 0f ||
                bottomBar.worldBound.height <= 0f)
            {
                tableView?.SetHandBottomInset(0f);
                return;
            }

            var fraction = Mathf.Clamp01((root.worldBound.yMax - bottomBar.worldBound.yMin) /
                                         root.worldBound.height);
            tableView.SetHandBottomInset(fraction);
        }

        private void UpdateBoardInsets()
        {
            if (tableView == null || root == null || root.worldBound.width <= 0f || root.worldBound.height <= 0f)
            {
                return;
            }

            var rootBounds = root.worldBound;
            var top = 0f;
            var left = 0f;
            var right = 0f;
            if (TryGetVisibleBounds(topBar, out var topBounds))
            {
                top = Mathf.Max(top, (topBounds.yMax - rootBounds.yMin) / rootBounds.height);
            }

            if (TryGetVisibleBounds(topOpponents, out var opponentTopBounds))
            {
                top = Mathf.Max(top, (opponentTopBounds.yMax - rootBounds.yMin) / rootBounds.height);
            }

            if (TryGetVisibleBounds(leftOpponents, out var leftBounds))
            {
                left = Mathf.Max(0f, (leftBounds.xMax - rootBounds.xMin) / rootBounds.width);
            }

            if (TryGetVisibleBounds(rightOpponents, out var rightBounds))
            {
                right = Mathf.Max(0f, (rootBounds.xMax - rightBounds.xMin) / rootBounds.width);
            }

            tableView.SetBoardInsets(Mathf.Clamp01(top), Mathf.Clamp01(left), Mathf.Clamp01(right));
        }

        private static bool TryGetVisibleBounds(VisualElement element, out Rect bounds)
        {
            bounds = default;
            if (element == null || element.resolvedStyle.display == DisplayStyle.None)
            {
                return false;
            }

            bounds = element.worldBound;
            return bounds.width > 0f && bounds.height > 0f && !float.IsNaN(bounds.xMin) &&
                   !float.IsNaN(bounds.yMin) && !float.IsInfinity(bounds.xMax) && !float.IsInfinity(bounds.yMax);
        }

        private void UpdateFastForwardButton()
        {
            if (fastForwardButton == null)
            {
                return;
            }

            var visible = GameSettings.ShowFastForward &&
                          (tableController != null && tableController.IsAutoFixing ||
                           matchFlow != null && matchFlow.BotDriver != null && matchFlow.BotDriver.IsPlaying);
            fastForwardButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            fastForwardButton.text = matchFlow != null && matchFlow.FastForward ? "Continue" : "Fast forward";
        }

        private void UpdateSetMarkers()
        {
            if (markerLayer == null || tableController == null || tableView == null || tableView.ViewCamera == null ||
                root == null || root.panel == null)
            {
                return;
            }

            markerLayer.Clear();
            var marks = tableController.MarkedSets;
            for (var index = 0; index < marks.Count; index++)
            {
                if (!tableView.TryGetMeldAnchor(marks[index].MeldId, out var world))
                {
                    continue;
                }

                var position = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, tableView.ViewCamera);
                var marker = new Label(marks[index].Symbol);
                marker.pickingMode = PickingMode.Ignore;
                marker.AddToClassList("set-marker");
                marker.style.left = position.x - 15f;
                marker.style.top = position.y - 36f;
                markerLayer.Add(marker);
            }

            var cardMarks = tableController.MarkedCards;
            for (var index = 0; index < cardMarks.Count; index++)
            {
                if (!tableView.TryGetCardAnchor(cardMarks[index].CardId, out var world))
                {
                    continue;
                }

                var position = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, tableView.ViewCamera);
                var marker = new Label(cardMarks[index].Symbol);
                marker.pickingMode = PickingMode.Ignore;
                marker.AddToClassList("card-marker");
                marker.style.left = position.x - 10f;
                marker.style.top = position.y - 17f;
                markerLayer.Add(marker);
            }
        }

        private void UpdateDrawPileStack(GameState state, MejnunGame game)
        {
            if (drawPileStack == null || state == null ||
                displayedDrawPileGame == game && displayedDrawPileCount == state.DrawPile.Count)
            {
                return;
            }

            displayedDrawPileGame = game;
            displayedDrawPileCount = state.DrawPile.Count;
            drawPileStack.Clear();
            drawPileStack.style.display = state.DrawPile.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (state.DrawPile.Count == 0)
            {
                return;
            }

            var layerCount = Mathf.Min(6, Mathf.CeilToInt(state.DrawPile.Count / 6f));
            var deckIndex = state.DrawPile[state.DrawPile.Count - 1].Deck;
            var backSprite = tableView == null ? null : tableView.GetCardBack(state, deckIndex);
            for (var index = 0; index < layerCount; index++)
            {
                var card = new VisualElement();
                card.pickingMode = PickingMode.Ignore;
                card.name = "draw-pile-card-" + index;
                card.AddToClassList("draw-pile-card");
                card.style.left = index * 1.5f;
                card.style.top = (layerCount - 1 - index) * 1.5f;
                if (backSprite != null)
                {
                    card.style.backgroundImage = new StyleBackground(backSprite);
                    card.AddToClassList("skinned");
                }

                drawPileStack.Add(card);
            }
        }

        private void UpdateDrawPilePosition()
        {
            UpdateOpponentCardPositions();
            if (drawPileStack == null || tableView == null || root == null || root.panel == null)
            {
                return;
            }

            var bounds = drawPileStack.worldBound;
            if (bounds.width <= 0f || bounds.height <= 0f)
            {
                return;
            }

            if (TryPanelToScreen(bounds.center, out var screenPosition))
            {
                tableView.SetDrawPileScreenRect(screenPosition, bounds.height * Screen.height /
                    Mathf.Max(root.panel.visualTree.worldBound.height, 1f));
            }
        }

        private void UpdateOpponentCardPositions()
        {
            if (tableView == null || tableView.CurrentState == null || root == null || root.panel == null)
            {
                return;
            }

            var state = tableView.CurrentState;
            var viewerSeat = tableView.ViewerSeat >= 0 ? tableView.ViewerSeat : state.CurrentSeat;
            var panelHeight = Mathf.Max(root.panel.visualTree.worldBound.height, 1f);
            for (var seat = 0; seat < state.Hands.Count; seat++)
            {
                if (seat == viewerSeat)
                {
                    continue;
                }

                var cards = root.Q<VisualElement>("opponent-cards-" + seat);
                var bounds = cards == null ? default : cards.worldBound;
                if (cards != null && bounds.width > 0f && bounds.height > 0f &&
                    TryPanelToScreen(bounds.center, out var screenPosition))
                {
                    tableView.SetSeatScreenRect(seat, screenPosition, bounds.height * Screen.height / panelHeight);
                }
                else
                {
                    tableView.SetSeatScreenRect(seat, default, 0f);
                }
            }
        }

        private bool TryPanelToScreen(Vector2 panelPosition, out Vector2 screenPosition)
        {
            screenPosition = default;
            var panelBounds = root.panel.visualTree.worldBound;
            if (!IsFinite(panelBounds.xMin) || !IsFinite(panelBounds.yMin) ||
                !IsFinite(panelBounds.width) || !IsFinite(panelBounds.height) ||
                panelBounds.width <= 0f || panelBounds.height <= 0f ||
                !IsFinite(panelPosition.x) || !IsFinite(panelPosition.y))
            {
                return false;
            }

            var normalizedX = (panelPosition.x - panelBounds.xMin) / panelBounds.width;
            var normalizedY = (panelPosition.y - panelBounds.yMin) / panelBounds.height;
            screenPosition = new Vector2(normalizedX * Screen.width, Screen.height - normalizedY * Screen.height);
            return IsFinite(screenPosition.x) && IsFinite(screenPosition.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void OnSynchronized(GameState state, TurnSession turn)
        {
            Refresh(state, turn);
            TryAnnounceTurn(state);
        }

        private void OnMarkModeChanged(bool active)
        {
            if (markSetButton != null)
            {
                markSetButton.text = active ? "Marking…" : "Mark";
            }
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
            goneDownTagShown = false;
            goneDownTagHide?.Pause();
            if (goneDownTag != null)
            {
                goneDownTag.RemoveFromClassList("popup-visible");
            }
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
            if (!goneDownTagShown)
            {
                goneDownTagShown = true;
                goneDownTag.AddToClassList("popup-visible");
                goneDownTagHide = goneDownTag.schedule.Execute(ClearDownRevealTag).StartingIn(3000);
            }
        }

        private void ClearDownRevealTag()
        {
            revealedDownMeldIds = null;
            revealedDownSeat = -1;
            revealedDownTurnNumber = -1;
            goneDownTagShown = false;
            goneDownTagHide?.Pause();
            if (goneDownTag != null)
            {
                goneDownTag.RemoveFromClassList("popup-visible");
            }
        }

        private static IVisualElementScheduledItem ShowPopup(Label popup,
            IVisualElementScheduledItem scheduledHide, long durationMilliseconds = 1600)
        {
            if (popup == null)
            {
                return scheduledHide;
            }

            scheduledHide?.Pause();
            popup.AddToClassList("popup-visible");
            return popup.schedule.Execute(() => popup.RemoveFromClassList("popup-visible"))
                .StartingIn(durationMilliseconds);
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

            UpdateDrawPileStack(state, game);

            if (drawPassButton != null)
            {
                drawPassButton.text = state.DrawPile.Count == 0 ? "Pass" : "Draw";
                drawPassButton.SetEnabled(interactive && !tableController.HasPlacedHandCard);
            }

            if (endTurnButton != null)
            {
                endTurnButton.SetEnabled(interactive && game.CanEndTurn());
            }

            if (undoButton != null)
            {
                undoButton.SetEnabled(interactive && tableController.UndoGroupCount > 0);
            }

            if (redoButton != null)
            {
                redoButton.SetEnabled(interactive && tableController.RedoGroupCount > 0);
            }

            if (revertButton != null)
            {
                revertButton.SetEnabled(interactive && turn != null && turn.Moves.Count > 0);
            }

            if (fixBoardButton != null)
            {
                fixBoardButton.SetEnabled(interactive && turn != null && turn.Moves.Count > 0 &&
                                          !MeldValidator.IsBoardValid(state.Board));
            }

            if (pauseButton != null) pauseButton.SetEnabled(game.State.Phase == GamePhase.Playing);
            if (menuButton != null) menuButton.SetEnabled(game != null);

            var handSeat = tableController.HandSeat;
            var hasCards = tableController.HandInputEnabled && handSeat >= 0 && handSeat < state.Hands.Count &&
                           state.Hands[handSeat].Count > 0;
            if (sortSuitButton != null) sortSuitButton.SetEnabled(hasCards);
            if (sortRankButton != null) sortRankButton.SetEnabled(hasCards);
            if (hintButton != null)
            {
                hintButton.text = tableController.HintsEnabled ? "Hint: on" : "Hint: off";
                hintButton.SetEnabled(interactive);
            }

            if (markSetButton != null)
            {
                markSetButton.text = tableController.MarkModeActive ? "Marking…" : "Mark";
            }

            RefreshSeats(state);
        }

        private void RefreshSeats(GameState state)
        {
            if (topOpponents == null && leftOpponents == null && rightOpponents == null)
            {
                return;
            }

            topOpponents?.Clear();
            leftOpponents?.Clear();
            rightOpponents?.Clear();
            var viewerSeat = tableView != null && tableView.ViewerSeat >= 0
                ? tableView.ViewerSeat
                : state.CurrentSeat;
            for (var seat = 0; seat < state.Hands.Count; seat++)
            {
                if (seat == viewerSeat)
                {
                    continue;
                }

                var opponent = new VisualElement();
                opponent.pickingMode = PickingMode.Ignore;
                opponent.AddToClassList("opponent-seat");
                var relativeSeat = (seat - viewerSeat + state.Hands.Count) % state.Hands.Count;
                var opponentSide = GetOpponentSide(relativeSeat, state.Hands.Count - 1);
                var name = new Label(GetPlayerName(seat) + (IsBotSeat(seat) ? " · bot" : string.Empty));
                name.pickingMode = PickingMode.Ignore;
                name.AddToClassList("opponent-name");
                opponent.Add(name);
                var cards = new VisualElement();
                cards.pickingMode = PickingMode.Ignore;
                cards.AddToClassList("opponent-cards");
                if (opponentSide != OpponentSide.Top)
                {
                    cards.AddToClassList("opponent-cards-vertical");
                }

                cards.name = "opponent-cards-" + seat;
                var cardScale = Mathf.Clamp(GameSettings.OpponentCardScale, 0.5f, 2f);
                var cardWidth = 30f * cardScale;
                var cardHeight = 44f * cardScale;
                for (var cardIndex = 0; cardIndex < state.Hands[seat].Count; cardIndex++)
                {
                    var cardBack = new VisualElement();
                    cardBack.pickingMode = PickingMode.Ignore;
                    cardBack.AddToClassList("opponent-card-back");
                    cardBack.name = "opponent-card-" + seat + "-" + cardIndex;
                    cardBack.style.width = cardWidth;
                    cardBack.style.height = cardHeight;
                    if (cardIndex > 0)
                    {
                        if (opponentSide == OpponentSide.Top)
                        {
                            cardBack.style.marginLeft = -cardWidth * 0.68f;
                        }
                        else
                        {
                            cardBack.style.marginTop = -cardHeight * 0.68f;
                        }
                    }

                    var backSprite = tableView == null
                        ? null
                        : tableView.GetCardBack(state, state.Hands[seat][cardIndex].Deck);
                    if (backSprite != null)
                    {
                        cardBack.style.backgroundImage = new StyleBackground(backSprite);
                        cardBack.AddToClassList("skinned");
                    }

                    cards.Add(cardBack);
                }

                if (state.Hands[seat].Count == 1)
                {
                    var warning = new Label("!");
                    warning.pickingMode = PickingMode.Ignore;
                    warning.name = "last-card-badge-" + seat;
                    warning.AddToClassList("last-card-badge");
                    cards.Add(warning);
                }

                opponent.Add(cards);
                if (seat == state.CurrentSeat)
                {
                    opponent.AddToClassList("active-seat");
                }

                switch (opponentSide)
                {
                    case OpponentSide.Left:
                        leftOpponents?.Add(opponent);
                        break;
                    case OpponentSide.Right:
                        rightOpponents?.Add(opponent);
                        break;
                    default:
                        topOpponents?.Add(opponent);
                        break;
                }
            }
        }

        public static OpponentSide GetOpponentSide(int relativeSeat, int opponentCount)
        {
            if (opponentCount <= 1 || relativeSeat < 1 || relativeSeat > opponentCount)
            {
                return OpponentSide.Top;
            }

            if (opponentCount == 2)
            {
                return relativeSeat == 1 ? OpponentSide.Left : OpponentSide.Right;
            }

            var sideCount = Mathf.Max(1, opponentCount / 3);
            if (relativeSeat <= sideCount)
            {
                return OpponentSide.Left;
            }

            return relativeSeat > opponentCount - sideCount ? OpponentSide.Right : OpponentSide.Top;
        }

        private void UpdateLastCardBadges()
        {
            if (Time.unscaledTime < nextBadgePulse || root == null)
            {
                return;
            }

            nextBadgePulse = Time.unscaledTime + 0.3f;
            badgePulse = !badgePulse;
            var badges = root.Query<Label>(className: "last-card-badge").ToList();
            for (var index = 0; index < badges.Count; index++)
            {
                var cardBack = badges[index].parent.Q<VisualElement>(className: "opponent-card-back");
                if (badgePulse)
                {
                    badges[index].AddToClassList("pulse");
                    cardBack?.AddToClassList("shake");
                }
                else
                {
                    badges[index].RemoveFromClassList("pulse");
                    cardBack?.RemoveFromClassList("shake");
                }
            }
        }

        private void UpdateTimerLabel()
        {
            if (timerLabel == null || matchFlow == null || matchFlow.TurnTimer == null ||
                matchFlow.TurnTimer.DurationSeconds <= 0)
            {
                if (timerLabel != null) timerLabel.text = string.Empty;
                return;
            }

            timerLabel.text = matchFlow.TurnTimer.IsRunning
                ? "Time: " + Mathf.CeilToInt(matchFlow.TurnTimer.RemainingSeconds)
                : "";
        }

        private void OnTimedOut()
        {
            if (halasPopup != null)
            {
                halasHide = ShowPopup(halasPopup, halasHide, 2200);
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
                tableController.SetMarkMode(false);
                tableController.SetHintsEnabled(!tableController.HintsEnabled);
            }
        }

        private void EndTurn()
        {
            tableController?.SetMarkMode(false);
            if (tableController != null && tableController.InputEnabled)
            {
                tableController.ApplyCommand(GameCommand.EndTurn(tableController.Game.State.CurrentSeat));
            }
        }

        private void DrawOrPass()
        {
            tableController?.SetMarkMode(false);
            if (tableController == null || !tableController.InputEnabled || tableController.HasPlacedHandCard)
            {
                return;
            }

            tableController.ApplyCommand(GameCommand.DrawOrPass(tableController.Game.State.CurrentSeat));
        }

        private void Undo()
        {
            if (tableController != null && tableController.InputEnabled)
            {
                tableController.UndoGesture();
            }
        }

        private void Redo()
        {
            if (tableController != null && tableController.InputEnabled)
            {
                tableController.RedoGesture();
            }
        }

        private void Revert()
        {
            if (tableController != null && tableController.InputEnabled)
            {
                RequestConfirmation("Return all your cards to your hand?",
                    () => GameSettings.ConfirmRevert, value => GameSettings.ConfirmRevert = value,
                    () => tableController.RevertTurn());
            }
        }

        private void FixBoard()
        {
            if (tableController != null && tableController.InputEnabled)
            {
                if (tableController.FixBoardWouldReturnCards())
                {
                    RequestConfirmation("Some invalid cards will return to your hand. Fix the board?",
                        () => GameSettings.ConfirmFix, value => GameSettings.ConfirmFix = value,
                        () => tableController.FixBoard());
                }
                else
                {
                    tableController.FixBoard();
                }
            }
        }

        private void RequestConfirmation(string message, Func<bool> isEnabled, Action<bool> setEnabled,
            Action action)
        {
            if (isEnabled == null || !isEnabled())
            {
                action?.Invoke();
                return;
            }

            pendingConfirmAction = action;
            pendingConfirmSetting = setEnabled;
            if (confirmMessage != null) confirmMessage.text = message;
            if (confirmDontAsk != null) confirmDontAsk.SetValueWithoutNotify(false);
            SetVisible(confirmDialog, true);
            confirmPaused = true;
            ApplyPause();
        }

        private void AcceptConfirmation()
        {
            if (confirmDontAsk != null && confirmDontAsk.value)
            {
                pendingConfirmSetting?.Invoke(false);
            }

            var action = pendingConfirmAction;
            ClearConfirmation();
            action?.Invoke();
        }

        private void CancelConfirmation()
        {
            ClearConfirmation();
        }

        private void ClearConfirmation()
        {
            pendingConfirmAction = null;
            pendingConfirmSetting = null;
            SetVisible(confirmDialog, false);
            confirmPaused = false;
            BlurFocusedElement();
            ApplyPause();
            RefreshCurrentGame();
        }

        private void TogglePause()
        {
            if (pausePanel == null) return;
            if (pausePanel.resolvedStyle.display == DisplayStyle.Flex)
            {
                Resume();
                return;
            }

            menuPaused = true;
            ApplyPause();
            SetVisible(pausePanel, true);
            SetVisible(settingsPanel, false);
        }

        private void Resume()
        {
            SetVisible(pausePanel, false);
            SetVisible(settingsPanel, false);
            menuPaused = false;
            BlurFocusedElement();
            ApplyPause();
            RefreshCurrentGame();
        }

        private void ApplyPause()
        {
            matchFlow?.SetPaused(menuPaused || confirmPaused);
        }

        private void OpenSettings()
        {
            SetVisible(pausePanel, false);
            SetVisible(settingsPanel, true);
        }

        private void CloseSettings()
        {
            SetVisible(settingsPanel, false);
            SetVisible(pausePanel, true);
            BlurFocusedElement();
            RefreshCurrentGame();
        }

        private void BlurFocusedElement()
        {
            var focusedElement = root == null || root.panel == null
                ? null
                : root.panel.focusController.focusedElement;
            focusedElement?.Blur();
        }

        private void RefreshCurrentGame()
        {
            if (tableController != null && tableController.Game != null)
            {
                Refresh(tableController.Game.State, tableController.Game.Turn);
            }
        }

        private void ToggleHelp()
        {
            if (helpPanel == null) return;
            SetVisible(helpPanel, helpPanel.resolvedStyle.display != DisplayStyle.Flex);
        }

        private void RequestLeave()
        {
            if (matchFlow == null || matchFlow.Game == null) return;
            RequestConfirmation("Exit this game? It will be saved so you can resume it later.",
                () => GameSettings.ConfirmLeave, value => GameSettings.ConfirmLeave = value,
                () =>
                {
                    Resume();
                    matchFlow.ReturnToSetup();
                });
        }

        private void RequestRestart()
        {
            RequestConfirmation("Restart this game? The current round will be lost.",
                () => GameSettings.ConfirmLeave, value => GameSettings.ConfirmLeave = value,
                () =>
                {
                    Resume();
                    matchFlow?.RestartMatch();
                });
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            if (element != null)
            {
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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
            tableController?.SetMarkMode(false);
            if (tableController == null || !tableController.HandInputEnabled || tableView == null)
            {
                return;
            }

            var state = tableController.Game.State;
            var seat = tableController.HandSeat;
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
            tableController?.SetMarkMode(false);
            tableView?.FitBoard();
        }

        private void ToggleFastForward()
        {
            if (matchFlow != null)
            {
                matchFlow.FastForward = !matchFlow.FastForward;
                UpdateFastForwardButton();
            }
        }

        private void ToggleMarkMode()
        {
            if (tableController == null)
            {
                return;
            }

            tableController.SetMarkMode(!tableController.MarkModeActive);
            if (markSetButton != null)
            {
                markSetButton.text = tableController.MarkModeActive ? "Marking…" : "Mark";
            }
        }

        private void HandleKeyboardShortcuts()
        {
            if (Keyboard.current == null || IsTextFieldFocused())
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (confirmDialog != null && confirmDialog.resolvedStyle.display == DisplayStyle.Flex)
                {
                    CancelConfirmation();
                    return;
                }
                else if (settingsPanel != null && settingsPanel.resolvedStyle.display == DisplayStyle.Flex)
                {
                    CloseSettings();
                    return;
                }
                else if (helpPanel != null && helpPanel.resolvedStyle.display == DisplayStyle.Flex)
                {
                    SetVisible(helpPanel, false);
                    return;
                }
                else if (tableController != null && tableController.SelectedCardIds.Count > 0)
                {
                    tableController.ClearSelection();
                    return;
                }
                else if (tableController != null && tableController.CancelActiveGesture())
                {
                    return;
                }
                else
                {
                    TogglePause();
                    return;
                }
            }

            if (keyboard.f1Key.wasPressedThisFrame ||
                (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) &&
                keyboard.slashKey.wasPressedThisFrame)
            {
                ToggleHelp();
            }

            if (confirmDialog != null && confirmDialog.resolvedStyle.display == DisplayStyle.Flex)
            {
                if (keyboard.enterKey.wasPressedThisFrame) AcceptConfirmation();
                return;
            }

            if ((pausePanel != null && pausePanel.resolvedStyle.display == DisplayStyle.Flex) ||
                (settingsPanel != null && settingsPanel.resolvedStyle.display == DisplayStyle.Flex) ||
                (helpPanel != null && helpPanel.resolvedStyle.display == DisplayStyle.Flex))
            {
                return;
            }

            if (tableController == null || tableController.Game == null)
            {
                return;
            }

            var commandOrControl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed ||
                                   keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed;
            if (commandOrControl && keyboard.zKey.wasPressedThisFrame)
            {
                if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) Redo();
                else Undo();
            }

            if (commandOrControl && keyboard.yKey.wasPressedThisFrame)
            {
                Redo();
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                Revert();
            }

            if (keyboard.fKey.wasPressedThisFrame)
            {
                FixBoard();
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                EndTurn();
            }

            if (keyboard.dKey.wasPressedThisFrame)
            {
                DrawOrPass();
            }

            if (keyboard.hKey.wasPressedThisFrame)
            {
                ToggleHints();
            }

            if (keyboard.sKey.wasPressedThisFrame)
            {
                SortBySuit();
            }

            if (keyboard.kKey.wasPressedThisFrame)
            {
                SortByRank();
            }

            if (keyboard.homeKey.wasPressedThisFrame || keyboard.digit0Key.wasPressedThisFrame)
            {
                FitBoard();
            }

            if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame)
            {
                tableView?.ZoomCamera(1.1f);
            }

            if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame)
            {
                tableView?.ZoomCamera(1f / 1.1f);
            }

            var pan = Vector2.zero;
            if (keyboard.leftArrowKey.isPressed) pan.x += 8f;
            if (keyboard.rightArrowKey.isPressed) pan.x -= 8f;
            if (keyboard.upArrowKey.isPressed) pan.y -= 8f;
            if (keyboard.downArrowKey.isPressed) pan.y += 8f;
            if (pan.sqrMagnitude > 0f)
            {
                tableView?.PanCamera(pan, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            }

            if (keyboard.pKey.wasPressedThisFrame)
            {
                TogglePause();
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