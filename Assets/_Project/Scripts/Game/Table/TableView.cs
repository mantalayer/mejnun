using System;
using System.Collections.Generic;
using Mejnun.Core;
using Mejnun.Cosmetics;
using UnityEngine;

namespace Mejnun.Game
{
    public sealed class TableView : MonoBehaviour
    {
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private DeckSkinCatalog skinCatalog;
        [SerializeField] private BoardView boardView;
        [SerializeField] private HandView handView;
        [SerializeField] private TableCamera tableCamera;
        [SerializeField] private int pooledCardCount = Card.CardCount;

        private readonly Dictionary<byte, CardView> activeCards = new Dictionary<byte, CardView>(Card.CardCount);
        private readonly Stack<CardView> cardPool = new Stack<CardView>(Card.CardCount);
        private readonly HashSet<byte> visibleCardIds = new HashSet<byte>();
        private readonly List<byte> staleCardIds = new List<byte>(Card.CardCount);
        private readonly List<Card> hitTestHandCards = new List<Card>(Card.CardsPerDeck);
        private readonly HandOrder handOrder = new HandOrder();
        private readonly Dictionary<byte, Vector3> liftedHandOffsets = new Dictionary<byte, Vector3>();
        private readonly Dictionary<byte, Vector3> liftedMeldOffsets = new Dictionary<byte, Vector3>();
        private readonly BoardArrangement boardArrangement = new BoardArrangement();
        private readonly List<Meld> arrangedMelds = new List<Meld>();
        private Vector3 meldLiftStartPointer;
        private int liftedMeldId = -1;
        private bool poolReady;
        private bool handVisible = true;
        private bool handConcealed;
        private int viewerSeat = -1;
        private Vector2 cardSize = new Vector2(0.64f, 0.97f);
        private float pixelsPerUnit = 100f;
        [SerializeField, Min(0.1f)] private float handReferenceSize = 5f;
        private Transform handRoot;
        private float lastHandAspect;
        private bool hasHandAspect;
        private int handGapIndex = -1;
        private float handBottomInset;

        public GameState CurrentState { get; private set; }
        public TurnSession CurrentTurn { get; private set; }
        public HandOrder HandOrder => handOrder;
        public BoardArrangement BoardArrangement => boardArrangement;
        public DeckSkinCatalog SkinCatalog => skinCatalog;
        public IReadOnlyDictionary<byte, CardView> ActiveCards => activeCards;
        public int ActiveCardCount => activeCards.Count;
        public bool HandVisible => handVisible;
        public bool HandConcealed => handConcealed;
        public int HandSeat => !handVisible || CurrentState == null
            ? -1
            : DisplaySeat(CurrentState);
        public Camera ViewCamera => tableCamera == null ? null : tableCamera.Camera;

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

        private void Awake()
        {
            PreparePool();
            EnsureHandRoot();
        }

        private void LateUpdate()
        {
            if (tableCamera == null || tableCamera.Camera == null)
            {
                return;
            }

            EnsureHandRoot();
            if (handRoot != null)
            {
                UpdateHandRoot();
            }

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
            }
        }

        public void Sync(GameState state, TurnSession turn)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            PreparePool();
            EnsureHandRoot();
            CurrentState = state;
            CurrentTurn = turn;
            visibleCardIds.Clear();

            var activeSeat = DisplaySeat(state);
            var activeHand = activeSeat >= 0 && activeSeat < state.Hands.Count
                ? state.Hands[activeSeat]
                : null;
            if (activeHand != null)
            {
                handOrder.Synchronize(activeSeat, activeHand);
                for (var cardIndex = 0; handVisible && cardIndex < activeHand.Count; cardIndex++)
                {
                    AddVisibleCard(activeHand[cardIndex], state, true);
                }
            }

            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
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
            if (boardView != null)
            {
                var arrangement = boardArrangement.Arrange(state.Board);
                arrangedMelds.Clear();
                for (var index = 0; index < arrangement.Count; index++)
                {
                    arrangedMelds.Add(arrangement[index]);
                }

                boardView.Layout(arrangedMelds, activeCards, aspect, cardSize);
                if (tableCamera != null)
                {
                    tableCamera.SetBoardBounds(boardView.CurrentBounds);
                }
            }

            if (handVisible && activeHand != null)
            {
                LayoutHand(activeSeat);
            }
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
                for (var index = hitTestHandCards.Count - 1; index >= 0; index--)
                {
                    var card = hitTestHandCards[index];
                    if (liftedHandOffsets.ContainsKey(card.Id))
                    {
                        continue;
                    }

                    if (activeCards.TryGetValue(card.Id, out var handViewCard) &&
                        handViewCard.ContainsWorldPoint(world))
                    {
                        return GestureHit.HandCard(card.Id, index);
                    }
                }
            }

            for (var meldIndex = 0; meldIndex < CurrentState.Board.Count; meldIndex++)
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

                for (var cardIndex = 0; cardIndex < meld.Cards.Count; cardIndex++)
                {
                    var card = meld.Cards[cardIndex];
                    if (!activeCards.TryGetValue(card.Id, out var boardViewCard) ||
                        !boardViewCard.Renderer.bounds.Contains(world))
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

            if (activeHand != null && handView != null && handRoot != null &&
                handRoot.InverseTransformPoint(world).y <= handView.HandTopY)
            {
                return GestureHit.HandArea(activeHand.Count);
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

        public void PanCamera(Vector2 screenDelta, Vector2 screenPosition)
        {
            if (tableCamera != null)
            {
                tableCamera.Pan(screenDelta, screenPosition);
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
                view.SetSortingOrder(400 + index);
                view.SetLayoutPose(Quaternion.identity, view.LayoutScale * 1.08f);
                view.transform.localScale = Vector3.one * view.LayoutScale;
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
            if (handRoot == null || liftedHandOffsets.Count == 0)
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
            if (hit.Kind == GestureHitKind.HandCard)
            {
                handGapIndex = CountNonLiftedBefore(hit.Index);
            }
            else if (hit.Kind == GestureHitKind.HandArea)
            {
                handGapIndex = Mathf.Max(0, hit.TargetIndex - liftedHandOffsets.Count);
            }
            else
            {
                handGapIndex = -1;
            }

            LayoutHand(DisplaySeat(CurrentState));
        }

        public void EndHandLift()
        {
            liftedHandOffsets.Clear();
            handGapIndex = -1;
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
            liftedMeldId = meldId;
            for (var index = 0; index < meld.Cards.Count; index++)
            {
                var cardId = meld.Cards[index].Id;
                if (!activeCards.TryGetValue(cardId, out var view))
                {
                    continue;
                }

                liftedMeldOffsets[cardId] = view.transform.localPosition - pointer;
                view.SetSortingOrder(400 + index);
                view.transform.localScale = Vector3.one * 1.06f;
            }

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
            if (boardView != null && liftedMeldId >= 0)
            {
                boardView.SetMeldOutlineOffset(liftedMeldId, Vector3.zero);
            }

            liftedMeldId = -1;
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
            var back = skin == null ? null : skin.CardBack;
            var ppu = skin == null ? 100f : skin.PixelsPerUnit;
            view.SetParentKeepWorld(isHandCard && handRoot != null ? handRoot : transform, isHandCard);
            view.ConfigureCamera(tableCamera, ppu);
            if (!isHandCard)
            {
                view.SetLayoutPose(Quaternion.identity, 1f);
            }
            view.SetCard(card, face, back, !(isHandCard && handConcealed), isSelected);
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

            var skin = skinCatalog.Resolve(state.DeckFaces[deckIndex], deckIndex);
            return skin == null ? null : skin.CardBack;
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
                    cardSize = new Vector2(sample.rect.width / pixelsPerUnit, sample.rect.height / pixelsPerUnit);
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
            var availableHeight = tableCamera == null ? 1f : 1f - tableCamera.BottomInsetFraction;
            return GetViewportAspect() / Mathf.Max(availableHeight, 0.1f);
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
            handRoot.localPosition = new Vector3(0f, 0f, localPosition.z);
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

        private Vector3 BoardLocalPoint(Vector2 screenPosition)
        {
            var camera = tableCamera.Camera;
            var distance = Mathf.Abs(camera.transform.position.z);
            var world = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distance));
            world.z = 0f;
            return transform.InverseTransformPoint(world);
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

        private int CountNonLiftedBefore(int cardIndex)
        {
            var seat = DisplaySeat(CurrentState);
            var ids = handOrder.GetCardIds(seat);
            var count = 0;
            for (var index = 0; index < ids.Count && index < cardIndex; index++)
            {
                if (!liftedHandOffsets.ContainsKey(ids[index]))
                {
                    count++;
                }
            }

            return count;
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