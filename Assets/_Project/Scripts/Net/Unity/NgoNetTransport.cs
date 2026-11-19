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
        private const string MessageName = "mejnun.session.v1";
        private const int MaximumPayloadBytes = 1024 * 1024 + 65536;

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
        private bool joinSent;

        public event Action<string> PlayerDisconnected;
        public event Action<Exception> TransportError;

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
            }

            Configure(manager, ugsSession, authenticationId, displayName);
            isHost = false;
            client = clientSession ?? throw new ArgumentNullException(nameof(clientSession));
            client.CommandsReady += SendToServer;
            EnsureRegistered();
            SendJoinIfConnected();
        }

        private void Update()
        {
            EnsureRegistered();
            SendJoinIfConnected();
        }

        private void OnDestroy()
        {
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback -= OnClientConnected;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
                if (handlerRegistered && networkManager.CustomMessagingManager != null)
                {
                    networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
                }
            }

            if (host != null)
            {
                host.MessageSent -= SendToPlayer;
            }

            if (client != null)
            {
                client.CommandsReady -= SendToServer;
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
            if (handlerRegistered || networkManager == null || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, ReceiveMessage);
            handlerRegistered = true;
        }

        private void OnClientConnected(ulong clientId)
        {
            EnsureRegistered();
            SendJoinIfConnected();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (isHost && authenticationByClient.TryGetValue(clientId, out var authenticationId))
            {
                authenticationByClient.Remove(clientId);
                clientByAuthentication.Remove(authenticationId);
                PlayerDisconnected?.Invoke(authenticationId);
            }

            if (!isHost && networkManager != null && clientId == networkManager.LocalClientId)
            {
                joinSent = false;
            }
        }

        private void SendJoinIfConnected()
        {
            if (isHost || joinSent || client == null || networkManager == null || !networkManager.IsClient ||
                networkManager.IsHost || !networkManager.IsConnectedClient || !handlerRegistered)
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

                    networkManager.CustomMessagingManager.SendNamedMessage(MessageName, clientId, writer,
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
                    client?.Receive(message);
                }
            }
            catch (Exception exception)
            {
                TransportError?.Invoke(exception);
            }
        }

        private void ReceiveHostMessage(ulong senderClientId, NetMessage message)
        {
            if (message is JoinMessage join)
            {
                if (!session.HasPlayer(join.AuthenticationId) ||
                    clientByAuthentication.TryGetValue(join.AuthenticationId, out var existingClient) &&
                    existingClient != senderClientId)
                {
                    Send(senderClientId, new RejectedMessage("The sender is not an available session member."));
                    return;
                }

                authenticationByClient[senderClientId] = join.AuthenticationId;
                clientByAuthentication[join.AuthenticationId] = senderClientId;
                host.Join(join.AuthenticationId, join.DisplayName);
                return;
            }

            if (message is CommandsMessage commands &&
                authenticationByClient.TryGetValue(senderClientId, out var authenticationId))
            {
                host.SubmitCommands(authenticationId, commands.Commands);
                return;
            }

            Send(senderClientId, new RejectedMessage("Unexpected message from the connected client."));
        }
    }
}