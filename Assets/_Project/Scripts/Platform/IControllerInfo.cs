namespace Mejnun.Platform
{
    public enum PlatformControllerKind
    {
        Unknown,
        Xbox,
        PlayStation4,
        PlayStation5,
        NintendoSwitch,
        SteamDeck,
        SteamController,
        Generic
    }

    public interface IControllerInfo
    {
        bool IsSteamDeck { get; }
        PlatformControllerKind ActiveControllerKind { get; }
    }
}