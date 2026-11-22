using Mejnun.Core;
using NUnit.Framework;
using UnityEngine;

namespace Mejnun.Game.Tests
{
    public sealed class CardShadowSettingsTests
    {
        [Test]
        public void CardShadowUsesSettingsAndUpdatesWhenTheyChange()
        {
            var settings = CardShadowSettings.Active;
            var originalEnabled = settings.IsEnabled;
            var originalOffset = settings.Offset;
            var originalScale = settings.Scale;
            var originalColor = settings.Color;
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f,
                1f / 0.92f);
            var cardObject = new GameObject("Card");

            try
            {
                settings.IsEnabled = true;
                settings.Offset = new Vector2(0.12f, -0.2f);
                settings.Scale = 1.2f;
                settings.Color = new Color(0.1f, 0.2f, 0.3f, 0.4f);

                var cardView = cardObject.AddComponent<CardView>();
                cardView.SetCard(Card.FromId(0), sprite, sprite, true, false);
                cardView.SetSortingOrder(2);

                var shadow = cardObject.transform.Find("Card Shadow");
                Assert.That(shadow, Is.Not.Null);
                var shadowRenderer = shadow.GetComponent<SpriteRenderer>();
                Assert.That(shadow.localPosition, Is.EqualTo(new Vector3(0.12f, -0.2f, 0.01f)));
                Assert.That(shadowRenderer.sortingOrder, Is.EqualTo(8));
                Assert.That(shadow.localScale, Is.EqualTo(Vector3.one * 1.2f));
                Assert.That(shadowRenderer.color, Is.EqualTo(new Color(0.1f, 0.2f, 0.3f, 0.4f)));
                Assert.That(shadowRenderer.enabled, Is.True);

                settings.IsEnabled = false;

                Assert.That(shadowRenderer.enabled, Is.False);
            }
            finally
            {
                settings.IsEnabled = originalEnabled;
                settings.Offset = originalOffset;
                settings.Scale = originalScale;
                settings.Color = originalColor;
                Object.DestroyImmediate(cardObject);
                Object.DestroyImmediate(sprite);
            }
        }

        [Test]
        public void CardShadowOffsetIsNormalizedForHighResolutionSprites()
        {
            var settings = CardShadowSettings.Active;
            var originalEnabled = settings.IsEnabled;
            var originalOffset = settings.Offset;
            var originalScale = settings.Scale;
            var originalColor = settings.Color;
            var texture = new Texture2D(394, 595);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 394f, 595f), Vector2.one * 0.5f, 100f);
            var cardObject = new GameObject("Card");

            try
            {
                settings.IsEnabled = true;
                settings.Offset = new Vector2(0.12f, -0.2f);
                settings.Scale = 1f;
                settings.Color = Color.black;

                var cardView = cardObject.AddComponent<CardView>();
                cardView.SetCard(Card.FromId(0), sprite, sprite, true, false);

                var shadow = cardObject.transform.Find("Card Shadow");
                Assert.That(shadow, Is.Not.Null);
                var normalizingScale = 0.92f / sprite.bounds.size.y;
                Assert.That(shadow.localPosition.x, Is.EqualTo(settings.Offset.x / normalizingScale).Within(0.001f));
                Assert.That(shadow.localPosition.y, Is.EqualTo(settings.Offset.y / normalizingScale).Within(0.001f));
                var worldOffset = cardObject.transform.TransformPoint(shadow.localPosition);
                Assert.That(worldOffset.x, Is.EqualTo(settings.Offset.x).Within(0.001f));
                Assert.That(worldOffset.y, Is.EqualTo(settings.Offset.y).Within(0.001f));
            }
            finally
            {
                settings.IsEnabled = originalEnabled;
                settings.Offset = originalOffset;
                settings.Scale = originalScale;
                settings.Color = originalColor;
                Object.DestroyImmediate(cardObject);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }
    }
}