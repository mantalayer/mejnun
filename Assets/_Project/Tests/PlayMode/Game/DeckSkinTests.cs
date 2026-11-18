using System.Collections.Generic;
using System.Reflection;
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
        [TestCase(DeckLayoutPreset.RowMajor27x2)]
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
            Assert.That(DeckLayoutPresetUtility.GetCardBackSpriteIndex(DeckLayoutPreset.RowMajor), Is.EqualTo(52));
            Assert.That(DeckLayoutPresetUtility.GetBlankSpriteIndex(), Is.EqualTo(53));
        }

        [Test]
        public void RowMajor27x2_MapsFacesAndBackToGridSlicerSlots()
        {
            var indices = new HashSet<int>();
            for (var suitIndex = 0; suitIndex < 4; suitIndex++)
            {
                for (var rankIndex = 0; rankIndex < 13; rankIndex++)
                {
                    var index = DeckLayoutPresetUtility.GetFaceSpriteIndex(DeckLayoutPreset.RowMajor27x2,
                        (Suit)suitIndex, (Rank)rankIndex);
                    Assert.That(index, Is.Not.EqualTo(26).And.Not.EqualTo(53));
                    indices.Add(index);
                }
            }

            Assert.That(DeckLayoutPresetUtility.GetFaceSpriteIndex(DeckLayoutPreset.RowMajor27x2, Suit.Clubs,
                Rank.Ace), Is.EqualTo(0));
            Assert.That(DeckLayoutPresetUtility.GetFaceSpriteIndex(DeckLayoutPreset.RowMajor27x2, Suit.Diamonds,
                Rank.King), Is.EqualTo(25));
            Assert.That(DeckLayoutPresetUtility.GetFaceSpriteIndex(DeckLayoutPreset.RowMajor27x2, Suit.Hearts,
                Rank.Ace), Is.EqualTo(27));
            Assert.That(DeckLayoutPresetUtility.GetFaceSpriteIndex(DeckLayoutPreset.RowMajor27x2, Suit.Spades,
                Rank.King), Is.EqualTo(52));
            Assert.That(indices, Has.Count.EqualTo(Card.CardsPerDeck));
            Assert.That(DeckLayoutPresetUtility.GetCardBackSpriteIndex(DeckLayoutPreset.RowMajor27x2), Is.EqualTo(26));
            Assert.That(DeckLayoutPresetUtility.GetBlankSpriteIndex(), Is.EqualTo(53));
        }

        [TestCase(Suit.Diamonds, Rank.Queen, 47)]
        [TestCase(Suit.Spades, Rank.Queen, 48)]
        [TestCase(Suit.Clubs, Rank.Jack, 18)]
        [TestCase(Suit.Hearts, Rank.Jack, 19)]
        [TestCase(Suit.Hearts, Rank.Queen, 20)]
        [TestCase(Suit.Clubs, Rank.Ace, 25)]
        [TestCase(Suit.Hearts, Rank.Ace, 24)]
        [TestCase(Suit.Clubs, Rank.Two, 0)]
        [TestCase(Suit.Hearts, Rank.Ten, 17)]
        [TestCase(Suit.Diamonds, Rank.King, 50)]
        public void ZigZag_MapsFacesToExpectedSpriteSlots(Suit suit, Rank rank, int expectedIndex)
        {
            Assert.That(DeckLayoutPresetUtility.GetFaceSpriteIndex(DeckLayoutPreset.ZigZag, suit, rank),
                Is.EqualTo(expectedIndex));
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

        [Test]
        public void GetSkinIds_ReturnsDistinctNonEmptyIdsInCatalogOrder()
        {
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            var first = CreateSkin("custom_a", sprite);
            var second = CreateSkin("custom_b", sprite);
            var duplicate = CreateSkin("custom_a", sprite);
            var empty = CreateSkin("empty", sprite);
            typeof(DeckSkin).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(empty, string.Empty);
            var catalog = ScriptableObject.CreateInstance<DeckSkinCatalog>();
            try
            {
                catalog.Configure(new[] { first, second, duplicate, empty });

                Assert.That(catalog.GetSkinIds(), Is.EqualTo(new[] { "custom_a", "custom_b" }));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(duplicate);
                Object.DestroyImmediate(empty);
                Object.DestroyImmediate(sprite);
            }
        }

        [Test]
        public void AlternateBacks_ResolveThroughSkinCatalogAndAreListedAsFaceVariants()
        {
            var primaryBack = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            var alternateBack = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one);
            var skin = CreateSkin("variants", primaryBack);
            var faces = new Sprite[Card.CardsPerDeck];
            for (var index = 0; index < faces.Length; index++)
            {
                faces[index] = primaryBack;
            }

            skin.Configure("variants", "Variants", faces, primaryBack,
                new[] { new DeckSkinBack("blue", alternateBack) }, null, true, 100f);
            var catalog = ScriptableObject.CreateInstance<DeckSkinCatalog>();
            try
            {
                catalog.Configure(new[] { skin });

                Assert.That(catalog.Resolve("variants:blue", 0), Is.SameAs(skin));
                Assert.That(catalog.ResolveBack("variants:blue", 0), Is.SameAs(alternateBack));
                Assert.That(catalog.ResolveBack("variants:missing", 0), Is.SameAs(primaryBack));
                Assert.That(catalog.ResolveBack("variants", 0), Is.SameAs(primaryBack));
                Assert.That(catalog.GetSkinIds(), Is.EqualTo(new[] { "variants", "variants:blue" }));
                Assert.That(skin.GetBack("blue"), Is.SameAs(alternateBack));
                Assert.That(skin.GetBack("unknown"), Is.SameAs(primaryBack));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(skin);
                Object.DestroyImmediate(primaryBack);
                Object.DestroyImmediate(alternateBack);
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