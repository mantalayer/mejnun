using System;

namespace Mejnun.Game
{
    public static class FeedbackCues
    {
        public static event Action<FeedbackCue> Raised;

        internal static void Raise(FeedbackCue cue)
        {
            Raised?.Invoke(cue);
        }
    }
}