using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mejnun.Platform
{
    // Offline fallback used in the Editor, in builds without a store define, and
    // whenever a store SDK fails to start. Keeps achievements and stats in memory
    // for the session while save data remains available through local storage.
    public sealed class NullPlatformServices : IPlatformServices, IControllerInfo, IAchievements, IStats, IRichPresence, IEntitlements
    {
        private readonly HashSet<AchievementId> m_unlockedAchievements = new();
        private readonly Dictionary<StatId, int> m_stats = new();
        private readonly bool m_isSteamDeck =
            string.Equals(Environment.GetEnvironmentVariable("SteamDeck"), "1", StringComparison.Ordinal);

        public string PlatformName => "Null";
        public bool IsInitialized => true;
        public IControllerInfo ControllerInfo => this;
        public ISaveStorage SaveStorage => JsonFileSaveStorage.Instance;
        public IAchievements Achievements => this;
        public IStats Stats => this;
        public IRichPresence RichPresence => this;
        public IEntitlements Entitlements => this;

        public bool IsDemo => PlatformBuild.IsDemo;
        public bool IsSteamDeck => m_isSteamDeck;
        public PlatformControllerKind ActiveControllerKind => PlatformControllerKind.Unknown;

        public void Tick()
        {
        }

        public void Shutdown()
        {
        }

        public void Unlock(AchievementId id)
        {
            if (m_unlockedAchievements.Add(id))
                Debug.Log($"[Platform:Null] Achievement unlocked: {id}");
        }

        public void SetProgress(AchievementId id, int current, int max)
        {
            if (current >= max)
                Unlock(id);
        }

        public bool IsUnlocked(AchievementId id)
        {
            return m_unlockedAchievements.Contains(id);
        }

        public int GetInt(StatId id)
        {
            return m_stats.TryGetValue(id, out int value) ? value : 0;
        }

        public void SetInt(StatId id, int value)
        {
            m_stats[id] = value;
        }

        public void IncrementInt(StatId id, int amount)
        {
            m_stats[id] = GetInt(id) + amount;
        }

        public void Set(string key, string value)
        {
        }

        public void Clear()
        {
        }

        public bool OwnsDlc(DlcId id)
        {
            return false;
        }

        public bool Owns(EntitlementId id)
        {
            return false;
        }
    }
}
