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

namespace Mejnun.Net.Unity
{
    public sealed class UgsOnlineService
    {
        public ISession Session { get; private set; }
        public OnlineSessionMigrationDataHandler MigrationHandler { get; } = new OnlineSessionMigrationDataHandler();
        public int SessionSeatCount => Session == null ? 2 : Session.MaxPlayers;
        public bool IsHost => Session != null && Session.IsHost;
        public string SessionCode => Session == null ? string.Empty : Session.Code;
        public string AuthenticationId => AuthenticationService.Instance.PlayerId;
        public string PlayerName => AuthenticationService.Instance.PlayerName;

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

        public async Task<IHostSession> CreateSessionAsync(int maxPlayers, string sessionName = "Mejnun")
        {
            EnsureNetworkManager();
            var options = CreateSessionOptions(maxPlayers, sessionName);
            var session = await MultiplayerService.Instance.CreateSessionAsync(options);
            Session = session;
            return session;
        }

        public async Task<ISession> JoinByCodeAsync(string sessionCode)
        {
            if (string.IsNullOrWhiteSpace(sessionCode))
            {
                throw new ArgumentException("A session code is required.", nameof(sessionCode));
            }

            EnsureNetworkManager();
            Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(sessionCode.Trim(),
                new JoinSessionOptions().WithPlayerName().WithHostMigration(MigrationHandler));
            return Session;
        }

        public async Task<ISession> QuickMatchAsync(int maxPlayers, string sessionName = "Mejnun")
        {
            EnsureNetworkManager();
            var options = CreateSessionOptions(maxPlayers, sessionName);
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

        private SessionOptions CreateSessionOptions(int maxPlayers, string sessionName)
        {
            if (maxPlayers < 2 || maxPlayers > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPlayers), "Mejnun sessions support 2–6 players.");
            }

            return new SessionOptions
            {
                Name = string.IsNullOrWhiteSpace(sessionName) ? "Mejnun" : sessionName.Trim(),
                MaxPlayers = maxPlayers,
                IsPrivate = false
            }.WithRelayNetwork().WithPlayerName().WithHostMigration(MigrationHandler);
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