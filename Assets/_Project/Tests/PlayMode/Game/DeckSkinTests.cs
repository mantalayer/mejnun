using System.Collections.Generic;
using Mejnun.Core;
using Mejnun.Cosmetics;
using NUnit.Framework;
using UnityEngine;

namespace Mejnun.Game.Tests
{
    public sealed class DeckSkinTests
    {
        [TestCase(DeckLayoutPreset.ZigZag)]
        [TestCase(DeckLayoutPreset.RowMajor)]
        public void LayoutPreset_MapsEveryFaceToADistinctSpriteSlot(DeckLayoutPreset preset)
        {
            var indices = new HashSet<int>();
            for (var suitIndex = 0; suitIndex < 4; suitIndex++)
            {
                for (var rankIndex = 0; rankIndex < 13; rankIndex++)
                {
                    var index = DeckLayoutPresetUtility.GetFaceSpriteIndex(preset, (Suit)suitIndex, (Rank)rankIndex);
                    Assert.That(indices.Add(index), Is.True, "Duplicate sprite slot " + index + ".");
                }
            }

            Assert.That(indices, Has.Count.EqualTo(Card.CardsPerDeck));
            Assert.That(DeckLayoutPresetUtility.GetCardBackSpriteIndex(DeckLayoutPreset.ZigZag), Is.EqualTo(26));
            Assert.That(DeckLayoutPresetUtility.GetBlankSpriteIndex(), Is.EqualTo(53));
        }

        [Test]
        public void Resolve_UsesRequestedSkinOrPerDeckFallback()
        {
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            var classic = CreateSkin("classic", sprite);
            var ivory = CreateSkin("classic_ivory", sprite);
            var catalog = ScriptableObject.CreateInstance<DeckSkinCatalog>();
            try
            {
                catalog.Configure(new[] { classic, ivory });

                Assert.That(catalog.Resolve("classic_ivory", 0), Is.SameAs(ivory));
                Assert.That(catalog.Resolve("missing", 0), Is.SameAs(classic));
                Assert.That(catalog.Resolve("missing", 1), Is.SameAs(ivory));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(classic);
                Object.DestroyImmediate(ivory);
                Object.DestroyImmediate(sprite);
            }
        }

        private static DeckSkin CreateSkin(string id, Sprite sprite)
        {
            var skin = ScriptableObject.CreateInstance<DeckSkin>();
            var faces = new Sprite[Card.CardsPerDeck];
            for (var index = 0; index < faces.Length; index++)
            {
                faces[index] = sprite;
            }

            skin.Configure(id, id, faces, sprite, sprite, true, 100f);
            return skin;
        }
    }
}