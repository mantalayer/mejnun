using System;
using Steamworks;
using UnityEngine;

namespace Mejnun.Platform.Steam
{
    // Steamworks.NET backend. Compiled only into MEJNUN_STEAM builds; nothing
    // outside this assembly references Steamworks types.
    public sealed class SteamPlatformServices : IPlatformServices, IControllerInfo, IAchievements, IStats, IRichPresence, IEntitlements
    {
        private const string BackendName = "Steam";
        private const int BackendPriority = 100;

        // Steam rate-limits StoreStats, so stat writes are batched and
        // uploaded at most this often. Achievement unlocks store immediately.
        private const float StatsStoreIntervalSeconds = 5f;

        // Mirrors native SteamAPI state, which belongs to the process rather
        // than to a Play session, so it is deliberately not reset on Play
        // entry. Init and Shutdown are always paired through this flag.
        private static bool s_steamApiInitialized;
        // Steam Input shares the process lifetime of SteamAPI and is initialized
        // separately so it remains paired across Play sessions without domain reload.
        private static bool s_steamInputInitialized;

        private const float ControllerInfoRefreshIntervalSeconds = 0.5f;

        private readonly PlatformSettings m_settings;
        private readonly bool m_isSteamDeck;
        private bool m_isShutdown;
        private bool m_statsDirty;
        private float m_nextStatsStoreTime;
        private float m_nextControllerInfoRefreshTime;
        private PlatformControllerKind m_activeControllerKind;

        private SteamPlatformServices(PlatformSettings settings)
        {
            m_settings = settings;
            m_isSteamDeck = SteamUtils.IsRunningOnSteamHardware() == ESteamHardwareType.k_ESteamHardwareTypeSteamDeck
                            || string.Equals(Environment.GetEnvironmentVariable("SteamDeck"), "1", StringComparison.Ordinal);
            m_activeControllerKind = m_isSteamDeck ? PlatformControllerKind.SteamDeck : PlatformControllerKind.Unknown;
        }

        public string PlatformName => BackendName;
        public bool IsInitialized => !m_isShutdown && s_steamApiInitialized;
        public IControllerInfo ControllerInfo => this;
        public ISaveStorage SaveStorage => JsonFileSaveStorage.Instance;
        public IAchievements Achievements => this;
        public IStats Stats => this;
        public IRichPresence RichPresence => this;
        public IEntitlements Entitlements => this;

        public bool IsDemo => PlatformBuild.IsDemo;
        public bool IsSteamDeck => m_isSteamDeck;
        public PlatformControllerKind ActiveControllerKind =>
            m_isSteamDeck ? PlatformControllerKind.SteamDeck : m_activeControllerKind;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            PlatformServices.RegisterBackend(BackendName, BackendPriority, TryCreate);
        }

        private static bool TryCreate(PlatformSettings settings, out IPlatformServices services)
        {
            services = null;

            uint appId = settings.SteamAppIdForBuild;
            if (appId == 0)
            {
                Debug.LogWarning($"[Platform:Steam] No Steam App ID is set in PlatformSettings for this build (demo: {PlatformBuild.IsDemo}).");
                return false;
            }

            if (!Packsize.Test())
            {
                Debug.LogError("[Platform:Steam] Steamworks.NET packsize check failed; the wrong Steamworks.NET binaries are in use for this platform.");
                return false;
            }

            if (!DllCheck.Test())
            {
                Debug.LogError("[Platform:Steam] steam_api native library check failed; one or more Steam binaries are the wrong version.");
                return false;
            }

            if (!s_steamApiInitialized)
            {
                try
                {
                    // Outside the Editor, a build launched directly (not through
                    // Steam) is relaunched by the Steam client. steam_appid.txt
                    // next to the executable suppresses this for local testing.
                    if (!Application.isEditor && SteamAPI.RestartAppIfNecessary(new AppId_t(appId)))
                    {
                        Debug.Log("[Platform:Steam] Relaunching through the Steam client.");
                        Application.Quit();
                        return false;
                    }
                }
                catch (DllNotFoundException exception)
                {
                    Debug.LogError("[Platform:Steam] steam_api native library could not be loaded.");
                    Debug.LogException(exception);
                    return false;
                }

                ESteamAPIInitResult result = SteamAPI.InitEx(out string errorMessage);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    Debug.LogWarning($"[Platform:Steam] SteamAPI.InitEx failed ({result}): {errorMessage}. Is the Steam client running and logged in to an account that owns App {appId}?");
                    return false;
                }

                s_steamApiInitialized = true;
            }

            if (!s_steamInputInitialized)
            {
                s_steamInputInitialized = SteamInput.Init(false);
                if (!s_steamInputInitialized)
                    Debug.LogWarning("[Platform:Steam] Steam Input initialization failed; controller model detection is unavailable.");
            }

            uint runningAppId = SteamUtils.GetAppID().m_AppId;
            if (runningAppId != appId)
                Debug.LogWarning($"[Platform:Steam] Steam reports App {runningAppId}, but this build expects App {appId}. Check steam_appid.txt.");

            services = new SteamPlatformServices(settings);
            return true;
        }

        public void Tick()
        {
            if (!IsInitialized)
                return;

            SteamAPI.RunCallbacks();

            if (s_steamInputInitialized && Time.unscaledTime >= m_nextControllerInfoRefreshTime)
            {
                m_activeControllerKind = GetActiveControllerKind();
                m_nextControllerInfoRefreshTime = Time.unscaledTime + ControllerInfoRefreshIntervalSeconds;
            }

            if (m_statsDirty && Time.unscaledTime >= m_nextStatsStoreTime)
                StoreStats();
        }

        public void Shutdown()
        {
            if (m_isShutdown)
                return;

            if (IsInitialized && m_statsDirty)
                SteamUserStats.StoreStats();

            m_isShutdown = true;
            m_statsDirty = false;

            if (s_steamInputInitialized)
            {
                SteamInput.Shutdown();
                s_steamInputInitialized = false;
            }

            if (s_steamApiInitialized)
            {
                SteamAPI.Shutdown();
                s_steamApiInitialized = false;
            }
        }

        public void Unlock(AchievementId id)
        {
            if (!IsInitialized || !TryGetAchievementName(id, out string apiName))
                return;

            if (SteamUserStats.GetAchievement(apiName, out bool achieved) && achieved)
                return;

            if (SteamUserStats.SetAchievement(apiName))
                SteamUserStats.StoreStats();
        }

        public void SetProgress(AchievementId id, int current, int max)
        {
            if (current >= max)
            {
                Unlock(id);
                return;
            }

            if (!IsInitialized || current <= 0 || !TryGetAchievementName(id, out string apiName))
                return;

            SteamUserStats.IndicateAchievementProgress(apiName, (uint)current, (uint)max);
        }

        public bool IsUnlocked(AchievementId id)
        {
            return IsInitialized
                   && TryGetAchievementName(id, out string apiName)
                   && SteamUserStats.GetAchievement(apiName, out bool achieved)
                   && achieved;
        }

        public int GetInt(StatId id)
        {
            if (!IsInitialized || !TryGetStatName(id, out string apiName))
                return 0;

            return SteamUserStats.GetStat(apiName, out int value) ? value : 0;
        }

        public void SetInt(StatId id, int value)
        {
            if (!IsInitialized || !TryGetStatName(id, out string apiName))
                return;

            if (SteamUserStats.SetStat(apiName, value))
                m_statsDirty = true;
        }

        public void IncrementInt(StatId id, int amount)
        {
            SetInt(id, GetInt(id) + amount);
        }

        public void Set(string key, string value)
        {
            if (IsInitialized)
                SteamFriends.SetRichPresence(key, value);
        }

        public void Clear()
        {
            if (IsInitialized)
                SteamFriends.ClearRichPresence();
        }

        public bool OwnsDlc(DlcId id)
        {
            return IsInitialized
                   && m_settings.TryGetSteamDlcAppId(id, out uint dlcAppId)
                   && SteamApps.BIsDlcInstalled(new AppId_t(dlcAppId));
        }

        public bool Owns(EntitlementId id)
        {
            return false;
        }

        private static PlatformControllerKind GetActiveControllerKind()
        {
            for (int gamepadIndex = 0; gamepadIndex < 4; gamepadIndex++)
            {
                InputHandle_t inputHandle = SteamInput.GetControllerForGamepadIndex(gamepadIndex);
                if (inputHandle.m_InputHandle == 0)
                    continue;

                PlatformControllerKind controllerKind = MapControllerKind(SteamInput.GetInputTypeForHandle(inputHandle));
                if (controllerKind != PlatformControllerKind.Unknown)
                    return controllerKind;
            }

            return PlatformControllerKind.Unknown;
        }

        private static PlatformControllerKind MapControllerKind(ESteamInputType inputType)
        {
            switch (inputType)
            {
                case ESteamInputType.k_ESteamInputType_XBox360Controller:
                case ESteamInputType.k_ESteamInputType_XBoxOneController:
                    return PlatformControllerKind.Xbox;
                case ESteamInputType.k_ESteamInputType_PS3Controller:
                case ESteamInputType.k_ESteamInputType_PS4Controller:
                    return PlatformControllerKind.PlayStation4;
                case ESteamInputType.k_ESteamInputType_PS5Controller:
                    return PlatformControllerKind.PlayStation5;
                case ESteamInputType.k_ESteamInputType_SwitchJoyConPair:
                case ESteamInputType.k_ESteamInputType_SwitchJoyConSingle:
                case ESteamInputType.k_ESteamInputType_SwitchProController:
                case ESteamInputType.k_ESteamInputType_Switch2ProController:
                    return PlatformControllerKind.NintendoSwitch;
                case ESteamInputType.k_ESteamInputType_SteamDeckController:
                case ESteamInputType.k_ESteamInputType_SteamOSHandheld:
                    return PlatformControllerKind.SteamDeck;
                case ESteamInputType.k_ESteamInputType_SteamController:
                case ESteamInputType.k_ESteamInputType_SteamController2026:
                    return PlatformControllerKind.SteamController;
                case ESteamInputType.k_ESteamInputType_GenericGamepad:
                    return PlatformControllerKind.Generic;
                default:
                    return PlatformControllerKind.Unknown;
            }
        }

        private void StoreStats()
        {
            SteamUserStats.StoreStats();
            m_statsDirty = false;
            m_nextStatsStoreTime = Time.unscaledTime + StatsStoreIntervalSeconds;
        }

        private bool TryGetAchievementName(AchievementId id, out string apiName)
        {
            if (m_settings.TryGetSteamAchievementName(id, out apiName))
                return true;

            if (id != AchievementId.None)
                Debug.LogWarning($"[Platform:Steam] Achievement {id} has no Steam API name in PlatformSettings.");
            return false;
        }

        private bool TryGetStatName(StatId id, out string apiName)
        {
            if (m_settings.TryGetSteamStatName(id, out apiName))
                return true;

            if (id != StatId.None)
                Debug.LogWarning($"[Platform:Steam] Stat {id} has no Steam API name in PlatformSettings.");
            return false;
        }
    }
}
