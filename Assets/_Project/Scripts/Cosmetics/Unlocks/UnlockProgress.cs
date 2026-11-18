namespace Mejnun.Cosmetics
{
    public readonly struct UnlockProgress
    {
        public UnlockProgress(bool unlocked, int current, int required, bool isCountable)
        {
            Unlocked = unlocked;
            Current = current;
            Required = required;
            IsCountable = isCountable;
        }

        public bool Unlocked { get; }
        public int Current { get; }
        public int Required { get; }
        public bool IsCountable { get; }
    }
}