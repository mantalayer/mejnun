using System;
using Unity.Services.Multiplayer;

namespace Mejnun.Net.Unity
{
    public static class OnlineLaunch
    {
        private static OnlineSessionLaunch pendingLaunch;

        public static bool HasPending => pendingLaunch != null;

        public static void Set(UgsOnlineService service, ISession session, bool isHost, string displayName,
            string sessionCode = null, bool startedEarly = false, bool joinedStartedMatch = false)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            pendingLaunch = new OnlineSessionLaunch(service, session, isHost,
                service.AuthenticationId, displayName, sessionCode, startedEarly, joinedStartedMatch);
        }

        public static bool TryTake(out OnlineSessionLaunch launch)
        {
            launch = pendingLaunch;
            pendingLaunch = null;
            return launch != null;
        }
    }

    public sealed class OnlineSessionLaunch
    {
        private readonly bool startedAsHost;
        private readonly ISession initialSession;

        internal OnlineSessionLaunch(UgsOnlineService service, ISession session, bool isHost,
            string authenticationId, string displayName, string sessionCode, bool startedEarly,
            bool joinedStartedMatch)
        {
            Service = service;
            initialSession = session;
            startedAsHost = isHost;
            StartedEarly = startedEarly;
            JoinedStartedMatch = joinedStartedMatch;
            AuthenticationId = authenticationId;
            DisplayName = displayName;
            SessionCode = !string.IsNullOrWhiteSpace(session.Code)
                ? session.Code
                : !string.IsNullOrWhiteSpace(sessionCode)
                    ? sessionCode
                    : service.SessionCode;
        }

        public UgsOnlineService Service { get; }
        public ISession Session => Service.Session ?? initialSession;
        public bool IsHost => Session == null ? startedAsHost : Session.IsHost;
        public bool StartedEarly { get; }
        public bool JoinedStartedMatch { get; }
        public string AuthenticationId { get; }
        public string DisplayName { get; }
        public string SessionCode { get; }
    }
}