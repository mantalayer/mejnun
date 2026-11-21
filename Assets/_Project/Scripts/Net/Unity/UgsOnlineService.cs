using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Mejnun.Net;

namespace Mejnun.Net.Unity
{
    public sealed class UgsOnlineService
    {
        private const string SessionType = "mejnun.online";
        private const string LastSessionIdKey = "online.lastSessionId";
        private const string LastSessionCodeKey = "online.lastSessionCode";
        private const string UnfinishedMatchKey = "online.unfinishedMatch";
        private const double SessionCleanupTimeoutSeconds = 5;
        private static readonly TimeSpan MigrationUploadInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan MigrationTimeout = TimeSpan.FromSeconds(10);
        private const double NetworkRecoveryTimeoutSeconds = 15;

        public ISession Session { get; private set; }
        public OnlineSessionMigrationDataHandler MigrationHandler { get; } = new OnlineSessionMigrationDataHandler();
        public int SessionSeatCount => Session == null ? 2 : Session.MaxPlayers;
        public bool IsHost => Session != null && Session.IsHost;
        public string SessionCode => Session == null ? string.Empty : Session.Code;
        public string SessionId => Session == null ? string.Empty : Session.Id;
        public static string LastSessionId => PlayerPrefs.GetString(LastSessionIdKey, string.Empty);
        public static string LastSessionCode => PlayerPrefs.GetString(LastSessionCodeKey, string.Empty);
        public static bool HasUnfinishedMatch => PlayerPrefs.GetInt(UnfinishedMatchKey, 0) != 0;
        public string AuthenticationId => AuthenticationService.Instance.PlayerId;
        public string PlayerName => AuthenticationService.Instance.PlayerName;

        public static void MarkUnfinishedMatch()
        {
            if (string.IsNullOrWhiteSpace(LastSessionCode))
            {
                return;
            }

            PlayerPrefs.SetInt(UnfinishedMatchKey, 1);
            PlayerPrefs.Save();
        }

        public static void ClearUnfinishedMatch()
        {
            PlayerPrefs.DeleteKey(UnfinishedMatchKey);
            PlayerPrefs.Save();
        }

        public async Task InitializeAsync(string nickname)
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            var authentication = AuthenticationService.Instance;
            if (!authentication.IsSignedIn)
            {
                await authentication.SignInAnonymouslyAsync();
            }

            if (!string.IsNullOrWhiteSpace(nickname))
            {
                await authentication.UpdatePlayerNameAsync(nickname.Trim());
            }
        }

        public async Task<IHostSession> CreateSessionAsync(int maxPlayers, string sessionName = "Mejnun",
            Dictionary<string, PlayerProperty> playerProperties = null)
        {
            EnsureNetworkManager();
            var options = CreateSessionOptions(maxPlayers, sessionName, playerProperties);
            var session = await MultiplayerService.Instance.CreateSessionAsync(options);
            Session = session;
            SaveLastSession(session);
            return session;
        }

        public async Task<ISession> JoinByCodeAsync(string sessionCode,
            Dictionary<string, PlayerProperty> playerProperties = null)
        {
            if (string.IsNullOrWhiteSpace(sessionCode))
            {
                throw new ArgumentException("A session code is required.", nameof(sessionCode));
            }

            EnsureNetworkManager();
            Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(sessionCode.Trim(),
                new JoinSessionOptions
                {
                    Type = SessionType,
                    PlayerProperties = playerProperties ?? new Dictionary<string, PlayerProperty>()
                }.WithPlayerName().WithHostMigration(MigrationHandler, MigrationUploadInterval, MigrationTimeout));
            SaveLastSession(Session);
            return Session;
        }

        public async Task<ISession> RejoinByCodeAsync(string sessionCode,
            Dictionary<string, PlayerProperty> playerProperties = null)
        {
            if (string.IsNullOrWhiteSpace(sessionCode))
            {
                throw new ArgumentException("A session code is required.", nameof(sessionCode));
            }

            await LeaveRegisteredSessionsAsync();
            return await JoinByCodeAsync(sessionCode, playerProperties);
        }

        public async Task<ISession> ReconnectAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("A session id is required.", nameof(sessionId));
            }

            EnsureNetworkManager();
            Session = await MultiplayerService.Instance.ReconnectToSessionAsync(sessionId.Trim(),
                new ReconnectSessionOptions { Type = SessionType });
            SaveLastSession(Session);
            return Session;
        }

        public async Task UploadMigrationRecordAsync(byte[] data)
        {
            var session = Session;
            if (session == null || !session.IsHost || data == null || data.Length == 0)
            {
                return;
            }

            await session.AsHost().SetHostMigrationDataAsync(data, MigrationTimeout);
        }

        public async Task<SessionRecord> DownloadMigrationRecordAsync()
        {
            var session = Session;
            if (session == null || !session.IsHost)
            {
                return null;
            }

            var migrationData = await session.AsHost().GetHostMigrationDataAsync(MigrationTimeout);
            return migrationData != null && SessionRecordCodec.TryRead(migrationData.Data, out var record)
                ? record
                : null;
        }

        public async Task EnsureHostRelayNetworkAsync()
        {
            var session = Session;
            if (session == null || !session.IsHost)
            {
                throw new InvalidOperationException("Only the session host can restart its relay network.");
            }

            var hostSession = session.AsHost();
            var deadline = Time.realtimeSinceStartupAsDouble + NetworkRecoveryTimeoutSeconds;
            while (hostSession.Network.State == NetworkState.Starting ||
                   hostSession.Network.State == NetworkState.Stopping ||
                   hostSession.Network.State == NetworkState.Migrating)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                {
                    throw new TimeoutException("The session relay network did not finish its pending transition.");
                }

                await Task.Delay(100);
            }

            if (hostSession.Network.State == NetworkState.Started)
            {
                await hostSession.Network.StopNetworkAsync();
                while (hostSession.Network.State != NetworkState.Stopped)
                {
                    if (Time.realtimeSinceStartupAsDouble >= deadline)
                    {
                        throw new TimeoutException("The previous session relay network did not stop in time.");
                    }

                    await Task.Delay(100);
                }
            }

            if (hostSession.Network.State != NetworkState.Stopped)
            {
                throw new InvalidOperationException("The session relay network is not ready to restart.");
            }

            await hostSession.Network.StartRelayNetworkAsync(RelayNetworkOptions.Default);
        }

        public async Task<string> FindRejoinSessionIdAsync(string sessionCode)
        {
            var code = sessionCode == null ? string.Empty : sessionCode.Trim();
            if (string.Equals(code, LastSessionCode, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(LastSessionId))
            {
                return LastSessionId;
            }

            var joinedIds = await MultiplayerService.Instance.GetJoinedSessionIdsAsync();
            if (Session != null && joinedIds.Contains(Session.Id) &&
                string.Equals(code, Session.Code, StringComparison.OrdinalIgnoreCase))
            {
                return Session.Id;
            }

            foreach (var activeSession in MultiplayerService.Instance.Sessions.Values)
            {
                if (activeSession != null && joinedIds.Contains(activeSession.Id) &&
                    string.Equals(code, activeSession.Code, StringComparison.OrdinalIgnoreCase))
                {
                    return activeSession.Id;
                }
            }

            return string.Empty;
        }

        public async Task RemovePlayerAsync(string playerId)
        {
            if (Session == null || !Session.IsHost)
            {
                throw new InvalidOperationException("Only the session host can remove a player.");
            }

            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("A player id is required.", nameof(playerId));
            }

            await Session.AsHost().RemovePlayerAsync(playerId);
        }

        public async Task<ISession> QuickMatchAsync(int maxPlayers, string sessionName = "Mejnun",
            Dictionary<string, PlayerProperty> playerProperties = null)
        {
            EnsureNetworkManager();
            var options = CreateSessionOptions(maxPlayers, sessionName, playerProperties);
            Session = await MultiplayerService.Instance.MatchmakeSessionAsync(
                new QuickJoinOptions
                {
                    CreateSession = true,
                    Filters = new List<FilterOption>
                    {
                        new FilterOption(FilterField.MaxPlayers, maxPlayers.ToString(CultureInfo.InvariantCulture),
                            FilterOperation.Equal)
                    }
                }, options);
            SaveLastSession(Session);
            return Session;
        }

        public async Task LeaveAsync()
        {
            var session = Session;
            Session = null;
            if (session != null)
            {
                await session.LeaveAsync();
            }
        }

        private async Task LeaveRegisteredSessionsAsync()
        {
            var currentSession = Session;
            var sessionsToLeave = new List<ISession>();
            foreach (var activeSession in MultiplayerService.Instance.Sessions.Values)
            {
                if (activeSession != null && activeSession.Type == SessionType)
                {
                    sessionsToLeave.Add(activeSession);
                }
            }

            if (currentSession != null && currentSession.Type == SessionType &&
                !sessionsToLeave.Contains(currentSession))
            {
                sessionsToLeave.Add(currentSession);
            }

            Session = null;
            for (var index = 0; index < sessionsToLeave.Count; index++)
            {
                try
                {
                    await sessionsToLeave[index].LeaveAsync();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Leaving the previous online session before rejoining failed: " +
                                     exception.Message);
                }
            }

            var deadline = Time.realtimeSinceStartupAsDouble + SessionCleanupTimeoutSeconds;
            while (HasRegisteredSession() && Time.realtimeSinceStartupAsDouble < deadline)
            {
                await Task.Delay(100);
            }
        }

        private static bool HasRegisteredSession()
        {
            foreach (var activeSession in MultiplayerService.Instance.Sessions.Values)
            {
                if (activeSession != null && activeSession.Type == SessionType)
                {
                    return true;
                }
            }

            return false;
        }

        public async Task DeleteSessionAsync()
        {
            var session = Session;
            if (session == null)
            {
                return;
            }

            try
            {
                if (session.IsHost)
                {
                    await session.AsHost().DeleteAsync();
                }
                else
                {
                    await session.LeaveAsync();
                }
            }
            catch when (session.IsHost)
            {
                await session.LeaveAsync();
            }
            finally
            {
                if (Session == session)
                {
                    Session = null;
                }
            }
        }

        private SessionOptions CreateSessionOptions(int maxPlayers, string sessionName,
            Dictionary<string, PlayerProperty> playerProperties)
        {
            if (maxPlayers < 2 || maxPlayers > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPlayers), "Mejnun sessions support 2–6 players.");
            }

            return new SessionOptions
            {
                Type = SessionType,
                Name = string.IsNullOrWhiteSpace(sessionName) ? "Mejnun" : sessionName.Trim(),
                MaxPlayers = maxPlayers,
                IsPrivate = false,
                PlayerProperties = playerProperties ?? new Dictionary<string, PlayerProperty>()
            }.WithRelayNetwork().WithPlayerName().WithHostMigration(MigrationHandler, MigrationUploadInterval,
                MigrationTimeout);
        }

        private static void SaveLastSession(ISession session)
        {
            if (session == null)
            {
                return;
            }

            PlayerPrefs.SetString(LastSessionIdKey, session.Id ?? string.Empty);
            PlayerPrefs.SetString(LastSessionCodeKey, session.Code ?? string.Empty);
            PlayerPrefs.Save();
        }

        public static NetworkManager EnsureNetworkManager()
        {
            var manager = NetworkManager.Singleton;
            if (manager != null)
            {
                return manager;
            }

            var networkObject = new GameObject("Mejnun Network Manager");
            var transport = networkObject.AddComponent<UnityTransport>();
            manager = networkObject.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                PlayerPrefab = null
            };
            UnityEngine.Object.DontDestroyOnLoad(networkObject);
            return manager;
        }
    }
}