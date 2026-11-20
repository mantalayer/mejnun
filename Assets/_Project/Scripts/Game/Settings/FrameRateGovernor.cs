using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace Mejnun.Game
{
    public sealed class FrameRateGovernor : MonoBehaviour
    {
        private const float IdleDelaySeconds = 2f;
        private static FrameRateGovernor instance;
        private float lastActivityTime;
        private IDisposable inputListener;

        public static void Initialize()
        {
            if (!Application.isMobilePlatform || instance != null)
                return;

            QualitySettings.vSyncCount = 0;
            var host = new GameObject(nameof(FrameRateGovernor));
            DontDestroyOnLoad(host);
            instance = host.AddComponent<FrameRateGovernor>();
        }

        public static void NotifyActivity()
        {
            if (instance == null)
                return;

            var now = Time.unscaledTime;
            var wasIdle = now - instance.lastActivityTime >= IdleDelaySeconds;
            instance.lastActivityTime = now;
            if (wasIdle)
                instance.ApplyFrameRate();
        }

        public static int ResolveTargetFrameRate(int refreshRate, bool batterySaver, bool idle)
        {
            int activeRate = refreshRate <= 0 ? 60 : Mathf.Clamp(refreshRate, 1, 60);
            return batterySaver || idle ? Mathf.Min(activeRate, 30) : activeRate;
        }

        private void OnEnable()
        {
            lastActivityTime = Time.unscaledTime;
            inputListener = InputSystem.onAnyButtonPress.Call(_ => NotifyActivity());
            GameSettings.Changed += OnSettingsChanged;
        }

        private void OnDisable()
        {
            inputListener?.Dispose();
            inputListener = null;
            GameSettings.Changed -= OnSettingsChanged;
        }

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer != null && pointer.delta.ReadValue() != Vector2.zero)
                NotifyActivity();

            ApplyFrameRate();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused)
            {
                NotifyActivity();
                ApplyFrameRate();
            }
        }

        private void OnSettingsChanged(string key)
        {
            NotifyActivity();
            ApplyFrameRate();
        }

        private void ApplyFrameRate()
        {
            int refreshRate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            bool idle = Time.unscaledTime - lastActivityTime >= IdleDelaySeconds;
            int targetRate = ResolveTargetFrameRate(refreshRate, GameSettings.BatterySaver, idle);
            if (Application.targetFrameRate != targetRate)
                Application.targetFrameRate = targetRate;
        }
    }
}