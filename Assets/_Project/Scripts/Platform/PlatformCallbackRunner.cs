using UnityEngine;

namespace Mejnun.Platform
{
    // Persistent object created by PlatformServices before the first scene
    // loads; pumps the active backend once per frame.
    [AddComponentMenu("")]
    public sealed class PlatformCallbackRunner : MonoBehaviour
    {
        private void Update()
        {
            PlatformServices.Tick();
        }
    }
}
