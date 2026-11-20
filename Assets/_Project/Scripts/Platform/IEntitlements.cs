namespace Mejnun.Platform
{
    public enum EntitlementId
    {
        None = 0,
        RemoveAds = 1,
    }

    public interface IEntitlements
    {
        // True for demo builds; gate demo-only content on this rather than on
        // compile defines wherever possible.
        bool IsDemo { get; }

        bool OwnsDlc(DlcId id);
        bool Owns(EntitlementId id);
    }
}
