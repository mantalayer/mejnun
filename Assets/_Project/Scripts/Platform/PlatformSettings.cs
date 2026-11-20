using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mejnun.Platform
{
    // Store App IDs and the table mapping neutral achievement / stat / DLC IDs
    // to per-store names. Loaded from Resources because backends are chosen
    // before the first scene loads, when no Inspector reference is available.
    [CreateAssetMenu(fileName = "PlatformSettings", menuName = "Mejnun/Platform Settings")]
    public sealed class PlatformSettings : ScriptableObject
    {
        public const string ResourcesPath = "PlatformSettings";

        [Serializable]
        public struct AchievementEntry
        {
            public AchievementId Id;
            [Tooltip("Achievement API name from the Steamworks partner site.")]
            public string SteamApiName;
        }

        [Serializable]
        public struct StatEntry
        {
            public StatId Id;
            [Tooltip("Stat API name from the Steamworks partner site.")]
            public string SteamApiName;
        }

        [Serializable]
        public struct DlcEntry
        {
            public DlcId Id;
            [Tooltip("Steam App ID of the DLC.")]
            public uint SteamAppId;
        }

        [Header("Steam")]
        [Tooltip("App ID of the full game.")]
        [SerializeField] private uint m_steamFullAppId;
        [Tooltip("App ID of the demo. Demo builds (MEJNUN_DEMO) fall back to the offline backend while this is 0.")]
        [SerializeField] private uint m_steamDemoAppId;

        [Header("ID mapping")]
        [SerializeField] private List<AchievementEntry> m_achievements = new();
        [SerializeField] private List<StatEntry> m_stats = new();
        [SerializeField] private List<DlcEntry> m_dlc = new();

        public uint SteamFullAppId => m_steamFullAppId;
        public uint SteamDemoAppId => m_steamDemoAppId;
        public uint SteamAppIdForBuild => PlatformBuild.IsDemo ? m_steamDemoAppId : m_steamFullAppId;

        public static PlatformSettings Load()
        {
            PlatformSettings settings = Resources.Load<PlatformSettings>(ResourcesPath);
            if (settings != null)
                return settings;

            Debug.LogWarning($"[Platform] No PlatformSettings asset at Resources/{ResourcesPath}; store backends will run without App IDs or ID mappings.");
            return CreateInstance<PlatformSettings>();
        }

        public bool TryGetSteamAchievementName(AchievementId id, out string apiName)
        {
            for (int i = 0; i < m_achievements.Count; i++)
            {
                if (m_achievements[i].Id == id && !string.IsNullOrEmpty(m_achievements[i].SteamApiName))
                {
                    apiName = m_achievements[i].SteamApiName;
                    return true;
                }
            }

            apiName = null;
            return false;
        }

        public bool TryGetSteamStatName(StatId id, out string apiName)
        {
            for (int i = 0; i < m_stats.Count; i++)
            {
                if (m_stats[i].Id == id && !string.IsNullOrEmpty(m_stats[i].SteamApiName))
                {
                    apiName = m_stats[i].SteamApiName;
                    return true;
                }
            }

            apiName = null;
            return false;
        }

        public bool TryGetSteamDlcAppId(DlcId id, out uint appId)
        {
            for (int i = 0; i < m_dlc.Count; i++)
            {
                if (m_dlc[i].Id == id && m_dlc[i].SteamAppId != 0)
                {
                    appId = m_dlc[i].SteamAppId;
                    return true;
                }
            }

            appId = 0;
            return false;
        }
    }
}
