using System.Collections.Generic;
using System.Linq;
using Mejnun.Cosmetics;
using Mejnun.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;

namespace Mejnun.Localization.Tests
{
    // Keys built from data at run time cannot be checked by the source
    // scanner, so every builder is checked here against the String Tables.
    public sealed class DataDrivenKeyTests
    {
        private static HashSet<string> Keys(string table)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(table);
            Assert.That(collection, Is.Not.Null, table);
            return new HashSet<string>(collection.SharedData.Entries.Select(entry => entry.Key));
        }

        private static void AssertAllPresent(IEnumerable<string> expected, HashSet<string> keys)
        {
            var missing = expected.Where(key => !keys.Contains(key)).ToList();
            Assert.That(missing, Is.Empty, "Missing String Table entries:\n" + string.Join("\n", missing));
        }

        [Test]
        public void EveryAchievementHasNameAndDescription()
        {
            var keys = Keys(Loc.UiTable);
            AssertAllPresent(AchievementIds.All.Select(AchievementIds.NameKey), keys);
            AssertAllPresent(AchievementIds.All.Select(AchievementIds.DescriptionKey), keys);
        }

        [Test]
        public void EveryDeckSkinAndFeltAssetHasAName()
        {
            var keys = Keys(Loc.UiTable);
            var skins = AssetDatabase.FindAssets("t:DeckSkin", new[] { "Assets/_Project" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<DeckSkin>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(skin => skin != null && !string.IsNullOrWhiteSpace(skin.Id))
                .ToList();
            var felts = AssetDatabase.FindAssets("t:TableFelt", new[] { "Assets/_Project" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<TableFelt>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(felt => felt != null && !string.IsNullOrWhiteSpace(felt.Id))
                .ToList();

            Assert.That(skins, Is.Not.Empty);
            Assert.That(felts, Is.Not.Empty);
            AssertAllPresent(skins.Select(skin => DeckSkin.NameKey(skin.Id)), keys);
            AssertAllPresent(felts.Select(felt => TableFelt.NameKey(felt.Id)), keys);
        }

        [Test]
        public void EveryBotNameHasAnEntry()
        {
            Assert.That(BotNameCatalog.Count, Is.GreaterThan(0));
            AssertAllPresent(BotNameCatalog.Keys, Keys(Loc.UiTable));
        }

        [Test]
        public void DealerQuestionsTableHasQuestions()
        {
            var keys = Keys(Loc.DealerQuestionsTable);
            Assert.That(keys.Count, Is.GreaterThanOrEqualTo(8));
            Assert.That(Loc.KeysOf(Loc.DealerQuestionsTable), Is.EquivalentTo(keys));
        }
    }
}
