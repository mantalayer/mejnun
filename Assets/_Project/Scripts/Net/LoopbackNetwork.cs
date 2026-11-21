using System;
using System.Collections.Generic;
using Mejnun.Core;

namespace Mejnun.Net
{
    public sealed class LoopbackNetwork : IDisposable
    {
        private readonly HostSession host;
        private readonly Dictionary<string, ClientSession> clients = new Dictionary<string, ClientSession>();
        private readonly HashSet<string> connected = new HashSet<string>();

        public LoopbackNetwork(HostSession host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            host.MessageSent += HandleMessage;
        }

        public ClientSession Connect(string authenticationId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(authenticationId))
            {
                throw new ArgumentException("Authentication id is required.", nameof(authenticationId));
            }

            if (!clients.TryGetValue(authenticationId, out var client))
            {
                client = new ClientSession();
                var capturedId = authenticationId;
                client.CommandsReady += message => host.SubmitCommands(capturedId, message.Commands);
                client.RematchRequested += _ => host.RequestRematch(capturedId);
                clients.Add(authenticationId, client);
            }

            connected.Add(authenticationId);
            if (host.Join(authenticationId, displayName) < 0)
            {
                connected.Remove(authenticationId);
            }

            return client;
        }

        public ClientSession GetClient(string authenticationId)
        {
            return clients.TryGetValue(authenticationId, out var client) ? client : null;
        }

        public bool Disconnect(string authenticationId, DisconnectChoice choice)
        {
            var wasConnected = connected.Remove(authenticationId);
            var result = host.Disconnect(authenticationId, choice);
            return wasConnected && result;
        }

        public void MarkAway(string authenticationId)
        {
            if (connected.Contains(authenticationId))
            {
                host.MarkAway(authenticationId);
            }
        }

        public bool Drop(string authenticationId)
        {
            return Disconnect(authenticationId, DisconnectChoice.Wait);
        }

        public ClientSession Rejoin(string authenticationId, string displayName)
        {
            return Connect(authenticationId, displayName);
        }

        public void Dispose()
        {
            host.MessageSent -= HandleMessage;
            connected.Clear();
        }

        private void HandleMessage(string authenticationId, NetMessage message)
        {
            if (!connected.Contains(authenticationId) || !clients.TryGetValue(authenticationId, out var client))
            {
                return;
            }

            client.Receive(NetCodec.Decode(NetCodec.Encode(message)));
        }
    }
}