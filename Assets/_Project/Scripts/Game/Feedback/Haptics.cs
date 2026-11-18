using UnityEngine;

namespace Mejnun.Game
{
    public static class Haptics
    {
        public static void Pickup()
        {
            FeedbackCues.Raise(FeedbackCue.Pickup);
            Vibrate();
        }

        public static void LongPress()
        {
            FeedbackCues.Raise(FeedbackCue.LongPress);
            Vibrate();
        }

        public static void Drop()
        {
            FeedbackCues.Raise(FeedbackCue.Drop);
            Vibrate();
        }

        public static void Invalid()
        {
            FeedbackCues.Raise(FeedbackCue.Invalid);
            Vibrate();
        }

        private static void Vibrate()
        {
            if (!GameSettings.HapticsEnabled)
            {
                return;
            }

#if UNITY_IOS || UNITY_ANDROID
            if (Application.isMobilePlatform)
            {
                Handheld.Vibrate();
            }
#endif
        }
    }
}