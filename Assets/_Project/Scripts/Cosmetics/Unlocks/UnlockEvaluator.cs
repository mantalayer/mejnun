using System;
using System.Collections.Generic;

namespace Mejnun.Cosmetics
{
    public sealed class UnlockEvaluator
    {
        private readonly PlayerProfile profile;
        private readonly List<CosmeticUnlockEntry> entries;

        public UnlockEvaluator(PlayerProfile playerProfile, CosmeticCatalog catalog)
            : this(playerProfile, catalog == null ? null : catalog.GetUnlockEntries())
        {
        }

        public UnlockEvaluator(PlayerProfile playerProfile, IReadOnlyList<CosmeticUnlockEntry> cosmeticEntries)
        {
            profile = playerProfile ?? throw new ArgumentNullException(nameof(playerProfile));
            entries = new List<CosmeticUnlockEntry>();
            if (cosmeticEntries == null)
            {
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < cosmeticEntries.Count; index++)
            {
                var entry = cosmeticEntries[index];
                if (!ids.Add(entry.Id))
                {
                    throw new ArgumentException("Cosmetic catalog item ids must be unique.", nameof(cosmeticEntries));
                }

                entries.Add(entry);
                if (entry.Requirement.Kind == UnlockKind.Free)
                {
                    profile.MarkSeen(entry.Id);
                }
            }
        }

        public bool IsOwned(string itemId, UnlockRequirement requirement)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            if (profile.HasGrantedItem(itemId))
            {
                return true;
            }

            return (requirement ?? UnlockRequirement.Free).Evaluate(profile).Unlocked;
        }

        public UnlockProgress Evaluate(string itemId)
        {
            if (profile.HasGrantedItem(itemId))
            {
                return new UnlockProgress(true, 1, 1, false);
            }

            var entry = Find(itemId);
            return entry.HasValue
                ? entry.Value.Requirement.Evaluate(profile)
                : new UnlockProgress(false, 0, 0, false);
        }

        public UnlockRequirement GetRequirement(string itemId)
        {
            var entry = Find(itemId);
            return entry.HasValue ? entry.Value.Requirement : null;
        }

        public string[] OwnedFaceIds()
        {
            return OwnedIds(false);
        }

        public string[] OwnedFeltIds()
        {
            return OwnedIds(true);
        }

        public IReadOnlyList<string> NewItems()
        {
            var newItems = new List<string>();
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (IsOwned(entry.Id, entry.Requirement) && !profile.HasSeen(entry.Id))
                {
                    newItems.Add(entry.Id);
                }
            }

            return newItems.AsReadOnly();
        }

        public IReadOnlyList<string> AllItemIds()
        {
            var ids = new string[entries.Count];
            for (var index = 0; index < entries.Count; index++)
            {
                ids[index] = entries[index].Id;
            }

            return ids;
        }

        private string[] OwnedIds(bool felts)
        {
            var ids = new List<string>();
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry.IsFelt == felts && IsOwned(entry.Id, entry.Requirement))
                {
                    ids.Add(entry.Id);
                }
            }

            return ids.ToArray();
        }

        private CosmeticUnlockEntry? Find(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                if (string.Equals(entries[index].Id, itemId, StringComparison.Ordinal))
                {
                    return entries[index];
                }
            }

            return null;
        }
    }
}