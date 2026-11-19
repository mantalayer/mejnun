using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mejnun.Game
{
    public sealed class SceneFader : MonoBehaviour
    {
        private const float FadeOutDuration = 0.35f;
        private const float FadeInDuration = 0.45f;
        private const float MaxFrameDelta = 0.1f;
        private const int SettleFrames = 2;

        private static SceneFader instance;

        private Texture2D blackTexture;
        private float alpha;
        private bool isTransitioning;

        public static bool IsTransitioning => instance != null && instance.isTransitioning;

        public static void LoadScene(string sceneName)
        {
            if (!Application.isPlaying)
            {
                SceneManager.LoadScene(sceneName);
                return;
            }

            var fader = GetOrCreate();
            if (fader.isTransitioning)
            {
                return;
            }

            fader.StartCoroutine(fader.FadeAndLoad(sceneName));
        }

        private static SceneFader GetOrCreate()
        {
            if (instance == null)
            {
                var go = new GameObject("Scene Fader");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<SceneFader>();
            }

            return instance;
        }

        private void Awake()
        {
            blackTexture = new Texture2D(1, 1);
            blackTexture.SetPixel(0, 0, Color.black);
            blackTexture.Apply();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            if (blackTexture != null)
            {
                Destroy(blackTexture);
            }
        }

        private IEnumerator FadeAndLoad(string sceneName)
        {
            isTransitioning = true;
            yield return Fade(alpha, 1f, FadeOutDuration);

            SceneManager.LoadScene(sceneName);
            for (var i = 0; i < SettleFrames; i++)
            {
                yield return null;
            }

            yield return Fade(1f, 0f, FadeInDuration);
            isTransitioning = false;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            var elapsed = 0f;
            var last = Time.realtimeSinceStartup;
            alpha = from;
            while (elapsed < duration)
            {
                yield return null;
                var now = Time.realtimeSinceStartup;
                elapsed += Mathf.Clamp(now - last, 0f, MaxFrameDelta);
                last = now;
                var t = Mathf.Clamp01(elapsed / duration);
                alpha = Mathf.Lerp(from, to, t * t * (3f - 2f * t));
            }

            alpha = to;
        }

        private void OnGUI()
        {
            if (alpha <= 0f || blackTexture == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            GUI.depth = -1000;
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), blackTexture);
            GUI.color = previous;
        }
    }
}
