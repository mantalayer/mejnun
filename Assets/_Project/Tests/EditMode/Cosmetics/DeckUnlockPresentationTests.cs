using System.Collections.Generic;
using System.Linq;
using Mejnun.Core;
using Mejnun.Cosmetics;
using Mejnun.UI;
using Mejnun.UI.FrontEnd;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.Cosmetics.Tests
{
    public sealed class DeckUnlockPresentationTests
    {
        private Texture2D texture;
        private Sprite backSprite;
        private readonly List<Sprite> sprites = new List<Sprite>();
        private DeckSkin skin;
        private VisualElement root;
        private DeckUnlockPresentation presentation;

        [SetUp]
        public void SetUp()
        {
            texture = new Texture2D(Card.CardsPerDeck + 1, 1, TextureFormat.RGBA32, false);
            texture.SetPixels(Enumerable.Repeat(Color.white, Card.CardsPerDeck + 1).ToArray());
            texture.Apply();
            for (var index = 0; index < Card.CardsPerDeck; index++)
            {
                sprites.Add(Sprite.Create(texture, new Rect(index, 0f, 1f, 1f), new Vector2(0.5f, 0.5f)));
            }

            skin = ScriptableObject.CreateInstance<DeckSkin>();
            backSprite = Sprite.Create(texture, new Rect(Card.CardsPerDeck, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            skin.Configure("testDeck", "Test Deck", sprites.ToArray(), backSprite, null, false, 100f);
            root = new VisualElement();
            presentation = new DeckUnlockPresentation(root);
        }

        [TearDown]
        public void TearDown()
        {
            presentation?.Cancel();
            UnityEngine.Object.DestroyImmediate(skin);
            for (var index = 0; index < sprites.Count; index++)
            {
                UnityEngine.Object.DestroyImmediate(sprites[index]);
            }

            UnityEngine.Object.DestroyImmediate(backSprite);
            UnityEngine.Object.DestroyImmediate(texture);
            sprites.Clear();
        }

        [Test]
        public void PresentationSpreadsBacksThenFlipsFacesInNewDeckOrderAndSquaresWithGlow()
        {
            var faces = CosmeticPreviews.GetNewDeckFaces(skin);
            var suitOrder = new[] { Suit.Spades, Suit.Diamonds, Suit.Clubs, Suit.Hearts };
            var expectedIndex = 0;
            for (var suitIndex = 0; suitIndex < suitOrder.Length; suitIndex++)
            {
                for (var rankIndex = 0; rankIndex < 13; rankIndex++)
                {
                    Assert.That(faces[expectedIndex], Is.SameAs(skin.GetFace(suitOrder[suitIndex], (Rank)rankIndex)));
                    expectedIndex++;
                }
            }

            presentation.Play(new[] { new DeckUnlockPresentationItem(skin, "Test Deck") }, null);

            Assert.That(root.Q<Label>("deck-unlock-name").text, Is.EqualTo("Test Deck"));
            var cards = root.Query<Image>(className: "deck-unlock-card").ToList();
            Assert.That(cards, Has.Count.EqualTo(Card.CardsPerDeck));
            for (var index = 0; index < cards.Count; index++)
            {
                Assert.That(cards[index].sprite, Is.SameAs(backSprite));
            }

            Assert.That(presentation.AreFacesShowing, Is.False);

            presentation.Evaluate(DeckUnlockPresentation.FullDuration);

            Assert.That(presentation.AreFacesShowing, Is.True);
            for (var index = 0; index < cards.Count; index++)
            {
                Assert.That(cards[index].sprite, Is.SameAs(faces[index]));
            }

            Assert.That(presentation.IsSquared, Is.True);
            Assert.That(root.Q<VisualElement>("deck-unlock-glow-outer").style.opacity.value, Is.GreaterThan(0f));
        }

        [Test]
        public void FirstTapSquaresDeckAndSecondTapFinishes()
        {
            var finished = false;
            presentation.Play(new[] { new DeckUnlockPresentationItem(skin, "Test Deck") }, () => finished = true);
            SendTap();

            Assert.That(presentation.IsSquared, Is.True);
            Assert.That(finished, Is.False);

            SendTap();

            Assert.That(presentation.IsPlaying, Is.False);
            Assert.That(finished, Is.True);
        }

        private void SendTap()
        {
            var evt = PointerDownEvent.GetPooled();
            root.Q<VisualElement>("deck-unlock-overlay").SendEvent(evt);
            evt.Dispose();
        }
    }
}