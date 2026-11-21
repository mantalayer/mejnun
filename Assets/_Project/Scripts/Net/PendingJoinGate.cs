using System;
using System.Collections.Generic;

namespace Mejnun.Net
{
    public sealed class PendingJoinGate
    {
        public const double DefaultTimeoutSeconds = 15;

        private readonly Dictionary<ulong, PendingJoinRequest> pending =
            new Dictionary<ulong, PendingJoinRequest>();
        private readonly double timeoutSeconds;

        public PendingJoinGate(double timeoutSeconds = DefaultTimeoutSeconds)
        {
            if (timeoutSeconds <= 0 || double.IsNaN(timeoutSeconds) || double.IsInfinity(timeoutSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            }

            this.timeoutSeconds = timeoutSeconds;
        }

        public int Count => pending.Count;

        public bool ContainsAuthentication(string authenticationId)
        {
            foreach (var request in pending.Values)
            {
                if (string.Equals(request.AuthenticationId, authenticationId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public void Add(ulong clientId, JoinMessage message, double nowSeconds)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            ValidateTime(nowSeconds);
            pending[clientId] = new PendingJoinRequest(clientId, message, nowSeconds + timeoutSeconds);
        }

        public bool Remove(ulong clientId)
        {
            return pending.Remove(clientId);
        }

        public void Clear()
        {
            pending.Clear();
        }

        public PendingJoinGateResult Evaluate(double nowSeconds, Func<PendingJoinRequest, bool> isMember)
        {
            ValidateTime(nowSeconds);
            if (isMember == null)
            {
                throw new ArgumentNullException(nameof(isMember));
            }

            var accepted = new List<PendingJoinRequest>();
            var expired = new List<PendingJoinRequest>();
            var requests = new List<PendingJoinRequest>(pending.Values);
            for (var index = 0; index < requests.Count; index++)
            {
                var request = requests[index];
                if (nowSeconds >= request.Deadline)
                {
                    expired.Add(request);
                    pending.Remove(request.ClientId);
                }
                else if (isMember(request))
                {
                    accepted.Add(request);
                    pending.Remove(request.ClientId);
                }
            }

            return new PendingJoinGateResult(accepted, expired);
        }

        private static void ValidateTime(double nowSeconds)
        {
            if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(nowSeconds));
            }
        }
    }

    public sealed class PendingJoinRequest
    {
        internal PendingJoinRequest(ulong clientId, JoinMessage message, double deadline)
        {
            ClientId = clientId;
            Message = message;
            AuthenticationId = message.AuthenticationId;
            DisplayName = message.DisplayName;
            Deadline = deadline;
        }

        public ulong ClientId { get; }
        public JoinMessage Message { get; }
        public string AuthenticationId { get; }
        public string DisplayName { get; }
        public double Deadline { get; }
    }

    public sealed class PendingJoinGateResult
    {
        internal PendingJoinGateResult(IReadOnlyList<PendingJoinRequest> accepted,
            IReadOnlyList<PendingJoinRequest> expired)
        {
            Accepted = accepted;
            Expired = expired;
        }

        public IReadOnlyList<PendingJoinRequest> Accepted { get; }
        public IReadOnlyList<PendingJoinRequest> Expired { get; }
    }
}