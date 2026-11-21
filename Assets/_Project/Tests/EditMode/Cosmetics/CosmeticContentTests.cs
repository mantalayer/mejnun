using System.Linq;
using Mejnun.Cosmetics;
using NUnit.Framework;
using UnityEditor.AddressableAssets;
using UnityEditor;

namespace Mejnun.Cosmetics.Tests
{
    public sealed class CosmeticContentTests
    {
        [Test]
        public void CosmeticCatalogContainsAllSkinsAndSixConfiguredFelts()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(
                "Assets/_Project/Data/Cosmetics/CosmeticCatalog.asset");

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Decks.GetEntries(), Has.Count.EqualTo(26));
            Assert.That(catalog.Decks.Skins, Has.Count.EqualTo(25));
            Assert.That(catalog.Decks.AddressableSkins, Has.Count.EqualTo(1));
            Assert.That(catalog.Felts.Felts, Has.Count.EqualTo(6));
            Assert.That(catalog.GetUnlockEntries(), Has.Count.EqualTo(32));

            var pixelPastel = catalog.Decks.AddressableSkins[0];
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var addressableEntry = settings.FindAssetEntry(pixelPastel.Asset.AssetGUID);
            Assert.That(addressableEntry, Is.Not.Null);
            Assert.That(addressableEntry.parentGroup.Name, Is.EqualTo("OptionalDecks"));
        }

        [Test]
        public void CosmeticUnlockTableMatchesTheM6ContentRequirements()
        {
            var decks = AssetDatabase.LoadAssetAtPath<DeckSkinCatalog>(
                "Assets/_Project/Data/Skins/DeckSkinCatalog.asset");
            var pixelPastel = decks.AddressableSkins.Single(skin => skin.Id == "pixelPastel");
            var reigns = decks.Skins.Single(skin => skin.name == "reigns");
            var hackDeck = decks.Skins.Single(skin => skin.name == "hack_deck");
            var felts = AssetDatabase.LoadAssetAtPath<TableFeltCatalog>(
                "Assets/_Project/Data/Felts/TableFeltCatalog.asset");
            var green = felts.Resolve("green");
            var burgundy = felts.Resolve("burgundy");
            var blue = felts.Resolve("blue");

            Assert.That(pixelPastel.Unlock.Kind, Is.EqualTo(UnlockKind.Level));
            Assert.That(pixelPastel.Unlock.Target, Is.EqualTo(2));
            Assert.That(reigns.Unlock.AchievementId, Is.EqualTo(AchievementIds.FirstMejnun));
            Assert.That(hackDeck.Unlock.AchievementId, Is.EqualTo(AchievementIds.BeatHardBot));
            Assert.That(green.Unlock.Kind, Is.EqualTo(UnlockKind.Free));
            Assert.That(green.Mode, Is.EqualTo(FeltRenderMode.Tiled));
            Assert.That(burgundy.Unlock.Kind, Is.EqualTo(UnlockKind.Level));
            Assert.That(burgundy.Unlock.Target, Is.EqualTo(2));
            Assert.That(burgundy.Mode, Is.EqualTo(FeltRenderMode.Tiled));
            Assert.That(blue.Id, Is.EqualTo("blue"));
            Assert.That(blue.Unlock.Kind, Is.EqualTo(UnlockKind.Level));
            Assert.That(blue.Unlock.Target, Is.EqualTo(3));
            Assert.That(blue.Mode, Is.EqualTo(FeltRenderMode.Tiled));

            foreach (var (id, level) in new[] { ("teal", 5), ("purple", 7), ("black", 10) })
            {
                var felt = felts.Resolve(id);
                Assert.That(felt.Id, Is.EqualTo(id));
                Assert.That(felt.Unlock.Kind, Is.EqualTo(UnlockKind.Level), id);
                Assert.That(felt.Unlock.Target, Is.EqualTo(level), id);
                Assert.That(felt.Mode, Is.EqualTo(FeltRenderMode.Tiled), id);
            }
        }
    }
}