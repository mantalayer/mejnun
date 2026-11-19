using System;
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
#if UNITY_ANDROID && !UNITY_EDITOR
                VibrateAndroid();
#else
                Handheld.Vibrate();
#endif
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void VibrateAndroid()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
                    {
                        return;
                    }

                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    {
                        if (version.GetStatic<int>("SDK_INT") >= 26)
                        {
                            using (var vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect"))
                            using (var effect = vibrationEffect.CallStatic<AndroidJavaObject>("createOneShot", 25L, 80))
                            {
                                vibrator.Call("vibrate", effect);
                            }
                        }
                        else
                        {
                            vibrator.Call("vibrate", 25L);
                        }
                    }
                }
            }
            catch (Exception)
            {
                Handheld.Vibrate();
            }
        }
#endif
    }
}