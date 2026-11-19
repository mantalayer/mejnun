using System;
using System.Collections.Generic;
using Mejnun.Cosmetics;
using Mejnun.Core;
using Mejnun.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class HudPresenter : MonoBehaviour
    {
        private const float HandBottomBarFitCompensationPixels = 8f;
        private const float PhoneMaxShortSideInches = 4.5f;
        private const int PhoneMaxShortSidePixels = 1200;
        private const float PhoneMinAspectRatio = 1.9f;
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
        private Button compactBoardButton;
        private Button hintButton;
        private Button fastForwardButton;
        private Button markSetButton;
        private Button pauseButton;
        private Button menuButton;
        private Button helpButton;
        private Button skipTutorialButton;
        private TutorialOverlay tutorialOverlay;
        private Label turnPopup;
        private Label halasPopup;
        private Label timerLabel;
        private Label goneDownPopup;
        private Label goneDownTag;
        private Label invalidMovePopup;
        private Label onlineNotice;
        private Label onlineWaiting;
        private readonly Queue<string> pendingOnlineNotices = new Queue<string>();
        private bool onlineNoticeShowing;
        private IVisualElementScheduledItem turnPopupHide;
        private IVisualElementScheduledItem goneDownPopupHide;
        private IVisualElementScheduledItem goneDownTagHide;
        private IVisualElementScheduledItem halasHide;
        private IVisualElementScheduledItem invalidMoveHide;
        private IVisualElementScheduledItem onlineNoticeHide;
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
        private Label roomCodeLabel;
        private VisualElement settingsPanel;
        private SettingsPanel settingsPanelBinder;
        private VisualElement helpPanel;
        private VisualElement bottomBar;
        private VisualElement opponentsStrip;
        private VisualElement fastForwardDock;
        private bool activeTurnPulsing;
        private bool phoneSafeAreaApplied;
        private bool hasSafeAreaSignature;
        private Rect lastSafeArea;
        private int lastSafeAreaWidth;
        private int lastSafeAreaHeight;
        private float lastSafeAreaPanelWidth;
        private float lastSafeAreaPanelHeight;
        private bool lastTallPhoneLayout;
        private bool hasPhoneLayoutSignature;
        private const float ActiveTurnPulsePeriod = 1.6f;
        private const float ActiveTurnPulseMinAlpha = 0.35f;
        private static readonly Color ActiveTurnOutlineColor = new Color(177f / 255f, 224f / 255f, 120f / 255f, 1f);
        private VisualElement statusPanel;
        private VisualElement turnPanel;
        private VisualElement markerLayer;
        private Action pendingConfirmAction;
        private Action<bool> pendingConfirmSetting;
        private float nextBadgePulse;
        private bool badgePulse;
        private bool menuPaused;
        private bool confirmPaused;
        private int displayedDrawPileCount = -1;
        private MejnunGame displayedDrawPileGame;
        private MejnunGame lastInputRefreshGame;
        private int lastInputRefreshTurn = -1;
        private int lastInputRefreshMoves = -1;
        private GamePhase lastInputRefreshPhase;
        private bool lastInputRefreshEnabled;
        private bool lastInputRefreshHandEnabled;
        private bool hasInputRefreshSignature;
        private readonly SoftKeyboardBackGuard keyboardBackGuard = new SoftKeyboardBackGuard();

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
            if (settingsPanel != null)
            {
                settingsPanelBinder = new SettingsPanel();
                settingsPanelBinder.Initialize(settingsPanel, tableView, tableController);
                settingsPanelBinder.Closed += CloseSettings;
            }

            RegisterButtons();
            tutorialOverlay = new TutorialOverlay(root, tableView, tableController, RequestSkipTutorial,
                RefreshCurrentGame);
        }

        private void Start()
        {
            tutorialOverlay?.Bind(matchFlow == null ? null : matchFlow.Tutorial);
        }

        private void OnEnable()
        {
            if (tableController != null)
            {
                tableController.Synchronized += OnSynchronized;
                tableController.WentDown += OnWentDown;
                tableController.DownRevealed += OnDownRevealed;
                tableController.InvalidGesture += OnInvalidGesture;
                tableController.MarkModeChanged += OnMarkModeChanged;
                tableController.GameWon += OnGameResult;
                tableController.GameStalemate += OnGameResult;
            }

            if (matchFlow != null)
            {
                matchFlow.ScreenChanged += OnMatchFlowScreenChanged;
                matchFlow.OnlineSeatsChanged += OnOnlineSeatsChanged;
                matchFlow.OnlineNotice += OnOnlineNotice;
                if (matchFlow.TurnTimer != null)
                {
                    matchFlow.TurnTimer.TimedOut += OnTimedOut;
                }
            }

            if (tableView != null)
            {
                tableView.IncomingCardsChanged += OnIncomingCardsChanged;
            }
        }

        private void OnDisable()
        {
            if (tableController != null)
            {
                tableController.Synchronized -= OnSynchronized;
                tableController.WentDown -= OnWentDown;
                tableController.DownRevealed -= OnDownRevealed;
                tableController.InvalidGesture -= OnInvalidGesture;
                tableController.MarkModeChanged -= OnMarkModeChanged;
                tableController.GameWon -= OnGameResult;
                tableController.GameStalemate -= OnGameResult;
            }

            if (matchFlow != null)
            {
                matchFlow.ScreenChanged -= OnMatchFlowScreenChanged;
                matchFlow.OnlineSeatsChanged -= OnOnlineSeatsChanged;
                matchFlow.OnlineNotice -= OnOnlineNotice;
                if (matchFlow.TurnTimer != null)
                {
                    matchFlow.TurnTimer.TimedOut -= OnTimedOut;
                }
            }

            if (tableView != null)
            {
                tableView.IncomingCardsChanged -= OnIncomingCardsChanged;
            }

            turnPopupHide?.Pause();
            goneDownPopupHide?.Pause();
            goneDownTagHide?.Pause();
            halasHide?.Pause();
            invalidMoveHide?.Pause();
        }

        private void OnDestroy()
        {
            tutorialOverlay?.Unbind();
            onlineNoticeHide?.Pause();
            if (settingsPanelBinder != null)
            {
                settingsPanelBinder.Closed -= CloseSettings;
                settingsPanelBinder.Dispose();
            }
        }

        private void Update()
        {
            HandleKeyboardShortcuts();
            if (pausePanel != null && pausePanel.resolvedStyle.display == DisplayStyle.Flex)
            {
                RefreshRoomCode();
            }

            RefreshIfInputStateChanged();
            UpdateDownRevealTag();
            UpdateTimerLabel();
            UpdateLastCardBadges();
            UpdateDrawPilePosition();
            UpdateDrawFlightOverlay();
            compactBoardButton?.SetEnabled(GameSettings.OrganicBoard && tableView != null);
            tutorialOverlay?.Tick();
            if (root != null)
            {
                UiScaleUtility.Apply(uiDocument);
                var phoneLayout = IsPhoneScreen(Application.isMobilePlatform, Screen.width, Screen.height, Screen.dpi);
                root.EnableInClassList("portrait-layout", Screen.height > Screen.width);
                root.EnableInClassList("phone-layout", phoneLayout);
                ApplyPhoneSafeArea(phoneLayout);
                UpdateOnlineWaiting();
                var tallPhoneLayout = phoneLayout && Screen.height > Screen.width;
                if (!hasPhoneLayoutSignature || tallPhoneLayout != lastTallPhoneLayout)
                {
                    hasPhoneLayoutSignature = true;
                    lastTallPhoneLayout = tallPhoneLayout;
                    if (tableController != null && tableController.Game != null)
                    {
                        RefreshSeats(tableController.Game.State);
                    }
                }

                UpdateHandBottomInset();
                UpdateBoardInsets();
                UpdateFastForwardButton();
                UpdateSetMarkers();
                UpdateActiveTurnPulse();
            }
        }

        private void ApplyPhoneSafeArea(bool phoneLayout)
        {
            if (root == null)
            {
                return;
            }

            if (!phoneLayout)
            {
                if (phoneSafeAreaApplied)
                {
                    root.style.paddingLeft = StyleKeyword.Null;
                    root.style.paddingRight = StyleKeyword.Null;
                    root.style.paddingTop = StyleKeyword.Null;
                    root.style.paddingBottom = StyleKeyword.Null;
                    if (turnPanel != null)
                    {
                        turnPanel.style.left = StyleKeyword.Null;
                        turnPanel.style.top = StyleKeyword.Null;
                    }

                    if (pauseButton != null)
                    {
                        pauseButton.style.right = StyleKeyword.Null;
                        pauseButton.style.top = StyleKeyword.Null;
                    }

                    if (opponentsStrip != null)
                    {
                        opponentsStrip.style.left = StyleKeyword.Null;
                        opponentsStrip.style.right = StyleKeyword.Null;
                        opponentsStrip.style.top = StyleKeyword.Null;
                        opponentsStrip.style.bottom = StyleKeyword.Null;
                    }

                    if (topOpponents != null)
                    {
                        topOpponents.style.top = StyleKeyword.Null;
                    }

                    phoneSafeAreaApplied = false;
                    hasSafeAreaSignature = false;
                }

                return;
            }

            if (root.panel == null || root.worldBound.width <= 0f || root.worldBound.height <= 0f)
            {
                return;
            }

            var safeArea = Screen.safeArea;
            var bounds = root.worldBound;
            if (hasSafeAreaSignature && lastSafeArea == safeArea && lastSafeAreaWidth == Screen.width &&
                lastSafeAreaHeight == Screen.height && Mathf.Approximately(lastSafeAreaPanelWidth, bounds.width) &&
                Mathf.Approximately(lastSafeAreaPanelHeight, bounds.height))
            {
                return;
            }

            var screenWidth = Mathf.Max(1f, Screen.width);
            var screenHeight = Mathf.Max(1f, Screen.height);
            var leftInset = Mathf.Max(0f, safeArea.xMin / screenWidth * bounds.width);
            var rightInset = Mathf.Max(0f, (Screen.width - safeArea.xMax) / screenWidth * bounds.width);
            var topInset = Mathf.Max(0f, (Screen.height - safeArea.yMax) / screenHeight * bounds.height);
            var bottomInset = Mathf.Max(0f, safeArea.yMin / screenHeight * bounds.height);
            const float cornerMargin = 12f;
            root.style.paddingLeft = leftInset + cornerMargin;
            root.style.paddingRight = rightInset + cornerMargin;
            root.style.paddingTop = topInset + cornerMargin;
            root.style.paddingBottom = bottomInset + cornerMargin;
            if (turnPanel != null)
            {
                turnPanel.style.left = leftInset + cornerMargin;
                turnPanel.style.top = topInset + cornerMargin;
            }

            if (pauseButton != null)
            {
                pauseButton.style.right = rightInset + cornerMargin;
                pauseButton.style.top = topInset + cornerMargin;
            }

            if (opponentsStrip != null)
            {
                opponentsStrip.style.left = leftInset + cornerMargin;
                opponentsStrip.style.right = rightInset + cornerMargin;
                opponentsStrip.style.top = topInset + bounds.height * 0.2f;
                opponentsStrip.style.bottom = bottomInset + bounds.height * 0.24f;
            }

            if (topOpponents != null)
            {
                topOpponents.style.top = topInset + 84f;
            }

            lastSafeArea = safeArea;
            lastSafeAreaWidth = Screen.width;
            lastSafeAreaHeight = Screen.height;
            lastSafeAreaPanelWidth = bounds.width;
            lastSafeAreaPanelHeight = bounds.height;
            hasSafeAreaSignature = true;
            phoneSafeAreaApplied = true;
        }

        private void UpdateOnlineWaiting()
        {
            if (onlineWaiting == null)
            {
                return;
            }

            var message = matchFlow == null ? string.Empty : matchFlow.OnlineWaitingMessage;
            onlineWaiting.text = message;
            onlineWaiting.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnOnlineSeatsChanged()
        {
            var game = tableController == null ? null : tableController.Game;
            if (game != null)
            {
                RefreshSeats(game.State);
            }
        }

        private void OnOnlineNotice(string message)
        {
            if (onlineNotice == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            pendingOnlineNotices.Enqueue(message);
            ShowNextOnlineNotice();
        }

        private void ShowNextOnlineNotice()
        {
            if (onlineNotice == null || onlineNoticeShowing || pendingOnlineNotices.Count == 0)
            {
                return;
            }

            onlineNotice.text = pendingOnlineNotices.Dequeue();
            onlineNoticeShowing = true;
            onlineNotice.AddToClassList("popup-visible");
            onlineNoticeHide = onlineNotice.schedule.Execute(() =>
            {
                onlineNotice.RemoveFromClassList("popup-visible");
                onlineNoticeShowing = false;
                ShowNextOnlineNotice();
            }).StartingIn(3200);
        }

        private void UpdateActiveTurnPulse()
        {
            if (bottomBar == null)
            {
                return;
            }

            if (!bottomBar.ClassListContains("active-turn"))
            {
                if (activeTurnPulsing)
                {
                    activeTurnPulsing = false;
                    SetBottomBarBorderColor(new StyleColor(StyleKeyword.Null));
                }

                return;
            }

            activeTurnPulsing = true;
            var wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / ActiveTurnPulsePeriod);
            var color = ActiveTurnOutlineColor;
            color.a = Mathf.Lerp(ActiveTurnPulseMinAlpha, 1f, wave);
            SetBottomBarBorderColor(new StyleColor(color));
        }

        private void SetBottomBarBorderColor(StyleColor color)
        {
            bottomBar.style.borderTopColor = color;
            bottomBar.style.borderBottomColor = color;
            bottomBar.style.borderLeftColor = color;
            bottomBar.style.borderRightColor = color;
        }

        public static bool IsPhoneScreen(bool isMobilePlatform, int width, int height, float dpi)
        {
            var shortSide = Mathf.Min(width, height);
            var longSide = Mathf.Max(width, height);
            if (shortSide <= 0)
            {
                return false;
            }

            // Phone-shaped screens (about 19.5:9) count as phones on any platform, so Editor
            // Game view resolutions such as 1320x2868 get the phone layout too.
            if ((float)longSide / shortSide >= PhoneMinAspectRatio)
            {
                return true;
            }

            if (!isMobilePlatform)
            {
                return false;
            }

            return dpi > 0f ? shortSide / dpi < PhoneMaxShortSideInches : shortSide < PhoneMaxShortSidePixels;
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
            compactBoardButton = root.Q<Button>("compact-board-button");
            hintButton = root.Q<Button>("hint-button");
            pauseButton = root.Q<Button>("pause-button");
            menuButton = root.Q<Button>("menu-button");
            helpButton = root.Q<Button>("help-button");
            turnPopup = root.Q<Label>("turn-popup");
            halasPopup = root.Q<Label>("halas-popup");
            goneDownPopup = root.Q<Label>("gone-down-popup");
            goneDownTag = root.Q<Label>("gone-down-tag");
            invalidMovePopup = root.Q<Label>("invalid-move-popup");
            onlineNotice = root.Q<Label>("online-notice");
            onlineWaiting = root.Q<Label>("online-waiting");
            confirmDialog = root.Q<VisualElement>("confirm-dialog");
            confirmMessage = root.Q<Label>("confirm-message");
            confirmDontAsk = root.Q<Toggle>("confirm-dont-ask");
            pausePanel = root.Q<VisualElement>("pause-panel");
            settingsPanel = root.Q<VisualElement>("settings-panel");
            helpPanel = root.Q<VisualElement>("help-panel");
            bottomBar = root.Q<VisualElement>("bottom-bar");
            opponentsStrip = root.Q<VisualElement>("opponents-strip");
            fastForwardDock = root.Q<VisualElement>("fast-forward-dock");
            statusPanel = root.Q<VisualElement>("status-panel");
            turnPanel = root.Q<VisualElement>("turn-panel");
            markerLayer = root.Q<VisualElement>("marker-layer");
            fastForwardButton = root.Q<Button>("fast-forward-button");
            markSetButton = root.Q<Button>("mark-set-button");
            skipTutorialButton = root.Q<Button>("skip-tutorial-button");
            roomCodeLabel = root.Q<Label>("room-code-label");
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
            if (compactBoardButton != null) compactBoardButton.clicked += CompactBoard;
            if (hintButton != null) hintButton.clicked += ToggleHints;
            if (fastForwardButton != null) fastForwardButton.clicked += ToggleFastForward;
            if (markSetButton != null) markSetButton.clicked += ToggleMarkMode;
            if (pauseButton != null) pauseButton.clicked += TogglePause;
            if (menuButton != null) menuButton.clicked += RequestLeave;
            if (helpButton != null) helpButton.clicked += ToggleHelp;
            if (skipTutorialButton != null) skipTutorialButton.clicked += RequestSkipTutorial;
            root.Q<Button>("confirm-cancel-button").clicked += CancelConfirmation;
            root.Q<Button>("confirm-accept-button").clicked += AcceptConfirmation;
            root.Q<Button>("resume-button").clicked += Resume;
            root.Q<Button>("restart-button").clicked += RequestRestart;
            root.Q<Button>("settings-button").clicked += OpenSettings;
            root.Q<Button>("help-close-button").clicked += ToggleHelp;
        }

        private void UpdateHandBottomInset()
        {
            if (tableView == null || root == null || root.worldBound.height <= 0f)
            {
                tableView?.SetHandBottomInset(0f);
                return;
            }

            var insetY = root.worldBound.yMax;
            var hasBottomBar = TryGetVisibleBounds(bottomBar, out var bottomBounds);
            if (hasBottomBar)
            {
                insetY = Mathf.Min(insetY, bottomBounds.yMin);
            }

            var hasStatusPanel = TryGetVisibleBounds(statusPanel, out var statusBounds);
            if (hasStatusPanel)
            {
                insetY = Mathf.Min(insetY, statusBounds.yMin);
            }

            if (!hasBottomBar && !hasStatusPanel)
            {
                tableView.SetHandBottomInset(0f);
                return;
            }

            var fraction = Mathf.Clamp01((root.worldBound.yMax - insetY - HandBottomBarFitCompensationPixels) /
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
            if (TryGetVisibleBounds(turnPanel, out var turnBounds))
            {
                top = Mathf.Max(top, (turnBounds.yMax - rootBounds.yMin) / rootBounds.height);
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

            var phoneLayout = root != null && root.ClassListContains("phone-layout");
            var target = phoneLayout ? fastForwardDock : bottomBar;
            if (target != null && fastForwardButton.parent != target)
            {
                target.Add(fastForwardButton);
            }

            var visible = GameSettings.ShowFastForward &&
                          (tableController != null && tableController.IsAutoFixing ||
                           matchFlow != null && matchFlow.BotDriver != null && matchFlow.BotDriver.IsPlaying);
            fastForwardButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (fastForwardDock != null)
            {
                fastForwardDock.style.display = phoneLayout && visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (phoneLayout && root != null && bottomBar != null && bottomBar.worldBound.height > 0f)
                {
                    fastForwardDock.style.bottom = root.worldBound.yMax - bottomBar.worldBound.yMin + 8f;
                }
            }

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
                card.style.left = (48f - 22f) * 0.5f + index * 1.5f;
                card.style.top = (layerCount - 1 - index) * 1.5f;
                if (backSprite != null)
                {
                    card.style.backgroundImage = new StyleBackground(backSprite);
                    card.AddToClassList("skinned");
                }

                drawPileStack.Add(card);
            }
        }

        private VisualElement drawFlightLayer;

        private void UpdateDrawFlightOverlay()
        {
            if (root == null || root.panel == null || tableView == null || tableView.ViewCamera == null)
            {
                return;
            }

            if (drawFlightLayer == null || drawFlightLayer.parent != root)
            {
                drawFlightLayer = new VisualElement();
                drawFlightLayer.name = "draw-flight-layer";
                drawFlightLayer.pickingMode = PickingMode.Ignore;
                drawFlightLayer.style.position = Position.Absolute;
                drawFlightLayer.style.left = 0f;
                drawFlightLayer.style.top = 0f;
                drawFlightLayer.style.right = 0f;
                drawFlightLayer.style.bottom = 0f;
                root.Add(drawFlightLayer);
            }

            drawFlightLayer.BringToFront();
            var cards = tableView.DrawingCards;
            var visibleCount = 0;
            for (var index = 0; index < cards.Count; index++)
            {
                var view = cards[index];
                if (view == null || view.Renderer == null || view.Renderer.sprite == null)
                {
                    continue;
                }

                var bounds = view.Renderer.bounds;
                var camera = tableView.ViewCamera;
                var topLeft = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel,
                    new Vector3(bounds.min.x, bounds.max.y, bounds.center.z), camera);
                var bottomRight = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel,
                    new Vector3(bounds.max.x, bounds.min.y, bounds.center.z), camera);
                VisualElement element;
                if (visibleCount < drawFlightLayer.childCount)
                {
                    element = drawFlightLayer[visibleCount];
                }
                else
                {
                    element = new VisualElement();
                    element.pickingMode = PickingMode.Ignore;
                    element.name = "draw-flight-card";
                    element.style.position = Position.Absolute;
                    drawFlightLayer.Add(element);
                }

                element.style.backgroundImage = new StyleBackground(view.Renderer.sprite);
                element.style.left = topLeft.x;
                element.style.top = topLeft.y;
                element.style.width = Mathf.Abs(bottomRight.x - topLeft.x);
                element.style.height = Mathf.Abs(bottomRight.y - topLeft.y);
                element.style.display = DisplayStyle.Flex;
                visibleCount++;
            }

            for (var index = visibleCount; index < drawFlightLayer.childCount; index++)
            {
                drawFlightLayer[index].style.display = DisplayStyle.None;
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
                    tableView.SetSeatScreenRect(seat, default, 0f);
                    continue;
                }

                var cards = root.Q<VisualElement>("opponent-cards-" + seat);
                var bounds = cards == null ? default : cards.worldBound;
                if (cards == null)
                {
                    tableView.SetSeatScreenRect(seat, default, 0f);
                    continue;
                }

                var cardCount = state.Hands[seat].Count;
                var incomingCount = tableView.GetIncomingCardCount(seat);
                var visibleCardCount = Mathf.Max(0, cardCount - incomingCount);
                var lastCard = visibleCardCount > 0
                    ? cards.Q<VisualElement>("opponent-card-" + seat + "-" + (visibleCardCount - 1))
                    : null;
                var anchorBounds = lastCard != null
                    ? lastCard.worldBound
                    : visibleCardCount == 0 && cards.parent != null
                        ? cards.parent.worldBound
                        : bounds;
                var anchorHeight = anchorBounds.height;
                if (anchorHeight <= 0f)
                {
                    anchorHeight = 44f * Mathf.Clamp(GameSettings.OpponentCardScale, 0.5f, 2f);
                }

                if (anchorBounds.width > 0f && anchorHeight > 0f &&
                    TryPanelToScreen(anchorBounds.center, out var anchorPosition))
                {
                    var stepSize = 30f * Mathf.Clamp(GameSettings.OpponentCardScale, 0.5f, 2f) * 0.32f *
                                   Screen.height / panelHeight;
                    var vertical = cards.ClassListContains("opponent-cards-vertical");
                    var step = vertical ? new Vector2(0f, -stepSize) : new Vector2(stepSize, 0f);
                    var target = lastCard == null ? anchorPosition : anchorPosition + step;
                    tableView.SetSeatScreenRect(seat, target, anchorHeight * Screen.height / panelHeight, step);
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
            RefreshCurrentGame();
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
                turnPopup.text = Possessive(GetPlayerName(state.CurrentSeat)) + " Turn";
                turnPopupHide = ShowPopup(turnPopup, turnPopupHide,
                    root != null && root.ClassListContains("phone-layout") ? 2600 : 1600);
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
            goneDownTag.style.left = panelPosition.x;
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
            RememberInputRefreshSignature(game, state, turn);
            var activeSeat = state.CurrentSeat;
            var interactive = tableController.InputEnabled;
            if (turnLabel != null)
            {
                turnLabel.text = IsBotSeat(activeSeat)
                    ? GetSeatLabel(activeSeat) + " is thinking…"
                    : Possessive(GetSeatLabel(activeSeat)) + " turn";
            }

            bottomBar?.EnableInClassList("active-turn",
                state.Phase == GamePhase.Playing && !IsBotSeat(activeSeat));

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
                hintButton.SetEnabled(game.State.Phase == GamePhase.Playing);
            }

            if (markSetButton != null)
            {
                markSetButton.text = tableController.MarkModeActive ? "Marking…" : "Mark";
            }

            compactBoardButton?.SetEnabled(GameSettings.OrganicBoard && tableView != null);
            ApplyTutorialButtons();

            RefreshSeats(state);
        }

        // During the tutorial only the button the current step points at stays usable, and it gets a gold glow.
        private void ApplyTutorialButtons()
        {
            var tutorial = matchFlow == null ? null : matchFlow.Tutorial;
            var step = tutorial != null && tutorial.IsRunning ? tutorial.CurrentStep : null;
            ApplyTutorialButton(endTurnButton, TutorialButton.EndTurn, step);
            ApplyTutorialButton(drawPassButton, TutorialButton.Draw, step);
            ApplyTutorialButton(undoButton, TutorialButton.Undo, step);
            ApplyTutorialButton(redoButton, TutorialButton.Redo, step);
            if (step != null)
            {
                revertButton?.SetEnabled(false);
                fixBoardButton?.SetEnabled(false);
            }

            markSetButton?.SetEnabled(step == null);
            if (hintButton != null && matchFlow != null && matchFlow.IsTutorial)
            {
                hintButton.SetEnabled(false);
            }
        }

        private static void ApplyTutorialButton(Button button, TutorialButton kind, TutorialStep step)
        {
            if (button == null)
            {
                return;
            }

            if (step == null)
            {
                button.RemoveFromClassList("tutorial-focus");
                return;
            }

            var focused = step.Target.Kind == TutorialTargetKind.Button && step.Target.Button == kind;
            if (!focused)
            {
                button.SetEnabled(false);
            }

            button.EnableInClassList("tutorial-focus", focused && button.enabledSelf);
        }

        private void RefreshIfInputStateChanged()
        {
            if (tableController == null || tableController.Game == null)
            {
                return;
            }

            var game = tableController.Game;
            var state = game.State;
            var turn = game.Turn;
            var moves = turn == null ? 0 : turn.Moves.Count;
            if (hasInputRefreshSignature && lastInputRefreshGame == game &&
                lastInputRefreshTurn == state.TurnNumber && lastInputRefreshMoves == moves &&
                lastInputRefreshPhase == state.Phase && lastInputRefreshEnabled == tableController.InputEnabled &&
                lastInputRefreshHandEnabled == tableController.HandInputEnabled)
            {
                return;
            }

            Refresh(state, turn);
        }

        private void RememberInputRefreshSignature(MejnunGame game, GameState state, TurnSession turn)
        {
            lastInputRefreshGame = game;
            lastInputRefreshTurn = state.TurnNumber;
            lastInputRefreshMoves = turn == null ? 0 : turn.Moves.Count;
            lastInputRefreshPhase = state.Phase;
            lastInputRefreshEnabled = tableController.InputEnabled;
            lastInputRefreshHandEnabled = tableController.HandInputEnabled;
            hasInputRefreshSignature = true;
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
                var opponentSide = GetOpponentSide(relativeSeat, state.Hands.Count - 1,
                    root != null && root.ClassListContains("phone-layout") && Screen.height > Screen.width);
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
                var incomingCount = tableView == null ? 0 : tableView.GetIncomingCardCount(seat);
                var visibleCardCount = Mathf.Max(0, state.Hands[seat].Count - incomingCount);
                var cardScale = Mathf.Clamp(GameSettings.OpponentCardScale, 0.5f, 2f);
                var cardWidth = 30f * cardScale;
                var cardHeight = 44f * cardScale;
                for (var cardIndex = 0; cardIndex < state.Hands[seat].Count; cardIndex++)
                {
                    var cardBack = new VisualElement();
                    cardBack.pickingMode = PickingMode.Ignore;
                    cardBack.AddToClassList("opponent-card-back");
                    cardBack.name = "opponent-card-" + seat + "-" + cardIndex;
                    if (cardIndex >= visibleCardCount)
                    {
                        cardBack.style.visibility = Visibility.Hidden;
                    }

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

                if (visibleCardCount == 1)
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
            return GetOpponentSide(relativeSeat, opponentCount, false);
        }

        public static OpponentSide GetOpponentSide(int relativeSeat, int opponentCount, bool tallLayout)
        {
            if (opponentCount <= 1 || relativeSeat < 1 || relativeSeat > opponentCount)
            {
                return OpponentSide.Top;
            }

            if (tallLayout)
            {
                if (opponentCount == 3)
                {
                    return relativeSeat == 2 ? OpponentSide.Top :
                        relativeSeat == 1 ? OpponentSide.Left : OpponentSide.Right;
                }

                if (opponentCount == 4)
                {
                    return relativeSeat <= 2 ? OpponentSide.Left : OpponentSide.Right;
                }

                if (opponentCount == 5)
                {
                    return relativeSeat <= 2 ? OpponentSide.Left :
                        relativeSeat == 3 ? OpponentSide.Top : OpponentSide.Right;
                }
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
            if (timerLabel == null)
            {
                return;
            }

            if (matchFlow == null || matchFlow.TurnTimer == null || matchFlow.TurnTimer.DurationSeconds <= 0 ||
                !matchFlow.TurnTimer.IsRunning)
            {
                timerLabel.text = string.Empty;
                timerLabel.style.display = DisplayStyle.None;
                return;
            }

            timerLabel.text = "Time: " + Mathf.CeilToInt(matchFlow.TurnTimer.RemainingSeconds);
            timerLabel.style.display = DisplayStyle.Flex;
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

        /// <summary>"You" becomes "Your" (tutorial player); any other name gets "'s".</summary>
        private static string Possessive(string name)
        {
            return name == "You" ? "Your" : name + "'s";
        }

        private string GetSeatLabel(int seat)
        {
            return matchFlow != null && matchFlow.SeatCount > 0 ? matchFlow.GetSeatLabel(seat) : "Seat " + (seat + 1);
        }

        private void ToggleHints()
        {
            if (matchFlow != null && matchFlow.IsTutorial)
            {
                return;
            }

            if (tableController != null)
            {
                tableController.SetMarkMode(false);
                tableController.SetHintsEnabled(!tableController.HintsEnabled);
                if (hintButton != null)
                {
                    hintButton.text = tableController.HintsEnabled ? "Hint: on" : "Hint: off";
                }
            }
        }

        private void EndTurn()
        {
            tableController?.SetMarkMode(false);
            if (tableController != null && tableController.InputEnabled)
            {
                var result = tableController.ApplyCommand(GameCommand.EndTurn(tableController.Game.State.CurrentSeat));
                if (!result.Ok)
                {
                    ShowInvalidMove(result.Error);
                }
            }
        }

        private void DrawOrPass()
        {
            tableController?.SetMarkMode(false);
            if (tableController == null || !tableController.InputEnabled || tableController.HasPlacedHandCard)
            {
                return;
            }

            var result = tableController.ApplyCommand(GameCommand.DrawOrPass(tableController.Game.State.CurrentSeat));
            if (!result.Ok)
            {
                ShowInvalidMove(result.Error);
            }
        }

        private void CompactBoard()
        {
            tableView?.CompactBoard();
        }

        private void OnInvalidGesture(RuleError error)
        {
            ShowInvalidMove(error);
        }

        private void OnIncomingCardsChanged()
        {
            RefreshCurrentGame();
        }

        private void ShowInvalidMove(RuleError error)
        {
            if (invalidMovePopup == null)
            {
                return;
            }

            invalidMovePopup.text = InvalidMoveMessage(error);
            invalidMoveHide = ShowPopup(invalidMovePopup, invalidMoveHide, 2400);
        }

        private static string InvalidMoveMessage(RuleError error)
        {
            switch (error)
            {
                case RuleError.NotYourTurn:
                    return "You can only move cards on your turn.";
                case RuleError.WrongPhase:
                    return "That move is not available right now.";
                case RuleError.NotDownBoardAccess:
                    return "Go down before using cards from the board.";
                case RuleError.BoardCardToHand:
                    return "Cards on the board cannot be returned to your hand.";
                case RuleError.CannotEndTurn:
                    return "You must play a valid move before ending your turn.";
                case RuleError.GoDownInvalid:
                case RuleError.GoDownCardToHand:
                    return "That set does not meet the requirements to go down.";
                case RuleError.DoubleInSet:
                    return "A set cannot contain the same card twice.";
                case RuleError.SetTooLarge:
                    return "That set contains too many cards.";
                case RuleError.InvalidCardForSet:
                    return "That card cannot be added to this set.";
                case RuleError.InvalidTarget:
                    return "Choose a valid place for that card.";
                case RuleError.DuplicateCard:
                    return "That move includes a card more than once.";
                case RuleError.EmptyMove:
                    return "Select at least one card to move.";
                case RuleError.UnknownCard:
                case RuleError.UnknownMeld:
                    return "That card or set is no longer available.";
                case RuleError.GameFinished:
                    return "This game has already ended.";
                case RuleError.TutorialBlocked:
                    return "Try the highlighted cards.";
                default:
                    return "That move is not allowed by the game rules.";
            }
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
            if (confirmDontAsk != null)
            {
                confirmDontAsk.SetValueWithoutNotify(false);
                confirmDontAsk.style.display = setEnabled == null ? DisplayStyle.None : DisplayStyle.Flex;
            }
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
            SetVisible(skipTutorialButton, matchFlow != null && matchFlow.Tutorial != null &&
                                           matchFlow.Tutorial.IsRunning);
            RefreshRoomCode();
        }

        private void RefreshRoomCode()
        {
            var roomCode = matchFlow != null && matchFlow.Mode == MatchMode.Online
                ? matchFlow.OnlineSessionCode
                : null;
            if (roomCodeLabel != null)
            {
                roomCodeLabel.text = string.IsNullOrWhiteSpace(roomCode) ? string.Empty : "Room code: " + roomCode;
            }

            SetVisible(roomCodeLabel, !string.IsNullOrWhiteSpace(roomCode));
        }

        private void RequestSkipTutorial()
        {
            if (matchFlow == null || matchFlow.Tutorial == null || !matchFlow.Tutorial.IsRunning)
            {
                return;
            }

            if (pausePanel != null && pausePanel.resolvedStyle.display == DisplayStyle.Flex)
            {
                Resume();
            }

            RequestConfirmation("Skip the tutorial? You can replay it any time from How to play.", () => true, null,
                () => matchFlow.SkipTutorial());
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
            if (settingsPanelBinder != null)
            {
                settingsPanelBinder.Show();
            }
            else
            {
                SetVisible(settingsPanel, true);
            }
        }

        private void CloseSettings()
        {
            if (settingsPanelBinder != null)
            {
                settingsPanelBinder.Hide();
            }
            else
            {
                SetVisible(settingsPanel, false);
            }
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
            RequestConfirmation("Return to the main menu? This game will be saved so you can resume it later.",
                () => GameSettings.ConfirmLeave, value => GameSettings.ConfirmLeave = value,
                () =>
                {
                    Resume();
                    matchFlow.ReturnToMenu();
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
            var keyboard = Keyboard.current;
            var focusedElement = root == null || root.panel == null
                ? null
                : root.panel.focusController.focusedElement as VisualElement;
            keyboardBackGuard.Track(focusedElement, TouchScreenKeyboard.visible, Time.unscaledTime);
            if (keyboardBackGuard.ShouldSuppressBack(Time.unscaledTime))
            {
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    BlurFocusedElement();
                }

                return;
            }

            if (keyboard == null || IsTextFieldFocused())
            {
                return;
            }

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