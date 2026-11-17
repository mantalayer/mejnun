using UnityEngine;

namespace Mejnun.Game
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class MeldOutline : MonoBehaviour
    {
        private static Material sharedLineMaterial;
        private LineRenderer lineRenderer;

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
            var color = isValid ? new Color(0.18f, 0.92f, 0.28f, 1f) : new Color(0.95f, 0.16f, 0.13f, 1f);
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.enabled = true;
        }

        public void Hide()
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }
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