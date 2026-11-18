using System;
using System.IO;
using Mejnun.AI;
using Mejnun.Cosmetics;
using Mejnun.Game;
using Mejnun.UI.FrontEnd;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Mejnun.Cosmetics.Tests
{
    public sealed class TablePickerTests
    {
        private string profilePath;
        private CosmeticCatalog catalog;
        private ProgressionService progression;
        private TablePickerScreen picker;
        private VisualElement pickerRoot;
        private string openedGalleryItem;

        [SetUp]
        public void SetUp()
        {
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "M6TablePickerTests");
            Directory.CreateDirectory(directory);
            profilePath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
            catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(
                "Assets/_Project/Data/Cosmetics/CosmeticCatalog.asset");
            progression = new ProgressionService(new ProfileStore(profilePath), catalog);
            pickerRoot = CreatePickerRoot();
            picker = new TablePickerScreen(pickerRoot, catalog, progression,
                id => openedGalleryItem = id);
        }

        [TearDown]
        public void TearDown()
        {
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
        public void PickerRemembersOwnedPicksAndRejectsDuplicateSlots()
        {
            var freeSkins = FindFreeSkins();
            progression.SetLastPicks(new[] { freeSkins[0].Id, freeSkins[0].Id }, "burgundy");
            picker.SetSetupRequest(SoloSetup());

            Assert.That(picker.GetDeckPick(0), Is.EqualTo(freeSkins[0].Id));
            Assert.That(picker.GetDeckPick(1), Is.Null);
            Assert.That(picker.FeltPick, Is.EqualTo("burgundy"));
            Assert.That(picker.SelectDeck(1, freeSkins[0].Id), Is.False);
            Assert.That(picker.SelectDeck(1, freeSkins[1].Id), Is.True);
            Assert.That(picker.CanStart, Is.True);
            CollectionAssert.AreEqual(new[] { freeSkins[0].Id, freeSkins[1].Id }, progression.Profile.LastDeckPicks);
        }

        [Test]
        public void LockedChoiceShowsNoticeAndDoesNotOpenGalleryOrBecomeASelection()
        {
            var locked = FindLockedLevelThreeSkin();
            picker.SetSetupRequest(SoloSetup());

            Assert.That(picker.SelectDeck(0, locked.Id), Is.False);
            Assert.That(openedGalleryItem, Is.Null);
            Assert.That(picker.GetDeckPick(0), Is.Null);
            Assert.That(picker.IsPopupOpen, Is.False);
            var notice = pickerRoot.Q<Label>("cosmetic-locked-notice");
            Assert.That(notice.text, Does.StartWith("Not unlocked yet"));
            Assert.That(notice.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void SoloRequestUsesOnlyTheHumanCosmetics()
        {
            var freeSkins = FindFreeSkins();
            picker.SetSetupRequest(SoloSetup());
            Assert.That(picker.SelectDeck(0, freeSkins[0].Id), Is.True);
            Assert.That(picker.SelectDeck(1, freeSkins[1].Id), Is.True);
            Assert.That(picker.SelectFelt("burgundy"), Is.True);

            var request = picker.CreateMatchRequest();

            CollectionAssert.AreEqual(new[] { freeSkins[0].Id, freeSkins[1].Id }, request.DeckPicks);
            CollectionAssert.AreEqual(new[] { "burgundy" }, request.FeltPool);
            CollectionAssert.AreEquivalent(progression.CreateUnlockEvaluator().OwnedFaceIds(), request.RandomFacePool);
            CollectionAssert.Contains(request.RandomFacePool, freeSkins[0].Id);
            Assert.That(request.Mode, Is.EqualTo(MatchMode.Solo));
        }

        [Test]
        public void PassAndPlayDoesNotAddBotFaceCandidates()
        {
            picker.SetSetupRequest(new MatchRequest(new[] { SeatConfig.Human("Sam"), SeatConfig.Human("Lee") },
                mode: MatchMode.PassAndPlay));

            var request = picker.CreateMatchRequest();

            Assert.That(request.RandomFacePool.Count,
                Is.EqualTo(progression.CreateUnlockEvaluator().OwnedFaceIds().Length));
        }

        [Test]
        public void RandomFeltResolvesToOneOwnedFelt()
        {
            picker.SetSetupRequest(SoloSetup());

            var request = picker.CreateMatchRequest();
            var ownedFelts = progression.CreateUnlockEvaluator().OwnedFeltIds();

            Assert.That(request.FeltPool.Count, Is.EqualTo(1));
            CollectionAssert.Contains(ownedFelts, request.FeltPool[0]);
        }

        [Test]
        public void DeckChoicesShowOwnedCosmeticsBeforeLockedCosmetics()
        {
            picker.OpenDeckPopup(0);

            AssertUnlockedChoicesFirst(pickerRoot.Q<ScrollView>("deck-selector-grid").contentContainer);
        }

        [Test]
        public void FeltChoicesShowOwnedCosmeticsBeforeLockedCosmetics()
        {
            picker.OpenFeltPopup();

            AssertUnlockedChoicesFirst(pickerRoot.Q<ScrollView>("felt-selector-grid").contentContainer);
        }

        private static void AssertUnlockedChoicesFirst(VisualElement choices)
        {
            var foundLockedChoice = false;
            var lockedChoiceCount = 0;
            foreach (var choice in choices.Children())
            {
                if (choice.ClassListContains("locked-choice"))
                {
                    foundLockedChoice = true;
                    lockedChoiceCount++;
                }
                else
                {
                    Assert.That(foundLockedChoice, Is.False, "An unlocked choice appeared after a locked choice.");
                }
            }

            Assert.That(lockedChoiceCount, Is.GreaterThan(0), "The test catalog should include locked choices.");
        }

        private MatchRequest SoloSetup()
        {
            return new MatchRequest(new[]
            {
                SeatConfig.Human("Sam"),
                SeatConfig.Bot("Leyla", BotDifficulty.Medium)
            }, mode: MatchMode.Solo, seed: 777UL);
        }

        private DeckSkinInfo[] FindFreeSkins()
        {
            var entries = catalog.Decks.GetEntries();
            var free = new System.Collections.Generic.List<DeckSkinInfo>();
            for (var index = 0; index < entries.Count; index++)
            {
                if (progression.CreateUnlockEvaluator().IsOwned(entries[index].Id, entries[index].Unlock) &&
                    entries[index].Unlock.Kind == UnlockKind.Free)
                {
                    free.Add(entries[index]);
                }
            }

            return free.ToArray();
        }

        private DeckSkinInfo FindLockedLevelThreeSkin()
        {
            var entries = catalog.Decks.GetEntries();
            for (var index = 0; index < entries.Count; index++)
            {
                if (entries[index].Unlock.Kind == UnlockKind.Level && entries[index].Unlock.Target == 3)
                {
                    return entries[index];
                }
            }

            throw new AssertionException("Could not find the level-three skin.");
        }

        private static VisualElement CreatePickerRoot()
        {
            var root = new VisualElement();
            var setup = new VisualElement { name = "setup-screen" };
            root.Add(setup);
            setup.Add(new Button { name = "deck-preview-0" });
            setup.Add(new Button { name = "deck-preview-1" });
            setup.Add(new Button { name = "felt-preview" });

            var popup = new VisualElement { name = "cosmetic-popup" };
            popup.style.display = DisplayStyle.None;
            root.Add(popup);
            popup.Add(new VisualElement { name = "cosmetic-popup-tail" });
            var popupPanel = new VisualElement();
            popupPanel.Add(new Label { name = "cosmetic-popup-title" });
            popupPanel.Add(new ScrollView { name = "deck-selector-grid" });
            popupPanel.Add(new ScrollView { name = "felt-selector-grid" });
            popupPanel.Add(new Label { name = "cosmetic-locked-notice" });
            popupPanel.Add(new Button { name = "cosmetic-popup-close" });
            popup.Add(popupPanel);
            return root;
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