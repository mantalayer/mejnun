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
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f);
            var cardObject = new GameObject("Card");

            try
            {
                settings.IsEnabled = true;
                settings.Offset = new Vector2(0.12f, -0.2f);
                settings.Scale = 1.2f;
                settings.Color = new Color(0.1f, 0.2f, 0.3f, 0.4f);

                var cardView = cardObject.AddComponent<CardView>();
                cardView.SetCard(Card.FromId(0), sprite, sprite, true, false);

                var shadow = cardObject.transform.Find("Card Shadow");
                Assert.That(shadow, Is.Not.Null);
                var shadowRenderer = shadow.GetComponent<SpriteRenderer>();
                Assert.That(shadow.localPosition, Is.EqualTo(new Vector3(0.12f, -0.2f, 0.01f)));
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
    }
}