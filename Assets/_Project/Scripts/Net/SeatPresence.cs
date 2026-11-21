namespace Mejnun.Net
{
    public enum SeatPresence : byte
    {
        Empty,
        Connected,
        Reconnecting,
        Disconnected,
        BotControlled,
        Bot
    }

    public enum PauseReason : byte
    {
        None,
        WaitingForPlayers,
        HostAway,
        SavedByHost
    }

    public enum JoinRejectReason : byte
    {
        NoFreeSeat,
        NotSessionMember,
        MissingAuthentication
    }
}