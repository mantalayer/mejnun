using UnityEngine;

namespace Mejnun.Audio
{
    public sealed class WwiseAudioService : IAudioService
    {
        private readonly GameObject emitter;

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
            if (!IsReady || string.IsNullOrEmpty(bankName))
            {
                return false;
            }

            return AkUnitySoundEngine.LoadBank(bankName, out _) == AKRESULT.AK_Success;
        }

        private static bool IsReady => AkUnitySoundEngine.IsInitialized();
    }
}