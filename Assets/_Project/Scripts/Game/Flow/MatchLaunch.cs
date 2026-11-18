namespace Mejnun.Game
{
    public static class MatchLaunch
    {
        private static MatchRequest pendingRequest;

        public static void Set(MatchRequest request)
        {
            pendingRequest = request;
        }

        public static bool TryTake(out MatchRequest request)
        {
            request = pendingRequest;
            pendingRequest = null;
            return request != null;
        }

        public static void Clear()
        {
            pendingRequest = null;
        }
    }
}