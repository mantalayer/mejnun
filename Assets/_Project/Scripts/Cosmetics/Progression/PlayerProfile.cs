using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mejnun.Cosmetics
{
    [Serializable]
    public sealed class PlayerProfile
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private int xp;
        [SerializeField] private PlayerStats stats = new PlayerStats();
        [SerializeField] private List<string> achievements = new List<string>();
        [SerializeField] private List<string> grantedItems = new List<string>();
        [SerializeField] private List<string> seenItems = new List<string>();
        [SerializeField] private string[] lastDeckPicks = { "", "" };
        [SerializeField] private string lastFeltPick = string.Empty;
        [SerializeField] private string lastUsedFelt = string.Empty;

        public int SchemaVersion => schemaVersion;
        public int Xp => xp;
        public int Level => ProgressionRules.LevelForXp(xp);
        public PlayerStats Stats => stats;
        public IReadOnlyList<string> Achievements => achievements;
        public IReadOnlyList<string> GrantedItems => grantedItems;
        public IReadOnlyList<string> SeenItems => seenItems;
        public string[] LastDeckPicks => (string[])lastDeckPicks.Clone();
        public string LastFeltPick => lastFeltPick;
        public string LastUsedFelt => lastUsedFelt;

        public void AddXp(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "XP cannot be negative.");
            }

            xp = xp > int.MaxValue - amount ? int.MaxValue : xp + amount;
        }

        public void RecordMatch(MatchSummary summary)
        {
            stats.RecordMatch(summary);
            AddXp(ProgressionRules.XpFor(summary));
        }

        public bool HasAchievement(string achievementId)
        {
            return !string.IsNullOrWhiteSpace(achievementId) && achievements.Contains(achievementId);
        }

        public bool AddAchievement(string achievementId)
        {
            if (string.IsNullOrWhiteSpace(achievementId))
            {
                throw new ArgumentException("An achievement id is required.", nameof(achievementId));
            }

            if (achievements.Contains(achievementId))
            {
                return false;
            }

            achievements.Add(achievementId);
            return true;
        }

        public bool HasGrantedItem(string itemId)
        {
            return !string.IsNullOrWhiteSpace(itemId) && grantedItems.Contains(itemId);
        }

        public bool GrantItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("An item id is required.", nameof(itemId));
            }

            if (grantedItems.Contains(itemId))
            {
                return false;
            }

            grantedItems.Add(itemId);
            return true;
        }

        public bool HasSeen(string itemId)
        {
            return !string.IsNullOrWhiteSpace(itemId) && seenItems.Contains(itemId);
        }

        public bool MarkSeen(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || seenItems.Contains(itemId))
            {
                return false;
            }

            seenItems.Add(itemId);
            return true;
        }

        public void SetLastPicks(IReadOnlyList<string> decks, string felt)
        {
            if (decks == null || decks.Count != 2)
            {
                throw new ArgumentException("Exactly two deck picks are required.", nameof(decks));
            }

            lastDeckPicks = new[] { decks[0] ?? string.Empty, decks[1] ?? string.Empty };
            lastFeltPick = felt ?? string.Empty;
        }

        public bool SetLastUsedFelt(string feltId)
        {
            feltId = feltId ?? string.Empty;
            if (string.Equals(lastUsedFelt, feltId, StringComparison.Ordinal))
            {
                return false;
            }

            lastUsedFelt = feltId;
            return true;
        }

        internal void MigrateAndNormalize()
        {
            if (schemaVersion < 0 || schemaVersion > CurrentSchemaVersion)
            {
                throw new InvalidDataException("Unsupported player profile schema version.");
            }

            if (schemaVersion == 0)
            {
                schemaVersion = CurrentSchemaVersion;
            }

            xp = Math.Max(0, xp);
            if (stats == null)
            {
                stats = new PlayerStats();
            }

            stats.Normalize();
            achievements = NormalizeIds(achievements);
            grantedItems = NormalizeIds(grantedItems);
            seenItems = NormalizeIds(seenItems);
            if (lastDeckPicks == null || lastDeckPicks.Length != 2)
            {
                lastDeckPicks = new[] { string.Empty, string.Empty };
            }
            else
            {
                for (var index = 0; index < lastDeckPicks.Length; index++)
                {
                    lastDeckPicks[index] = lastDeckPicks[index] ?? string.Empty;
                }
            }

            lastFeltPick = lastFeltPick ?? string.Empty;
            lastUsedFelt = lastUsedFelt ?? string.Empty;
        }

        private static List<string> NormalizeIds(List<string> ids)
        {
            var normalized = new List<string>();
            if (ids == null)
            {
                return normalized;
            }

            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < ids.Count; index++)
            {
                var id = ids[index];
                if (!string.IsNullOrWhiteSpace(id) && unique.Add(id))
                {
                    normalized.Add(id);
                }
            }

            return normalized;
        }
    }
}