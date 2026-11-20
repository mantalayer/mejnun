using System;
using Mejnun.Game;
using Mejnun.Platform;
using NUnit.Framework;

namespace Mejnun.Platform.Tests
{
    public sealed class PlatformIntegrationTests
    {
        [Test]
        public void JsonFileStorageRoundTripsValuesAndReturnsDefaults()
        {
            JsonFileSaveStorage storage = JsonFileSaveStorage.Instance;
            string file = "MejnunPlatformTests_" + Guid.NewGuid().ToString("N");
            try
            {
                Assert.That(storage.Load("missing", file, "fallback"), Is.EqualTo("fallback"));

                storage.Save("score", 42, file);
                storage.Save("profile", new SaveProfile { Name = "Junie", Level = 3 }, file);

                Assert.That(storage.FileExists(file), Is.True);
                Assert.That(storage.Load<int>("score", file), Is.EqualTo(42));
                SaveProfile profile = storage.Load<SaveProfile>("profile", file);
                Assert.That(profile.Name, Is.EqualTo("Junie"));
                Assert.That(profile.Level, Is.EqualTo(3));
            }
            finally
            {
                storage.DeleteFile(file);
            }
        }

        [Test]
        public void JsonFileStorageDeletesIndividualKeys()
        {
            JsonFileSaveStorage storage = JsonFileSaveStorage.Instance;
            string file = "MejnunPlatformTests_" + Guid.NewGuid().ToString("N");
            try
            {
                storage.Save("remove", 1, file);
                storage.Save("keep", 2, file);
                storage.DeleteKey("remove", file);

                Assert.That(storage.KeyExists("remove", file), Is.False);
                Assert.That(storage.Load("keep", file, 0), Is.EqualTo(2));
            }
            finally
            {
                storage.DeleteFile(file);
            }
        }

        [Test]
        public void NullBackendStoresAchievementsAndStatsForTheSession()
        {
            NullPlatformServices services = new NullPlatformServices();

            services.Unlock(AchievementId.FirstGame);
            services.SetProgress(AchievementId.FirstWin, 1, 1);
            services.IncrementInt(StatId.GamesPlayed, 2);
            services.SetInt(StatId.GamesWon, 1);

            Assert.That(services.IsUnlocked(AchievementId.FirstGame), Is.True);
            Assert.That(services.IsUnlocked(AchievementId.FirstWin), Is.True);
            Assert.That(services.GetInt(StatId.GamesPlayed), Is.EqualTo(2));
            Assert.That(services.GetInt(StatId.GamesWon), Is.EqualTo(1));
            Assert.That(services.Owns(EntitlementId.RemoveAds), Is.False);
        }

        [Test]
        public void AchievementBridgeMapsLocalIdsAndIgnoresUnknownIds()
        {
            AssertMapping("first_game", AchievementId.FirstGame);
            AssertMapping("first_win", AchievementId.FirstWin);
            AssertMapping("first_mejnun", AchievementId.FirstMejnun);
            AssertMapping("ten_wins", AchievementId.TenWins);
            AssertMapping("beat_hard_bot", AchievementId.BeatHardBot);
            AssertMapping("full_table", AchievementId.FullTable);

            Assert.That(PlatformAchievementBridge.TryMapAchievement("unknown", out _), Is.False);
            Assert.DoesNotThrow(() => PlatformAchievementBridge.UnlockAchievements(new[] { "unknown", null }));
        }

        private static void AssertMapping(string localId, AchievementId expected)
        {
            Assert.That(PlatformAchievementBridge.TryMapAchievement(localId, out AchievementId actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Serializable]
        private sealed class SaveProfile
        {
            public string Name;
            public int Level;
        }
    }
}