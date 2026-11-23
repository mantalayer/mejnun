using System;
using System.Collections.Generic;
using Mejnun.Core;
using Mejnun.Cosmetics;
using UnityEngine;

namespace Mejnun.Game
{
    public sealed class TableView : MonoBehaviour
    {
        private const int MaximumDrawFlightCards = 3;
        private const float DrawFlightDuration = 0.55f;
        public const float BoardCollectSeconds = 0.4f;
        private const float BoardCardFlightDuration = 0.28f;
        private const float InitialDealDuration = 0.3f;
        private const float InitialDealStagger = 0.05f;
        private const float InitialDealWaitSeconds = 2f;
        private const float BoardCardStagger = 0.003f;
        private const float BoardCardStaggerLimit = 0.12f;
        private const float BoardPileScale = 0.14f;

        private sealed class BoardCardFlight
        {
            public byte CardId;
            public CardView View;
            public Vector3 Source;
            public Vector3 Target;
            public float StartTime;
            public float EndTime;
            public bool Started;
        }

        private sealed class InitialDealFlight
        {
            public int Seat;
            public int CardIndex;
            public byte CardId;
            public CardView View;
            public Vector3 Source;
            public Vector3 Target;
            public Vector2 LastTargetScreen;
            public float SourceScale;
            public float ScreenHeight;
            public float TargetScale;
            public float StartTime;
            public float EndTime;
            public bool Started;
            public bool HasTarget;
            public bool IsTransient;
        }

        public delegate bool CardSlotResolver(int seat, int cardIndex, out Vector2 screenPosition,
            out float screenHeight);

        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private DeckSkinCatalog skinCatalog;
        [SerializeField] private FeltView feltView;
        [SerializeField] private BoardView boardView;
        [SerializeField] private HandView handView;
        [SerializeField] private TableCamera tableCamera;
        [SerializeField] private int pooledCardCount = Card.CardCount;

        private readonly Dictionary<byte, CardView> activeCards = new Dictionary<byte, CardView>(Card.CardCount);
        private readonly Stack<CardView> cardPool = new Stack<CardView>(Card.CardCount);
        private readonly HashSet<byte> visibleCardIds = new HashSet<byte>();
        private readonly List<byte> staleCardIds = new List<byte>(Card.CardCount);
        private readonly List<Card> hitTestHandCards = new List<Card>(Card.CardsPerDeck);
        private readonly List<CardView> drawingCards = new List<CardView>();
        private readonly List<int> drawingSeats = new List<int>();
        private readonly List<BoardCardFlight> boardCollectFlights = new List<BoardCardFlight>();
        private readonly List<BoardCardFlight> boardDealFlights = new List<BoardCardFlight>();
        private readonly List<InitialDealFlight> initialDealFlights = new List<InitialDealFlight>();
        private readonly Dictionary<byte, Vector3> boardStoredTargets = new Dictionary<byte, Vector3>();
        private readonly HashSet<byte> restoreHiddenCardIds = new HashSet<byte>();
        private readonly Dictionary<byte, Vector3> initialDealTargets = new Dictionary<byte, Vector3>();
        private readonly HashSet<byte> initialDealCardIds = new HashSet<byte>();
        private readonly List<Meld> emptyBoard = new List<Meld>();
        private readonly HandOrder handOrder = new HandOrder();
        private readonly Dictionary<byte, Vector3> liftedHandOffsets = new Dictionary<byte, Vector3>();
        private readonly Dictionary<byte, Vector3> liftedMeldOffsets = new Dictionary<byte, Vector3>();
        private readonly Dictionary<byte, Vector3> liftedBoardOffsets = new Dictionary<byte, Vector3>();
        private readonly HashSet<byte> liftedBoardCardIds = new HashSet<byte>();
        private readonly Dictionary<int, Rect> humanPlacementAreas = new Dictionary<int, Rect>();
        private readonly BoardArrangement boardArrangement = new BoardArrangement();
        private readonly List<Meld> arrangedMelds = new List<Meld>();
        private Vector3 meldLiftStartPointer;
        private Vector2 meldLiftStartBoardPointer;
        private int liftedMeldId = -1;
        private bool poolReady;
        private bool handVisible = true;
        private bool handConcealed;
        private int viewerSeat = -1;
        private Vector2 cardSize = new Vector2(0.64f, 0.92f);
        private float pixelsPerUnit = 100f;
        [SerializeField, Min(0.1f)] private float handReferenceSize = 5f;
        private Transform handRoot;
        private float handIntroStartedAt;
        private float handIntroDuration;
        private float handIntroDelay;
        private float handOutroStartedAt;
        private float handOutroDuration;
        private float handOutroDelay;
        private bool handOutroCompleted;
        private float lastHandAspect;
        private bool hasHandAspect;
        private int handGapIndex = -1;
        private float handBottomInset;
        private float edgePanBottomInset;
        private float boardTopInset;
        private float boardLeftInset;
        private float boardRightInset;
        private GameState lastSyncedState;
        private int lastDrawPileCount = -1;
        private int[] lastHandCounts = Array.Empty<int>();
        private Vector2 drawPileScreenPosition;
        private float drawPileScreenHeight;
        private bool hasDrawPileScreenPosition;
        private bool boardDealRequested;
        private bool boardDealPlaying;
        private bool boardCollecting;
        private bool boardCleared;
        private bool handsCleared;
        private bool boardRestorePending;
        private bool dealArmed;
        private bool dealPending;
        private bool dealStarted;
        private bool dealHeld;
        private bool collectionNotified;
        private float boardDealRequestedAt;
        private float boardDealDelay;
        private float boardDealEndTime;
        private float boardCollectEndTime;
        private float dealRequestedAt;
        private readonly Dictionary<int, Vector3> seatScreenRects = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, Vector2> seatScreenSteps = new Dictionary<int, Vector2>();
        private Func<int, bool> cpuSeatPredicate;

        public GameState CurrentState { get; private set; }
        public TurnSession CurrentTurn { get; private set; }
        public HandOrder HandOrder => handOrder;
        public BoardArrangement BoardArrangement => boardArrangement;
        public DeckSkinCatalog SkinCatalog => skinCatalog;
        public IReadOnlyDictionary<byte, CardView> ActiveCards => activeCards;
        public int ActiveCardCount => activeCards.Count;
        public int BoardCardCount => boardCleared ? 0 : CountBoardCards(CurrentState);
        public bool DealAnimationEnabled { get; set; }
        public bool DealHeld
        {
            get => dealHeld;
            set
            {
                if (dealHeld == value)
                {
                    return;
                }

                dealHeld = value;
                if (!dealHeld && !dealArmed && !dealPending && initialDealFlights.Count == 0)
                {
                    DealCompleted?.Invoke();
                }
            }
        }
        public bool IsDealing => DealHeld || dealArmed || dealPending || initialDealFlights.Count > 0;
        public CardSlotResolver SeatCardSlotResolver { get; set; }
        public bool IsCollectingCards => boardCollecting || boardCollectFlights.Count > 0;
        public bool HandVisible => handVisible;
        public bool HandConcealed => handConcealed;
        public int HandSeat => !handVisible || CurrentState == null
            ? -1
            : DisplaySeat(CurrentState);
        public Camera ViewCamera => tableCamera == null ? null : tableCamera.Camera;
        public event Action IncomingCardsChanged;
        public event Action CollectionCompleted;
        public event Action DealCompleted;

        /// <summary>The seat whose hand is shown, or -1 to follow the current seat.</summary>
        public int ViewerSeat => viewerSeat;
        public BoardLayoutResult CurrentBoardLayout => boardView == null ? null : boardView.CurrentLayout;

        public bool TryGetMeldsAnchor(IReadOnlyList<int> meldIds, out Vector3 world)
        {
            world = default;
            if (meldIds == null || boardView == null || boardView.CurrentLayout == null)
            {
                return false;
            }

            var layout = boardView.CurrentLayout;
            for (var idIndex = 0; idIndex < meldIds.Count; idIndex++)
            {
                for (var meldIndex = 0; meldIndex < layout.Melds.Count; meldIndex++)
                {
                    var meldLayout = layout.Melds[meldIndex];
                    if (meldLayout.MeldId == meldIds[idIndex])
                    {
                        var bounds = meldLayout.Bounds;
                        world = boardView.transform.TransformPoint(new Vector3(bounds.center.x, bounds.yMax, 0f));
                        return true;
                    }
                }
            }

            return false;
        }

        public bool TryGetMeldAnchor(int meldId, out Vector3 world)
        {
            return TryGetMeldsAnchor(new[] { meldId }, out world);
        }

        public bool TryGetCardAnchor(byte cardId, out Vector3 world)
        {
            world = default;
            if (!activeCards.TryGetValue(cardId, out var view) || view.Renderer == null)
            {
                return false;
            }

            var bounds = view.Renderer.bounds;
            world = new Vector3(bounds.center.x, bounds.max.y, view.transform.position.z);
            return true;
        }

        /// <summary>World centre of the empty-table slot where a new set would go.</summary>
        public bool TryGetNewGroupAnchor(out Vector3 world)
        {
            world = default;
            if (boardView == null || boardView.CurrentLayout == null)
            {
                return false;
            }

            var slot = boardView.CurrentLayout.NewGroupSlot;
            world = boardView.transform.TransformPoint(new Vector3(slot.center.x, slot.center.y, 0f));
            return true;
        }

        public void SetTutorialTarget(int meldId, bool emptyTarget)
        {
            if (boardView != null)
            {
                boardView.SetTutorialTarget(meldId, emptyTarget);
            }
        }

        /// <summary>Screen position (pixels, origin bottom-left) of a world point, for anchoring UI to the table.</summary>
        public bool TryGetScreenPoint(Vector3 world, out Vector2 screen)
        {
            screen = default;
            var viewCamera = ViewCamera;
            if (viewCamera == null)
            {
                return false;
            }

            var point = viewCamera.WorldToScreenPoint(world);
            if (point.z < 0f)
            {
                return false;
            }

            screen = new Vector2(point.x, point.y);
            return true;
        }

        /// <summary>Screen anchor above a card (hand or board), or above the set with the given id.</summary>
        public bool TryGetCardScreenAnchor(byte cardId, out Vector2 screen)
        {
            screen = default;
            return TryGetCardAnchor(cardId, out var world) && TryGetScreenPoint(world, out screen);
        }

        public bool TryGetMeldScreenAnchor(int meldId, out Vector2 screen)
        {
            screen = default;
            return TryGetMeldAnchor(meldId, out var world) && TryGetScreenPoint(world, out screen);
        }

        private void Awake()
        {
            PreparePool();
            EnsureHandRoot();
        }

        private void LateUpdate()
        {
            UpdateBoardCardFlights();
            UpdateInitialDealFlights();
            UpdateDrawingCards();
            if (dealPending)
            {
                TryStartInitialDeal();
            }

            if (tableCamera == null || tableCamera.Camera == null)
            {
                return;
            }

            EnsureHandRoot();
            if (handRoot != null)
            {
                UpdateHandRoot();
            }

            RecordHumanPlacementArea(CurrentState);
            if (!handVisible || CurrentState == null || handView == null || handRoot == null)
            {
                return;
            }

            var aspect = GetViewportAspect();
            if (hasHandAspect && Mathf.Approximately(aspect, lastHandAspect))
            {
                return;
            }

            var seat = DisplaySeat(CurrentState);
            if (seat >= 0 && seat < CurrentState.Hands.Count)
            {
                LayoutHand(seat, 0f);
                CaptureInitialDealTargets(seat);
            }
        }

        public void ApplyFelt(string feltId)
        {
            if (feltView != null)
            {
                feltView.Apply(feltId);
            }
        }

        public void PlayHandIntro(float duration, float delay)
        {
            handOutroCompleted = false;
            handOutroDuration = 0f;
            handOutroDelay = 0f;
            handIntroDuration = Mathf.Max(0f, duration);
            handIntroDelay = Mathf.Max(0f, delay);
            handIntroStartedAt = Time.unscaledTime;
        }

        public void PlayHandOutro(float duration)
        {
            PlayHandOutro(duration, 0f);
        }

        public void PlayHandOutro(float duration, float delay)
        {
            handOutroDuration = Mathf.Max(0f, duration);
            handOutroDelay = Mathf.Max(0f, delay);
            handOutroStartedAt = Time.unscaledTime;
            handOutroCompleted = handOutroDuration <= 0f && handOutroDelay <= 0f;
        }

        public void PlayBoardDeal(float delay)
        {
            boardDealFlights.Clear();
            boardDealPlaying = false;
            boardDealRequested = true;
            boardDealRequestedAt = Time.unscaledTime;
            boardDealDelay = Mathf.Max(0f, delay);
            TryStartBoardDeal();
        }

        public void ArmDeal()
        {
            if (!DealAnimationEnabled)
            {
                return;
            }

            dealArmed = true;
            dealPending = false;
            dealStarted = false;
            initialDealFlights.Clear();
            initialDealCardIds.Clear();
            initialDealTargets.Clear();
        }

        public void ResetForNewGame()
        {
            foreach (var cardId in restoreHiddenCardIds)
            {
                if (activeCards.TryGetValue(cardId, out var view))
                {
                    view.gameObject.SetActive(true);
                }
            }

            restoreHiddenCardIds.Clear();
            foreach (var cardId in initialDealCardIds)
            {
                if (activeCards.TryGetValue(cardId, out var view))
                {
                    view.gameObject.SetActive(true);
                }
            }

            for (var index = 0; index < boardCollectFlights.Count; index++)
            {
                ReturnViewToPool(boardCollectFlights[index].View);
            }

            for (var index = 0; index < initialDealFlights.Count; index++)
            {
                var flight = initialDealFlights[index];
                if (flight.IsTransient)
                {
                    ReturnViewToPool(flight.View);
                }
            }

            foreach (var target in initialDealTargets)
            {
                if (activeCards.TryGetValue(target.Key, out var view))
                {
                    view.MoveTo(target.Value, 0f);
                }
            }

            boardCleared = false;
            tableCamera?.ReleaseFeltAnchor();
            handsCleared = false;
            boardRestorePending = false;
            boardCollecting = false;
            boardCollectEndTime = 0f;
            collectionNotified = true;
            boardCollectFlights.Clear();
            boardDealRequested = false;
            boardDealPlaying = false;
            boardDealFlights.Clear();
            boardStoredTargets.Clear();
            dealArmed = false;
            dealPending = false;
            dealStarted = false;
            initialDealFlights.Clear();
            initialDealCardIds.Clear();
            initialDealTargets.Clear();
        }

        public void PrepareBoardRestore()
        {
            ResetForNewGame();
            boardCleared = false;
            boardRestorePending = true;
        }

        public float CollectBoardToPile()
        {
            if (boardCleared)
            {
                return Mathf.Max(0f, boardCollectEndTime - Time.unscaledTime);
            }

            boardDealRequested = false;
            boardDealPlaying = false;
            boardDealFlights.Clear();
            boardCollectFlights.Clear();
            boardStoredTargets.Clear();
            boardCleared = true;
            tableCamera?.FitFeltAnchor();
            if (CurrentState == null)
            {
                return 0f;
            }

            var now = Time.unscaledTime;
            var index = 0;
            for (var meldIndex = 0; meldIndex < CurrentState.Board.Count; meldIndex++)
            {
                var meld = CurrentState.Board[meldIndex];
                for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                {
                    var cardId = meld.Cards[cardIndex].Id;
                    if (activeCards.TryGetValue(cardId, out var view))
                    {
                        CollectActiveCard(view, now, index++);
                    }
                }
            }

            LayoutClearedBoard();
            if (boardCollectFlights.Count == 0)
            {
                return 0f;
            }

            boardCollecting = true;
            boardCollectEndTime = now + BoardCollectSeconds;
            collectionNotified = false;
            return BoardCollectSeconds;
        }

        public float CollectTableToPile()
        {
            var collectDelay = CollectBoardToPile();
            if (handsCleared || CurrentState == null)
            {
                return collectDelay;
            }

            handsCleared = true;
            var now = Time.unscaledTime;
            var nextIndex = boardCollectFlights.Count;
            var displayedSeat = DisplaySeat(CurrentState);
            for (var seat = 0; seat < CurrentState.Hands.Count; seat++)
            {
                var hand = CurrentState.Hands[seat];
                for (var cardIndex = 0; cardIndex < hand.Count; cardIndex++)
                {
                    var card = hand[cardIndex];
                    if (activeCards.TryGetValue(card.Id, out var view))
                    {
                        CollectActiveCard(view, now, nextIndex++);
                        continue;
                    }

                    if (TryCreateCollectionBack(card, seat, cardIndex, displayedSeat, out view))
                    {
                        CollectActiveCard(view, now, nextIndex++);
                    }
                }
            }

            LayoutClearedBoard();
            if (boardCollectFlights.Count == 0)
            {
                boardCollecting = false;
                return collectDelay;
            }

            boardCollecting = true;
            boardCollectEndTime = Mathf.Max(boardCollectEndTime, now + BoardCollectSeconds);
            collectionNotified = false;
            return Mathf.Max(collectDelay, boardCollectEndTime - now);
        }

        public void Sync(GameState state, TurnSession turn)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (DealAnimationEnabled && state.Phase == GamePhase.ChoosingDealer && !dealArmed)
            {
                ArmDeal();
            }

            if (dealArmed && state.Phase == GamePhase.Playing && !dealStarted && !dealPending)
            {
                dealPending = true;
                dealRequestedAt = Time.unscaledTime;
            }

            if (!boardCollecting)
            {
                boardStoredTargets.Clear();
            }

            if (lastSyncedState != null && !ReferenceEquals(lastSyncedState, state))
            {
                ClearDrawingCards();
            }

            var cardsDrawingFromPile = FindCardsDrawnFromPile(state, out var drawingSeat);

            PreparePool();
            EnsureHandRoot();
            CurrentState = state;
            CurrentTurn = turn;
            if (feltView != null)
            {
                feltView.Apply(state.TableFeltId);
            }

            RecordHumanPlacementArea(state);
            UpdateCpuPlacementArea(state);
            visibleCardIds.Clear();

            var activeSeat = DisplaySeat(state);
            var activeHand = activeSeat >= 0 && activeSeat < state.Hands.Count
                ? state.Hands[activeSeat]
                : null;
            if (handsCleared)
            {
                activeHand = null;
            }
            var ownDrawnViews = new List<CardView>();
            var ownDrew = activeHand != null && ReferenceEquals(lastSyncedState, state) &&
                          state.Phase == GamePhase.Playing && hasDrawPileScreenPosition &&
                          IsFinite(drawPileScreenPosition.x) && IsFinite(drawPileScreenPosition.y) &&
                          lastDrawPileCount > state.DrawPile.Count &&
                          activeSeat < lastHandCounts.Length && activeHand.Count > lastHandCounts[activeSeat];
            if (activeHand != null)
            {
                handOrder.Synchronize(activeSeat, activeHand);
                for (var cardIndex = 0; handVisible && cardIndex < activeHand.Count; cardIndex++)
                {
                    var isNew = ownDrew && !activeCards.ContainsKey(activeHand[cardIndex].Id);
                    AddVisibleCard(activeHand[cardIndex], state, true);
                    if (isNew)
                    {
                        ownDrawnViews.Add(activeCards[activeHand[cardIndex].Id]);
                    }
                }
            }

            for (var meldIndex = 0; !boardCleared && meldIndex < state.Board.Count; meldIndex++)
            {
                var cards = state.Board[meldIndex].Cards;
                for (var cardIndex = 0; cardIndex < cards.Count; cardIndex++)
                {
                    AddVisibleCard(cards[cardIndex], state, false);
                }
            }

            staleCardIds.Clear();
            foreach (var pair in activeCards)
            {
                if (!visibleCardIds.Contains(pair.Key))
                {
                    staleCardIds.Add(pair.Key);
                }
            }

            for (var index = 0; index < staleCardIds.Count; index++)
            {
                var cardId = staleCardIds[index];
                var view = activeCards[cardId];
                activeCards.Remove(cardId);
                view.gameObject.SetActive(false);
                cardPool.Push(view);
            }

            var aspect = GetBoardViewportAspect();
            if (boardView != null && tableCamera != null && tableCamera.Camera != null)
            {
                var arrangement = boardArrangement.Arrange(boardCleared ? emptyBoard : state.Board);
                arrangedMelds.Clear();
                for (var index = 0; index < arrangement.Count; index++)
                {
                    arrangedMelds.Add(arrangement[index]);
                }

                UpdateLiftedBoardCardIds();
                boardView.Layout(arrangedMelds, activeCards, aspect, cardSize, liftedBoardCardIds);
                RestartCollectingFlightsAfterSync();
                RestartBoardDealFlightsAfterSync();
                if (boardRestorePending && !boardDealPlaying)
                {
                    if (hasDrawPileScreenPosition)
                    {
                        MoveRestoredBoardToPile();
                    }
                    else
                    {
                        HideRestoredBoardCards();
                    }
                }
                if (tableCamera != null)
                {
                    tableCamera.SetBoardBounds(boardView.CurrentBounds);
                }
            }

            if (handRoot != null)
            {
                UpdateHandRoot();
            }

            if (handVisible && activeHand != null && !handsCleared)
            {
                if (handRoot != null && tableCamera != null && tableCamera.Camera != null)
                {
                    for (var index = 0; index < ownDrawnViews.Count; index++)
                    {
                        ownDrawnViews[index].transform.localPosition = HandLocalPoint(drawPileScreenPosition);
                    }
                }

                LayoutHand(activeSeat, ownDrawnViews.Count > 0 ? DrawFlightDuration : -1f);
                CaptureInitialDealTargets(activeSeat);
            }

            ReconcileInitialDealFlights(activeSeat);

            if (dealPending)
            {
                StashInitialDealHandAtPile();
            }

            if (cardsDrawingFromPile.Count > 0 && handRoot != null &&
                seatScreenRects.TryGetValue(drawingSeat, out var seatScreenRect))
            {
                seatScreenSteps.TryGetValue(drawingSeat, out var screenStep);
                for (var index = 0; index < cardsDrawingFromPile.Count; index++)
                {
                    var target = new Vector2(seatScreenRect.x, seatScreenRect.y) + screenStep * index;
                    AnimateDrawnCard(cardsDrawingFromPile[index], seatScreenRect, target);
                    drawingSeats.Add(drawingSeat);
                }

                IncomingCardsChanged?.Invoke();
            }

            lastSyncedState = state;
            lastDrawPileCount = state.DrawPile.Count;
            RememberHandCounts(state);
        }

        public CardView GetCardView(byte cardId)
        {
            return activeCards.TryGetValue(cardId, out var view) ? view : null;
        }

        public GestureHit HitTest(Vector2 screenPosition, bool allowMeldFrameGrip = false)
        {
            if (CurrentState == null || tableCamera == null || tableCamera.Camera == null)
            {
                return GestureHit.None;
            }

            var camera = tableCamera.Camera;
            var distance = Mathf.Abs(camera.transform.position.z);
            var world = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distance));
            world.z = 0f;
            var activeSeat = DisplaySeat(CurrentState);
            var activeHand = activeSeat >= 0 && activeSeat < CurrentState.Hands.Count
                ? CurrentState.Hands[activeSeat]
                : null;
            if (activeHand != null)
            {
                handOrder.Synchronize(activeSeat, activeHand);
                handOrder.CopyCards(activeSeat, hitTestHandCards);
                var topCardIndex = -1;
                var topSortingOrder = int.MinValue;
                for (var index = hitTestHandCards.Count - 1; index >= 0; index--)
                {
                    var card = hitTestHandCards[index];
                    if (liftedHandOffsets.ContainsKey(card.Id) || initialDealCardIds.Contains(card.Id))
                    {
                        continue;
                    }

                    if (activeCards.TryGetValue(card.Id, out var handViewCard) &&
                        !handViewCard.IsMoving &&
                        handViewCard.ContainsWorldPoint(world) && handViewCard.SortingOrder > topSortingOrder)
                    {
                        topSortingOrder = handViewCard.SortingOrder;
                        topCardIndex = index;
                    }
                }

                if (topCardIndex >= 0)
                {
                    return GestureHit.HandCard(hitTestHandCards[topCardIndex].Id, topCardIndex);
                }
            }

            for (var meldIndex = CurrentState.Board.Count - 1; meldIndex >= 0; meldIndex--)
            {
                var meld = CurrentState.Board[meldIndex];
                var meldLayout = boardView != null && boardView.CurrentLayout != null
                    ? FindMeldLayout(boardView.CurrentLayout, meld.Id)
                    : null;
                if (meldLayout != null)
                {
                    var grip = new Rect(meldLayout.Bounds.xMin, meldLayout.Bounds.yMax - cardSize.y * 0.18f,
                        cardSize.x * 0.18f, cardSize.y * 0.18f);
                    if (grip.Contains(boardView.transform.InverseTransformPoint(world)))
                    {
                        return GestureHit.MeldGrip(meld.Id, boardArrangement.IndexOf(meld.Id));
                    }
                }

                for (var cardIndex = meld.Cards.Count - 1; cardIndex >= 0; cardIndex--)
                {
                    var card = meld.Cards[cardIndex];
                    if (liftedBoardOffsets.ContainsKey(card.Id) ||
                        !activeCards.TryGetValue(card.Id, out var boardViewCard) ||
                        !boardViewCard.RestBoundsContains(world))
                    {
                        continue;
                    }

                    var targetIndex = cardIndex;
                    if (boardView != null && boardView.CurrentLayout != null &&
                        boardView.CurrentLayout.TryHit(world, out var layoutHit) && !layoutHit.IsNewGroup &&
                        layoutHit.MeldId == meld.Id)
                    {
                        targetIndex = layoutHit.InsertIndex;
                    }

                    return GestureHit.BoardCard(card.Id, meld.Id, cardIndex, targetIndex);
                }
            }

            if (allowMeldFrameGrip && boardView != null && boardView.CurrentLayout != null &&
                boardView.CurrentLayout.TryHitMeldFrame(boardView.transform.InverseTransformPoint(world), out var frameMeldId))
            {
                return GestureHit.MeldGrip(frameMeldId, boardArrangement.IndexOf(frameMeldId));
            }

            if (boardView != null && boardView.CurrentLayout != null &&
                boardView.CurrentLayout.TryHit(world, out var boardHit))
            {
                return boardHit.IsNewGroup
                    ? GestureHit.EmptyBoard()
                    : GestureHit.MeldGap(boardHit.MeldId, boardHit.InsertIndex);
            }

            if (activeHand != null && handView != null && handRoot != null)
            {
                var localPointer = handRoot.InverseTransformPoint(world);
                if (localPointer.y <= handView.HandTopY && handView.ContainsHandX(localPointer.x))
                {
                    return GestureHit.HandArea(activeHand.Count);
                }
            }

            return GestureHit.EmptyBoard();
        }

        public void FitBoard()
        {
            if (tableCamera != null)
            {
                tableCamera.FitBoard();
            }
        }

        public void ZoomCamera(float factor)
        {
            if (tableCamera != null)
            {
                tableCamera.Zoom(factor, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            }
        }

        public void BeginCameraPan()
        {
            tableCamera?.BeginPan();
        }

        public void PanCamera(Vector2 screenDelta, Vector2 screenPosition, bool trackForInertia = true)
        {
            if (tableCamera != null)
            {
                tableCamera.Pan(screenDelta, screenPosition, trackForInertia);
            }
        }

        public void ReleaseCameraPan()
        {
            tableCamera?.ReleasePan();
        }

        public void CancelCameraPan()
        {
            tableCamera?.CancelPanInertia();
        }

        public void SetDrawPileScreenPosition(Vector2 screenPosition)
        {
            SetDrawPileScreenRect(screenPosition, 0f);
        }

        public void SetDrawPileScreenRect(Vector2 screenPosition, float screenHeight)
        {
            drawPileScreenPosition = screenPosition;
            drawPileScreenHeight = Mathf.Max(0f, screenHeight);
            hasDrawPileScreenPosition = true;
        }

        public void SetSeatScreenRect(int seat, Vector2 screenPosition, float screenHeight)
        {
            SetSeatScreenRect(seat, screenPosition, screenHeight, Vector2.zero);
        }

        public void SetSeatScreenRect(int seat, Vector2 screenPosition, float screenHeight, Vector2 screenStep)
        {
            if (seat < 0 || screenHeight <= 0f || !IsFinite(screenPosition.x) ||
                !IsFinite(screenPosition.y) || !IsFinite(screenHeight) || !IsFinite(screenStep.x) ||
                !IsFinite(screenStep.y))
            {
                seatScreenRects.Remove(seat);
                seatScreenSteps.Remove(seat);
                return;
            }

            seatScreenRects[seat] = new Vector3(screenPosition.x, screenPosition.y, screenHeight);
            seatScreenSteps[seat] = screenStep;
        }

        public int GetIncomingCardCount(int seat)
        {
            var count = dealPending && CurrentState != null && seat >= 0 && seat < CurrentState.Hands.Count
                ? CurrentState.Hands[seat].Count
                : 0;
            for (var index = 0; index < drawingSeats.Count; index++)
            {
                if (drawingSeats[index] == seat)
                {
                    count++;
                }
            }

            for (var index = 0; index < initialDealFlights.Count; index++)
            {
                if (initialDealFlights[index].Seat == seat)
                {
                    count++;
                }
            }

            return count;
        }

        public bool CompactBoard()
        {
            if (boardView == null || !boardView.CompactOrganicBoard())
            {
                return false;
            }

            tableCamera?.SetBoardBounds(boardView.CurrentBounds);
            return true;
        }

        public void ShowDropPreview(GestureHit target, bool isValid, IReadOnlyList<byte> draggedCardIds,
            IReadOnlyCollection<byte> liftedCardIds, Vector2 screenPosition)
        {
            if (boardView != null)
            {
                boardView.ShowDropPreview(target, isValid, draggedCardIds, liftedCardIds,
                    BoardPoint(screenPosition), GameSettings.OverlapSets, GameSettings.OrganicBoard);
            }
        }

        public void ClearDropPreview()
        {
            boardView?.ClearDropPreview();
        }

        public void SetPendingNewMeldPoint(Vector2 screenPosition)
        {
            if (boardView != null && tableCamera != null && tableCamera.Camera != null && GameSettings.OrganicBoard)
            {
                boardView.SetPendingNewMeldPoint(BoardPoint(screenPosition));
            }
        }

        public void ClearPendingNewMeldPoint()
        {
            boardView?.ClearPendingNewMeldPoint();
        }

        private void UpdateBoardCardFlights()
        {
            var now = Time.unscaledTime;
            for (var index = boardCollectFlights.Count - 1; index >= 0; index--)
            {
                var flight = boardCollectFlights[index];
                if (!flight.Started && now >= flight.StartTime)
                {
                    flight.View.MoveTo(flight.Target, BoardCardFlightDuration, BoardPileScale);
                    flight.Started = true;
                }

                if (now >= flight.EndTime)
                {
                    flight.View.MoveTo(flight.Target, 0f, BoardPileScale);
                    ReturnViewToPool(flight.View);
                    boardCollectFlights.RemoveAt(index);
                }
            }

            if (boardCollecting && now >= boardCollectEndTime && boardCollectFlights.Count == 0)
            {
                boardCollecting = false;
                if (!collectionNotified)
                {
                    collectionNotified = true;
                    CollectionCompleted?.Invoke();
                }
            }

            if (boardDealRequested)
            {
                TryStartBoardDeal();
            }

            for (var index = boardDealFlights.Count - 1; index >= 0; index--)
            {
                var flight = boardDealFlights[index];
                if (!flight.Started && now >= flight.StartTime)
                {
                    if (activeCards.TryGetValue(flight.CardId, out var view) && view == flight.View)
                    {
                        view.MoveTo(flight.Target, BoardCardFlightDuration);
                    }

                    flight.Started = true;
                }

                if (now >= flight.EndTime)
                {
                    boardDealFlights.RemoveAt(index);
                }
            }

            if (boardDealPlaying && now >= boardDealEndTime)
            {
                boardDealPlaying = false;
            }
        }

        private void TryStartBoardDeal()
        {
            if (!boardDealRequested)
            {
                return;
            }

            if (Time.unscaledTime - boardDealRequestedAt > 2f)
            {
                boardDealRequested = false;
                boardRestorePending = false;
                RevealRestoredBoardCards();
                return;
            }

            if (CurrentState == null || tableCamera == null || tableCamera.Camera == null ||
                !hasDrawPileScreenPosition || !IsFinite(drawPileScreenPosition.x) ||
                !IsFinite(drawPileScreenPosition.y) || (BoardCardCount > 0 && boardView?.CurrentLayout == null))
            {
                return;
            }

            boardDealRequested = false;
            if (boardRestorePending)
            {
                MoveRestoredBoardToPile();
            }

            if (BoardCardCount == 0)
            {
                boardRestorePending = false;
                return;
            }

            var cards = new List<BoardCardFlight>(BoardCardCount);
            for (var meldIndex = 0; meldIndex < CurrentState.Board.Count; meldIndex++)
            {
                var meld = CurrentState.Board[meldIndex];
                for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                {
                    var cardId = meld.Cards[cardIndex].Id;
                    if (!activeCards.TryGetValue(cardId, out var view) ||
                        !TryGetPilePoint(view.transform.parent, out var pilePoint))
                    {
                        continue;
                    }

                    cards.Add(new BoardCardFlight
                    {
                        CardId = cardId,
                        View = view,
                        Target = boardStoredTargets.TryGetValue(cardId, out var storedTarget)
                            ? storedTarget
                            : view.TargetLocalPosition
                    });
                    view.transform.localPosition = pilePoint;
                    view.SetStartScale(BoardPileScale);
                }
            }

            if (cards.Count == 0)
            {
                return;
            }

            var now = Time.unscaledTime;
            for (var index = 0; index < cards.Count; index++)
            {
                var flight = cards[index];
                var stagger = Mathf.Min(index * BoardCardStagger, BoardCardStaggerLimit);
                flight.StartTime = now + boardDealDelay + stagger;
                flight.EndTime = flight.StartTime + BoardCardFlightDuration;
                boardDealFlights.Add(flight);
            }

            boardDealPlaying = true;
            boardRestorePending = false;
            boardDealEndTime = now + boardDealDelay + BoardCardStaggerLimit + BoardCardFlightDuration;
        }

        private bool TryGetPilePoint(Transform parent, out Vector3 localPoint)
        {
            localPoint = default;
            if (tableCamera == null || tableCamera.Camera == null || !hasDrawPileScreenPosition)
            {
                return false;
            }

            var camera = tableCamera.Camera;
            var worldPoint = camera.ScreenToWorldPoint(new Vector3(drawPileScreenPosition.x,
                drawPileScreenPosition.y, Mathf.Abs(camera.transform.position.z)));
            worldPoint.z = 0f;
            localPoint = parent == null ? worldPoint : parent.InverseTransformPoint(worldPoint);
            return IsFinite(localPoint.x) && IsFinite(localPoint.y) && IsFinite(localPoint.z);
        }

        private void RestartCollectingFlightsAfterSync()
        {
            if (!boardCollecting)
            {
                return;
            }

            var now = Time.unscaledTime;
            for (var index = 0; index < boardCollectFlights.Count; index++)
            {
                var flight = boardCollectFlights[index];
                if (!flight.Started || now >= flight.EndTime)
                {
                    continue;
                }

                flight.View.MoveTo(flight.Target, Mathf.Max(0f, flight.EndTime - now), BoardPileScale);
            }
        }

        private void RestartBoardDealFlightsAfterSync()
        {
            if (!boardDealPlaying)
            {
                return;
            }

            var now = Time.unscaledTime;
            for (var index = 0; index < boardDealFlights.Count; index++)
            {
                var flight = boardDealFlights[index];
                if (!flight.Started || now >= flight.EndTime ||
                    !activeCards.TryGetValue(flight.CardId, out var view) || view != flight.View)
                {
                    continue;
                }

                view.MoveTo(flight.Target, Mathf.Max(0f, flight.EndTime - now));
            }
        }

        private void MoveRestoredBoardToPile()
        {
            if (CurrentState == null || !hasDrawPileScreenPosition)
            {
                return;
            }

            for (var meldIndex = 0; meldIndex < CurrentState.Board.Count; meldIndex++)
            {
                var meld = CurrentState.Board[meldIndex];
                for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                {
                    var cardId = meld.Cards[cardIndex].Id;
                    if (!activeCards.TryGetValue(cardId, out var view) ||
                        !TryGetPilePoint(view.transform.parent, out var pilePoint))
                    {
                        continue;
                    }

                    if (!boardStoredTargets.ContainsKey(cardId))
                    {
                        boardStoredTargets[cardId] = view.TargetLocalPosition;
                    }

                    view.transform.localPosition = pilePoint;
                    view.SetStartScale(BoardPileScale);
                    view.gameObject.SetActive(true);
                    restoreHiddenCardIds.Remove(cardId);
                }
            }
        }

        private void HideRestoredBoardCards()
        {
            if (CurrentState == null)
            {
                return;
            }

            for (var meldIndex = 0; meldIndex < CurrentState.Board.Count; meldIndex++)
            {
                var meld = CurrentState.Board[meldIndex];
                for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                {
                    var cardId = meld.Cards[cardIndex].Id;
                    if (activeCards.TryGetValue(cardId, out var view))
                    {
                        if (!boardStoredTargets.ContainsKey(cardId))
                        {
                            boardStoredTargets[cardId] = view.TargetLocalPosition;
                        }

                        restoreHiddenCardIds.Add(cardId);
                        view.gameObject.SetActive(false);
                    }
                }
            }
        }

        private void RevealRestoredBoardCards()
        {
            foreach (var cardId in restoreHiddenCardIds)
            {
                if (!activeCards.TryGetValue(cardId, out var view))
                {
                    continue;
                }

                view.gameObject.SetActive(true);
                if (boardStoredTargets.TryGetValue(cardId, out var target))
                {
                    view.MoveTo(target, 0f);
                }
            }

            restoreHiddenCardIds.Clear();
        }

        private void UpdateInitialDealFlights()
        {
            var now = Time.unscaledTime;
            var incomingCardsChanged = false;
            for (var index = initialDealFlights.Count - 1; index >= 0; index--)
            {
                var flight = initialDealFlights[index];
                if (flight.View == null)
                {
                    initialDealFlights.RemoveAt(index);
                    incomingCardsChanged = true;
                    continue;
                }

                if (TryGetPilePoint(handRoot, out var pilePoint))
                {
                    if (!flight.Started)
                    {
                        flight.Source = pilePoint;
                    }
                }

                if (!flight.Started && now < flight.StartTime)
                {
                    flight.View.ApplyScale(flight.TargetScale);
                    flight.View.SetStartScale(flight.SourceScale / Mathf.Max(flight.TargetScale, 0.01f));
                    flight.View.transform.localPosition = flight.Source;
                    continue;
                }

                if (!flight.Started)
                {
                    flight.Started = true;
                    flight.EndTime = flight.StartTime + InitialDealDuration;
                    flight.View.gameObject.SetActive(true);
                }

                TryGetInitialDealTarget(flight, out var target, out var targetScale);
                flight.Target = target;
                flight.TargetScale = targetScale;
                flight.View.ApplyScale(targetScale);
                var progress = Mathf.Clamp01((now - flight.StartTime) / InitialDealDuration);
                var eased = progress * progress * (3f - 2f * progress);
                flight.View.transform.localPosition = Vector3.Lerp(flight.Source, target, eased);
                var scale = Mathf.Lerp(flight.SourceScale, targetScale, eased);
                flight.View.transform.localScale = Vector3.one * (flight.View.BaseScale * scale);
                if (progress < 1f)
                {
                    continue;
                }

                flight.View.transform.localPosition = target;
                flight.View.ApplyScale(targetScale);
                if (flight.IsTransient)
                {
                    ReturnViewToPool(flight.View);
                }
                else
                {
                    initialDealCardIds.Remove(flight.CardId);
                }

                initialDealFlights.RemoveAt(index);
                incomingCardsChanged = true;
            }

            if (incomingCardsChanged)
            {
                IncomingCardsChanged?.Invoke();
            }

            if (dealStarted && initialDealFlights.Count == 0)
            {
                CompleteInitialDeal();
            }
        }

        private void CaptureInitialDealTargets(int seat)
        {
            if (!handVisible || CurrentState == null || seat < 0 || seat >= CurrentState.Hands.Count)
            {
                return;
            }

            var hand = CurrentState.Hands[seat];
            for (var index = 0; index < hand.Count; index++)
            {
                var cardId = hand[index].Id;
                if (initialDealTargets.ContainsKey(cardId) && activeCards.TryGetValue(cardId, out var view) &&
                    view.transform.parent == handRoot)
                {
                    initialDealTargets[cardId] = view.TargetLocalPosition;
                }
            }
        }

        private void ReconcileInitialDealFlights(int activeSeat)
        {
            if (CurrentState == null || initialDealFlights.Count == 0)
            {
                return;
            }

            for (var index = 0; index < initialDealFlights.Count; index++)
            {
                var flight = initialDealFlights[index];
                CardView handView = null;
                var hasHandView = handVisible && flight.Seat == activeSeat && flight.Seat >= 0 &&
                                  flight.Seat < CurrentState.Hands.Count &&
                                  activeCards.TryGetValue(flight.CardId, out handView) && handView != null &&
                                  handView.transform.parent == handRoot;
                if (hasHandView)
                {
                    if (flight.View != handView)
                    {
                        var position = flight.Started && flight.View != null &&
                                       flight.View.CardId == flight.CardId
                            ? flight.View.transform.localPosition
                            : flight.Source;
                        if (flight.IsTransient)
                        {
                            ReturnViewToPool(flight.View);
                        }

                        flight.View = handView;
                        flight.IsTransient = false;
                        initialDealTargets[flight.CardId] = handView.TargetLocalPosition;
                        initialDealCardIds.Add(flight.CardId);
                        handView.ApplyScale(handView.LayoutScale);
                        handView.transform.localPosition = position;
                        handView.gameObject.SetActive(flight.Started);
                    }
                    else
                    {
                        initialDealTargets[flight.CardId] = handView.TargetLocalPosition;
                        initialDealCardIds.Add(flight.CardId);
                        if (!flight.Started)
                        {
                            handView.gameObject.SetActive(false);
                        }
                    }

                    continue;
                }

                if (flight.IsTransient || flight.Seat < 0 || flight.Seat >= CurrentState.Hands.Count ||
                    flight.CardIndex < 0 || flight.CardIndex >= CurrentState.Hands[flight.Seat].Count)
                {
                    continue;
                }

                var card = CurrentState.Hands[flight.Seat][flight.CardIndex];
                if (card.Id != flight.CardId)
                {
                    continue;
                }

                var carriedPosition = flight.Started && flight.View != null &&
                                      flight.View.CardId == flight.CardId
                    ? flight.View.transform.localPosition
                    : flight.Source;
                var transientView = CreateTransientCardView(card, CurrentState, false);
                transientView.SetLayoutPose(Quaternion.identity, 0.7f);
                transientView.transform.localPosition = carriedPosition;
                transientView.gameObject.SetActive(flight.Started);
                flight.View = transientView;
                flight.IsTransient = true;
                initialDealTargets.Remove(flight.CardId);
                initialDealCardIds.Remove(flight.CardId);
            }
        }

        private bool TryGetInitialDealTarget(InitialDealFlight flight, out Vector3 target, out float targetScale)
        {
            target = flight.Target;
            targetScale = flight.TargetScale;
            if (flight.View == null)
            {
                return false;
            }

            if (!flight.IsTransient && initialDealTargets.TryGetValue(flight.CardId, out target))
            {
                targetScale = Mathf.Max(0.01f, flight.View.LayoutScale);
                return true;
            }

            if (SeatCardSlotResolver != null &&
                SeatCardSlotResolver(flight.Seat, flight.CardIndex, out var screenPosition,
                    out var screenHeight) && IsFinite(screenPosition.x) && IsFinite(screenPosition.y) &&
                IsFinite(screenHeight) && screenHeight > 0f)
            {
                flight.LastTargetScreen = screenPosition;
                flight.ScreenHeight = screenHeight;
                flight.HasTarget = true;
            }
            else if (!flight.HasTarget)
            {
                var displayedSeat = CurrentState == null ? -1 : DisplaySeat(CurrentState);
                flight.LastTargetScreen = GetDealScreenPosition(flight.Seat, flight.CardIndex, displayedSeat);
                flight.ScreenHeight = seatScreenRects.TryGetValue(flight.Seat, out var screenRect)
                    ? screenRect.z
                    : 0f;
                flight.HasTarget = true;
            }

            target = HandLocalPoint(flight.LastTargetScreen);
            targetScale = flight.ScreenHeight > 0f
                ? GetScreenHeightScale(flight.LastTargetScreen, flight.ScreenHeight, flight.View)
                : Mathf.Max(0.01f, flight.View.LayoutScale);
            return true;
        }

        private float GetInitialDealSourceScale(CardView view, float targetScale)
        {
            if (drawPileScreenHeight > 0f)
            {
                return GetScreenHeightScale(drawPileScreenPosition, drawPileScreenHeight, view);
            }

            return Mathf.Max(0.01f, targetScale * BoardPileScale);
        }

        private void StashInitialDealHandAtPile()
        {
            if (CurrentState == null)
            {
                return;
            }

            var pilePoint = default(Vector3);
            var hasPilePoint = handRoot != null && tableCamera != null && tableCamera.Camera != null &&
                               TryGetPilePoint(handRoot, out pilePoint);

            var seat = DisplaySeat(CurrentState);
            if (!handVisible || seat < 0 || seat >= CurrentState.Hands.Count)
            {
                return;
            }

            var hand = CurrentState.Hands[seat];
            for (var index = 0; index < hand.Count; index++)
            {
                var cardId = hand[index].Id;
                if (!activeCards.TryGetValue(cardId, out var view) || initialDealTargets.ContainsKey(cardId))
                {
                    continue;
                }

                initialDealTargets.Add(cardId, view.TargetLocalPosition);
                initialDealCardIds.Add(cardId);
                if (hasPilePoint)
                {
                    view.transform.localPosition = pilePoint;
                    view.SetStartScale(BoardPileScale);
                }

                view.gameObject.SetActive(false);
            }
        }

        private void TryStartInitialDeal()
        {
            if (!dealPending || CurrentState == null || CurrentState.Phase != GamePhase.Playing)
            {
                return;
            }

            if (DealHeld)
            {
                dealRequestedAt = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - dealRequestedAt > InitialDealWaitSeconds)
            {
                RevealStashedInitialDealCards();
                CompleteInitialDeal();
                return;
            }

            if (tableCamera == null || tableCamera.Camera == null || handRoot == null ||
                !hasDrawPileScreenPosition || !IsFinite(drawPileScreenPosition.x) ||
                !IsFinite(drawPileScreenPosition.y) || !TryGetPilePoint(handRoot, out var pilePoint))
            {
                return;
            }

            var maxCards = 0;
            for (var seat = 0; seat < CurrentState.Hands.Count; seat++)
            {
                maxCards = Mathf.Max(maxCards, CurrentState.Hands[seat].Count);
            }

            var displayedSeat = DisplaySeat(CurrentState);
            var now = Time.unscaledTime;
            var sequence = 0;
            for (var cardIndex = 0; cardIndex < maxCards; cardIndex++)
            {
                for (var offset = 1; offset <= CurrentState.Hands.Count; offset++)
                {
                    var seat = CurrentState.Dealer.HasValue
                        ? (CurrentState.Dealer.Value + offset) % CurrentState.Hands.Count
                        : (displayedSeat + offset) % CurrentState.Hands.Count;
                    var hand = CurrentState.Hands[seat];
                    if (cardIndex >= hand.Count)
                    {
                        continue;
                    }

                    var card = hand[cardIndex];
                    CardView view = null;
                    var isVisibleOwnCard = seat == displayedSeat && handVisible &&
                                           activeCards.TryGetValue(card.Id, out view);
                    Vector3 target;
                    var isTransient = !isVisibleOwnCard;
                    if (isVisibleOwnCard)
                    {
                        target = initialDealTargets.TryGetValue(card.Id, out var savedTarget)
                            ? savedTarget
                            : view.TargetLocalPosition;
                        initialDealTargets[card.Id] = target;
                        initialDealCardIds.Add(card.Id);
                        view.transform.localPosition = pilePoint;
                        view.SetStartScale(BoardPileScale);
                    }
                    else
                    {
                        view = CreateTransientCardView(card, CurrentState, false);
                        target = HandLocalPoint(GetDealScreenPosition(seat, cardIndex, displayedSeat));
                        view.SetLayoutPose(Quaternion.identity, 0.7f);
                        view.transform.localPosition = pilePoint;
                        view.SetStartScale(BoardPileScale);
                        view.gameObject.SetActive(false);
                    }

                    var startTime = now + sequence * InitialDealStagger;
                    var flight = new InitialDealFlight
                    {
                        Seat = seat,
                        CardIndex = cardIndex,
                        CardId = card.Id,
                        View = view,
                        Source = pilePoint,
                        Target = target,
                        StartTime = startTime,
                        EndTime = startTime + InitialDealDuration,
                        IsTransient = isTransient
                    };
                    TryGetInitialDealTarget(flight, out flight.Target, out flight.TargetScale);
                    flight.SourceScale = GetInitialDealSourceScale(view, flight.TargetScale);
                    view.ApplyScale(flight.TargetScale);
                    view.SetStartScale(flight.SourceScale / Mathf.Max(flight.TargetScale, 0.01f));
                    initialDealFlights.Add(flight);
                    sequence++;
                }
            }

            dealPending = false;
            dealStarted = true;
            dealArmed = false;
            if (initialDealFlights.Count == 0)
            {
                CompleteInitialDeal();
            }

            IncomingCardsChanged?.Invoke();
        }

        private void RevealStashedInitialDealCards()
        {
            foreach (var target in initialDealTargets)
            {
                if (activeCards.TryGetValue(target.Key, out var view))
                {
                    view.gameObject.SetActive(true);
                    view.MoveTo(target.Value, 0f);
                }
            }

            initialDealCardIds.Clear();
            initialDealTargets.Clear();
        }

        private void CompleteInitialDeal()
        {
            dealPending = false;
            dealArmed = false;
            dealStarted = false;
            if (initialDealFlights.Count == 0)
            {
                initialDealCardIds.Clear();
                initialDealTargets.Clear();
            }

            IncomingCardsChanged?.Invoke();
            DealCompleted?.Invoke();
        }

        private Vector2 GetDealScreenPosition(int seat, int cardIndex, int displayedSeat)
        {
            if (seatScreenRects.TryGetValue(seat, out var screenRect))
            {
                seatScreenSteps.TryGetValue(seat, out var step);
                return new Vector2(screenRect.x, screenRect.y) + step * cardIndex;
            }

            var screenX = Screen.width * (seat + 1f) / Mathf.Max(CurrentState.Hands.Count + 1f, 1f);
            var screenY = seat == displayedSeat ? Screen.height * 0.08f : Screen.height * 0.82f;
            return new Vector2(screenX, screenY);
        }

        private CardView CreateTransientCardView(Card card, GameState state, bool faceUp)
        {
            PreparePool();
            var view = cardPool.Count > 0 ? cardPool.Pop() : InstantiateCardView();
            view.gameObject.SetActive(true);
            var skin = ResolveSkin(state, card.Deck);
            var face = skin == null ? null : skin.GetFace(card.Suit, card.Rank);
            var back = skin == null
                ? null
                : skinCatalog.ResolveBack(state.DeckFaces[card.Deck], card.Deck);
            view.SetParentKeepWorld(handRoot, true);
            view.ConfigureCamera(tableCamera, skin == null ? 100f : skin.PixelsPerUnit);
            view.SetLayoutPose(Quaternion.identity, 1f);
            view.SetCard(card, face, back, faceUp, false);
            view.SetSortingOrder(CardView.DragSortingOrder);
            return view;
        }

        private bool TryCreateCollectionBack(Card card, int seat, int cardIndex, int displayedSeat,
            out CardView view)
        {
            view = null;
            if (handRoot == null || tableCamera == null || tableCamera.Camera == null)
            {
                return false;
            }

            view = CreateTransientCardView(card, CurrentState, false);
            view.transform.localPosition = HandLocalPoint(GetDealScreenPosition(seat, cardIndex, displayedSeat));
            view.SetStartScale(0.7f);
            return true;
        }

        private void CollectActiveCard(CardView view, float now, int index)
        {
            if (view == null)
            {
                return;
            }

            if (activeCards.TryGetValue(view.CardId, out var activeView) && activeView == view)
            {
                activeCards.Remove(view.CardId);
            }

            if (!TryGetPilePoint(view.transform.parent, out var target))
            {
                ReturnViewToPool(view);
                return;
            }

            var startTime = now + Mathf.Min(index * BoardCardStagger, BoardCardStaggerLimit);
            boardCollectFlights.Add(new BoardCardFlight
            {
                CardId = view.CardId,
                View = view,
                Target = target,
                StartTime = startTime,
                EndTime = startTime + BoardCardFlightDuration
            });
        }

        private void ReturnViewToPool(CardView view)
        {
            if (view == null)
            {
                return;
            }

            if (activeCards.TryGetValue(view.CardId, out var activeView) && activeView == view)
            {
                activeCards.Remove(view.CardId);
            }

            view.gameObject.SetActive(false);
            cardPool.Push(view);
        }

        private void LayoutClearedBoard()
        {
            if (boardView == null || tableCamera == null || tableCamera.Camera == null)
            {
                return;
            }

            boardView.Layout(emptyBoard, activeCards, GetBoardViewportAspect(), cardSize, liftedBoardCardIds);
            tableCamera.SetBoardBounds(boardView.CurrentBounds);
        }

        private static int CountBoardCards(GameState state)
        {
            if (state == null || state.Board == null)
            {
                return 0;
            }

            var count = 0;
            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                count += state.Board[meldIndex].Cards.Count;
            }

            return count;
        }

        private List<Card> FindCardsDrawnFromPile(GameState state, out int drawingSeat)
        {
            var cards = new List<Card>();
            drawingSeat = -1;
            var drawCount = lastDrawPileCount - state.DrawPile.Count;
            if (!ReferenceEquals(lastSyncedState, state) || state.Phase != GamePhase.Playing ||
                drawCount <= 0 || drawCount > MaximumDrawFlightCards ||
                lastHandCounts.Length != state.Hands.Count || !hasDrawPileScreenPosition ||
                !IsFinite(drawPileScreenPosition.x) || !IsFinite(drawPileScreenPosition.y))
            {
                return cards;
            }

            var gainedCount = 0;
            for (var seat = 0; seat < state.Hands.Count; seat++)
            {
                var gained = state.Hands[seat].Count - lastHandCounts[seat];
                if (gained <= 0)
                {
                    continue;
                }

                if (drawingSeat >= 0 || seat == DisplaySeat(state) || gained > MaximumDrawFlightCards ||
                    !seatScreenRects.ContainsKey(seat))
                {
                    return cards;
                }

                drawingSeat = seat;
                gainedCount = gained;
            }

            if (drawingSeat < 0 || gainedCount != drawCount)
            {
                drawingSeat = -1;
                return cards;
            }

            var hand = state.Hands[drawingSeat];
            for (var index = lastHandCounts[drawingSeat]; index < hand.Count; index++)
            {
                cards.Add(hand[index]);
            }

            return cards;
        }

        private void RememberHandCounts(GameState state)
        {
            if (lastHandCounts.Length != state.Hands.Count)
            {
                lastHandCounts = new int[state.Hands.Count];
            }

            for (var seat = 0; seat < state.Hands.Count; seat++)
            {
                lastHandCounts[seat] = state.Hands[seat].Count;
            }
        }

        private void AnimateDrawnCard(Card card, Vector3 seatScreenRect, Vector2 targetScreenPosition)
        {
            var view = cardPool.Count == 0 ? InstantiateCardView() : cardPool.Pop();
            view.gameObject.SetActive(true);
            view.SetParentKeepWorld(handRoot, true);
            var skin = ResolveSkin(CurrentState, card.Deck);
            var face = skin == null ? null : skin.GetFace(card.Suit, card.Rank);
            var back = GetCardBack(CurrentState, card.Deck);
            view.ConfigureCamera(tableCamera, skin == null ? 100f : skin.PixelsPerUnit);
            view.SetHinted(false);
            view.SetLastCardWarning(false);
            view.SetCard(card, face, back, false, false);

            var seatScreenPosition = targetScreenPosition;
            var targetScale = GetScreenHeightScale(seatScreenPosition, seatScreenRect.z, view);
            var pileScale = GetScreenHeightScale(drawPileScreenPosition, drawPileScreenHeight, view);
            view.SetLayoutPose(Quaternion.identity, targetScale);
            view.SetStartScale(pileScale / Mathf.Max(targetScale, 0.01f));
            view.transform.localPosition = HandLocalPoint(drawPileScreenPosition);
            view.SetSortingOrder(CardView.DragSortingOrder - 1);
            view.MoveTo(HandLocalPoint(seatScreenPosition), DrawFlightDuration);
            drawingCards.Add(view);
        }

        private float GetScreenHeightScale(Vector2 screenPosition, float screenHeight, CardView cardView)
        {
            if (screenHeight <= 0f || tableCamera == null || tableCamera.Camera == null ||
                cardView == null || cardView.Renderer == null)
            {
                return 1f;
            }

            var camera = tableCamera.Camera;
            var distance = Mathf.Abs(camera.transform.position.z);
            var halfHeight = screenHeight * 0.5f;
            var top = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y + halfHeight, distance));
            var bottom = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y - halfHeight, distance));
            var sprite = cardView.Renderer.sprite;
            var parentScale = cardView.transform.parent == null ? 1f : cardView.transform.parent.lossyScale.y;
            var cardHeight = sprite == null ? 0f : sprite.bounds.size.y * cardView.BaseScale * parentScale;
            return cardHeight <= 0f ? 1f : Mathf.Clamp(Mathf.Abs(top.y - bottom.y) / cardHeight, 0.01f, 1f);
        }

        public IReadOnlyList<CardView> DrawingCards => drawingCards;

        private void UpdateDrawingCards()
        {
            var changed = false;
            for (var index = drawingCards.Count - 1; index >= 0; index--)
            {
                var view = drawingCards[index];
                if (view != null && view.IsMoving)
                {
                    continue;
                }

                drawingCards.RemoveAt(index);
                drawingSeats.RemoveAt(index);
                changed = true;
                if (view != null)
                {
                    view.gameObject.SetActive(false);
                    cardPool.Push(view);
                }
            }

            if (changed)
            {
                IncomingCardsChanged?.Invoke();
            }
        }

        private void ClearDrawingCards()
        {
            var hadDrawingCards = drawingCards.Count > 0;
            for (var index = 0; index < drawingCards.Count; index++)
            {
                var view = drawingCards[index];
                if (view == null)
                {
                    continue;
                }

                view.gameObject.SetActive(false);
                cardPool.Push(view);
            }

            drawingCards.Clear();
            drawingSeats.Clear();
            if (hadDrawingCards)
            {
                IncomingCardsChanged?.Invoke();
            }
        }

        public void SetViewerSeat(int seat)
        {
            if (viewerSeat == seat)
            {
                return;
            }

            viewerSeat = seat;
            if (CurrentState != null)
            {
                Sync(CurrentState, CurrentTurn);
            }
        }

        public void SetCpuSeatPredicate(Func<int, bool> predicate)
        {
            cpuSeatPredicate = predicate;
        }

        public void ClearCpuPlacementViews()
        {
            humanPlacementAreas.Clear();
        }

        private void RecordHumanPlacementArea(GameState state)
        {
            if (state == null || !handVisible || boardView == null || tableCamera == null ||
                tableCamera.Camera == null)
            {
                return;
            }

            var seat = DisplaySeat(state);
            if (seat < 0 || seat >= state.Hands.Count ||
                (cpuSeatPredicate != null && cpuSeatPredicate(seat)))
            {
                return;
            }

            humanPlacementAreas[seat] = GetBoardLocalVisibleRect();
        }

        private void UpdateCpuPlacementArea(GameState state)
        {
            if (boardView == null)
            {
                return;
            }

            if (state == null || cpuSeatPredicate == null || !cpuSeatPredicate(state.CurrentSeat))
            {
                boardView.SetCpuPlacementArea(null);
                return;
            }

            var area = GetSmallestHumanPlacementArea();
            if (!area.HasValue)
            {
                area = GetBoardLocalVisibleRect();
            }

            var bounds = area.Value;
            var marginX = Mathf.Min(cardSize.x * 0.2f, bounds.width * 0.25f);
            var marginY = Mathf.Min(cardSize.y * 0.12f, bounds.height * 0.25f);
            bounds.xMin += marginX;
            bounds.xMax -= marginX;
            bounds.yMin += marginY;
            bounds.yMax -= marginY;
            boardView.SetCpuPlacementArea(bounds);
        }

        private Rect? GetSmallestHumanPlacementArea()
        {
            if (humanPlacementAreas.Count == 0)
            {
                return null;
            }

            var hasIntersection = false;
            var intersection = default(Rect);
            var smallest = default(Rect);
            var smallestArea = float.PositiveInfinity;
            foreach (var pair in humanPlacementAreas)
            {
                var area = pair.Value;
                if (!hasIntersection)
                {
                    intersection = area;
                    hasIntersection = true;
                }
                else
                {
                    intersection = Rect.MinMaxRect(Mathf.Max(intersection.xMin, area.xMin),
                        Mathf.Max(intersection.yMin, area.yMin), Mathf.Min(intersection.xMax, area.xMax),
                        Mathf.Min(intersection.yMax, area.yMax));
                }

                var size = area.width * area.height;
                if (size < smallestArea)
                {
                    smallestArea = size;
                    smallest = area;
                }
            }

            return intersection.width > 0f && intersection.height > 0f ? intersection : smallest;
        }

        private Rect GetBoardLocalVisibleRect()
        {
            var world = tableCamera.GetVisibleBoardWorldRect();
            var bottomLeft = boardView.transform.InverseTransformPoint(new Vector3(world.xMin, world.yMin, 0f));
            var bottomRight = boardView.transform.InverseTransformPoint(new Vector3(world.xMax, world.yMin, 0f));
            var topLeft = boardView.transform.InverseTransformPoint(new Vector3(world.xMin, world.yMax, 0f));
            var topRight = boardView.transform.InverseTransformPoint(new Vector3(world.xMax, world.yMax, 0f));
            return Rect.MinMaxRect(
                Mathf.Min(bottomLeft.x, bottomRight.x, topLeft.x, topRight.x),
                Mathf.Min(bottomLeft.y, bottomRight.y, topLeft.y, topRight.y),
                Mathf.Max(bottomLeft.x, bottomRight.x, topLeft.x, topRight.x),
                Mathf.Max(bottomLeft.y, bottomRight.y, topLeft.y, topRight.y));
        }

        public void SetHandVisible(bool visible)
        {
            if (handVisible == visible)
            {
                return;
            }

            handVisible = visible;
            if (CurrentState != null)
            {
                Sync(CurrentState, CurrentTurn);
            }
        }

        public void SetHandConcealed(bool concealed)
        {
            if (handConcealed == concealed)
            {
                return;
            }

            handConcealed = concealed;
            if (CurrentState != null)
            {
                Sync(CurrentState, CurrentTurn);
            }
        }

        public void SetHandBottomInset(float screenFraction)
        {
            var inset = Mathf.Clamp01(screenFraction) * handReferenceSize * 2f;
            if (Mathf.Approximately(handBottomInset, inset))
            {
                return;
            }

            handBottomInset = inset;
            RefreshHandLayout();
        }

        public float EdgePanBottomInset => edgePanBottomInset;

        public void SetEdgePanBottomInset(float screenFraction)
        {
            edgePanBottomInset = Mathf.Clamp01(screenFraction);
        }

        public void SetBoardInsets(float top, float left, float right)
        {
            var nextTop = Mathf.Clamp(top, 0f, 0.45f);
            var nextLeft = Mathf.Clamp(left, 0f, 0.45f);
            var nextRight = Mathf.Clamp(right, 0f, 0.45f);
            var bottom = tableCamera == null ? 0f : tableCamera.BottomInsetFraction;
            if (handVisible && handView != null && handReferenceSize > 0f &&
                !float.IsNaN(handView.HandTopY) && !float.IsInfinity(handView.HandTopY))
            {
                bottom = Mathf.Clamp01((handView.HandTopY + handReferenceSize) / (handReferenceSize * 2f));
            }

            if (Mathf.Approximately(boardTopInset, nextTop) && Mathf.Approximately(boardLeftInset, nextLeft) &&
                Mathf.Approximately(boardRightInset, nextRight) &&
                (tableCamera == null || Mathf.Approximately(tableCamera.BottomInsetFraction,
                    Mathf.Max(tableCamera.BottomInsetFloor, bottom))))
            {
                return;
            }

            boardTopInset = nextTop;
            boardLeftInset = nextLeft;
            boardRightInset = nextRight;

            tableCamera?.SetViewportInsets(boardTopInset, bottom, boardLeftInset, boardRightInset);
            if (CurrentState != null)
            {
                Sync(CurrentState, CurrentTurn);
            }
        }

        public void RefreshHandLayout()
        {
            if (CurrentState != null)
            {
                LayoutHand(DisplaySeat(CurrentState));
            }
        }

        public void ScrollHandByScreenDelta(float screenDelta)
        {
            if (CurrentState == null || handView == null || tableCamera == null || tableCamera.Camera == null ||
                Screen.height <= 0)
            {
                return;
            }

            var seat = DisplaySeat(CurrentState);
            if (seat < 0 || seat >= CurrentState.Hands.Count)
            {
                return;
            }

            var referenceDelta = -screenDelta * handReferenceSize * 2f / Screen.height;
            handView.Scroll(referenceDelta);
            LayoutHand(seat);
        }

        public void BeginHandLift(byte sourceId, IReadOnlyList<byte> selectedIds, Vector2 screenPosition)
        {
            if (handRoot == null || tableCamera == null || tableCamera.Camera == null)
            {
                return;
            }

            liftedHandOffsets.Clear();
            var pointer = HandLocalPoint(screenPosition);
            var ids = new List<byte>();
            if (selectedIds != null)
            {
                for (var index = 0; index < selectedIds.Count; index++)
                {
                    if (!ids.Contains(selectedIds[index])) ids.Add(selectedIds[index]);
                }
            }

            if (!ids.Contains(sourceId)) ids.Add(sourceId);
            for (var index = 0; index < ids.Count; index++)
            {
                if (!activeCards.TryGetValue(ids[index], out var view) || view.transform.parent != handRoot)
                {
                    continue;
                }

                liftedHandOffsets[ids[index]] = view.transform.localPosition - pointer;
                view.SetSortingOrder(CardView.DragSortingOrder + index);
                view.SetLayoutPose(Quaternion.identity, view.LayoutScale * 1.08f);
                view.ApplyScale(view.LayoutScale);
            }

            handGapIndex = 0;
            var seat = DisplaySeat(CurrentState);
            var orderedIds = handOrder.GetCardIds(seat);
            for (var index = 0; index < orderedIds.Count; index++)
            {
                if (liftedHandOffsets.ContainsKey(orderedIds[index]))
                {
                    break;
                }

                handGapIndex++;
            }

            UpdateHandLift(screenPosition);
            LayoutHand(seat);
        }

        public void UpdateHandLift(Vector2 screenPosition)
        {
            if (handRoot == null || handView == null || liftedHandOffsets.Count == 0)
            {
                return;
            }

            var pointer = HandLocalPoint(screenPosition);
            foreach (var pair in liftedHandOffsets)
            {
                if (activeCards.TryGetValue(pair.Key, out var view))
                {
                    view.transform.localPosition = pointer + pair.Value;
                }
            }

            var hit = HitTest(screenPosition);
            var localPointer = HandLocalPoint(screenPosition);
            if (localPointer.y <= handView.HandTopY || hit.Kind == GestureHitKind.HandCard ||
                hit.Kind == GestureHitKind.HandArea)
            {
                var averagePosition = Vector3.zero;
                var liftedCount = 0;
                foreach (var pair in liftedHandOffsets)
                {
                    if (activeCards.TryGetValue(pair.Key, out var view))
                    {
                        averagePosition += view.transform.localPosition;
                        liftedCount++;
                    }
                }

                if (liftedCount > 0)
                {
                    averagePosition /= liftedCount;
                    var slot = handView.GetSlotIndex(averagePosition);
                    var groupOffset = (liftedCount - 1) * 0.5f;
                    var seat = DisplaySeat(CurrentState);
                    var nonLiftedCount = seat >= 0 && seat < CurrentState.Hands.Count
                        ? Mathf.Max(0, CurrentState.Hands[seat].Count - liftedCount)
                        : 0;
                    handGapIndex = Mathf.Clamp(Mathf.RoundToInt(slot - groupOffset), 0, nonLiftedCount);
                }
            }
            else
            {
                handGapIndex = -1;
            }

            LayoutHand(DisplaySeat(CurrentState));
        }

        public bool TryGetHandDropTarget(out GestureHit target)
        {
            if (liftedHandOffsets.Count > 0 && handGapIndex >= 0)
            {
                target = GestureHit.HandSlot(handGapIndex);
                return true;
            }

            target = GestureHit.None;
            return false;
        }

        public void EndHandLift()
        {
            liftedHandOffsets.Clear();
            handGapIndex = -1;
        }

        public void BeginBoardLift(byte sourceId, IReadOnlyList<byte> selectedIds, Vector2 screenPosition)
        {
            if (CurrentState == null || tableCamera == null || tableCamera.Camera == null)
            {
                return;
            }

            liftedBoardOffsets.Clear();
            var pointer = BoardLocalPoint(screenPosition);
            var ids = new List<byte>();
            if (selectedIds != null)
            {
                for (var index = 0; index < selectedIds.Count; index++)
                {
                    if (!ids.Contains(selectedIds[index]))
                    {
                        ids.Add(selectedIds[index]);
                    }
                }
            }

            if (!ids.Contains(sourceId))
            {
                ids.Add(sourceId);
            }

            for (var index = 0; index < ids.Count; index++)
            {
                if (!activeCards.TryGetValue(ids[index], out var view) || view.transform.parent == handRoot)
                {
                    continue;
                }

                liftedBoardOffsets[ids[index]] = view.transform.localPosition - pointer;
                view.SetSortingOrder(CardView.DragSortingOrder + index);
                view.SetLayoutPose(Quaternion.identity, 1.06f);
                view.ApplyScale(1.06f);
            }

            UpdateLiftedBoardCardIds();
            UpdateBoardLift(screenPosition);
        }

        public void UpdateBoardLift(Vector2 screenPosition)
        {
            if (liftedBoardOffsets.Count == 0)
            {
                return;
            }

            var pointer = BoardLocalPoint(screenPosition);
            foreach (var pair in liftedBoardOffsets)
            {
                if (activeCards.TryGetValue(pair.Key, out var view))
                {
                    view.transform.localPosition = pointer + pair.Value;
                }
            }
        }

        public void EndBoardLift()
        {
            liftedBoardOffsets.Clear();
            UpdateLiftedBoardCardIds();
        }

        public bool ReorderMeldByScreenPosition(int meldId, Vector2 screenPosition)
        {
            if (boardView == null || boardView.CurrentLayout == null || tableCamera == null || tableCamera.Camera == null)
            {
                return false;
            }

            var camera = tableCamera.Camera;
            var distance = Mathf.Abs(camera.transform.position.z);
            var world = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distance));
            var localPoint = boardView.transform.InverseTransformPoint(world);
            var local = new Vector2(localPoint.x, localPoint.y);
            if (GameSettings.OrganicBoard)
            {
                var movedOrganically = boardView.MoveMeldByDelta(meldId, local - meldLiftStartBoardPointer);
                if (movedOrganically && tableCamera != null)
                {
                    tableCamera.SetBoardBounds(boardView.CurrentBounds);
                }

                return movedOrganically;
            }

            var layouts = boardView.CurrentLayout.Melds;
            var targetIndex = 0;
            var bestDistance = float.MaxValue;
            for (var index = 0; index < layouts.Count; index++)
            {
                if (layouts[index].MeldId == meldId)
                {
                    continue;
                }

                var center = layouts[index].Bounds.center;
                var distanceToMeld = Vector2.SqrMagnitude(local - center);
                if (distanceToMeld < bestDistance)
                {
                    bestDistance = distanceToMeld;
                    targetIndex = index + (local.x > center.x ? 1 : 0);
                }
            }

            var moved = boardArrangement.Move(meldId, targetIndex);
            if (CurrentState != null)
            {
                Sync(CurrentState, CurrentTurn);
            }

            return moved;
        }

        public void BeginMeldLift(int meldId, Vector2 screenPosition)
        {
            if (CurrentState == null || boardView == null || tableCamera == null || tableCamera.Camera == null)
            {
                return;
            }

            var meld = FindMeld(CurrentState.Board, meldId);
            if (meld == null)
            {
                return;
            }

            liftedMeldOffsets.Clear();
            var pointer = BoardLocalPoint(screenPosition);
            meldLiftStartPointer = pointer;
            meldLiftStartBoardPointer = BoardPoint(screenPosition);
            liftedMeldId = meldId;
            for (var index = 0; index < meld.Cards.Count; index++)
            {
                var cardId = meld.Cards[index].Id;
                if (!activeCards.TryGetValue(cardId, out var view))
                {
                    continue;
                }

                liftedMeldOffsets[cardId] = view.transform.localPosition - pointer;
                view.SetSortingOrder(CardView.DragSortingOrder + index);
                view.SetLayoutPose(Quaternion.identity, 1.06f);
                view.ApplyScale(1.06f);
            }

            UpdateLiftedBoardCardIds();
            UpdateMeldLift(screenPosition);
        }

        public void UpdateMeldLift(Vector2 screenPosition)
        {
            if (liftedMeldOffsets.Count == 0)
            {
                return;
            }

            var pointer = BoardLocalPoint(screenPosition);
            foreach (var pair in liftedMeldOffsets)
            {
                if (activeCards.TryGetValue(pair.Key, out var view))
                {
                    view.transform.localPosition = pointer + pair.Value;
                }
            }

            if (boardView != null)
            {
                var offset = pointer - meldLiftStartPointer;
                boardView.SetMeldOutlineOffset(liftedMeldId, new Vector3(offset.x, offset.y, 0f));
            }
        }

        public void EndMeldLift()
        {
            liftedMeldOffsets.Clear();
            UpdateLiftedBoardCardIds();
            if (boardView != null && liftedMeldId >= 0)
            {
                boardView.SetMeldOutlineOffset(liftedMeldId, Vector3.zero);
            }

            liftedMeldId = -1;
        }

        private void UpdateLiftedBoardCardIds()
        {
            liftedBoardCardIds.Clear();
            foreach (var pair in liftedBoardOffsets)
            {
                liftedBoardCardIds.Add(pair.Key);
            }

            foreach (var pair in liftedMeldOffsets)
            {
                liftedBoardCardIds.Add(pair.Key);
            }

            boardView?.SetLiftedCardIds(liftedBoardCardIds);
        }

        private void LayoutHand(int seat, float moveDuration = -1f)
        {
            if (!handVisible || CurrentState == null || handView == null || handRoot == null ||
                tableCamera == null || tableCamera.Camera == null || seat < 0 || seat >= CurrentState.Hands.Count)
            {
                return;
            }

            handView.Layout(CurrentState.Hands[seat], handOrder, seat, activeCards, handRoot, cardSize,
                handReferenceSize, GetViewportAspect(), handBottomInset, moveDuration,
                liftedHandOffsets.Count == 0 ? null : liftedHandOffsets.Keys, handGapIndex);
            lastHandAspect = GetViewportAspect();
            hasHandAspect = true;
        }

        private int DisplaySeat(GameState state)
        {
            return viewerSeat >= 0 && viewerSeat < state.Hands.Count ? viewerSeat : state.CurrentSeat;
        }

        private void AddVisibleCard(Card card, GameState state, bool isHandCard)
        {
            var skin = ResolveSkin(state, card.Deck);
            var isSelected = activeCards.TryGetValue(card.Id, out var existingView) && existingView.IsSelected;
            var view = GetOrCreateCardView(card.Id);
            var face = skin == null ? null : skin.GetFace(card.Suit, card.Rank);
            var back = skin == null
                ? null
                : skinCatalog.ResolveBack(state.DeckFaces[card.Deck], card.Deck);
            var ppu = skin == null ? 100f : skin.PixelsPerUnit;
            view.SetParentKeepWorld(isHandCard && handRoot != null ? handRoot : transform, isHandCard);
            view.ConfigureCamera(tableCamera, ppu);
            if (!isHandCard)
            {
                view.SetLayoutPose(Quaternion.identity, 1f);
            }
            view.SetCard(card, face, back, !handConcealed, isSelected, skin == null || skin.FourColourInk);
            var displaySeat = DisplaySeat(state);
            var hasSingleHandCard = isHandCard && displaySeat >= 0 && displaySeat < state.Hands.Count &&
                                    state.Hands[displaySeat].Count == 1;
            view.SetLastCardWarning(hasSingleHandCard);
            visibleCardIds.Add(card.Id);
        }

        public Sprite GetCardBack(GameState state, int deckIndex)
        {
            if (state == null || skinCatalog == null || state.DeckFaces == null || deckIndex < 0 ||
                deckIndex >= state.DeckFaces.Length)
            {
                return null;
            }

            return skinCatalog.ResolveBack(state.DeckFaces[deckIndex], deckIndex);
        }

        private DeckSkin ResolveSkin(GameState state, int deckIndex)
        {
            if (skinCatalog == null || state.DeckFaces == null || deckIndex < 0 || deckIndex >= state.DeckFaces.Length)
            {
                return null;
            }

            var skin = skinCatalog.Resolve(state.DeckFaces[deckIndex], deckIndex);
            if (skin != null && skin.PixelsPerUnit > 0f)
            {
                var sample = skin.GetFace(Suit.Clubs, Rank.Ace);
                if (sample != null)
                {
                    pixelsPerUnit = skin.PixelsPerUnit;
                    var aspect = sample.bounds.size.y > 0f
                        ? sample.bounds.size.x / sample.bounds.size.y
                        : sample.rect.width / sample.rect.height;
                    cardSize = new Vector2(0.92f * aspect, 0.92f);
                }
            }

            return skin;
        }

        private CardView GetOrCreateCardView(byte cardId)
        {
            if (activeCards.TryGetValue(cardId, out var view))
            {
                return view;
            }

            if (cardPool.Count == 0)
            {
                view = InstantiateCardView();
            }
            else
            {
                view = cardPool.Pop();
                view.gameObject.SetActive(true);
            }

            activeCards.Add(cardId, view);
            return view;
        }

        private void PreparePool()
        {
            if (poolReady)
            {
                return;
            }

            if (cardPrefab == null)
            {
                throw new InvalidOperationException("TableView requires a CardView prefab.");
            }

            var count = Mathf.Clamp(pooledCardCount, 1, Card.CardCount);
            for (var index = 0; index < count; index++)
            {
                var view = InstantiateCardView();
                view.gameObject.SetActive(false);
                cardPool.Push(view);
            }

            poolReady = true;
        }

        private CardView InstantiateCardView()
        {
            var cardObject = Instantiate(cardPrefab, transform);
            var view = cardObject.GetComponent<CardView>();
            if (view == null)
            {
                throw new InvalidOperationException("The Card prefab must contain a CardView component.");
            }

            return view;
        }

        private float GetViewportAspect()
        {
            if (Screen.width > 0 && Screen.height > 0)
            {
                return (float)Screen.width / Screen.height;
            }

            return tableCamera != null && tableCamera.Camera != null ? tableCamera.Camera.aspect : 1f;
        }

        private float GetBoardViewportAspect()
        {
            var availableHeight = tableCamera == null
                ? 1f
                : 1f - tableCamera.TopInsetFraction - tableCamera.BottomInsetFraction;
            var availableWidth = tableCamera == null
                ? 1f
                : 1f - tableCamera.LeftInsetFraction - tableCamera.RightInsetFraction;
            return GetViewportAspect() * Mathf.Max(availableWidth, 0.1f) / Mathf.Max(availableHeight, 0.1f);
        }

        private void EnsureHandRoot()
        {
            if (handRoot != null || tableCamera == null || tableCamera.Camera == null)
            {
                return;
            }

            handRoot = new GameObject("Hand Root").transform;
            handRoot.SetParent(tableCamera.Camera.transform, false);
            UpdateHandRoot();
        }

        private void UpdateHandRoot()
        {
            var camera = tableCamera.Camera;
            var cameraPosition = camera.transform.position;
            var viewPlanePoint = new Vector3(cameraPosition.x, cameraPosition.y, 0f);
            var localPosition = camera.transform.InverseTransformPoint(viewPlanePoint);
            var introOffset = 0f;
            if (handIntroDuration > 0f)
            {
                introOffset = -camera.orthographicSize * 2f;
                var elapsed = Time.unscaledTime - handIntroStartedAt - handIntroDelay;
                if (elapsed >= 0f)
                {
                    var progress = Mathf.Clamp01(elapsed / handIntroDuration);
                    var eased = progress * progress * (3f - 2f * progress);
                    introOffset = -camera.orthographicSize * 2f * (1f - eased);
                    if (progress >= 1f)
                    {
                        handIntroDuration = 0f;
                    }
                }
            }

            var handHeight = camera.orthographicSize * 2f;
            var outroOffset = 0f;
            if (handOutroCompleted)
            {
                outroOffset = -handHeight;
            }
            else if (handOutroDuration > 0f || handOutroDelay > 0f)
            {
                var elapsed = Time.unscaledTime - handOutroStartedAt - handOutroDelay;
                if (elapsed >= 0f)
                {
                    var progress = handOutroDuration <= 0f
                        ? 1f
                        : Mathf.Clamp01(elapsed / handOutroDuration);
                    var eased = progress * progress * (3f - 2f * progress);
                    outroOffset = -handHeight * eased;
                    if (progress >= 1f)
                    {
                        handOutroDuration = 0f;
                        handOutroDelay = 0f;
                        handOutroCompleted = true;
                    }
                }
            }

            handRoot.localPosition = new Vector3(localPosition.x, introOffset + outroOffset, localPosition.z);
            handRoot.localRotation = Quaternion.identity;
            var scale = camera.orthographicSize / Mathf.Max(handReferenceSize, 0.1f);
            handRoot.localScale = Vector3.one * scale;
        }

        private Vector3 HandLocalPoint(Vector2 screenPosition)
        {
            var camera = tableCamera.Camera;
            var distance = Mathf.Abs(camera.transform.position.z);
            var world = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distance));
            world.z = 0f;
            return handRoot.InverseTransformPoint(world);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private Vector3 BoardLocalPoint(Vector2 screenPosition)
        {
            var camera = tableCamera.Camera;
            var distance = Mathf.Abs(camera.transform.position.z);
            var world = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distance));
            world.z = 0f;
            return transform.InverseTransformPoint(world);
        }

        private Vector2 BoardPoint(Vector2 screenPosition)
        {
            var camera = tableCamera.Camera;
            var distance = Mathf.Abs(camera.transform.position.z);
            var world = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distance));
            world.z = 0f;
            var local = boardView.transform.InverseTransformPoint(world);
            return new Vector2(local.x, local.y);
        }

        private static Meld FindMeld(IReadOnlyList<Meld> melds, int meldId)
        {
            for (var index = 0; index < melds.Count; index++)
            {
                if (melds[index].Id == meldId)
                {
                    return melds[index];
                }
            }

            return null;
        }

        private static BoardMeldLayout FindMeldLayout(BoardLayoutResult layout, int meldId)
        {
            for (var index = 0; index < layout.Melds.Count; index++)
            {
                if (layout.Melds[index].MeldId == meldId)
                {
                    return layout.Melds[index];
                }
            }

            return null;
        }
    }
}