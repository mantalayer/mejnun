using System.Collections;
using System.Collections.Generic;
using Mejnun.Core;
using Mejnun.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Mejnun.Game.Tests
{
    public sealed class TableViewTests
    {
        [Test]
        public void PhoneHandUpperRowDrawsBelowLowerRow()
        {
            const int cardsPerRow = 6;
            Assert.That(HandView.CalculateSortingOrder(1, 0, cardsPerRow, 2),
                Is.LessThan(HandView.CalculateSortingOrder(0, 0, cardsPerRow, 2)));
            Assert.That(HandView.CalculateSortingOrder(1, 1, cardsPerRow, 2),
                Is.GreaterThan(HandView.CalculateSortingOrder(1, 0, cardsPerRow, 2)));
        }

        [Test]
        public void DraggedCardRemainsAboveBoardCardsAcrossLayout()
        {
            var oldOrganicBoard = GameSettings.OrganicBoard;
            var boardObject = new GameObject("Board View Test");
            try
            {
                GameSettings.OrganicBoard = false;
                var boardView = boardObject.AddComponent<BoardView>();
                var cards = new List<Card>();
                var cardViews = new Dictionary<byte, CardView>();
                for (var index = 0; index < 3; index++)
                {
                    var card = Card.FromId(index);
                    cards.Add(card);
                    var cardObject = new GameObject("Card View Test " + index);
                    cardObject.transform.SetParent(boardObject.transform, false);
                    var cardView = cardObject.AddComponent<CardView>();
                    cardViews.Add(card.Id, cardView);
                }

                var liftedCardId = cards[1].Id;
                var liftedCard = cardViews[liftedCardId];
                liftedCard.SetSortingOrder(CardView.DragSortingOrder);
                boardView.Layout(new[] { new Meld(1, cards) }, cardViews, 1f, new Vector2(0.64f, 0.97f),
                    new HashSet<byte> { liftedCardId });

                Assert.That(liftedCard.SortingOrder, Is.EqualTo(CardView.DragSortingOrder));
                Assert.That(cardViews[cards[0].Id].SortingOrder, Is.EqualTo(10));
                Assert.That(cardViews[cards[2].Id].SortingOrder, Is.EqualTo(12));
            }
            finally
            {
                GameSettings.OrganicBoard = oldOrganicBoard;
                Object.DestroyImmediate(boardObject);
            }
        }

        [Test]
        public void DragSortingOrderExceedsHandAndPreviewLayers()
        {
            Assert.That(CardView.DragSortingOrder,
                Is.GreaterThan(HandView.CalculateSortingOrder(0, 0, Card.CardCount, 1)));
            Assert.That(CardView.DragSortingOrder, Is.GreaterThan(300 + Card.CardCount));
            Assert.That(CardView.DragSortingOrder * 4, Is.GreaterThan(1000 + Card.CardCount));
        }

        [UnityTest]
        public IEnumerator ChoosingFirstPlayerStartsThatSeat()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var matchFlow = Object.FindAnyObjectByType<MatchFlow>();
            Assert.That(matchFlow, Is.Not.Null);
            Assert.That(matchFlow.StartMatch(3, new[] { "Sam", "Alex", "Lee" }, 20261118), Is.True);
            yield return WaitForRoundInitialization(matchFlow);

            Assert.That(matchFlow.ChooseFirstPlayer(0).Ok, Is.True);

            Assert.That(matchFlow.Game.State.CurrentSeat, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Sync_RendersCardsUsingTheFaceAssignedToTheirDeck()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var matchFlow = Object.FindAnyObjectByType<MatchFlow>();
            Assert.That(matchFlow, Is.Not.Null);
            Assert.That(matchFlow.StartMatch(3, new[] { "Sam", "Alex", "Lee" }, 202601), Is.True);
            yield return WaitForRoundInitialization(matchFlow);
            Assert.That(matchFlow.ChooseDealer(0).Ok, Is.True);
            Assert.That(matchFlow.RevealHand(), Is.True);

            var tableView = Object.FindAnyObjectByType<TableView>();
            Assert.That(tableView, Is.Not.Null);
            Assert.That(tableView.CurrentState, Is.Not.Null);
            Assert.That(tableView.ActiveCardCount,
                Is.EqualTo(tableView.CurrentState.Hands[tableView.CurrentState.CurrentSeat].Count +
                           CountBoardCards(tableView.CurrentState)));

            var decksSeen = 0;
            foreach (var pair in tableView.ActiveCards)
            {
                var card = Card.FromId(pair.Key);
                var skin = tableView.SkinCatalog.Resolve(tableView.CurrentState.DeckFaces[card.Deck], card.Deck);
                Assert.That(pair.Value.IsFaceUp, Is.True);
                Assert.That(pair.Value.Renderer.sprite, Is.EqualTo(skin.GetFace(card.Suit, card.Rank)));
                decksSeen |= 1 << card.Deck;
            }

            Assert.That(decksSeen, Is.EqualTo(3), "The dealt hand should include cards from both decks.");
        }

        [UnityTest]
        public IEnumerator PausingConcealsHandAndBoardCardsAndPausesTurnTimer()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var matchFlow = Object.FindAnyObjectByType<MatchFlow>();
            Assert.That(matchFlow, Is.Not.Null);
            var seats = new[] { SeatConfig.Human("Sam"), SeatConfig.Human("Alex") };
            Assert.That(matchFlow.StartMatch(seats, 20261117UL, 10), Is.True);
            yield return WaitForRoundInitialization(matchFlow);
            Assert.That(matchFlow.ChooseDealer(0).Ok, Is.True);
            Assert.That(matchFlow.RevealHand(), Is.True);

            var tableView = Object.FindAnyObjectByType<TableView>();
            var state = tableView.CurrentState;
            var boardCards = new[] { state.DrawPile[0], state.DrawPile[1], state.DrawPile[2] };
            state.DrawPile.RemoveRange(0, boardCards.Length);
            state.Board.Add(new Meld(1, boardCards));
            tableView.Sync(state, tableView.CurrentTurn);

            var hand = state.Hands[state.CurrentSeat];
            matchFlow.SetPaused(true);

            Assert.That(matchFlow.TurnTimer.Paused, Is.True);
            for (var index = 0; index < hand.Count; index++)
            {
                var cardView = tableView.GetCardView(hand[index].Id);
                var expectedBack = tableView.GetCardBack(state, hand[index].Deck);
                Assert.That(cardView.IsFaceUp, Is.False);
                Assert.That(cardView.Renderer.sprite, Is.EqualTo(expectedBack));
            }

            for (var index = 0; index < boardCards.Length; index++)
            {
                var cardView = tableView.GetCardView(boardCards[index].Id);
                var expectedBack = tableView.GetCardBack(state, boardCards[index].Deck);
                Assert.That(cardView.IsFaceUp, Is.False);
                Assert.That(cardView.Renderer.sprite, Is.EqualTo(expectedBack));
            }

            matchFlow.SetPaused(false);

            Assert.That(matchFlow.TurnTimer.Paused, Is.False);
            for (var index = 0; index < hand.Count; index++)
            {
                var cardView = tableView.GetCardView(hand[index].Id);
                var skin = tableView.SkinCatalog.Resolve(state.DeckFaces[hand[index].Deck], hand[index].Deck);
                Assert.That(cardView.IsFaceUp, Is.True);
                Assert.That(cardView.Renderer.sprite,
                    Is.EqualTo(skin.GetFace(hand[index].Suit, hand[index].Rank)));
            }

            for (var index = 0; index < boardCards.Length; index++)
            {
                var cardView = tableView.GetCardView(boardCards[index].Id);
                var skin = tableView.SkinCatalog.Resolve(state.DeckFaces[boardCards[index].Deck],
                    boardCards[index].Deck);
                Assert.That(cardView.IsFaceUp, Is.True);
                Assert.That(cardView.Renderer.sprite,
                    Is.EqualTo(skin.GetFace(boardCards[index].Suit, boardCards[index].Rank)));
            }
        }

        [UnityTest]
        public IEnumerator BoardCardsCollectToThePileAndDealBackToTheirSavedPositions()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var matchFlow = Object.FindAnyObjectByType<MatchFlow>();
            Assert.That(matchFlow, Is.Not.Null);
            Assert.That(matchFlow.StartMatch(2, new[] { "Sam", "Alex" }, 20261119), Is.True);
            yield return WaitForRoundInitialization(matchFlow);
            Assert.That(matchFlow.ChooseDealer(0).Ok, Is.True);
            Assert.That(matchFlow.RevealHand(), Is.True);

            var hud = Object.FindAnyObjectByType<Mejnun.UI.HudPresenter>();
            if (hud != null)
            {
                hud.enabled = false;
            }

            var tableView = Object.FindAnyObjectByType<TableView>();
            var state = tableView.CurrentState;
            var boardCards = new[] { state.DrawPile[0], state.DrawPile[1], state.DrawPile[2] };
            state.DrawPile.RemoveRange(0, boardCards.Length);
            state.Board.Add(new Meld(1, boardCards));
            tableView.Sync(state, tableView.CurrentTurn);
            var pileScreenPosition = new Vector2(Screen.width * 0.72f, Screen.height * 0.48f);
            tableView.SetDrawPileScreenPosition(pileScreenPosition);
            yield return new WaitForSecondsRealtime(0.25f);

            var savedPositions = new Dictionary<byte, Vector3>();
            for (var index = 0; index < boardCards.Length; index++)
            {
                var view = tableView.GetCardView(boardCards[index].Id);
                savedPositions[boardCards[index].Id] = view.TargetLocalPosition;
            }

            Assert.That(tableView.CollectBoardToPile(), Is.EqualTo(TableView.BoardCollectSeconds));
            yield return new WaitForSecondsRealtime(TableView.BoardCollectSeconds + 0.1f);

            for (var index = 0; index < boardCards.Length; index++)
            {
                Assert.That(tableView.GetCardView(boardCards[index].Id), Is.Null);
            }

            tableView.Sync(state, tableView.CurrentTurn);
            Assert.That(tableView.BoardCardCount, Is.Zero);
            for (var index = 0; index < boardCards.Length; index++)
            {
                Assert.That(tableView.GetCardView(boardCards[index].Id), Is.Null);
            }

            tableView.PrepareBoardRestore();
            tableView.Sync(state, tableView.CurrentTurn);
            tableView.PlayBoardDeal(0f);
            yield return new WaitForSecondsRealtime(0.45f);
            for (var index = 0; index < boardCards.Length; index++)
            {
                var view = tableView.GetCardView(boardCards[index].Id);
                Assert.That(Vector3.Distance(view.transform.localPosition, savedPositions[boardCards[index].Id]),
                    Is.LessThan(0.01f));
            }
        }

        [UnityTest]
        public IEnumerator InitialDealMovesCardsFromThePileToEachSeat()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var matchFlow = Object.FindAnyObjectByType<MatchFlow>();
            Assert.That(matchFlow, Is.Not.Null);
            Assert.That(matchFlow.StartMatch(2, new[] { "Sam", "Alex" }, 20261120), Is.True);
            yield return WaitForRoundInitialization(matchFlow);

            var tableView = Object.FindAnyObjectByType<TableView>();
            Assert.That(tableView, Is.Not.Null);
            var tableController = Object.FindAnyObjectByType<TableController>();
            Assert.That(tableController, Is.Not.Null);
            tableView.DealAnimationEnabled = true;
            tableView.ArmDeal();
            Assert.That(matchFlow.ChooseDealer(0).Ok, Is.True);
            var seat = matchFlow.Game.State.CurrentSeat;
            tableView.SetViewerSeat(seat);
            tableView.SetHandVisible(true);
            yield return null;

            Assert.That(tableView.GetIncomingCardCount(seat), Is.GreaterThan(0));
            Assert.That(tableView.GetIncomingCardCount(1 - seat), Is.GreaterThan(0));
            var hand = matchFlow.Game.State.Hands[seat];
            Assert.That(tableView.IsDealing, Is.True);
            Assert.That(tableController.InputEnabled, Is.False);
            Assert.That(tableController.HandInputEnabled, Is.False);
            var visibleHandCards = 0;
            for (var index = 0; index < hand.Count; index++)
            {
                var view = tableView.GetCardView(hand[index].Id);
                if (view != null && view.gameObject.activeSelf)
                {
                    visibleHandCards++;
                }
            }

            Assert.That(visibleHandCards, Is.LessThan(hand.Count));
            var initiallyVisibleHandCards = visibleHandCards;
            yield return new WaitForSecondsRealtime(0.35f);
            visibleHandCards = 0;
            for (var index = 0; index < hand.Count; index++)
            {
                var view = tableView.GetCardView(hand[index].Id);
                if (view != null && view.gameObject.activeSelf)
                {
                    visibleHandCards++;
                }
            }

            Assert.That(visibleHandCards, Is.GreaterThan(initiallyVisibleHandCards));
            Assert.That(visibleHandCards, Is.LessThan(hand.Count));
            var totalDealtCards = 0;
            for (var handIndex = 0; handIndex < matchFlow.Game.State.Hands.Count; handIndex++)
            {
                totalDealtCards += matchFlow.Game.State.Hands[handIndex].Count;
            }

            yield return new WaitForSecondsRealtime(totalDealtCards * 0.1f + 1f);
            Assert.That(tableView.IsDealing, Is.False);
            Assert.That(tableController.InputEnabled, Is.True);
            Assert.That(tableController.HandInputEnabled, Is.True);
            Assert.That(tableView.GetIncomingCardCount(seat), Is.Zero);
            Assert.That(tableView.GetIncomingCardCount(1 - seat), Is.Zero);
            for (var index = 0; index < hand.Count; index++)
            {
                var view = tableView.GetCardView(hand[index].Id);
                Assert.That(view, Is.Not.Null);
                Assert.That(Vector3.Distance(view.transform.localPosition, view.TargetLocalPosition),
                    Is.LessThan(0.01f));
            }
        }

        [UnityTest]
        public IEnumerator HandCardsStayInPlaceWhenTheBoardCameraPansAndZooms()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var matchFlow = Object.FindAnyObjectByType<MatchFlow>();
            Assert.That(matchFlow.StartMatch(2, new[] { "Sam", "Alex" }, 20261117), Is.True);
            yield return WaitForRoundInitialization(matchFlow);
            Assert.That(matchFlow.ChooseDealer(0).Ok, Is.True);
            Assert.That(matchFlow.RevealHand(), Is.True);
            yield return new WaitForSeconds(0.25f);

            var tableView = Object.FindAnyObjectByType<TableView>();
            var camera = tableView.ViewCamera;
            var tableCamera = Object.FindAnyObjectByType<TableCamera>();
            Assert.That(tableCamera, Is.Not.Null);
            var hand = tableView.CurrentState.Hands[tableView.CurrentState.CurrentSeat];
            var card = tableView.GetCardView(hand[0].Id);
            var initial = camera.WorldToScreenPoint(card.transform.position);

            tableView.PanCamera(new Vector2(42f, -23f), new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return null;
            var afterPan = camera.WorldToScreenPoint(card.transform.position);
            Assert.That(Vector2.Distance(initial, afterPan), Is.LessThan(1.5f));

            tableCamera.Zoom(1.2f, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            yield return null;
            var afterZoom = camera.WorldToScreenPoint(card.transform.position);
            Assert.That(Vector2.Distance(initial, afterZoom), Is.LessThan(1.5f));
        }

        [UnityTest]
        public IEnumerator HandCardsAreRaisedAboveTheBottomHudBar()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var matchFlow = Object.FindAnyObjectByType<MatchFlow>();
            Assert.That(matchFlow.StartMatch(2, new[] { "Sam", "Alex" }, 20261118), Is.True);
            yield return WaitForRoundInitialization(matchFlow);
            Assert.That(matchFlow.ChooseDealer(0).Ok, Is.True);
            Assert.That(matchFlow.RevealHand(), Is.True);
            yield return new WaitForSeconds(0.3f);

            var tableView = Object.FindAnyObjectByType<TableView>();
            var documents = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
            UIDocument document = null;
            for (var index = 0; index < documents.Length; index++)
            {
                if (documents[index].rootVisualElement.Q<VisualElement>("bottom-bar") != null)
                {
                    document = documents[index];
                    break;
                }
            }

            Assert.That(tableView, Is.Not.Null);
            Assert.That(document, Is.Not.Null);
            var bottomBar = document.rootVisualElement.Q<VisualElement>("bottom-bar");
            Assert.That(bottomBar, Is.Not.Null);
            Assert.That(bottomBar.worldBound.height, Is.GreaterThan(0f));

            var hand = tableView.CurrentState.Hands[tableView.CurrentState.CurrentSeat];
            var card = tableView.GetCardView(hand[0].Id);
            var bounds = card.Renderer.bounds;
            var cardBottomScreen = tableView.ViewCamera.WorldToScreenPoint(
                new Vector3(card.transform.position.x, bounds.min.y, card.transform.position.z));
            var cardBottomPanel = RuntimePanelUtils.ScreenToPanel(document.rootVisualElement.panel,
                cardBottomScreen);

            Assert.That(cardBottomPanel.y, Is.LessThan(bottomBar.worldBound.yMin),
                "The hand card's lower edge should remain above the UI bar so pointer presses reach the card.");
        }

        private static int CountBoardCards(GameState state)
        {
            var count = 0;
            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                count += state.Board[meldIndex].Cards.Count;
            }

            return count;
        }

        private static IEnumerator WaitForRoundInitialization(MatchFlow flow)
        {
            var frames = 0;
            while (flow.IsInitializingRound && frames++ < 300)
            {
                yield return null;
            }

            Assert.That(flow.IsInitializingRound, Is.False);
            Assert.That(flow.Game, Is.Not.Null);
        }
    }
}