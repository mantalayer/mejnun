using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mejnun.Platform
{
    // Entry point for store-neutral platform features. Store backends live in
    // their own define-gated assemblies and register a factory here during
    // AfterAssembliesLoaded; before the first scene loads, the highest-priority
    // factory that starts successfully becomes Current, and the Null backend
    // is used when none does.
    public static class PlatformServices
    {
        // Returns false (and leaves services null) when the backend cannot
        // start, e.g. the store client is not running.
        public delegate bool BackendFactory(PlatformSettings settings, out IPlatformServices services);

        private struct BackendRegistration
        {
            public string Name;
            public int Priority;
            public BackendFactory TryCreate;
        }

        private static readonly List<BackendRegistration> s_registrations = new();
        private static IPlatformServices s_current;
        private static PlatformCallbackRunner s_runner;
        private static bool s_isInitialized;

        public static IPlatformServices Current => s_current ??= new NullPlatformServices();
        public static bool IsInitialized => s_isInitialized;
        public static IControllerInfo ControllerInfo => Current.ControllerInfo;
        public static ISaveStorage SaveStorage => Current.SaveStorage;
        public static IAchievements Achievements => Current.Achievements;
        public static IStats Stats => Current.Stats;
        public static IRichPresence RichPresence => Current.RichPresence;
        public static IEntitlements Entitlements => Current.Entitlements;

        public static void RegisterBackend(string name, int priority, BackendFactory tryCreate)
        {
            if (tryCreate == null)
                throw new ArgumentNullException(nameof(tryCreate));

            for (int i = 0; i < s_registrations.Count; i++)
            {
                if (s_registrations[i].Name == name)
                {
                    s_registrations.RemoveAt(i);
                    break;
                }
            }

            s_registrations.Add(new BackendRegistration { Name = name, Priority = priority, TryCreate = tryCreate });
        }

        internal static void Tick()
        {
            s_current?.Tick();
        }

        // Required because "Enter Play Mode Options → Reload Domain" is off:
        // drops the previous session's backend, factories, runner and quit hook.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsOnEnterPlayMode()
        {
            s_registrations.Clear();
            s_current = null;
            s_runner = null;
            s_isInitialized = false;
            Application.quitting -= OnApplicationQuitting;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            PlatformSettings settings = PlatformSettings.Load();
            s_current = CreateBackend(settings);
            s_isInitialized = true;
            Debug.Log($"[Platform] Active backend: {s_current.PlatformName}");
            // Save files live here; the cloud-sync folder mapping for each OS must match it.
            Debug.Log($"[Platform] Persistent data path: {Application.persistentDataPath}");

            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;

            if (s_runner == null)
            {
                GameObject runnerObject = new GameObject(nameof(PlatformCallbackRunner));
                UnityEngine.Object.DontDestroyOnLoad(runnerObject);
                s_runner = runnerObject.AddComponent<PlatformCallbackRunner>();
            }
        }

        private static IPlatformServices CreateBackend(PlatformSettings settings)
        {
            s_registrations.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            for (int i = 0; i < s_registrations.Count; i++)
            {
                BackendRegistration registration = s_registrations[i];
                try
                {
                    if (registration.TryCreate(settings, out IPlatformServices services) && services != null)
                        return services;

                    Debug.LogWarning($"[Platform] Backend '{registration.Name}' did not start; trying the next one.");
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Platform] Backend '{registration.Name}' threw during startup; trying the next one.");
                    Debug.LogException(exception);
                }
            }

            return new NullPlatformServices();
        }

        private static void OnApplicationQuitting()
        {
            Application.quitting -= OnApplicationQuitting;
            IPlatformServices current = s_current;
            s_current = null;
            current?.Shutdown();
        }
    }
}
