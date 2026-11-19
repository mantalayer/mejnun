using System;
using Unity.Services.Multiplayer;

namespace Mejnun.Net.Unity
{
    public static class OnlineLaunch
    {
        private static OnlineSessionLaunch pendingLaunch;

        public static bool HasPending => pendingLaunch != null;

        public static void Set(UgsOnlineService service, ISession session, bool isHost, string displayName,
            string sessionCode = null, bool startedEarly = false)
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
                service.AuthenticationId, displayName, sessionCode, startedEarly);
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
        internal OnlineSessionLaunch(UgsOnlineService service, ISession session, bool isHost,
            string authenticationId, string displayName, string sessionCode, bool startedEarly)
        {
            Service = service;
            Session = session;
            IsHost = isHost;
            StartedEarly = startedEarly;
            AuthenticationId = authenticationId;
            DisplayName = displayName;
            SessionCode = !string.IsNullOrWhiteSpace(session.Code)
                ? session.Code
                : !string.IsNullOrWhiteSpace(sessionCode)
                    ? sessionCode
                    : service.SessionCode;
        }

        public UgsOnlineService Service { get; }
        public ISession Session { get; }
        public bool IsHost { get; }
        public bool StartedEarly { get; }
        public string AuthenticationId { get; }
        public string DisplayName { get; }
        public string SessionCode { get; }
    }
}