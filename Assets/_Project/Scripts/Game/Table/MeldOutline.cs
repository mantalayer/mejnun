using UnityEngine;

namespace Mejnun.Game
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class MeldOutline : MonoBehaviour
    {
        private const float BaseWidth = 0.025f;
        private const float TargetWidth = 0.055f;
        private static readonly Color TargetColor = new Color(1f, 0.82f, 0.22f, 1f);
        private static Material sharedLineMaterial;
        private LineRenderer lineRenderer;
        private Color baseColor = Color.white;
        private bool targetGlow;

        public bool IsTargetGlowing => targetGlow && lineRenderer != null && lineRenderer.enabled;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = false;
            lineRenderer.positionCount = 5;
            lineRenderer.startWidth = 0.025f;
            lineRenderer.endWidth = 0.025f;
            lineRenderer.sortingOrder = 0;
            lineRenderer.sharedMaterial = GetLineMaterial();
        }

        public void SetBounds(Rect bounds, bool isValid, Vector2 cardSize)
        {
            if (lineRenderer == null)
            {
                Awake();
            }

            var paddingX = cardSize.x * 0.08f;
            var paddingY = cardSize.y * 0.06f;
            var xMin = bounds.xMin - paddingX;
            var xMax = bounds.xMax + paddingX;
            var yMin = bounds.yMin - paddingY;
            var yMax = bounds.yMax + paddingY;
            var z = -0.02f;
            lineRenderer.SetPosition(0, new Vector3(xMin, yMin, z));
            lineRenderer.SetPosition(1, new Vector3(xMin, yMax, z));
            lineRenderer.SetPosition(2, new Vector3(xMax, yMax, z));
            lineRenderer.SetPosition(3, new Vector3(xMax, yMin, z));
            lineRenderer.SetPosition(4, new Vector3(xMin, yMin, z));
            baseColor = isValid ? new Color(0.18f, 0.92f, 0.28f, 1f) : new Color(0.95f, 0.16f, 0.13f, 1f);
            ApplyStyle(Time.unscaledTime);
            lineRenderer.enabled = true;
        }

        /// <summary>The tutorial's drop target: a thicker, pulsing gold outline.</summary>
        public void SetTargetGlow(bool glow)
        {
            if (lineRenderer == null)
            {
                Awake();
            }

            targetGlow = glow;
            ApplyStyle(Time.unscaledTime);
        }

        private void Update()
        {
            if (targetGlow && lineRenderer != null && lineRenderer.enabled)
            {
                ApplyStyle(Time.unscaledTime);
            }
        }

        private void ApplyStyle(float time)
        {
            var color = baseColor;
            var width = BaseWidth;
            if (targetGlow)
            {
                var pulse = GameSettings.ReducedMotion ? 1f : (Mathf.Sin(time * 4.5f) + 1f) * 0.5f;
                color = Color.Lerp(baseColor, TargetColor, 0.65f + pulse * 0.35f);
                width = Mathf.Lerp(BaseWidth, TargetWidth, 0.6f + pulse * 0.4f);
            }

            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
        }

        public void Hide()
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }

            targetGlow = false;
        }

        private static Material GetLineMaterial()
        {
            if (sharedLineMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    sharedLineMaterial = new Material(shader)
                    {
                        name = "Mejnun Meld Outline",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }

            return sharedLineMaterial;
        }
    }
}