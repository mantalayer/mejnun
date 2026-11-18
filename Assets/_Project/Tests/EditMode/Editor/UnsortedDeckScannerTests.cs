using System.IO;
using System.Linq;
using Mejnun.Core;
using Mejnun.Cosmetics;
using Mejnun.Editor.DeckImport;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mejnun.Editor.Tests
{
    public sealed class UnsortedDeckScannerTests
    {
        [TestCase("AofClubs.png", Suit.Clubs, Rank.Ace)]
        [TestCase("10ofDmnds.png", Suit.Diamonds, Rank.Ten)]
        [TestCase("ace_of_clubs.png", Suit.Clubs, Rank.Ace)]
        [TestCase("club-1-256.png", Suit.Clubs, Rank.Ace)]
        [TestCase("card_clubs_02.png", Suit.Clubs, Rank.Two)]
        [TestCase("card_hearts_10.png", Suit.Hearts, Rank.Ten)]
        [TestCase("10_of_spades.png", Suit.Spades, Rank.Ten)]
        [TestCase("card_spades_K.png", Suit.Spades, Rank.King)]
        public void Parser_RecognizesSupportedFaceNamingSchemes(string fileName, Suit suit, Rank rank)
        {
            var parsed = CardFileNameParser.Parse(fileName);

            Assert.That(parsed.Kind, Is.EqualTo(CardFileKind.Face));
            Assert.That(parsed.Suit, Is.EqualTo(suit));
            Assert.That(parsed.Rank, Is.EqualTo(rank));
        }

        [TestCase("card_back_01.png", "back_01")]
        [TestCase("CardBack1.png", "back_01")]
        [TestCase("back2-256.png", "back_02")]
        public void Parser_RecognizesNumberedBackNames(string fileName, string expectedBackId)
        {
            var parsed = CardFileNameParser.Parse(fileName);

            Assert.That(parsed.Kind, Is.EqualTo(CardFileKind.Back));
            Assert.That(parsed.BackId, Is.EqualTo(expectedBackId));
        }

        [TestCase("joker_red.png", "red")]
        [TestCase("card_joker_black.png", "black")]
        public void Parser_RecognizesJokerNames(string fileName, string expectedColor)
        {
            var parsed = CardFileNameParser.Parse(fileName);

            Assert.That(parsed.Kind, Is.EqualTo(CardFileKind.Joker));
            Assert.That(parsed.JokerColor, Is.EqualTo(expectedColor));
        }

        [TestCase("Blank Card.png")]
        [TestCase("card_empty.png")]
        public void Parser_RecognizesBlankNames(string fileName)
        {
            Assert.That(CardFileNameParser.Parse(fileName).Kind, Is.EqualTo(CardFileKind.Blank));
        }

        [TestCase("background.png")]
        [TestCase("background-1.png")]
        [TestCase("palette_classic.png")]
        public void Parser_DoesNotMisclassifyUnrelatedImages(string fileName)
        {
            Assert.That(CardFileNameParser.Parse(fileName).Kind, Is.EqualTo(CardFileKind.Unknown));
        }

        [Test]
        public void NameUtility_CleansPackAndVariantNames()
        {
            Assert.That(DeckNameUtility.GetDisplayName("REIGNS - Playing Card Deck"), Is.EqualTo("Reigns"));
            Assert.That(DeckNameUtility.GetDisplayName(
                    "sofu_playing_cards_pack/PNG/cards(large)"), Is.EqualTo("Sofu Large"));
            Assert.That(DeckNameUtility.GetDisplayName(
                    "Pixel Assets - Playing Cards - Blueeyedrat/Assets/deck_classic_light_4color_0.png"),
                Is.EqualTo("Blueeyedrat Classic Light 4color 0"));
            Assert.That(DeckNameUtility.GetId("Blueeyedrat Classic Light 4color 0"),
                Is.EqualTo("blueeyedrat_classic_light_4color_0"));
        }

        [Test]
        public void Scanner_RealUnsortedCollectionsProduceCompleteDeckPlansAndBackCounts()
        {
            var unsortedPath = Path.Combine(Application.dataPath,
                "_Project/Art/Cards/Decks/Unsorted");
            Assert.That(Directory.Exists(unsortedPath), Is.True, "The Unsorted art folder is required for this test.");
            var paths = Directory.GetFiles(unsortedPath, "*.png", SearchOption.AllDirectories)
                .Select(path => "Assets/" + path.Substring(Application.dataPath.Length + 1).Replace('\\', '/'))
                .ToArray();
            if (paths.Length == 0)
            {
                AssertImportedCatalog();
                return;
            }

            var result = UnsortedDeckScanner.Scan(paths);

            Assert.That(result.Decks.Count, Is.EqualTo(23));
            Assert.That(result.Warnings, Is.Empty);
            Assert.That(FindDeck(result, "PaperCards1.1").Backs.Count, Is.EqualTo(3));
            Assert.That(FindDeck(result, "REIGNS - Playing Card Deck").Backs.Count, Is.EqualTo(1));
            Assert.That(FindDeck(result, "card_pngs").Backs.Count, Is.EqualTo(3));
            Assert.That(FindDeck(result, "hack deck").Backs.Count, Is.EqualTo(2));

            var blueeyedratDecks = result.Decks.Where(deck => deck.SourceRoot.Contains("Blueeyedrat")).ToArray();
            Assert.That(blueeyedratDecks, Has.Length.EqualTo(16));
            Assert.That(blueeyedratDecks.All(deck => deck.Backs.Count == 28), Is.True);
            Assert.That(blueeyedratDecks.Any(deck => deck.Name == "Blueeyedrat Classic Light 4color 0"), Is.True);

            var sofuDecks = result.Decks.Where(deck => deck.Name.StartsWith("Sofu ")).ToArray();
            Assert.That(sofuDecks.Select(deck => deck.Name).OrderBy(name => name),
                Is.EqualTo(new[] { "Sofu Large", "Sofu Medium", "Sofu Small" }));
            Assert.That(sofuDecks.All(deck => deck.Backs.Count == 6), Is.True);
            Assert.That(sofuDecks.All(deck => deck.Faces.All(face => face != null)), Is.True);
        }

        [Test]
        public void ImportedCatalog_ContainsSharedAlternateBacksAndCompleteFaces()
        {
            AssertImportedCatalog();
        }

        [Test]
        public void Scanner_ReportsIncompleteCollectionWithoutMakingAnImportPlan()
        {
            var paths = new[]
            {
                "Assets/Unsorted/partial/ace_of_clubs.png",
                "Assets/Unsorted/partial/2_of_clubs.png"
            };

            var result = UnsortedDeckScanner.Scan(paths);

            Assert.That(result.Decks, Is.Empty);
            Assert.That(result.Warnings, Has.Count.EqualTo(1));
            Assert.That(result.Warnings[0], Does.Contain("incomplete or ambiguous"));
        }

        private static DeckImportPlan FindDeck(UnsortedDeckScanResult result, string sourceName)
        {
            return result.Decks.Single(deck => deck.SourceRoot.IndexOf(sourceName, System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void AssertImportedCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DeckSkinCatalog>(UnsortedDeckImporter.CatalogPath);
            Assert.That(catalog, Is.Not.Null);

            var light = AssetDatabase.LoadAssetAtPath<DeckSkin>(
                "Assets/_Project/Data/Skins/blueeyedrat_classic_light_4color_0.asset");
            var contrast = AssetDatabase.LoadAssetAtPath<DeckSkin>(
                "Assets/_Project/Data/Skins/blueeyedrat_classic_contrast_2color_0.asset");
            Assert.That(light, Is.Not.Null);
            Assert.That(contrast, Is.Not.Null);
            for (var suitIndex = 0; suitIndex < 4; suitIndex++)
            {
                for (var rankIndex = 0; rankIndex < 13; rankIndex++)
                {
                    Assert.That(light.GetFace((Suit)suitIndex, (Rank)rankIndex), Is.Not.Null);
                }
            }

            Assert.That(light.BackIds, Has.Length.EqualTo(27));
            Assert.That(light.GetBack("back_28"), Is.Not.Null);
            Assert.That(contrast.AlternateBacks[0].Sprite, Is.SameAs(light.AlternateBacks[0].Sprite));
            Assert.That(catalog.GetSkinIds(), Does.Contain(light.Id + ":back_28"));

            var reigns = AssetDatabase.LoadAssetAtPath<DeckSkin>("Assets/_Project/Data/Skins/reigns.asset");
            Assert.That(reigns, Is.Not.Null);
            Assert.That(reigns.Blank, Is.Null);
        }
    }
}