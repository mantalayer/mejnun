using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Mejnun.Net.Unity
{
    public sealed class NgoNetTransport : MonoBehaviour
    {
        private const string MessageName = "mejnun.session.v2";
        private const int MaximumPayloadBytes = 1024 * 1024 + 65536;
        private const double MembershipRefreshIntervalSeconds = 2;
        private const int MaximumJoinRetries = 6;
        private const double JoinRetryDelaySeconds = 2;

        [SerializeField] private NetworkManager networkManager;

        private readonly Dictionary<ulong, string> authenticationByClient = new Dictionary<ulong, string>();
        private readonly Dictionary<string, ulong> clientByAuthentication = new Dictionary<string, ulong>();
        private HostSession host;
        private ClientSession client;
        private ISession session;
        private string localAuthenticationId;
        private string localDisplayName;
        private bool isHost;
        private bool handlerRegistered;
        private CustomMessagingManager registeredMessagingManager;
        private bool joinSent;
        private bool membershipRefreshInProgress;
        private int joinRetryCount;
        private double nextMembershipRefreshAt;
        private double nextJoinAttemptAt;
        private readonly PendingJoinGate pendingJoinGate = new PendingJoinGate();

        public event Action<string> PlayerDisconnected;
        public event Action HostDisconnected;
        public event Action<Exception> TransportError;
        public bool IsHost => isHost;

        public bool HasActivePlayer(string authenticationId)
        {
            if (string.IsNullOrWhiteSpace(authenticationId))
            {
                return false;
            }

            return (clientByAuthentication.TryGetValue(authenticationId, out var clientId) &&
                    networkManager != null && networkManager.ConnectedClients.ContainsKey(clientId)) ||
                   pendingJoinGate.ContainsAuthentication(authenticationId);
        }

        public void SendAway()
        {
            if (isHost)
            {
                host?.SetPauseReason(PauseReason.HostAway);
                return;
            }

            SendToServer(new AwayMessage());
        }

        public void RejoinClientSeat()
        {
            if (isHost || client == null)
            {
                return;
            }

            joinSent = false;
            joinRetryCount = 0;
            nextJoinAttemptAt = 0;
            SendJoinIfConnected();
        }

        public void PrepareForHostMigration()
        {
            joinSent = false;
            authenticationByClient.Clear();
            clientByAuthentication.Clear();
        }

        public void ConfigureHost(NetworkManager manager, ISession ugsSession, HostSession hostSession,
            string authenticationId, string displayName)
        {
            if (client != null)
            {
                client.CommandsReady -= SendToServer;
                client.RematchRequested -= SendToServer;
                client = null;
            }

            if (host != null)
            {
                host.MessageSent -= SendToPlayer;
            }

            Configure(manager, ugsSession, authenticationId, displayName);
            isHost = true;
            host = hostSession ?? throw new ArgumentNullException(nameof(hostSession));
            host.MessageSent += SendToPlayer;
            host.Join(localAuthenticationId, localDisplayName);
            EnsureRegistered();
        }

        public void ConfigureClient(NetworkManager manager, ISession ugsSession, ClientSession clientSession,
            string authenticationId, string displayName)
        {
            if (host != null)
            {
                host.MessageSent -= SendToPlayer;
                host = null;
            }

            if (client != null)
            {
                client.CommandsReady -= SendToServer;
                client.RematchRequested -= SendToServer;
            }

            Configure(manager, ugsSession, authenticationId, displayName);
            isHost = false;
            client = clientSession ?? throw new ArgumentNullException(nameof(clientSession));
            client.CommandsReady += SendToServer;
            client.RematchRequested += SendToServer;
            EnsureRegistered();
            SendJoinIfConnected();
        }

        private void Update()
        {
            EnsureRegistered();
            SendJoinIfConnected();
            ProcessPendingJoins();
        }

        private void OnDestroy()
        {
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback -= OnClientConnected;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            UnregisterMessageHandler();

            if (host != null)
            {
                host.MessageSent -= SendToPlayer;
            }

            if (client != null)
            {
                client.CommandsReady -= SendToServer;
                client.RematchRequested -= SendToServer;
            }
        }

        private void Configure(NetworkManager manager, ISession ugsSession, string authenticationId,
            string displayName)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            if (ugsSession == null)
            {
                throw new ArgumentNullException(nameof(ugsSession));
            }

            if (networkManager != null && !ReferenceEquals(networkManager, manager))
            {
                networkManager.OnClientConnectedCallback -= OnClientConnected;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
                UnregisterMessageHandler();
            }

            networkManager = manager;
            session = ugsSession;
            localAuthenticationId = authenticationId ?? string.Empty;
            localDisplayName = displayName ?? string.Empty;
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void EnsureRegistered()
        {
            var messagingManager = networkManager == null ? null : networkManager.CustomMessagingManager;
            if (messagingManager == null ||
                handlerRegistered && ReferenceEquals(messagingManager, registeredMessagingManager))
            {
                return;
            }

            var isNewNetworkSession = !ReferenceEquals(messagingManager, registeredMessagingManager);
            UnregisterMessageHandler();
            registeredMessagingManager = messagingManager;
            if (isNewNetworkSession)
            {
                ResetNetworkSessionState();
            }

            registeredMessagingManager.RegisterNamedMessageHandler(MessageName, ReceiveMessage);
            handlerRegistered = true;
        }

        private void UnregisterMessageHandler()
        {
            if (handlerRegistered && registeredMessagingManager != null)
            {
                registeredMessagingManager.UnregisterNamedMessageHandler(MessageName);
            }

            handlerRegistered = false;
            registeredMessagingManager = null;
        }

        private void ResetNetworkSessionState()
        {
            joinSent = false;
            joinRetryCount = 0;
            nextJoinAttemptAt = 0;
            nextMembershipRefreshAt = 0;
            membershipRefreshInProgress = false;
            authenticationByClient.Clear();
            clientByAuthentication.Clear();
            pendingJoinGate.Clear();
        }

        private void OnClientConnected(ulong clientId)
        {
            EnsureRegistered();
            if (!isHost && networkManager != null && clientId == networkManager.LocalClientId)
            {
                joinSent = false;
                joinRetryCount = 0;
                nextJoinAttemptAt = 0;
            }

            SendJoinIfConnected();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (isHost && authenticationByClient.TryGetValue(clientId, out var authenticationId))
            {
                authenticationByClient.Remove(clientId);
                if (clientByAuthentication.TryGetValue(authenticationId, out var currentClientId) &&
                    currentClientId == clientId)
                {
                    clientByAuthentication.Remove(authenticationId);
                    PlayerDisconnected?.Invoke(authenticationId);
                }
            }

            if (isHost)
            {
                pendingJoinGate.Remove(clientId);
            }

            if (!isHost && networkManager != null && clientId == networkManager.LocalClientId)
            {
                joinSent = false;
                joinRetryCount = 0;
                nextJoinAttemptAt = 0;
                HostDisconnected?.Invoke();
            }
        }

        private void SendJoinIfConnected()
        {
            if (isHost || joinSent || client == null || networkManager == null || !networkManager.IsClient ||
                networkManager.IsHost || !networkManager.IsConnectedClient || !handlerRegistered ||
                Time.realtimeSinceStartupAsDouble < nextJoinAttemptAt)
            {
                return;
            }

            SendToServer(new JoinMessage(localAuthenticationId, localDisplayName));
            joinSent = true;
        }

        private void SendToServer(CommandsMessage message)
        {
            SendToServer((NetMessage)message);
        }

        private void SendToServer(RematchMessage message)
        {
            SendToServer((NetMessage)message);
        }

        private void SendToServer(NetMessage message)
        {
            if (networkManager == null || !networkManager.IsClient || networkManager.IsHost)
            {
                return;
            }

            Send(NetworkManager.ServerClientId, message);
        }

        private void SendToPlayer(string authenticationId, NetMessage message)
        {
            if (!clientByAuthentication.TryGetValue(authenticationId, out var clientId))
            {
                return;
            }

            Send(clientId, message);
        }

        private void Send(ulong clientId, NetMessage message)
        {
            try
            {
                EnsureRegistered();
                var bytes = NetCodec.Encode(message);
                if (bytes.Length > MaximumPayloadBytes)
                {
                    throw new InvalidOperationException("Network message is too large.");
                }

                using (var writer = new FastBufferWriter(bytes.Length + sizeof(int), Allocator.Temp,
                           bytes.Length + sizeof(int)))
                {
                    writer.WriteValueSafe(bytes.Length);
                    unsafe
                    {
                        fixed (byte* pointer = bytes)
                        {
                            writer.WriteBytesSafe(pointer, bytes.Length);
                        }
                    }

                    registeredMessagingManager.SendNamedMessage(MessageName, clientId, writer,
                        NetworkDelivery.ReliableFragmentedSequenced);
                }
            }
            catch (Exception exception)
            {
                TransportError?.Invoke(exception);
            }
        }

        private void ReceiveMessage(ulong senderClientId, FastBufferReader reader)
        {
            try
            {
                reader.ReadValueSafe(out int length);
                if (length < 0 || length > MaximumPayloadBytes)
                {
                    throw new InvalidOperationException("Invalid network message length.");
                }

                var bytes = new byte[length];
                unsafe
                {
                    fixed (byte* pointer = bytes)
                    {
                        reader.ReadBytesSafe(pointer, length);
                    }
                }

                var message = NetCodec.Decode(bytes);
                if (isHost)
                {
                    ReceiveHostMessage(senderClientId, message);
                }
                else
                {
                    ReceiveClientMessage(message);
                }
            }
            catch (Exception exception)
            {
                TransportError?.Invoke(exception);
            }
        }

        private void ReceiveHostMessage(ulong senderClientId, NetMessage message)
        {
            if (message is AwayMessage)
            {
                if (authenticationByClient.TryGetValue(senderClientId, out var awayAuthenticationId))
                {
                    host.MarkAway(awayAuthenticationId);
                }
                else
                {
                    Send(senderClientId, new RejectedMessage(JoinRejectReason.MissingAuthentication,
                        "The sender has not authenticated."));
                }

                return;
            }

            if (message is JoinMessage join)
            {
                if (string.IsNullOrWhiteSpace(join.AuthenticationId))
                {
                    Send(senderClientId, new RejectedMessage(JoinRejectReason.MissingAuthentication,
                        "The sender did not provide an authentication id."));
                    return;
                }

                if (!session.HasPlayer(join.AuthenticationId))
                {
                    pendingJoinGate.Add(senderClientId, join, Time.realtimeSinceStartupAsDouble);
                    ProcessPendingJoins();
                    return;
                }

                AcceptJoin(senderClientId, join);
                return;
            }

            if (message is CommandsMessage commands &&
                authenticationByClient.TryGetValue(senderClientId, out var authenticationId))
            {
                host.SubmitCommands(authenticationId, commands.Commands);
                return;
            }

            if (message is RematchMessage &&
                authenticationByClient.TryGetValue(senderClientId, out authenticationId))
            {
                host.RequestRematch(authenticationId);
                return;
            }

            Send(senderClientId, new RejectedMessage(null, "Unexpected message from the connected client."));
        }

        private void AcceptJoin(ulong senderClientId, JoinMessage join)
        {
            if (clientByAuthentication.TryGetValue(join.AuthenticationId, out var existingClient) &&
                existingClient != senderClientId)
            {
                clientByAuthentication.Remove(join.AuthenticationId);
                authenticationByClient.Remove(existingClient);
                if (networkManager.ConnectedClients.ContainsKey(existingClient))
                {
                    networkManager.DisconnectClient(existingClient);
                }
            }

            authenticationByClient[senderClientId] = join.AuthenticationId;
            clientByAuthentication[join.AuthenticationId] = senderClientId;
            if (host.Join(join.AuthenticationId, join.DisplayName) >= 0)
            {
                return;
            }

            authenticationByClient.Remove(senderClientId);
            if (clientByAuthentication.TryGetValue(join.AuthenticationId, out var currentClient) &&
                currentClient == senderClientId)
            {
                clientByAuthentication.Remove(join.AuthenticationId);
            }
        }

        private void ProcessPendingJoins()
        {
            if (!isHost || pendingJoinGate.Count == 0 || session == null)
            {
                return;
            }

            var now = Time.realtimeSinceStartupAsDouble;
            PendingJoinGateResult result;
            try
            {
                result = pendingJoinGate.Evaluate(now,
                    request => session.HasPlayer(request.AuthenticationId));
            }
            catch (Exception exception)
            {
                TransportError?.Invoke(exception);
                return;
            }

            for (var index = 0; index < result.Accepted.Count; index++)
            {
                var request = result.Accepted[index];
                AcceptJoin(request.ClientId, request.Message);
            }

            for (var index = 0; index < result.Expired.Count; index++)
            {
                var request = result.Expired[index];
                Send(request.ClientId, new RejectedMessage(JoinRejectReason.NotSessionMember,
                    "The sender did not become a session member before the join request expired."));
            }

            if (pendingJoinGate.Count > 0 && !membershipRefreshInProgress && now >= nextMembershipRefreshAt)
            {
                nextMembershipRefreshAt = now + MembershipRefreshIntervalSeconds;
                RefreshSessionAsync();
            }
        }

        private async void RefreshSessionAsync()
        {
            membershipRefreshInProgress = true;
            try
            {
                await session.RefreshAsync();
            }
            catch (Exception exception)
            {
                TransportError?.Invoke(exception);
            }
            finally
            {
                membershipRefreshInProgress = false;
            }
        }

        private void ReceiveClientMessage(NetMessage message)
        {
            if (message is WelcomeMessage)
            {
                joinRetryCount = 0;
                nextJoinAttemptAt = 0;
            }
            else if (message is RejectedMessage rejected &&
                     rejected.Code == JoinRejectReason.NotSessionMember && joinRetryCount < MaximumJoinRetries)
            {
                joinRetryCount++;
                joinSent = false;
                nextJoinAttemptAt = Time.realtimeSinceStartupAsDouble + JoinRetryDelaySeconds;
            }

            client?.Receive(message);
        }
    }
}