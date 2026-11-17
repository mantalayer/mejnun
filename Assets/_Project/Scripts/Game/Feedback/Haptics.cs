using UnityEngine;

namespace Mejnun.Game
{
    public static class Haptics
    {
        public static void Pickup()
        {
            Vibrate();
        }

        public static void LongPress()
        {
            Vibrate();
        }

        public static void Drop()
        {
            Vibrate();
        }

        public static void Invalid()
        {
            Vibrate();
        }

        private static void Vibrate()
        {
#if UNITY_IOS || UNITY_ANDROID
            if (Application.isMobilePlatform)
            {
                Handheld.Vibrate();
            }
#endif
        }
    }
}