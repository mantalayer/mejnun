using System;
using System.Reflection;
using UnityEngine;

namespace Mejnun.Audio
{
    public sealed class WwiseAudioService : IAudioService
    {
        private const string DedicatedListenerName = "Wwise Listener";
        private readonly GameObject emitter;
        private static GameObject dedicatedListener;

        public WwiseAudioService(GameObject emitter)
        {
            this.emitter = emitter;
        }

        public void Post(string eventName)
        {
            if (IsReady && !string.IsNullOrEmpty(eventName) && emitter != null)
            {
                AkUnitySoundEngine.PostEvent(eventName, emitter);
            }
        }

        public void SetState(string stateGroup, string state)
        {
            if (IsReady && !string.IsNullOrEmpty(stateGroup) && !string.IsNullOrEmpty(state))
            {
                AkUnitySoundEngine.SetState(stateGroup, state);
            }
        }

        public void SetSwitch(string switchGroup, string switchValue)
        {
            if (IsReady && !string.IsNullOrEmpty(switchGroup) && !string.IsNullOrEmpty(switchValue) &&
                emitter != null)
            {
                AkUnitySoundEngine.SetSwitch(switchGroup, switchValue, emitter);
            }
        }

        public void SetRtpc(string rtpcName, float value)
        {
            if (IsReady && !string.IsNullOrEmpty(rtpcName))
            {
                AkUnitySoundEngine.SetRTPCValue(rtpcName, value);
            }
        }

        public bool LoadBank(string bankName)
        {
            return LoadBank(bankName, out _) == AKRESULT.AK_Success;
        }

        public AKRESULT LoadBank(string bankName, out uint bankId)
        {
            if (string.IsNullOrEmpty(bankName))
            {
                bankId = 0;
                return AKRESULT.AK_InvalidParameter;
            }

            if (!IsReady)
            {
                bankId = 0;
                return AKRESULT.AK_NotInitialized;
            }

            return AkUnitySoundEngine.LoadBank(bankName, out bankId);
        }

        public string LoadBankResult(string bankName)
        {
            return LoadBank(bankName, out _).ToString();
        }

        public static void EnsureInitialized()
        {
            var initializerType = FindAkInitializerType();
            if (initializerType == null || FindAkInitializer(initializerType) != null)
            {
                return;
            }

            var initializerObject = new GameObject("Wwise AkInitializer");
            var initializer = initializerObject.AddComponent(initializerType);
            initializerType.GetMethod("InitializeInitializationSettings", BindingFlags.Instance | BindingFlags.Public)
                ?.Invoke(initializer, null);
        }

        public static void EnsureDedicatedListener()
        {
            var listenerType = FindAkType("AkAudioListener");
            if (listenerType == null)
            {
                return;
            }

            Component listener = dedicatedListener == null ? null : dedicatedListener.GetComponent(listenerType);
            if (listener == null)
            {
                var existingListeners = Resources.FindObjectsOfTypeAll(listenerType);
                foreach (var existing in existingListeners)
                {
                    if (existing is Component component && component.gameObject.scene.IsValid() &&
                        component.gameObject.name == DedicatedListenerName)
                    {
                        listener = component;
                        break;
                    }
                }
            }

            if (listener == null)
            {
                dedicatedListener = new GameObject(DedicatedListenerName);
                listener = dedicatedListener.AddComponent(listenerType);
            }
            else
            {
                dedicatedListener = listener.gameObject;
            }

            if (listener.transform.parent != null)
            {
                listener.transform.SetParent(null);
            }

            UnityEngine.Object.DontDestroyOnLoad(listener.gameObject);
            var setDefaultListener = listenerType.GetMethod("SetIsDefaultListener",
                BindingFlags.Instance | BindingFlags.Public);
            if (setDefaultListener != null)
            {
                setDefaultListener.Invoke(listener, new object[] { true });
            }
            else
            {
                listenerType.GetField("isDefaultListener", BindingFlags.Instance | BindingFlags.Public)
                    ?.SetValue(listener, true);
            }

            if (listener is Behaviour listenerBehaviour)
            {
                listenerBehaviour.enabled = true;
            }

            var allListeners = Resources.FindObjectsOfTypeAll(listenerType);
            foreach (var existing in allListeners)
            {
                if (!(existing is Component component) || component == listener || !component.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (component is Behaviour behaviour)
                {
                    behaviour.enabled = false;
                }

                UnityEngine.Object.Destroy(component);
            }
        }

        private static Type FindAkType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null && typeof(MonoBehaviour).IsAssignableFrom(type))
                {
                    return type;
                }
            }

            return null;
        }

        private static Type FindAkInitializerType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var initializerType = assembly.GetType("AkInitializer");
                if (initializerType != null && typeof(MonoBehaviour).IsAssignableFrom(initializerType))
                {
                    return initializerType;
                }
            }

            return null;
        }

        private static Component FindAkInitializer(Type initializerType)
        {
            var components = Resources.FindObjectsOfTypeAll(initializerType);
            foreach (var component in components)
            {
                if (component is Component initializer && initializer.gameObject.scene.IsValid())
                {
                    return initializer;
                }
            }

            return null;
        }

        public bool IsInitialized => AkUnitySoundEngine.IsInitialized();

        private static bool IsReady => AkUnitySoundEngine.IsInitialized();
    }
}