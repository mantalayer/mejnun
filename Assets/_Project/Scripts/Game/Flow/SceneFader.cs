using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mejnun.Game
{
    public sealed class SceneFader : MonoBehaviour
    {
        public static bool IsTransitioning => false;

        public static void LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
