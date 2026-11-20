namespace Mejnun.Platform
{
    // One store / platform backend (Steam, Epic, console, or the offline Null
    // fallback). Gameplay talks to the active backend through PlatformServices
    // and never references a store SDK directly.
    public interface IPlatformServices
    {
        string PlatformName { get; }
        bool IsInitialized { get; }
        IControllerInfo ControllerInfo { get; }
        ISaveStorage SaveStorage { get; }
        IAchievements Achievements { get; }
        IStats Stats { get; }
        IRichPresence RichPresence { get; }
        IEntitlements Entitlements { get; }

        // Called once per frame by PlatformCallbackRunner to pump SDK callbacks
        // and flush pending writes.
        void Tick();
        void Shutdown();
    }
}
