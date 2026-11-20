using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace Mejnun.Platform.Editor
{
    [CreateAssetMenu(fileName = "SteamUploadConfig", menuName = "Mejnun/Steam Upload Config")]
    public sealed class SteamUploadConfig : ScriptableObject
    {
        public const string AssetPath = "Assets/_Project/Settings/Platform/SteamUploadConfig.asset";
        private const uint DefaultFullAppId = 0;
        private const uint DefaultWindowsDepotId = 0;
        private const uint DefaultMacDepotId = 0;

        [SerializeField] private List<SteamUploadApp> m_apps = new();

        public IReadOnlyList<SteamUploadApp> Apps => m_apps;

        internal void InitializeDefaults()
        {
            m_apps = new List<SteamUploadApp>
            {
                new(
                    "Full",
                    DefaultFullAppId,
                    false,
                    "beta",
                    new List<SteamUploadDepot>
                    {
                        new(
                            DefaultWindowsDepotId,
                            new List<SteamUploadPlayerBuild>
                            {
                                new(true, null, BuildTarget.StandaloneWindows64, "windows"),
                            }),
                        new(
                            DefaultMacDepotId,
                            new List<SteamUploadPlayerBuild>
                            {
                                new(true, null, BuildTarget.StandaloneOSX, "macos"),
                            }),
                    })
            };
        }
    }

    [Serializable]
    public sealed class SteamUploadApp
    {
        [SerializeField] private string m_name;
        [SerializeField] private uint m_appId;
        [SerializeField] private bool m_isDemo;
        [Tooltip("Leave empty to upload without setting a branch live. Set a non-public beta branch to make it live after upload.")]
        [SerializeField] private string m_branch;
        [SerializeField] private List<SteamUploadDepot> m_depots = new();

        public string Name => m_name;
        public uint AppId => m_appId;
        public bool IsDemo => m_isDemo;
        public string Branch => m_branch;
        public IReadOnlyList<SteamUploadDepot> Depots => m_depots;

        internal SteamUploadApp(string name, uint appId, bool isDemo, string branch,
            List<SteamUploadDepot> depots)
        {
            m_name = name;
            m_appId = appId;
            m_isDemo = isDemo;
            m_branch = branch;
            m_depots = depots;
        }
    }

    [Serializable]
    public sealed class SteamUploadDepot
    {
        [SerializeField] private uint m_depotId;
        [SerializeField] private List<SteamUploadPlayerBuild> m_builds = new();

        public uint DepotId => m_depotId;
        public IReadOnlyList<SteamUploadPlayerBuild> Builds => m_builds;

        internal SteamUploadDepot(uint depotId, List<SteamUploadPlayerBuild> builds)
        {
            m_depotId = depotId;
            m_builds = builds;
        }
    }

    [Serializable]
    public sealed class SteamUploadPlayerBuild
    {
        [SerializeField] private bool m_enabled;
        [SerializeField] private BuildProfile m_buildProfile;
        [SerializeField] private BuildTarget m_target;
        [SerializeField] private string m_subfolder;

        public bool Enabled => m_enabled;
        public BuildProfile Profile => m_buildProfile;
        public BuildTarget Target => m_target;
        public string Subfolder => m_subfolder;

        internal SteamUploadPlayerBuild(bool enabled, BuildProfile profile, BuildTarget target, string subfolder)
        {
            m_enabled = enabled;
            m_buildProfile = profile;
            m_target = target;
            m_subfolder = subfolder;
        }
    }
}