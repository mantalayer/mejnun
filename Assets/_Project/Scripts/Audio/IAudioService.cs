namespace Mejnun.Audio
{
    public interface IAudioService
    {
        void Post(string eventName);
        void SetState(string stateGroup, string state);
        void SetSwitch(string switchGroup, string switchValue);
        void SetRtpc(string rtpcName, float value);
        bool LoadBank(string bankName);
    }
}