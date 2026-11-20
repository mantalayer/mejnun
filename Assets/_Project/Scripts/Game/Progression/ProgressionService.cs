using System;
using System.Collections.Generic;
using Mejnun.Cosmetics;
using UnityEngine;

namespace Mejnun.Game
{
    public sealed class ProgressionService
    {
        private static ProgressionService instance;
        private readonly ProfileStore profileStore;
        private CosmeticCatalog cosmeticCatalog;
        private UnlockEvaluator unlockEvaluator;

        public ProgressionService(ProfileStore store, CosmeticCatalog catalog = null)
        {
            profileStore = store ?? throw new ArgumentNullException(nameof(store));
            Profile = profileStore.Load();
            ConfigureCatalog(catalog);
            PlatformAchievementBridge.SyncOwnedAchievements(Profile.Achievements);
        }

        public static ProgressionService Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ProgressionService(new ProfileStore());
                }

                return instance;
            }
        }

        public PlayerProfile Profile { get; private set; }
        public CosmeticCatalog Catalog => cosmeticCatalog;
        public event Action Changed;
        public event Action<ProgressionReport> MatchRecorded;

        public static void Initialize(CosmeticCatalog catalog)
        {
            Instance.ConfigureCatalog(catalog);
        }

        public void ConfigureCatalog(CosmeticCatalog catalog)
        {
            if (ReferenceEquals(cosmeticCatalog, catalog))
            {
                return;
            }

            cosmeticCatalog = catalog;
            unlockEvaluator = catalog == null ? null : new UnlockEvaluator(Profile, catalog);
            if (catalog != null)
            {
                SaveProfile();
            }

            Changed?.Invoke();
        }

        public UnlockEvaluator CreateUnlockEvaluator()
        {
            return new UnlockEvaluator(Profile, cosmeticCatalog);
        }

        public ProgressionReport RecordMatch(MatchSummary summary)
        {
            var ownedBefore = new HashSet<string>(StringComparer.Ordinal);
            if (unlockEvaluator != null)
            {
                AddOwnedItems(ownedBefore, unlockEvaluator);
            }

            var levelBefore = Profile.Level;
            var xpGained = ProgressionRules.XpFor(summary);
            Profile.RecordMatch(summary);
            var newAchievements = AchievementTracker.Evaluate(Profile, summary);
            for (var index = 0; index < newAchievements.Count; index++)
            {
                Profile.AddAchievement(newAchievements[index]);
            }
            PlatformAchievementBridge.RecordMatch(summary, newAchievements);

            var newlyUnlocked = new List<string>();
            if (unlockEvaluator != null)
            {
                AddOwnedItems(unlockedItems: null, evaluator: unlockEvaluator, newlyUnlocked: newlyUnlocked,
                    previouslyOwned: ownedBefore);
            }

            var report = new ProgressionReport(xpGained, levelBefore, Profile.Level,
                newAchievements, newlyUnlocked.AsReadOnly());
            SaveProfile();
            Changed?.Invoke();
            MatchRecorded?.Invoke(report);
            return report;
        }

        public void MarkSeen(IEnumerable<string> itemIds)
        {
            if (itemIds == null)
            {
                return;
            }

            var changed = false;
            foreach (var itemId in itemIds)
            {
                changed |= Profile.MarkSeen(itemId);
            }

            if (changed)
            {
                SaveProfile();
                Changed?.Invoke();
            }
        }

        public void SetLastPicks(IReadOnlyList<string> decks, string felt)
        {
            Profile.SetLastPicks(decks, felt);
            SaveProfile();
            Changed?.Invoke();
        }

        public void SetLastUsedFelt(string feltId)
        {
            if (string.IsNullOrWhiteSpace(feltId) || !Profile.SetLastUsedFelt(feltId))
            {
                return;
            }

            SaveProfile();
            Changed?.Invoke();
        }

        public void AddDebugXp(int amount)
        {
            Profile.AddXp(amount);
            SaveProfile();
            Changed?.Invoke();
        }

        public void AddDebugWin()
        {
            RecordMatch(new MatchSummary(MatchMode.Solo, 2, 1, true, false, false, false));
        }

        public void GrantAllItems()
        {
            if (unlockEvaluator == null)
            {
                return;
            }

            var ids = unlockEvaluator.AllItemIds();
            for (var index = 0; index < ids.Count; index++)
            {
                Profile.GrantItem(ids[index]);
            }

            SaveProfile();
            Changed?.Invoke();
        }

        public void ResetProfile()
        {
            Profile = new PlayerProfile();
            unlockEvaluator = cosmeticCatalog == null ? null : new UnlockEvaluator(Profile, cosmeticCatalog);
            SaveProfile();
            Changed?.Invoke();
        }

        public void SaveProfile()
        {
            try
            {
                profileStore.Save(Profile);
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not save the Mejnun profile: " + exception.Message);
            }
        }

        public static void ResetForTests()
        {
            instance = null;
        }

        public static ProgressionService ConfigureForTests(ProfileStore store, CosmeticCatalog catalog = null)
        {
            instance = new ProgressionService(store, catalog);
            return instance;
        }

        private static void AddOwnedItems(HashSet<string> unlockedItems, UnlockEvaluator evaluator,
            List<string> newlyUnlocked = null, HashSet<string> previouslyOwned = null)
        {
            var faceIds = evaluator.OwnedFaceIds();
            var feltIds = evaluator.OwnedFeltIds();
            for (var index = 0; index < faceIds.Length; index++)
            {
                AddOwned(faceIds[index], unlockedItems, newlyUnlocked, previouslyOwned);
            }

            for (var index = 0; index < feltIds.Length; index++)
            {
                AddOwned(feltIds[index], unlockedItems, newlyUnlocked, previouslyOwned);
            }
        }

        private static void AddOwned(string id, HashSet<string> unlockedItems, List<string> newlyUnlocked,
            HashSet<string> previouslyOwned)
        {
            if (unlockedItems != null)
            {
                unlockedItems.Add(id);
            }

            if (newlyUnlocked != null && (previouslyOwned == null || !previouslyOwned.Contains(id)))
            {
                newlyUnlocked.Add(id);
            }
        }
    }
}