using System;
using System.IO;
using System.Linq;
using Mejnun.Cosmetics;
using Mejnun.Core;
using Mejnun.Game;
using Mejnun.UI.FrontEnd;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mejnun.Cosmetics.Tests
{
    public sealed class GalleryTests
    {
        private string profilePath;
        private CosmeticCatalog catalog;
        private ProgressionService progression;
        private GalleryScreen gallery;
        private VisualElement root;

        [SetUp]
        public void SetUp()
        {
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "M6GalleryTests");
            Directory.CreateDirectory(directory);
            profilePath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
            catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(
                "Assets/_Project/Data/Cosmetics/CosmeticCatalog.asset");
            progression = new ProgressionService(new ProfileStore(profilePath), catalog);
            root = CreateGalleryRoot();
            gallery = new GalleryScreen(root, catalog, progression, null);
        }

        [TearDown]
        public void TearDown()
        {
            gallery?.Dispose();
            DeleteIfExists(profilePath);
            DeleteIfExists(profilePath + ".tmp");
            DeleteIfExists(profilePath + ".bak");
            var directory = Path.GetDirectoryName(profilePath);
            if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
            {
                Directory.Delete(directory);
            }
        }

        [Test]
        public void LockedItemDeepLinkShowsRequirementAndProgress()
        {
            var locked = FindBuiltInUnlock(UnlockKind.Level, 3);

            gallery.Show(locked.Id);

            Assert.That(gallery.SelectedItemId, Is.EqualTo(locked.Id));
            Assert.That(root.Q<Label>("gallery-selected-title").text, Is.EqualTo(locked.DisplayName));
            Assert.That(root.Q<Label>("gallery-requirement").text, Is.EqualTo("Reach level 3"));
            Assert.That(root.Q<Label>("gallery-progress-label").text, Is.EqualTo("1 / 3"));
            Assert.That(root.Q<ProgressBar>("gallery-progress").style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void NewlyUnlockedItemsShowBadgeUntilGalleryIsLeft()
        {
            progression.AddDebugXp(100);
            gallery.Show();

            var newBadges = root.Query<Label>(name: "gallery-item-new").ToList();
            CollectionAssert.IsNotEmpty(newBadges);
            Assert.That(progression.Profile.HasSeen("pixelPastel"), Is.False);

            gallery.MarkSeen();

            Assert.That(progression.Profile.HasSeen("pixelPastel"), Is.True);
            Assert.That(gallery.SelectedItemId, Is.Null);
        }

        [Test]
        public void TabsShowSeparateOwnedCountsAndFeltDeepLinks()
        {
            gallery.Show("midnight");

            Assert.That(gallery.SelectedItemId, Is.EqualTo("midnight"));
            Assert.That(root.Q<Label>("gallery-count-label").text, Does.Contain("board backgrounds owned"));
            Assert.That(root.Q<Label>("gallery-selected-title").text, Is.EqualTo("Midnight"));
            Assert.That(root.Query<Button>(className: "gallery-item-tile").ToList().Count, Is.EqualTo(3));
        }

        [Test]
        public void DeckTilesShowArtistOnlyWhenSkinHasOne()
        {
            gallery.Dispose();
            var texture = new Texture2D(1, 1);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            var creditedSkin = CreateSkin("credited", "Credited Deck", sprite, "A. Creator");
            var uncreditedSkin = CreateSkin("uncredited", "Uncredited Deck", sprite, string.Empty);
            var deckCatalog = ScriptableObject.CreateInstance<DeckSkinCatalog>();
            deckCatalog.Configure(new[] { creditedSkin, uncreditedSkin });
            var feltCatalog = ScriptableObject.CreateInstance<TableFeltCatalog>();
            feltCatalog.Configure(Array.Empty<TableFelt>());
            var testCatalog = ScriptableObject.CreateInstance<CosmeticCatalog>();
            testCatalog.Configure(deckCatalog, feltCatalog);
            catalog = testCatalog;
            progression = new ProgressionService(new ProfileStore(profilePath), catalog);
            gallery = new GalleryScreen(root, catalog, progression, null);

            try
            {
                gallery.Show();

                var tiles = root.Query<Button>(className: "gallery-item-tile").ToList();
                var creditedTile = tiles.Single(tile => tile.Q<Label>("gallery-item-name").text == "Credited Deck");
                var uncreditedTile = tiles.Single(tile => tile.Q<Label>("gallery-item-name").text == "Uncredited Deck");
                Assert.That(creditedTile.Q<Label>("gallery-item-artist").text, Is.EqualTo("by A. Creator"));
                Assert.That(uncreditedTile.Q<Label>("gallery-item-artist"), Is.Null);
            }
            finally
            {
                gallery.Dispose();
                gallery = null;
                UnityEngine.Object.DestroyImmediate(testCatalog);
                UnityEngine.Object.DestroyImmediate(feltCatalog);
                UnityEngine.Object.DestroyImmediate(deckCatalog);
                UnityEngine.Object.DestroyImmediate(creditedSkin);
                UnityEngine.Object.DestroyImmediate(uncreditedSkin);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private DeckSkinInfo FindBuiltInUnlock(UnlockKind kind, int target)
        {
            var entries = catalog.Decks.GetEntries();
            for (var index = 0; index < entries.Count; index++)
            {
                if (!entries[index].IsAddressable && entries[index].Unlock.Kind == kind &&
                    entries[index].Unlock.Target == target)
                {
                    return entries[index];
                }
            }

            throw new AssertionException("Could not find a built-in skin with the requested unlock.");
        }

        private static VisualElement CreateGalleryRoot()
        {
            var root = new VisualElement();
            var gallery = new VisualElement { name = "gallery-screen" };
            root.Add(gallery);
            gallery.Add(new Button { name = "gallery-decks-tab" });
            gallery.Add(new Button { name = "gallery-felts-tab" });
            gallery.Add(new Label { name = "gallery-count-label" });
            gallery.Add(new ScrollView { name = "gallery-items" });
            gallery.Add(new Label { name = "gallery-selected-title" });
            gallery.Add(new Label { name = "gallery-requirement" });
            gallery.Add(new Label { name = "gallery-progress-label" });
            gallery.Add(new Label { name = "gallery-selected-new" });
            gallery.Add(new VisualElement { name = "gallery-preview" });
            gallery.Add(new ProgressBar { name = "gallery-progress" });
            return root;
        }

        private static DeckSkin CreateSkin(string id, string displayName, Sprite sprite, string artist)
        {
            var skin = ScriptableObject.CreateInstance<DeckSkin>();
            skin.Configure(id, displayName, Enumerable.Repeat(sprite, Card.CardsPerDeck).ToArray(), sprite,
                null, false, 100f, artist);
            return skin;
        }

        private static void DeleteIfExists(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}