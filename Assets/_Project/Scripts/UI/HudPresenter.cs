using System;
using System.Collections.Generic;
using Mejnun.Cosmetics;
using Mejnun.Core;
using Mejnun.Game;
using Mejnun.Localization;
using Mejnun.Net.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Mejnun.UI
{
    public sealed class HudPresenter : MonoBehaviour
    {
        private const float HandBottomBarFitCompensationPixels = 8f;
        private const float TableExitDuration = 0.7f;
        private const int OnlineWaitingFadeMilliseconds = 180;
        private const float TableEdgeOffscreenMargin = 24f;
        private const float OpponentHandTuckDuration = 0.25f;
        private const float HurryUpDelaySeconds = 20f;
        private const float HurryUpCooldownSeconds = 20f;

        private enum TableEdge
        {
            Top,
            Bottom,
            Left,
            Right
        }

        private sealed class TableEdgeOffset
        {
            public VisualElement Element;
            public TableEdge Edge;
            public Vector2 Offset;
            public Vector2 AppliedOffset;
            public float StartedAt;
            public bool HasEdge;
        }

        private sealed class OpponentHandTuckState
        {
            public float From;
            public float To;
            public float StartedAt;
            public Vector2 LastTuckedOffset;
        }

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
        private Button hurryUpButton;
        private Button markSetButton;
        private Button pauseButton;
        private Button menuButton;
        private Button helpButton;
        private Button skipTutorialButton;
        private TutorialOverlay tutorialOverlay;
        private OnlineWaitingOverlayPresenter onlineWaitingOverlay;
        private Label turnPopup;
        private Label halasPopup;
        private Label hurryUpPopup;
        private Label timerLabel;
        private Label goneDownPopup;
        private Label goodMovePopup;
        private Label goneDownTag;
        private Label invalidMovePopup;
        private Label onlineNotice;
        private Label onlineWaiting;
        private readonly Queue<string> pendingOnlineNotices = new Queue<string>();
        private bool onlineNoticeShowing;
        private IVisualElementScheduledItem turnPopupHide;
        private IVisualElementScheduledItem goneDownPopupHide;
        private IVisualElementScheduledItem goodMovePopupHide;
        private IVisualElementScheduledItem goneDownTagHide;
        private IVisualElementScheduledItem halasHide;
        private IVisualElementScheduledItem hurryUpPopupHide;
        private IVisualElementScheduledItem invalidMoveHide;
        private readonly List<TableEdgeOffset> tableEdgeAnimations = new List<TableEdgeOffset>();
        private readonly Dictionary<int, OpponentHandTuckState> opponentHandTuckStates =
            new Dictionary<int, OpponentHandTuckState>();
        private readonly List<IVisualElementScheduledItem> opponentHandTuckAnimations =
            new List<IVisualElementScheduledItem>();
        private List<TableEdgeOffset> tableExitTargets;
        private float tableExitStartedAt;
        private bool tableExitAnimating;
        private Action tableExitCompleted;
        private IVisualElementScheduledItem onlineNoticeHide;
        private IVisualElementScheduledItem onlineWaitingHide;
        private IVisualElementScheduledItem onlineWaitingShow;
        private bool onlineWaitingVisible;
        private bool suppressOnlineWaiting;
        private IReadOnlyList<int> revealedDownMeldIds;
        private int revealedDownSeat = -1;
        private int revealedDownTurnNumber = -1;
        private ulong announcedSeed;
        private int announcedTurnNumber = -1;
        private bool hasAnnouncedTurn;
        private bool goneDownTagShown;
        private VisualElement confirmDialog;
        private Label confirmMessage;
        private Toggle confirmDontAsk;
        private VisualElement pausePanel;
        private Label roomCodeLabel;
        private Label pauseMatchTimeLabel;
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
        private bool lastSafeAreaMirrored;
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
        private Func<string> pendingConfirmMessage;
        private RuleError? lastInvalidMoveError;
        private string lastInvalidFlavorKey;
        private string lastGoodMoveFlavorKey;
        private int goodMoveMeldId = -1;
        private int lastMoveFlavorFrame = -1;
        private int announcedSeat = -1;
        private float nextBadgePulse;
        private float nextHurryUpRequestAt;
        private int hurryUpTurnNumber = -1;
        private int hurryUpTurnSeat = -1;
        private float hurryUpTurnStartedAt;
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
        private UiDirection uiDirection;

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
            ApplyDefaultText();
        }

        /// <summary>Text for the code-owned HUD labels before the first game sync (the UXML leaves them empty).</summary>
        private void ApplyDefaultText()
        {
            if (turnLabel != null) turnLabel.text = TurnLabelText(0);
            if (drawPileLabel != null) drawPileLabel.text = Loc.Format("hud.draw_pile.count", 0);
            if (drawPassButton != null) drawPassButton.text = Loc.Get("hud.button.draw");
            if (markSetButton != null)
            {
                markSetButton.text = tableController != null && tableController.MarkModeActive
                    ? Loc.Get("hud.button.marking")
                    : Loc.Get("hud.button.mark");
            }

            if (hintButton != null)
            {
                hintButton.text = tableController != null && tableController.HintsEnabled
                    ? Loc.Get("hud.button.hint_on")
                    : Loc.Get("hud.button.hint_off");
            }

            if (fastForwardButton != null)
            {
                fastForwardButton.text = matchFlow != null && matchFlow.FastForward
                    ? Loc.Get("hud.button.fast_forward_stop")
                    : Loc.Get("hud.button.fast_forward");
            }

            if (hurryUpButton != null)
            {
                hurryUpButton.text = Loc.Get("hud.button.hurry_up");
            }
        }

        private void OnLanguageChanged()
        {
            ApplyDefaultText();
            var game = tableController == null ? null : tableController.Game;
            if (game != null)
            {
                Refresh(game.State, game.Turn);
            }

            if (turnPopup != null && announcedSeat >= 0)
            {
                turnPopup.text = TurnPopupText(announcedSeat);
            }

            if (invalidMovePopup != null && lastInvalidMoveError.HasValue)
            {
                invalidMovePopup.text = InvalidMoveText(lastInvalidMoveError.Value, lastInvalidFlavorKey);
            }

            if (goodMovePopup != null && !string.IsNullOrEmpty(lastGoodMoveFlavorKey))
            {
                goodMovePopup.text = Loc.Get(lastGoodMoveFlavorKey);
            }

            if (confirmMessage != null && pendingConfirmMessage != null)
            {
                confirmMessage.text = pendingConfirmMessage();
            }

            RefreshRoomCode();
            RefreshMatchTime();
            UpdateTimerLabel();
            tutorialOverlay?.RefreshLanguage();
        }

        private void Start()
        {
            tutorialOverlay?.Bind(matchFlow == null ? null : matchFlow.Tutorial);
            TryBindOnlineWaitingOverlay();
            if (SceneFlow.TryTakeTableIntro())
            {
                if (root != null && root.worldBound.width > 0f && root.worldBound.height > 0f)
                {
                    PlayTableIntro();
                }
                else if (root != null)
                {
                    root.RegisterCallback<GeometryChangedEvent>(OnTableIntroGeometryChanged);
                }
            }
        }

        private void OnTableIntroGeometryChanged(GeometryChangedEvent evt)
        {
            root.UnregisterCallback<GeometryChangedEvent>(OnTableIntroGeometryChanged);
            PlayTableIntro();
        }

        private void PlayTableIntro()
        {
            suppressOnlineWaiting = false;
            var offsets = TableEdgeOffsets();
            for (var index = 0; index < offsets.Count; index++)
            {
                SlideFromEdge(offsets[index]);
            }

        }

        private List<TableEdgeOffset> TableEdgeOffsets()
        {
            var offsets = new List<TableEdgeOffset>();
            if (root == null)
            {
                return offsets;
            }

            var bounds = root.worldBound;
            AddTopEdgeOffset(offsets, turnPanel);
            AddTopEdgeOffset(offsets, pauseButton);
            AddBottomEdgeOffset(offsets, root.Q<VisualElement>(className: "bottom-area"));
            AddNearestEdgeOffset(offsets, fastForwardDock, bounds);
            AddNearestEdgeOffset(offsets, leftOpponents, bounds);
            AddNearestEdgeOffset(offsets, rightOpponents, bounds);
            AddNearestEdgeOffset(offsets, topOpponents, bounds);
            AddNearestEdgeOffset(offsets, root.Q<VisualElement>(className: "tutorial-layer"), bounds);
            return offsets;
        }

        private static void AddNearestEdgeOffset(List<TableEdgeOffset> offsets, VisualElement element, Rect bounds)
        {
            if (element == null || element.panel == null)
            {
                return;
            }

            if (!TryFindNearestEdge(element, bounds, out var edge))
            {
                offsets.Add(new TableEdgeOffset { Element = element, StartedAt = Time.unscaledTime });
                return;
            }

            AddEdgeOffset(offsets, element, edge);
        }

        private static bool TryFindNearestEdge(VisualElement element, Rect bounds, out TableEdge edge)
        {
            edge = TableEdge.Top;
            var elementBounds = element == null ? default : element.worldBound;
            if (!IsFinite(bounds.xMin) || !IsFinite(bounds.yMin) || !IsFinite(bounds.width) ||
                !IsFinite(bounds.height) || bounds.width <= 0f || bounds.height <= 0f ||
                !IsFinite(elementBounds.xMin) || !IsFinite(elementBounds.yMin) ||
                !IsFinite(elementBounds.width) || !IsFinite(elementBounds.height) || elementBounds.width <= 0f ||
                elementBounds.height <= 0f)
            {
                return false;
            }

            var leftDistance = elementBounds.xMin - bounds.xMin;
            var rightDistance = bounds.xMax - elementBounds.xMax;
            var topDistance = elementBounds.yMin - bounds.yMin;
            var bottomDistance = bounds.yMax - elementBounds.yMax;
            var closest = Mathf.Min(leftDistance, rightDistance, topDistance, bottomDistance);
            if (Mathf.Approximately(closest, topDistance))
            {
                edge = TableEdge.Top;
            }
            else if (Mathf.Approximately(closest, bottomDistance))
            {
                edge = TableEdge.Bottom;
            }
            else if (Mathf.Approximately(closest, leftDistance))
            {
                edge = TableEdge.Left;
            }
            else
            {
                edge = TableEdge.Right;
            }

            return true;
        }

        private static void AddTopEdgeOffset(List<TableEdgeOffset> offsets, VisualElement element)
        {
            if (element == null || element.panel == null)
            {
                return;
            }

            AddEdgeOffset(offsets, element, TableEdge.Top);
        }

        private static void AddBottomEdgeOffset(List<TableEdgeOffset> offsets, VisualElement element)
        {
            if (element == null || element.panel == null)
            {
                return;
            }

            AddEdgeOffset(offsets, element, TableEdge.Bottom);
        }

        private static void AddEdgeOffset(List<TableEdgeOffset> offsets, VisualElement element, TableEdge edge)
        {
            if (element == null || element.panel == null)
            {
                return;
            }

            TryCalculateEdgeOffset(element, edge, Vector2.zero, out var offset);
            offsets.Add(new TableEdgeOffset { Element = element, Edge = edge, Offset = offset, HasEdge = true });
        }

        private static bool TryCalculateEdgeOffset(VisualElement element, TableEdge edge, Vector2 appliedOffset,
            out Vector2 offset)
        {
            offset = default;
            if (element == null || element.panel == null)
            {
                return false;
            }

            var rootBounds = element.panel.visualTree.worldBound;
            var currentBounds = element.worldBound;
            var elementBounds = new Rect(currentBounds.xMin - appliedOffset.x, currentBounds.yMin - appliedOffset.y,
                currentBounds.width, currentBounds.height);
            if (!IsFinite(rootBounds.xMin) || !IsFinite(rootBounds.yMin) || !IsFinite(rootBounds.width) ||
                !IsFinite(rootBounds.height) || rootBounds.width <= 0f || rootBounds.height <= 0f ||
                !IsFinite(elementBounds.xMin) || !IsFinite(elementBounds.yMin) ||
                !IsFinite(elementBounds.width) || !IsFinite(elementBounds.height) || elementBounds.width <= 0f ||
                elementBounds.height <= 0f)
            {
                return false;
            }

            switch (edge)
            {
                case TableEdge.Top:
                    offset = new Vector2(0f, rootBounds.yMin - elementBounds.yMax - TableEdgeOffscreenMargin);
                    break;
                case TableEdge.Bottom:
                    offset = new Vector2(0f, rootBounds.yMax - elementBounds.yMin + TableEdgeOffscreenMargin);
                    break;
                case TableEdge.Left:
                    offset = new Vector2(rootBounds.xMin - elementBounds.xMax - TableEdgeOffscreenMargin, 0f);
                    break;
                default:
                    offset = new Vector2(rootBounds.xMax - elementBounds.xMin + TableEdgeOffscreenMargin, 0f);
                    break;
            }

            return IsFinite(offset.x) && IsFinite(offset.y);
        }

        private void SlideFromEdge(TableEdgeOffset animation)
        {
            var element = animation.Element;
            if (element == null || element.panel == null)
            {
                return;
            }

            for (var index = tableEdgeAnimations.Count - 1; index >= 0; index--)
            {
                if (tableEdgeAnimations[index].Element == element)
                {
                    tableEdgeAnimations.RemoveAt(index);
                }
            }

            element.style.translate = StyleKeyword.Null;
            element.style.opacity = 0f;
            animation.AppliedOffset = Vector2.zero;
            animation.StartedAt = Time.unscaledTime;
            tableEdgeAnimations.Add(animation);
        }

        private void OnEnable()
        {
            uiDirection = UiDirection.Attach(root);
            ApplyHandedness();
            SceneFlow.RegisterTableExit(PlayTableExit);
            root?.RegisterCallback<PointerDownEvent>(OnHudPointerDown, TrickleDown.TrickleDown);
            GameSettings.Changed += OnSettingsChanged;
            Loc.LanguageChanged += OnLanguageChanged;
            if (tableController != null)
            {
                tableController.Synchronized += OnSynchronized;
                tableController.WentDown += OnWentDown;
                tableController.GoodMove += OnGoodMove;
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
                matchFlow.HurryUpReceived += OnHurryUpReceived;
                if (matchFlow.TurnTimer != null)
                {
                    matchFlow.TurnTimer.TimedOut += OnTimedOut;
                }
            }

            if (tableView != null)
            {
                tableView.IncomingCardsChanged += OnIncomingCardsChanged;
                tableView.SeatCardSlotResolver = TryResolveOpponentCardSlot;
            }
        }

        private void OnDisable()
        {
            uiDirection?.Dispose();
            uiDirection = null;
            SceneFlow.UnregisterTableExit(PlayTableExit);
            HideOnlineWaitingImmediately();
            for (var index = 0; index < tableEdgeAnimations.Count; index++)
            {
                var element = tableEdgeAnimations[index].Element;
                if (element != null)
                {
                    element.style.translate = StyleKeyword.Null;
                    element.style.opacity = 1f;
                }
            }

            tableEdgeAnimations.Clear();
            for (var index = 0; index < opponentHandTuckAnimations.Count; index++)
            {
                opponentHandTuckAnimations[index]?.Pause();
            }

            opponentHandTuckAnimations.Clear();
            CompleteTableExit();
            root?.UnregisterCallback<PointerDownEvent>(OnHudPointerDown, TrickleDown.TrickleDown);
            GameSettings.Changed -= OnSettingsChanged;
            Loc.LanguageChanged -= OnLanguageChanged;
            if (tableController != null)
            {
                tableController.Synchronized -= OnSynchronized;
                tableController.WentDown -= OnWentDown;
                tableController.GoodMove -= OnGoodMove;
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
                matchFlow.HurryUpReceived -= OnHurryUpReceived;
                if (matchFlow.TurnTimer != null)
                {
                    matchFlow.TurnTimer.TimedOut -= OnTimedOut;
                }
            }

            if (tableView != null)
            {
                tableView.IncomingCardsChanged -= OnIncomingCardsChanged;
                tableView.SeatCardSlotResolver = null;
            }

            turnPopupHide?.Pause();
            goneDownPopupHide?.Pause();
            goodMovePopupHide?.Pause();
            goneDownTagHide?.Pause();
            halasHide?.Pause();
            hurryUpPopupHide?.Pause();
            hurryUpPopup?.RemoveFromClassList("hurry-up-visible");
            invalidMoveHide?.Pause();
        }

        private void PlayTableExit(Action completed)
        {
            suppressOnlineWaiting = true;
            var collectDelay = tableView == null ? 0f : tableView.CollectBoardToPile();
            FadeOutOnlineWaiting();
            for (var index = 0; index < tableEdgeAnimations.Count; index++)
            {
                var element = tableEdgeAnimations[index].Element;
                if (element != null)
                {
                    element.style.translate = StyleKeyword.Null;
                    element.style.opacity = 1f;
                }
            }

            tableEdgeAnimations.Clear();
            if (root == null || root.panel == null)
            {
                completed?.Invoke();
                return;
            }

            var targets = TableEdgeOffsets();
            if (targets.Count == 0 && collectDelay <= 0f)
            {
                completed?.Invoke();
                return;
            }

            tableExitCompleted = completed;
            tableExitTargets = targets;
            tableExitStartedAt = Time.unscaledTime + collectDelay;
            tableExitAnimating = true;
        }

        private void CompleteTableExit()
        {
            var completed = tableExitCompleted;
            tableExitCompleted = null;
            tableExitAnimating = false;
            tableExitTargets = null;
            completed?.Invoke();
        }

        private void UpdateTableEdgeAnimations()
        {
            for (var index = tableEdgeAnimations.Count - 1; index >= 0; index--)
            {
                var animation = tableEdgeAnimations[index];
                if (animation.Element == null || animation.Element.panel == null)
                {
                    tableEdgeAnimations.RemoveAt(index);
                    continue;
                }

                if (!animation.HasEdge)
                {
                    if (!TryFindNearestEdge(animation.Element, animation.Element.panel.visualTree.worldBound,
                            out var edge))
                    {
                        animation.StartedAt = Time.unscaledTime;
                        animation.Element.style.opacity = 0f;
                        continue;
                    }

                    animation.Edge = edge;
                    animation.HasEdge = true;
                    animation.StartedAt = Time.unscaledTime;
                }

                if (!TryCalculateEdgeOffset(animation.Element, animation.Edge, animation.AppliedOffset,
                        out var offset))
                {
                    animation.StartedAt = Time.unscaledTime;
                    animation.Element.style.opacity = 0f;
                    continue;
                }

                animation.Element.style.opacity = 1f;
                var progress = Mathf.Clamp01((Time.unscaledTime - animation.StartedAt) / 0.7f);
                var eased = progress * progress * (3f - 2f * progress);
                animation.AppliedOffset = offset * (1f - eased);
                animation.Element.style.translate = new Translate(
                    new Length(animation.AppliedOffset.x, LengthUnit.Pixel),
                    new Length(animation.AppliedOffset.y, LengthUnit.Pixel));
                if (progress >= 1f)
                {
                    animation.Element.style.translate = StyleKeyword.Null;
                    animation.Element.style.opacity = 1f;
                    animation.AppliedOffset = Vector2.zero;
                    tableEdgeAnimations.RemoveAt(index);
                }
            }
        }

        private void UpdateTableExitAnimation()
        {
            if (!tableExitAnimating || Time.unscaledTime < tableExitStartedAt)
            {
                return;
            }

            if (root == null || root.panel == null)
            {
                CompleteTableExit();
                return;
            }

            var progress = Mathf.Clamp01((Time.unscaledTime - tableExitStartedAt) / TableExitDuration);
            var eased = progress * progress * (3f - 2f * progress);
            if (tableExitTargets != null)
            {
                for (var index = 0; index < tableExitTargets.Count; index++)
                {
                    var target = tableExitTargets[index];
                    if (target.Element == null || target.Element.panel == null)
                    {
                        continue;
                    }

                    target.Element.style.translate = new Translate(
                        new Length(target.Offset.x * eased, LengthUnit.Pixel),
                        new Length(target.Offset.y * eased, LengthUnit.Pixel));
                }
            }

            if (progress >= 1f)
            {
                CompleteTableExit();
            }
        }

        private void OnDestroy()
        {
            tutorialOverlay?.Unbind();
            onlineWaitingOverlay?.Dispose();
            onlineNoticeHide?.Pause();
            if (settingsPanelBinder != null)
            {
                settingsPanelBinder.Closed -= CloseSettings;
                settingsPanelBinder.Dispose();
            }
        }

        private void Update()
        {
            TryBindOnlineWaitingOverlay();
            UpdateTableEdgeAnimations();
            UpdateTableExitAnimation();
            HandleKeyboardShortcuts();
            if (pausePanel != null && pausePanel.resolvedStyle.display == DisplayStyle.Flex)
            {
                RefreshRoomCode();
                RefreshMatchTime();
            }

            RefreshIfInputStateChanged();
            UpdateDownRevealTag();
            UpdateGoodMovePopupPosition();
            UpdateTimerLabel();
            UpdateLastCardBadges();
            UpdateDrawPilePosition();
            UpdateDrawFlightOverlay();
            if (compactBoardButton != null)
            {
                compactBoardButton.style.display = GameSettings.OrganicBoard ? DisplayStyle.Flex : DisplayStyle.None;
                compactBoardButton.SetEnabled(tableView != null);
            }
            tutorialOverlay?.Tick();
            if (root != null)
            {
                UiScaleUtility.Apply(uiDocument);
                var phoneLayout = DeviceProfile.IsPhoneLayout;
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
                        turnPanel.style.right = StyleKeyword.Null;
                        turnPanel.style.top = StyleKeyword.Null;
                    }

                    if (pauseButton != null)
                    {
                        pauseButton.style.left = StyleKeyword.Null;
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
            var mirrored = root.ClassListContains("rtl") != root.ClassListContains("left-handed");
            if (hasSafeAreaSignature && lastSafeAreaMirrored == mirrored && lastSafeArea == safeArea &&
                lastSafeAreaWidth == Screen.width &&
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
                turnPanel.style.left = mirrored ? StyleKeyword.Null : leftInset + cornerMargin;
                turnPanel.style.right = mirrored ? rightInset + cornerMargin : StyleKeyword.Null;
                turnPanel.style.top = topInset + cornerMargin;
            }

            if (pauseButton != null)
            {
                pauseButton.style.right = mirrored ? StyleKeyword.Null : rightInset + cornerMargin;
                pauseButton.style.left = mirrored ? leftInset + cornerMargin : StyleKeyword.Null;
                pauseButton.style.top = topInset + cornerMargin;
            }

            if (opponentsStrip != null)
            {
                opponentsStrip.style.left = leftInset + cornerMargin;
                opponentsStrip.style.right = rightInset + cornerMargin;
                opponentsStrip.style.top = topInset + bounds.height * 0.15f;
                opponentsStrip.style.bottom = bottomInset + bounds.height * 0.35f;
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
            lastSafeAreaMirrored = mirrored;
            hasSafeAreaSignature = true;
            phoneSafeAreaApplied = true;
        }

        private void UpdateOnlineWaiting()
        {
            if (onlineWaiting == null)
            {
                return;
            }

            if (suppressOnlineWaiting)
            {
                return;
            }

            var message = matchFlow == null ? string.Empty : matchFlow.OnlineWaitingMessage;
            if (!string.IsNullOrEmpty(message))
            {
                onlineWaiting.text = message;
                onlineWaitingHide?.Pause();
                onlineWaitingHide = null;
                onlineWaitingShow?.Pause();
                onlineWaitingShow = null;
                onlineWaiting.style.display = DisplayStyle.Flex;
                if (!onlineWaitingVisible)
                {
                    onlineWaiting.RemoveFromClassList("waiting-visible");
                    onlineWaitingVisible = true;
                    onlineWaitingShow = onlineWaiting.schedule.Execute(() =>
                    {
                        if (matchFlow != null && !string.IsNullOrEmpty(matchFlow.OnlineWaitingMessage))
                        {
                            onlineWaiting.AddToClassList("waiting-visible");
                        }

                        onlineWaitingShow = null;
                    }).StartingIn(0);
                }
                else
                {
                    onlineWaiting.AddToClassList("waiting-visible");
                }

                return;
            }

            FadeOutOnlineWaiting();
        }

        private void TryBindOnlineWaitingOverlay()
        {
            if (onlineWaitingOverlay != null || root == null || matchFlow == null)
            {
                return;
            }

            var bridge = matchFlow.GetComponent<OnlineMatchBridge>();
            if (bridge != null)
            {
                onlineWaitingOverlay = new OnlineWaitingOverlayPresenter(root, bridge, RequestLeave);
            }
        }

        private void FadeOutOnlineWaiting()
        {
            onlineWaitingShow?.Pause();
            onlineWaitingShow = null;
            if (!onlineWaitingVisible)
            {
                return;
            }

            onlineWaitingVisible = false;
            onlineWaiting.RemoveFromClassList("waiting-visible");
            onlineWaitingHide?.Pause();
            onlineWaitingHide = onlineWaiting.schedule.Execute(() =>
            {
                if (!onlineWaiting.ClassListContains("waiting-visible"))
                {
                    onlineWaiting.style.display = DisplayStyle.None;
                }

                onlineWaitingHide = null;
            }).StartingIn(OnlineWaitingFadeMilliseconds);
        }

        private void HideOnlineWaitingImmediately()
        {
            onlineWaitingShow?.Pause();
            onlineWaitingHide?.Pause();
            onlineWaitingShow = null;
            onlineWaitingHide = null;
            onlineWaitingVisible = false;
            if (onlineWaiting == null)
            {
                return;
            }

            onlineWaiting.RemoveFromClassList("waiting-visible");
            onlineWaiting.style.display = DisplayStyle.None;
        }

        private void OnOnlineSeatsChanged()
        {
            var game = tableController == null ? null : tableController.Game;
            if (game != null)
            {
                RefreshSeats(game.State);
            }
        }

        private void OnSettingsChanged(string key)
        {
            if (key == "settings.accessibility.leftHanded")
                ApplyHandedness();

            if ((key == GameSettings.OpponentCardScaleKey || key == GameSettings.HideOpponentHandsKey) &&
                tableController?.Game != null)
            {
                RefreshSeats(tableController.Game.State);
            }
        }

        private void ApplyHandedness()
        {
            root?.EnableInClassList("left-handed", GameSettings.LeftHanded);
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

        private void OnHudPointerDown(PointerDownEvent evt)
        {
            if (matchFlow == null || !matchFlow.IsOnline || tableController == null || tableController.Game == null ||
                matchFlow.IsLocalSeat(tableController.Game.State.CurrentSeat))
            {
                return;
            }

            var target = evt.target as VisualElement;
            if (IsInsideEnabledButton(target))
            {
                return;
            }

            if ((bottomBar != null && (bottomBar.Contains(target) || bottomBar.worldBound.Contains(evt.position))) ||
                (statusPanel != null && (statusPanel.Contains(target) || statusPanel.worldBound.Contains(evt.position))))
            {
                ShowInvalidMove(RuleError.NotYourTurn);
            }
        }

        public static bool IsInsideEnabledButton(VisualElement element)
        {
            while (element != null)
            {
                if (element is Button button && button.enabledInHierarchy)
                {
                    return true;
                }

                element = element.parent;
            }

            return false;
        }

        public static bool IsPhoneScreen(bool isMobilePlatform, int width, int height, float dpi)
        {
            return DeviceProfile.IsPhoneScreen(isMobilePlatform, width, height, dpi);
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
            goodMovePopup = root.Q<Label>("good-move-popup");
            goneDownTag = root.Q<Label>("gone-down-tag");
            invalidMovePopup = root.Q<Label>("invalid-move-popup");
            onlineNotice = root.Q<Label>("online-notice");
            onlineWaiting = root.Q<Label>("online-waiting");
            if (onlineWaiting != null)
            {
                onlineWaiting.RemoveFromClassList("waiting-visible");
                onlineWaiting.style.display = DisplayStyle.None;
            }
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
            hurryUpButton = root.Q<Button>("hurry-up-button");
            hurryUpPopup = root.Q<Label>("hurry-up-popup");
            markSetButton = root.Q<Button>("mark-set-button");
            skipTutorialButton = root.Q<Button>("skip-tutorial-button");
            roomCodeLabel = root.Q<Label>("room-code-label");
            pauseMatchTimeLabel = root.Q<Label>("pause-match-time-label");
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
            if (hurryUpButton != null) hurryUpButton.clicked += RequestHurryUp;
            if (markSetButton != null) markSetButton.clicked += ToggleMarkMode;
            if (pauseButton != null) pauseButton.clicked += TogglePause;
            if (menuButton != null) menuButton.clicked += RequestLeave;
            if (helpButton != null) helpButton.clicked += ToggleHelp;
            if (skipTutorialButton != null) skipTutorialButton.clicked += RequestSkipTutorial;
            root.Q<Button>("confirm-cancel-button").clicked += CancelConfirmation;
            root.Q<Button>("confirm-accept-button").clicked += AcceptConfirmation;
            root.Q<Button>("resume-button").clicked += Resume;
            root.Q<Button>("settings-button").clicked += OpenSettings;
            root.Q<Button>("help-close-button").clicked += ToggleHelp;
        }

        private void UpdateHandBottomInset()
        {
            if (tableView == null || root == null || root.worldBound.height <= 0f)
            {
                tableView?.SetHandBottomInset(0f);
                tableView?.SetEdgePanBottomInset(0f);
                return;
            }

            var insetY = root.worldBound.yMax;
            var hasBottomBar = TryGetVisibleBounds(bottomBar, out var bottomBounds);
            tableView.SetEdgePanBottomInset(hasBottomBar
                ? Mathf.Clamp01((root.worldBound.yMax - bottomBounds.yMin) / root.worldBound.height)
                : 0f);
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
            if (fastForwardButton == null && hurryUpButton == null)
            {
                return;
            }

            var phoneLayout = root != null && root.ClassListContains("phone-layout");
            if (fastForwardDock != null && fastForwardButton != null && fastForwardButton.parent != fastForwardDock)
            {
                fastForwardDock.Add(fastForwardButton);
            }

            if (fastForwardDock != null && hurryUpButton != null && hurryUpButton.parent != fastForwardDock)
            {
                fastForwardDock.Add(hurryUpButton);
            }

            var isOnline = matchFlow != null && matchFlow.IsOnline;
            var hurryUpDelayElapsed = HasHurryUpDelayElapsed();
            var showHurryUp = isOnline && hurryUpDelayElapsed && matchFlow.CanRequestHurryUp;
            var showFastForward = !isOnline && GameSettings.ShowFastForward &&
                                  (tableController != null && tableController.IsAutoFixing ||
                                   matchFlow != null && matchFlow.BotDriver != null && matchFlow.BotDriver.IsPlaying);
            var visible = showHurryUp || showFastForward;
            if (fastForwardButton != null)
            {
                fastForwardButton.style.display = showFastForward ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (hurryUpButton != null)
            {
                hurryUpButton.style.display = showHurryUp ? DisplayStyle.Flex : DisplayStyle.None;
                var cooldownRemaining = Mathf.Max(0f, nextHurryUpRequestAt - Time.unscaledTime);
                hurryUpButton.text = cooldownRemaining > 0f
                    ? Loc.Format("hud.button.hurry_up_cooldown", Mathf.CeilToInt(cooldownRemaining))
                    : Loc.Get("hud.button.hurry_up");
                hurryUpButton.SetEnabled(showHurryUp && cooldownRemaining <= 0f);
            }

            if (fastForwardDock != null)
            {
                fastForwardDock.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (root != null && bottomBar != null && bottomBar.worldBound.height > 0f)
                {
                    StyleLength dockHeight = new StyleLength(bottomBar.worldBound.height);
                    if (phoneLayout)
                    {
                        dockHeight = statusPanel != null && statusPanel.worldBound.height > 0f
                            ? new StyleLength(statusPanel.worldBound.height)
                            : new StyleLength(StyleKeyword.Auto);
                        fastForwardDock.style.left = StyleKeyword.Auto;
                        fastForwardDock.style.right = 12f;
                        fastForwardDock.style.bottom = statusPanel != null && statusPanel.worldBound.height > 0f
                            ? root.worldBound.yMax - statusPanel.worldBound.yMax
                            : root.worldBound.yMax - bottomBar.worldBound.yMin + 8f;
                        fastForwardDock.style.height = dockHeight;
                    }
                    else
                    {
                        fastForwardDock.style.left = bottomBar.worldBound.xMax - root.worldBound.xMin + 8f;
                        fastForwardDock.style.right = StyleKeyword.Auto;
                        var hasStatusPanel = statusPanel != null && statusPanel.worldBound.height > 0f;
                        var alignBounds = hasStatusPanel ? statusPanel.worldBound : bottomBar.worldBound;
                        fastForwardDock.style.bottom = root.worldBound.yMax - alignBounds.yMax;
                        fastForwardDock.style.height = alignBounds.height;
                    }
                }
            }

            if (fastForwardButton != null)
            {
                fastForwardButton.text = matchFlow != null && matchFlow.FastForward
                    ? Loc.Get("hud.button.fast_forward_stop")
                    : Loc.Get("hud.button.fast_forward");
            }
        }

        private bool HasHurryUpDelayElapsed()
        {
            var game = matchFlow == null || !matchFlow.IsOnline ? null : matchFlow.Game;
            if (game == null || game.State.Phase != GamePhase.Playing)
            {
                hurryUpTurnNumber = -1;
                hurryUpTurnSeat = -1;
                return false;
            }

            var turnNumber = game.State.TurnNumber;
            var seat = game.State.CurrentSeat;
            if (hurryUpTurnNumber != turnNumber || hurryUpTurnSeat != seat)
            {
                hurryUpTurnNumber = turnNumber;
                hurryUpTurnSeat = seat;
                hurryUpTurnStartedAt = Time.unscaledTime;
            }

            return Time.unscaledTime - hurryUpTurnStartedAt >= HurryUpDelaySeconds;
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
                    var baseHeight = cards.ClassListContains("opponent-cards-phone-stack") ? 32f : 44f;
                    anchorHeight = baseHeight * Mathf.Clamp(GameSettings.OpponentCardScale, 0.5f, 2f);
                }

                if (anchorBounds.width > 0f && anchorHeight > 0f &&
                    TryPanelToScreen(anchorBounds.center, out var anchorPosition))
                {
                    var compactStack = cards.ClassListContains("opponent-cards-phone-stack");
                    var overlap = compactStack ? 0.18f : 0.32f;
                    var cardHeight = compactStack ? 32f : 30f;
                    var stepSize = cardHeight * Mathf.Clamp(GameSettings.OpponentCardScale, 0.5f, 2f) * overlap *
                                   Screen.height / panelHeight;
                    var vertical = compactStack || cards.ClassListContains("opponent-cards-vertical");
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

        private bool TryResolveOpponentCardSlot(int seat, int cardIndex, out Vector2 screenPosition,
            out float screenHeight)
        {
            screenPosition = default;
            screenHeight = 0f;
            if (root == null || root.panel == null || cardIndex < 0)
            {
                return false;
            }

            var card = root.Q<VisualElement>("opponent-card-" + seat + "-" + cardIndex);
            if (card == null || card.panel != root.panel)
            {
                return false;
            }

            var bounds = card.worldBound;
            var panelHeight = root.panel.visualTree.worldBound.height;
            if (!IsFinite(bounds.xMin) || !IsFinite(bounds.yMin) || !IsFinite(bounds.width) ||
                !IsFinite(bounds.height) || bounds.width <= 0f || bounds.height <= 0f ||
                !IsFinite(panelHeight) || panelHeight <= 0f || !TryPanelToScreen(bounds.center, out screenPosition))
            {
                return false;
            }

            screenHeight = bounds.height * Screen.height / panelHeight;
            return IsFinite(screenHeight) && screenHeight > 0f;
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
                markSetButton.text = active ? Loc.Get("hud.button.marking") : Loc.Get("hud.button.mark");
            }
        }

        private void OnWentDown(int seat)
        {
            if (tableController != null && tableController.InputEnabled &&
                tableController.Game.State.CurrentSeat == seat)
            {
                lastMoveFlavorFrame = Time.frameCount;
                var flavorKey = FlavorLines.PickGood();
                if (string.IsNullOrEmpty(flavorKey))
                {
                    goneDownPopupHide = ShowPopup(goneDownPopup, goneDownPopupHide);
                }
                else
                {
                    var meldId = revealedDownSeat == seat && revealedDownMeldIds != null &&
                                 revealedDownMeldIds.Count > 0
                        ? revealedDownMeldIds[0]
                        : -1;
                    ShowGoodMoveFlavor(flavorKey, meldId);
                }
            }
        }

        private void OnGoodMove(GoodMoveKind kind, int meldId)
        {
            goodMoveMeldId = meldId;
            if (lastMoveFlavorFrame == Time.frameCount)
            {
                UpdateGoodMovePopupPosition();
                return;
            }

            lastMoveFlavorFrame = Time.frameCount;
            var flavorKey = FlavorLines.PickGood();
            if (!string.IsNullOrEmpty(flavorKey))
            {
                ShowGoodMoveFlavor(flavorKey, meldId);
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
            if (hasAnnouncedTurn && announcedSeed == game.Setup.Seed &&
                announcedTurnNumber == state.TurnNumber && announcedSeat == state.CurrentSeat)
            {
                return;
            }

            announcedSeed = game.Setup.Seed;
            announcedTurnNumber = state.TurnNumber;
            announcedSeat = state.CurrentSeat;
            hasAnnouncedTurn = true;
            if (turnPopup != null)
            {
                turnPopup.text = TurnPopupText(state.CurrentSeat);
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
            goneDownTag.text = Loc.Format("hud.popup.gone_down_tag", GetPlayerName(revealedDownSeat));
            goneDownTag.style.left = panelPosition.x;
            goneDownTag.style.top = panelPosition.y - 36f;
            if (!goneDownTagShown)
            {
                goneDownTagShown = true;
                goneDownTag.AddToClassList("popup-visible");
                goneDownTagHide = goneDownTag.schedule.Execute(ClearDownRevealTag).StartingIn(3000);
            }
        }

        private void UpdateGoodMovePopupPosition()
        {
            if (goodMovePopup == null)
            {
                return;
            }

            var popupParent = goodMovePopup.parent;
            FitGoodMovePopupToText(popupParent);
            if (goodMoveMeldId < 0 || root == null || root.panel == null || popupParent == null ||
                tableView == null || tableView.ViewCamera == null ||
                !tableView.TryGetMeldAnchor(goodMoveMeldId, out var world))
            {
                goodMovePopup.EnableInClassList("good-move-popup--centered", true);
                goodMovePopup.style.left = StyleKeyword.Null;
                goodMovePopup.style.top = StyleKeyword.Null;
                return;
            }

            var panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, tableView.ViewCamera);
            var localPosition = popupParent.WorldToLocal(panelPosition);
            var parentWidth = popupParent.contentRect.width;
            if (!IsFinite(localPosition.x) || !IsFinite(localPosition.y) || !IsFinite(parentWidth) || parentWidth <= 0f)
            {
                goodMovePopup.EnableInClassList("good-move-popup--centered", true);
                goodMovePopup.style.left = StyleKeyword.Null;
                goodMovePopup.style.top = StyleKeyword.Null;
                return;
            }

            var popupWidth = goodMovePopup.resolvedStyle.width;
            if (!IsFinite(popupWidth) || popupWidth <= 0f)
            {
                popupWidth = goodMovePopup.layout.width;
            }

            var halfWidth = IsFinite(popupWidth) && popupWidth > 0f ? popupWidth * 0.5f : 0f;
            const float edgeMargin = 8f;
            var minX = halfWidth + edgeMargin;
            var maxX = parentWidth - halfWidth - edgeMargin;
            var left = minX <= maxX ? Mathf.Clamp(localPosition.x, minX, maxX) : parentWidth * 0.5f;

            goodMovePopup.EnableInClassList("good-move-popup--centered", false);
            goodMovePopup.style.left = left;
            goodMovePopup.style.top = localPosition.y - 16f;
        }

        // An absolutely positioned auto-width label keeps its single-line height when max-width wraps the text,
        // so size the panel explicitly from the wrapped text measurement.
        private void FitGoodMovePopupToText(VisualElement popupParent)
        {
            var parentWidth = popupParent != null ? popupParent.contentRect.width : float.NaN;
            if (!IsFinite(parentWidth) || parentWidth <= 0f || string.IsNullOrEmpty(goodMovePopup.text))
            {
                goodMovePopup.style.width = StyleKeyword.Null;
                goodMovePopup.style.height = StyleKeyword.Null;
                return;
            }

            var resolved = goodMovePopup.resolvedStyle;
            var horizontalPadding = resolved.paddingLeft + resolved.paddingRight +
                                    resolved.borderLeftWidth + resolved.borderRightWidth;
            var verticalPadding = resolved.paddingTop + resolved.paddingBottom +
                                  resolved.borderTopWidth + resolved.borderBottomWidth;
            var maxContentWidth = Mathf.Max(1f, parentWidth * 0.6f - horizontalPadding);
            var natural = goodMovePopup.MeasureTextSize(goodMovePopup.text, 0f, VisualElement.MeasureMode.Undefined,
                0f, VisualElement.MeasureMode.Undefined);
            var contentWidth = IsFinite(natural.x) && natural.x > 0f
                ? Mathf.Min(Mathf.Ceil(natural.x) + 1f, maxContentWidth)
                : maxContentWidth;
            var wrapped = goodMovePopup.MeasureTextSize(goodMovePopup.text, contentWidth,
                VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined);
            if (!IsFinite(wrapped.y) || wrapped.y <= 0f)
            {
                return;
            }

            var width = contentWidth + horizontalPadding;
            var height = Mathf.Ceil(wrapped.y) + verticalPadding;
            if (!Mathf.Approximately(goodMovePopup.resolvedStyle.width, width))
            {
                goodMovePopup.style.width = width;
            }

            if (!Mathf.Approximately(goodMovePopup.resolvedStyle.height, height))
            {
                goodMovePopup.style.height = height;
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

            if (result?.Winner.HasValue == true && IsLocalWinner(result.Winner.Value))
            {
                lastMoveFlavorFrame = Time.frameCount;
                var flavorKey = FlavorLines.PickGood();
                if (!string.IsNullOrEmpty(flavorKey))
                {
                    ShowGoodMoveFlavor(flavorKey);
                }
            }
        }

        private bool IsLocalWinner(int seat)
        {
            if (matchFlow != null && matchFlow.IsOnline)
            {
                return matchFlow.IsLocalSeat(seat);
            }

            return !IsBotSeat(seat);
        }

        private void ShowGoodMoveFlavor(string flavorKey, int meldId = -1)
        {
            if (goodMovePopup == null)
            {
                return;
            }

            lastGoodMoveFlavorKey = flavorKey;
            goodMoveMeldId = meldId;
            goodMovePopup.text = Loc.Get(flavorKey);
            UpdateGoodMovePopupPosition();
            goodMovePopupHide = ShowPopup(goodMovePopup, goodMovePopupHide, 2200);
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
                turnLabel.text = TurnLabelText(activeSeat);
            }

            bottomBar?.EnableInClassList("active-turn",
                state.Phase == GamePhase.Playing && !IsBotSeat(activeSeat) &&
                (matchFlow == null || !matchFlow.IsOnline || matchFlow.IsLocalSeat(activeSeat)));

            if (drawPileLabel != null)
            {
                drawPileLabel.text = Loc.Format("hud.draw_pile.count", state.DrawPile.Count);
            }

            UpdateDrawPileStack(state, game);

            if (drawPassButton != null)
            {
                drawPassButton.text = state.DrawPile.Count == 0 ? Loc.Get("hud.button.pass") : Loc.Get("hud.button.draw");
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
                hintButton.text = tableController.HintsEnabled
                    ? Loc.Get("hud.button.hint_on")
                    : Loc.Get("hud.button.hint_off");
                hintButton.SetEnabled(game.State.Phase == GamePhase.Playing);
            }

            if (markSetButton != null)
            {
                markSetButton.text = tableController.MarkModeActive
                    ? Loc.Get("hud.button.marking")
                    : Loc.Get("hud.button.mark");
            }

            if (compactBoardButton != null)
            {
                compactBoardButton.style.display = GameSettings.OrganicBoard ? DisplayStyle.Flex : DisplayStyle.None;
                compactBoardButton.SetEnabled(tableView != null);
            }
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
            var phoneLayout = root != null && root.ClassListContains("phone-layout");
            for (var seat = 0; seat < state.Hands.Count; seat++)
            {
                if (seat == viewerSeat)
                {
                    continue;
                }

                var shouldTuck = GameSettings.HideOpponentHands && state.Phase == GamePhase.Playing &&
                                 state.Board.Count > 0 && seat != state.CurrentSeat;
                var tuckState = GetOpponentHandTuckState(seat, shouldTuck);
                var opponent = new VisualElement();
                opponent.pickingMode = PickingMode.Ignore;
                opponent.AddToClassList("opponent-seat");
                var relativeSeat = (seat - viewerSeat + state.Hands.Count) % state.Hands.Count;
                var opponentSide = GetOpponentSide(relativeSeat, state.Hands.Count - 1,
                    root != null && root.ClassListContains("phone-layout") && Screen.height > Screen.width);
                var name = new Label(GetPlayerName(seat));
                name.pickingMode = PickingMode.Ignore;
                name.AddToClassList("opponent-name");
                opponent.Add(name);
                if (IsBotSeat(seat))
                {
                    var botBadge = new BotBadgeIcon();
                    botBadge.AddToClassList("opponent-bot-badge");
                    if (opponentSide == OpponentSide.Right)
                    {
                        botBadge.AddToClassList("opponent-bot-badge-left");
                    }

                    opponent.Add(botBadge);
                }
                var cards = new VisualElement();
                cards.pickingMode = PickingMode.Ignore;
                cards.AddToClassList("opponent-cards");
                if (phoneLayout && opponentSide != OpponentSide.Top)
                {
                    cards.AddToClassList("opponent-cards-phone-stack");
                }

                if (opponentSide != OpponentSide.Top)
                {
                    cards.AddToClassList("opponent-cards-vertical");
                }

                cards.name = "opponent-cards-" + seat;
                var incomingCount = tableView == null ? 0 : tableView.GetIncomingCardCount(seat);
                var visibleCardCount = Mathf.Max(0, state.Hands[seat].Count - incomingCount);
                var cardScale = Mathf.Clamp(GameSettings.OpponentCardScale, 0.5f, 2f);
                var cardWidth = (phoneLayout ? 22f : 30f) * cardScale;
                var cardHeight = (phoneLayout ? 32f : 44f) * cardScale;
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
                        if (phoneLayout && opponentSide != OpponentSide.Top)
                        {
                            cardBack.style.marginTop = -cardHeight * 0.82f;
                        }
                        else if (opponentSide == OpponentSide.Top)
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

                var count = new Label(state.Hands[seat].Count.ToString());
                count.pickingMode = PickingMode.Ignore;
                count.name = "opponent-count-" + seat;
                count.AddToClassList("opponent-count");
                opponent.Add(count);

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

                SetOpponentHandOffset(opponent, tuckState.LastTuckedOffset * GetOpponentHandTuckAmount(tuckState));
                ConfigureOpponentHandTuck(opponent, cards, opponentSide, tuckState);
                switch (opponentSide)
                {
                    case OpponentSide.Left:
                        leftOpponents?.Insert(0, opponent);
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

        private OpponentHandTuckState GetOpponentHandTuckState(int seat, bool shouldTuck)
        {
            var target = shouldTuck ? 1f : 0f;
            if (!opponentHandTuckStates.TryGetValue(seat, out var tuckState))
            {
                tuckState = new OpponentHandTuckState
                {
                    From = 0f,
                    To = target,
                    StartedAt = Time.unscaledTime
                };
                opponentHandTuckStates.Add(seat, tuckState);
                return tuckState;
            }

            var current = GetOpponentHandTuckAmount(tuckState);
            if (!Mathf.Approximately(tuckState.To, target))
            {
                tuckState.From = current;
                tuckState.To = target;
                tuckState.StartedAt = Time.unscaledTime;
            }

            return tuckState;
        }

        private void ConfigureOpponentHandTuck(VisualElement opponent, VisualElement cards, OpponentSide side,
            OpponentHandTuckState tuckState)
        {
            void OnGeometryChanged(GeometryChangedEvent evt)
            {
                if (opponent.panel == null || opponent.worldBound.width <= 0f || opponent.worldBound.height <= 0f ||
                    cards.worldBound.width <= 0f || cards.worldBound.height <= 0f)
                {
                    return;
                }

                opponent.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                var tuckedOffset = CalculateOpponentHandTuckOffset(opponent, cards, side);
                tuckState.LastTuckedOffset = tuckedOffset;
                var currentAmount = GetOpponentHandTuckAmount(tuckState);
                if (GameSettings.ReducedMotion)
                {
                    tuckState.From = tuckState.To;
                    tuckState.StartedAt = Time.unscaledTime;
                    SetOpponentHandOffset(opponent, tuckedOffset * tuckState.To);
                    return;
                }

                SetOpponentHandOffset(opponent, tuckedOffset * currentAmount);
                if (Mathf.Approximately(currentAmount, tuckState.To))
                {
                    SetOpponentHandOffset(opponent, tuckedOffset * tuckState.To);
                    return;
                }

                AnimateOpponentHandOffset(opponent, tuckedOffset, tuckState);
            }

            opponent.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void AnimateOpponentHandOffset(VisualElement opponent, Vector2 tuckedOffset,
            OpponentHandTuckState tuckState)
        {
            FrameRateGovernor.NotifyActivity();
            IVisualElementScheduledItem animation = null;
            SetOpponentHandOffset(opponent, tuckedOffset * GetOpponentHandTuckAmount(tuckState));
            animation = opponent.schedule.Execute(() =>
            {
                if (opponent.panel == null)
                {
                    animation.Pause();
                    opponentHandTuckAnimations.Remove(animation);
                    return;
                }

                FrameRateGovernor.NotifyActivity();
                var amount = GetOpponentHandTuckAmount(tuckState);
                SetOpponentHandOffset(opponent, tuckedOffset * amount);
                if (Mathf.Approximately(amount, tuckState.To))
                {
                    SetOpponentHandOffset(opponent, tuckedOffset * tuckState.To);
                    animation.Pause();
                    opponentHandTuckAnimations.Remove(animation);
                }
            }).Every(0);
            opponentHandTuckAnimations.Add(animation);
        }

        private static float GetOpponentHandTuckAmount(OpponentHandTuckState tuckState)
        {
            var progress = Mathf.Clamp01((Time.unscaledTime - tuckState.StartedAt) / OpponentHandTuckDuration);
            var eased = 1f - Mathf.Pow(1f - progress, 3f);
            return Mathf.Lerp(tuckState.From, tuckState.To, eased);
        }

        private static Vector2 CalculateOpponentHandTuckOffset(VisualElement opponent, VisualElement cards,
            OpponentSide side)
        {
            var opponentBounds = opponent.worldBound;
            var cardBounds = cards.worldBound;
            switch (side)
            {
                case OpponentSide.Left:
                    return new Vector2(Mathf.Min(0f,
                        opponentBounds.xMin - cardBounds.xMax + cardBounds.width * 0.5f), 0f);
                case OpponentSide.Right:
                    return new Vector2(Mathf.Max(0f,
                        opponentBounds.xMax - cardBounds.xMin - cardBounds.width * 0.5f), 0f);
                default:
                    return new Vector2(0f, Mathf.Min(0f,
                        opponentBounds.yMin - cardBounds.yMax + cardBounds.height * 0.5f));
            }
        }

        private static void SetOpponentHandOffset(VisualElement opponent, Vector2 offset)
        {
            if (offset.sqrMagnitude <= 0.001f)
            {
                opponent.style.translate = StyleKeyword.Null;
                return;
            }

            opponent.style.translate = new Translate(new Length(offset.x, LengthUnit.Pixel),
                new Length(offset.y, LengthUnit.Pixel));
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

            timerLabel.text = Loc.Format("hud.timer.remaining", Mathf.CeilToInt(matchFlow.TurnTimer.RemainingSeconds));
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
            return matchFlow != null && matchFlow.SeatCount > 0
                ? matchFlow.GetPlayerName(seat)
                : Loc.Format("hud.seat.default_name", seat + 1);
        }

        /// <summary>The tutorial's human seat is addressed as "you" ("Your turn") rather than by name.</summary>
        private bool IsTutorialPlayerSeat(int seat)
        {
            return matchFlow != null && matchFlow.IsTutorial && !IsBotSeat(seat);
        }

        private string TurnLabelText(int seat)
        {
            if (IsBotSeat(seat))
            {
                return Loc.Format("hud.turn.label.thinking", GetSeatLabel(seat));
            }

            return IsTutorialPlayerSeat(seat)
                ? Loc.Get("hud.turn.label.yours")
                : Loc.Format("hud.turn.label.player", GetSeatLabel(seat));
        }

        private string TurnPopupText(int seat)
        {
            return IsTutorialPlayerSeat(seat)
                ? Loc.Get("hud.turn.popup.yours")
                : Loc.Format("hud.turn.popup.player", GetPlayerName(seat));
        }

        private string GetSeatLabel(int seat)
        {
            return matchFlow != null && matchFlow.SeatCount > 0
                ? matchFlow.GetSeatLabel(seat)
                : Loc.Format("hud.seat.default_name", seat + 1);
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
                    hintButton.text = tableController.HintsEnabled
                        ? Loc.Get("hud.button.hint_on")
                        : Loc.Get("hud.button.hint_off");
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

            lastInvalidMoveError = error;
            lastInvalidFlavorKey = FlavorLines.PickBad();
            invalidMovePopup.text = InvalidMoveText(error, lastInvalidFlavorKey);
            invalidMoveHide = ShowPopup(invalidMovePopup, invalidMoveHide, 2400);
        }

        private static string InvalidMoveText(RuleError error, string flavorKey)
        {
            var reason = InvalidMoveMessage(error);
            return string.IsNullOrEmpty(flavorKey)
                ? reason
                : Loc.Format("hud.invalid_move.flavor", Loc.Get(flavorKey), reason);
        }

        private static string InvalidMoveMessage(RuleError error)
        {
            switch (error)
            {
                case RuleError.NotYourTurn:
                    return Loc.Get("hud.invalid_move.not_your_turn");
                case RuleError.WrongPhase:
                    return Loc.Get("hud.invalid_move.wrong_phase");
                case RuleError.NotDownBoardAccess:
                    return Loc.Get("hud.invalid_move.not_down_board_access");
                case RuleError.BoardCardToHand:
                    return Loc.Get("hud.invalid_move.board_card_to_hand");
                case RuleError.CannotEndTurn:
                    return Loc.Get("hud.invalid_move.cannot_end_turn");
                case RuleError.GoDownInvalid:
                case RuleError.GoDownCardToHand:
                    return Loc.Get("hud.invalid_move.go_down_invalid");
                case RuleError.DoubleInSet:
                    return Loc.Get("hud.invalid_move.double_in_set");
                case RuleError.SetTooLarge:
                    return Loc.Get("hud.invalid_move.set_too_large");
                case RuleError.InvalidCardForSet:
                    return Loc.Get("hud.invalid_move.invalid_card_for_set");
                case RuleError.InvalidTarget:
                    return Loc.Get("hud.invalid_move.invalid_target");
                case RuleError.DuplicateCard:
                    return Loc.Get("hud.invalid_move.duplicate_card");
                case RuleError.EmptyMove:
                    return Loc.Get("hud.invalid_move.empty_move");
                case RuleError.UnknownCard:
                case RuleError.UnknownMeld:
                    return Loc.Get("hud.invalid_move.unknown_card");
                case RuleError.GameFinished:
                    return Loc.Get("hud.invalid_move.game_finished");
                case RuleError.TutorialBlocked:
                    return Loc.Get("hud.invalid_move.tutorial_blocked");
                default:
                    return Loc.Get("hud.invalid_move.not_allowed");
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
                RequestConfirmation(() => Loc.Get("hud.confirm.revert"),
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
                    RequestConfirmation(() => Loc.Get("hud.confirm.fix_board"),
                        () => GameSettings.ConfirmFix, value => GameSettings.ConfirmFix = value,
                        () => tableController.FixBoard());
                }
                else
                {
                    tableController.FixBoard();
                }
            }
        }

        private void RequestConfirmation(Func<string> message, Func<bool> isEnabled, Action<bool> setEnabled,
            Action action)
        {
            if (isEnabled == null || !isEnabled())
            {
                action?.Invoke();
                return;
            }

            pendingConfirmAction = action;
            pendingConfirmSetting = setEnabled;
            pendingConfirmMessage = message;
            if (confirmMessage != null) confirmMessage.text = message?.Invoke() ?? string.Empty;
            if (confirmDontAsk != null)
            {
                confirmDontAsk.SetValueWithoutNotify(false);
                confirmDontAsk.style.display = setEnabled == null ? DisplayStyle.None : DisplayStyle.Flex;
            }
            SetVisible(confirmDialog, true);
            confirmDialog?.BringToFront();
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
            pendingConfirmMessage = null;
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
            RefreshMatchTime();
        }

        private void RefreshRoomCode()
        {
            var roomCode = matchFlow != null && matchFlow.Mode == MatchMode.Online
                ? matchFlow.OnlineSessionCode
                : null;
            if (roomCodeLabel != null)
            {
                roomCodeLabel.text = string.IsNullOrWhiteSpace(roomCode)
                    ? string.Empty
                    : Loc.Format("hud.pause.room_code", roomCode);
            }

            SetVisible(roomCodeLabel, !string.IsNullOrWhiteSpace(roomCode));
        }

        private void RefreshMatchTime()
        {
            if (pauseMatchTimeLabel == null)
            {
                return;
            }

            var elapsedSeconds = matchFlow == null ? 0f : matchFlow.MatchElapsedSeconds;
            pauseMatchTimeLabel.text = Loc.Format("hud.pause.match_time", MatchFlow.FormatDuration(elapsedSeconds));
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

            RequestConfirmation(() => Loc.Get("hud.confirm.skip_tutorial"), () => true, null,
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
            RequestConfirmation(() => Loc.Get("hud.confirm.leave"),
                () => GameSettings.ConfirmLeave, value => GameSettings.ConfirmLeave = value,
                () =>
                {
                    Resume();
                    matchFlow.ReturnToMenu();
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

        private void RequestHurryUp()
        {
            if (matchFlow == null || !matchFlow.CanRequestHurryUp ||
                !HasHurryUpDelayElapsed() || Time.unscaledTime < nextHurryUpRequestAt)
            {
                return;
            }

            nextHurryUpRequestAt = Time.unscaledTime + HurryUpCooldownSeconds;
            matchFlow.RequestHurryUp();
            UpdateFastForwardButton();
        }

        private void OnHurryUpReceived(int fromSeat)
        {
            if (hurryUpPopup == null)
            {
                return;
            }

            hurryUpPopupHide?.Pause();
            hurryUpPopup.AddToClassList("hurry-up-visible");
            hurryUpPopupHide = hurryUpPopup.schedule.Execute(() =>
            {
                hurryUpPopup.RemoveFromClassList("hurry-up-visible");
                hurryUpPopupHide = null;
            }).StartingIn(1800);
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
                markSetButton.text = tableController.MarkModeActive
                    ? Loc.Get("hud.button.marking")
                    : Loc.Get("hud.button.mark");
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
                tableView?.PanCamera(pan, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), false);
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