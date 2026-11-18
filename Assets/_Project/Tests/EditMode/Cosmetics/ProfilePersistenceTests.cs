using System;
using System.IO;
using Mejnun.Cosmetics;
using Mejnun.Game;
using NUnit.Framework;

namespace Mejnun.Cosmetics.Tests
{
    public sealed class ProfilePersistenceTests
    {
        private string profilePath;

        [SetUp]
        public void SetUp()
        {
            var directory = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "M6ProfileTests");
            Directory.CreateDirectory(directory);
            profilePath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
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
        public void ProfileSerializerRoundTripsProfileData()
        {
            var profile = new PlayerProfile();
            profile.AddXp(245);
            profile.RecordMatch(new MatchSummary(MatchMode.Solo, 2, 1, true, true, false, false));
            profile.AddAchievement(AchievementIds.FirstWin);
            profile.GrantItem("future_purchase");
            profile.MarkSeen("classic");
            profile.SetLastPicks(new[] { "classic", "" }, "burgundy");
            profile.SetLastUsedFelt("midnight");

            var restored = ProfileSerializer.FromJson(ProfileSerializer.ToJson(profile));

            Assert.That(restored.SchemaVersion, Is.EqualTo(PlayerProfile.CurrentSchemaVersion));
            Assert.That(restored.Xp, Is.EqualTo(profile.Xp));
            Assert.That(restored.Stats.Played, Is.EqualTo(1));
            Assert.That(restored.Stats.Wins, Is.EqualTo(1));
            Assert.That(restored.Stats.MejnunWins, Is.EqualTo(1));
            Assert.That(restored.HasAchievement(AchievementIds.FirstWin), Is.True);
            Assert.That(restored.HasGrantedItem("future_purchase"), Is.True);
            Assert.That(restored.HasSeen("classic"), Is.True);
            CollectionAssert.AreEqual(new[] { "classic", "" }, restored.LastDeckPicks);
            Assert.That(restored.LastFeltPick, Is.EqualTo("burgundy"));
            Assert.That(restored.LastUsedFelt, Is.EqualTo("midnight"));
        }

        [Test]
        public void ProfileSerializerMigratesMissingSchemaVersion()
        {
            var profile = ProfileSerializer.FromJson("{\"xp\":42,\"stats\":{\"played\":2}}");

            Assert.That(profile.SchemaVersion, Is.EqualTo(PlayerProfile.CurrentSchemaVersion));
            Assert.That(profile.Xp, Is.EqualTo(42));
            Assert.That(profile.Stats.Played, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { "", "" }, profile.LastDeckPicks);
        }

        [Test]
        public void ProfileSerializerRejectsFutureSchemaAndMalformedJson()
        {
            Assert.Throws<InvalidDataException>(() => ProfileSerializer.FromJson("{\"schemaVersion\":2}"));
            Assert.Throws<InvalidDataException>(() => ProfileSerializer.FromJson("not json"));
        }

        [Test]
        public void ProfileStoreBacksUpCorruptJsonAndCreatesFreshProfile()
        {
            File.WriteAllText(profilePath, "not json");
            var store = new ProfileStore(profilePath);

            var profile = store.Load();

            Assert.That(profile.Xp, Is.Zero);
            Assert.That(File.Exists(profilePath + ".bak"), Is.True);
            Assert.That(File.ReadAllText(profilePath + ".bak"), Is.EqualTo("not json"));
            Assert.That(ProfileSerializer.FromJson(File.ReadAllText(profilePath)).Xp, Is.Zero);
        }

        [Test]
        public void ProgressionServiceRecordsAndPersistsMatchRewards()
        {
            var service = new ProgressionService(new ProfileStore(profilePath));
            var report = service.RecordMatch(
                new MatchSummary(MatchMode.Solo, 3, 1, true, true, false, true));
            var restored = new ProfileStore(profilePath).Load();

            Assert.That(report.XpGained, Is.EqualTo(80));
            Assert.That(report.LevelBefore, Is.EqualTo(1));
            Assert.That(report.LevelAfter, Is.EqualTo(1));
            CollectionAssert.AreEquivalent(new[]
            {
                AchievementIds.FirstGame,
                AchievementIds.FirstWin,
                AchievementIds.FirstMejnun,
                AchievementIds.BeatHardBot
            }, report.NewAchievements);
            Assert.That(restored.Xp, Is.EqualTo(80));
            Assert.That(restored.Stats.Wins, Is.EqualTo(1));
        }

        [Test]
        public void ProgressionServicePersistsLastUsedFelt()
        {
            var service = new ProgressionService(new ProfileStore(profilePath));

            service.SetLastUsedFelt("burgundy");

            var restored = new ProfileStore(profilePath).Load();
            Assert.That(restored.LastUsedFelt, Is.EqualTo("burgundy"));
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