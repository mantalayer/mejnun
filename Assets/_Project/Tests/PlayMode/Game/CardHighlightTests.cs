using System.Collections;
using Mejnun.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mejnun.Game.Tests
{
    public sealed class CardHighlightTests
    {
        [UnityTest]
        public IEnumerator SelectedCardsLiftAndHighlightWhileHoverHighlightCanBeDisabled()
        {
            var originalHoverHighlight = GameSettings.CardHoverHighlight;
            var originalReducedMotion = GameSettings.ReducedMotion;
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f);
            var cardObject = new GameObject("Card");

            try
            {
                GameSettings.CardHoverHighlight = true;
                GameSettings.ReducedMotion = false;

                var cardView = cardObject.AddComponent<CardView>();
                cardView.SetCard(Card.FromId(0), sprite, sprite, true, true);

                var highlight = cardObject.transform.Find("Card Highlight");
                Assert.That(highlight, Is.Not.Null);
                var highlightRenderer = highlight.GetComponent<SpriteRenderer>();
                Assert.That(highlightRenderer.enabled, Is.True);
                Assert.That(highlightRenderer.color.r, Is.EqualTo(1f));
                Assert.That(highlightRenderer.color.g, Is.LessThan(1f));
                Assert.That(highlightRenderer.color.b, Is.LessThan(0.3f));
                Assert.That(cardView.Renderer.color, Is.EqualTo(Color.white));

                for (var frame = 0; frame < 12; frame++)
                {
                    yield return null;
                }

                Assert.That(cardObject.transform.localPosition.y, Is.GreaterThan(0f));

                cardView.SetSelected(false);
                cardView.SetHovered(true);
                Assert.That(highlightRenderer.enabled, Is.True);

                GameSettings.CardHoverHighlight = false;
                yield return null;
                Assert.That(highlightRenderer.enabled, Is.False);
            }
            finally
            {
                GameSettings.CardHoverHighlight = originalHoverHighlight;
                GameSettings.ReducedMotion = originalReducedMotion;
                Object.DestroyImmediate(cardObject);
                Object.DestroyImmediate(sprite);
            }
        }
    }
}