using System;
using Unity.Services.Multiplayer;

namespace Mejnun.Net.Unity
{
    public static class OnlineLaunch
    {
        private static OnlineSessionLaunch pendingLaunch;

        public static bool HasPending => pendingLaunch != null;

        public static void Set(UgsOnlineService service, ISession session, bool isHost, string displayName)
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
                service.AuthenticationId, displayName);
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
            string authenticationId, string displayName)
        {
            Service = service;
            Session = session;
            IsHost = isHost;
            AuthenticationId = authenticationId;
            DisplayName = displayName;
        }

        public UgsOnlineService Service { get; }
        public ISession Session { get; }
        public bool IsHost { get; }
        public string AuthenticationId { get; }
        public string DisplayName { get; }
    }
}