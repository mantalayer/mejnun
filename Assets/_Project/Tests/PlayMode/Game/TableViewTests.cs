using System.Collections;
using Mejnun.Core;
using Mejnun.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Mejnun.Game.Tests
{
    public sealed class TableViewTests
    {
        [UnityTest]
        public IEnumerator Sync_RendersCardsUsingTheFaceAssignedToTheirDeck()
        {
            SceneManager.LoadScene("Table", LoadSceneMode.Single);
            yield return null;

            var matchFlow = Object.FindAnyObjectByType<MatchFlow>();
            Assert.That(matchFlow, Is.Not.Null);
            Assert.That(matchFlow.StartMatch(3, new[] { "Sam", "Alex", "Lee" }, 202601), Is.True);
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

        private static int CountBoardCards(GameState state)
        {
            var count = 0;
            for (var meldIndex = 0; meldIndex < state.Board.Count; meldIndex++)
            {
                count += state.Board[meldIndex].Cards.Count;
            }

            return count;
        }
    }
}