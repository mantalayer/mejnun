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
        private bool poolReady;
        private bool handVisible = true;
        private Vector2 cardSize = new Vector2(0.64f, 0.97f);
        private float pixelsPerUnit = 100f;

        public GameState CurrentState { get; private set; }
        public TurnSession CurrentTurn { get; private set; }
        public HandOrder HandOrder => handOrder;
        public DeckSkinCatalog SkinCatalog => skinCatalog;
        public IReadOnlyDictionary<byte, CardView> ActiveCards => activeCards;
        public int ActiveCardCount => activeCards.Count;
        public bool HandVisible => handVisible;
        public BoardLayoutResult CurrentBoardLayout => boardView == null ? null : boardView.CurrentLayout;

        private void Awake()
        {
            PreparePool();
        }

        public void Sync(GameState state, TurnSession turn)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            PreparePool();
            CurrentState = state;
            CurrentTurn = turn;
            visibleCardIds.Clear();

            var activeSeat = state.CurrentSeat;
            var activeHand = activeSeat >= 0 && activeSeat < state.Hands.Count
                ? state.Hands[activeSeat]
                : null;
            if (activeHand != null)
            {
                handOrder.Synchronize(activeSeat, activeHand);
                for (var cardIndex = 0; handVisible && cardIndex < activeHand.Count; cardIndex++)
                {
                    AddVisibleCard(activeHand[cardIndex], state);
                }
            }

            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                var cards = state.Board[meldIndex].Cards;
                for (var cardIndex = 0; cardIndex < cards.Count; cardIndex++)
                {
                    AddVisibleCard(cards[cardIndex], state);
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

            var aspect = GetViewportAspect();
            if (boardView != null)
            {
                boardView.Layout(state.Board, activeCards, aspect, cardSize);
                if (tableCamera != null)
                {
                    tableCamera.SetBoardBounds(boardView.CurrentBounds);
                }
            }

            if (handVisible && activeHand != null && handView != null && tableCamera != null &&
                tableCamera.Camera != null)
            {
                handView.Layout(activeHand, handOrder, activeSeat, activeCards, tableCamera.Camera, tableCamera,
                    cardSize, pixelsPerUnit);
            }
        }

        public CardView GetCardView(byte cardId)
        {
            return activeCards.TryGetValue(cardId, out var view) ? view : null;
        }

        public GestureHit HitTest(Vector2 screenPosition)
        {
            if (CurrentState == null || tableCamera == null || tableCamera.Camera == null)
            {
                return GestureHit.None;
            }

            var camera = tableCamera.Camera;
            var distance = Mathf.Abs(camera.transform.position.z);
            var world = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distance));
            world.z = 0f;
            var activeSeat = CurrentState.CurrentSeat;
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
                    if (activeCards.TryGetValue(card.Id, out var handViewCard) &&
                        handViewCard.Renderer.bounds.Contains(world))
                    {
                        return GestureHit.HandCard(card.Id, index);
                    }
                }
            }

            for (var meldIndex = 0; meldIndex < CurrentState.Board.Count; meldIndex++)
            {
                var meld = CurrentState.Board[meldIndex];
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

            if (boardView != null && boardView.CurrentLayout != null &&
                boardView.CurrentLayout.TryHit(world, out var boardHit))
            {
                return boardHit.IsNewGroup
                    ? GestureHit.EmptyBoard()
                    : GestureHit.MeldGap(boardHit.MeldId, boardHit.InsertIndex);
            }

            if (activeHand != null && world.y <= -camera.orthographicSize + cardSize.y + 0.12f)
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

        public void ScrollHandByScreenDelta(float screenDelta)
        {
            if (CurrentState == null || handView == null || tableCamera == null || tableCamera.Camera == null ||
                Screen.height <= 0)
            {
                return;
            }

            var seat = CurrentState.CurrentSeat;
            if (seat < 0 || seat >= CurrentState.Hands.Count)
            {
                return;
            }

            var worldDelta = -screenDelta * tableCamera.Camera.orthographicSize * 2f / Screen.height;
            handView.Scroll(worldDelta);
            handView.Layout(CurrentState.Hands[seat], handOrder, seat, activeCards, tableCamera.Camera, tableCamera,
                cardSize, pixelsPerUnit);
        }

        private void AddVisibleCard(Card card, GameState state)
        {
            var skin = ResolveSkin(state, card.Deck);
            var isSelected = activeCards.TryGetValue(card.Id, out var existingView) && existingView.IsSelected;
            var view = GetOrCreateCardView(card.Id);
            var face = skin == null ? null : skin.GetFace(card.Suit, card.Rank);
            var back = skin == null ? null : skin.CardBack;
            var ppu = skin == null ? 100f : skin.PixelsPerUnit;
            view.ConfigureCamera(tableCamera, ppu);
            view.SetCard(card, face, back, true, isSelected);
            visibleCardIds.Add(card.Id);
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
    }
}