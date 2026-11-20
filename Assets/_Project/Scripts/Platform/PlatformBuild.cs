namespace Mejnun.Platform
{
    // Build-variant facts fixed at compile time by the active Build Profile's
    // scripting defines.
    public static class PlatformBuild
    {
#if MEJNUN_STEAM
        public const bool IsSteam = true;
#else
        public const bool IsSteam = false;
#endif

#if MEJNUN_MOBILE
        public const bool IsMobile = true;
#else
        public const bool IsMobile = false;
#endif

#if MEJNUN_DEMO
        public const bool IsDemo = true;
#else
        public const bool IsDemo = false;
#endif
    }
}
