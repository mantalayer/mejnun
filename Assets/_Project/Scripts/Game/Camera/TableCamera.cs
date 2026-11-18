using UnityEngine;
using UnityEngine.InputSystem;

namespace Mejnun.Game
{
    [RequireComponent(typeof(Camera))]
    public sealed class TableCamera : MonoBehaviour
    {
        [SerializeField] private float minOrthographicSize = 1.5f;
        [SerializeField] private float maxOrthographicSize = 20f;
        [SerializeField] private float zoomSensitivity = 1f;
        [SerializeField] private float fitPadding = 1.12f;
        [SerializeField, Range(0f, 0.45f)] private float bottomInsetFraction = 0.24f;
        [SerializeField] private PointerRouter pointerRouter;

        private Camera tableCamera;
        private float baseOrthographicSize;
        private bool manuallyAdjusted;
        private Bounds boardBounds;
        private bool hasBoardBounds;

        public Camera Camera => tableCamera;
        public float BottomInsetFraction => bottomInsetFraction;
        public bool ManuallyAdjusted => manuallyAdjusted;
        public float ZoomFactor => baseOrthographicSize <= 0f
            ? 1f
            : baseOrthographicSize / Mathf.Max(tableCamera.orthographicSize, 0.0001f);

        private void Awake()
        {
            tableCamera = GetComponent<Camera>();
            tableCamera.orthographic = true;
            baseOrthographicSize = tableCamera.orthographicSize;
        }

        private void OnEnable()
        {
            if (pointerRouter != null)
            {
                pointerRouter.Gesture += HandlePointerGesture;
            }
        }

        private void OnDisable()
        {
            if (pointerRouter != null)
            {
                pointerRouter.Gesture -= HandlePointerGesture;
            }
        }

        private void Update()
        {
            HandleMouseCamera();
        }

        public void SetBoardBounds(Bounds bounds)
        {
            boardBounds = bounds;
            hasBoardBounds = true;
            AutoFit(bounds);
        }

        public void AutoFit(Bounds bounds)
        {
            if (!manuallyAdjusted)
            {
                FitBounds(bounds, fitPadding, false);
            }
        }

        public void FitBoard()
        {
            if (hasBoardBounds)
            {
                FitBounds(boardBounds, fitPadding, true);
            }
        }

        public void FitBounds(Bounds bounds, float padding = 1.1f, bool resetManualAdjustment = true)
        {
            if (tableCamera == null)
            {
                tableCamera = GetComponent<Camera>();
            }

            var aspect = Mathf.Max(tableCamera.aspect, 0.01f);
            var availableHeight = Mathf.Max(1f - bottomInsetFraction, 0.1f);
            var verticalSize = Mathf.Max(bounds.size.y * 0.5f / availableHeight, 0.5f);
            var horizontalSize = Mathf.Max(bounds.size.x * 0.5f / aspect, 0.5f);
            var fitSize = Mathf.Clamp(Mathf.Max(verticalSize, horizontalSize) * padding,
                minOrthographicSize, maxOrthographicSize);
            tableCamera.orthographic = true;
            tableCamera.orthographicSize = fitSize;
            var position = tableCamera.transform.position;
            position.x = bounds.center.x;
            position.y = bounds.center.y - fitSize * bottomInsetFraction;
            tableCamera.transform.position = position;
            baseOrthographicSize = fitSize;
            if (resetManualAdjustment)
            {
                manuallyAdjusted = false;
            }
        }

        public void Zoom(float zoomFactor, Vector2 screenPosition)
        {
            ZoomAtScreenPoint(zoomFactor, screenPosition, true);
        }

        public void Pan(Vector2 screenDelta)
        {
            PanByScreenDelta(screenDelta, true);
        }

        public void Pan(Vector2 screenDelta, Vector2 screenPosition)
        {
            PanByScreenDelta(screenDelta, screenPosition, true);
        }

        public Vector3 SnapWorldPosition(Vector3 position, float pixelsPerUnit)
        {
            if (pixelsPerUnit <= 0f)
            {
                return position;
            }

            var step = 1f / (pixelsPerUnit * Mathf.Max(ZoomFactor, 0.0001f));
            position.x = Mathf.Round(position.x / step) * step;
            position.y = Mathf.Round(position.y / step) * step;
            return position;
        }

        private void HandlePointerGesture(PointerGesture pointerGesture)
        {
            if (pointerGesture.Kind == PointerGestureKind.Pinch)
            {
                PanByScreenDelta(pointerGesture.Delta, pointerGesture.Position, true);
                ZoomAtScreenPoint(pointerGesture.Scale, pointerGesture.Position, true);
            }
        }

        private void HandleMouseCamera()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var pointer = mouse.position.ReadValue();
            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !UiPointerBlocker.IsOverUi(pointer))
            {
                var sensitivity = zoomSensitivity * Mathf.Clamp(GameSettings.ZoomSensitivity, 0.25f, 5f);
                var factor = Mathf.Pow(1.1f, scroll / 120f * sensitivity);
                ZoomAtScreenPoint(factor, pointer, true);
            }

            if (mouse.rightButton.isPressed || mouse.middleButton.isPressed)
            {
                PanByScreenDelta(mouse.delta.ReadValue(), pointer, true);
            }
        }

        private void ZoomAtScreenPoint(float zoomFactor, Vector2 screenPosition, bool markManual)
        {
            if (zoomFactor <= 0f)
            {
                return;
            }

            var worldBefore = ScreenToWorld(screenPosition);
            tableCamera.orthographicSize = Mathf.Clamp(tableCamera.orthographicSize / zoomFactor,
                minOrthographicSize, maxOrthographicSize);
            var worldAfter = ScreenToWorld(screenPosition);
            tableCamera.transform.position += worldBefore - worldAfter;
            if (markManual)
            {
                manuallyAdjusted = true;
            }
        }

        private void PanByScreenDelta(Vector2 screenDelta, bool markManual)
        {
            var mouse = Mouse.current;
            var pointer = mouse != null
                ? mouse.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            PanByScreenDelta(screenDelta, pointer, markManual);
        }

        private void PanByScreenDelta(Vector2 screenDelta, Vector2 pointer, bool markManual)
        {
            if (screenDelta.sqrMagnitude <= 0.001f)
            {
                return;
            }

            var current = ScreenToWorld(pointer);
            var previous = ScreenToWorld(pointer - screenDelta);
            tableCamera.transform.position += previous - current;
            if (markManual)
            {
                manuallyAdjusted = true;
            }
        }

        private Vector3 ScreenToWorld(Vector2 position)
        {
            var distance = Mathf.Abs(tableCamera.transform.position.z);
            var world = tableCamera.ScreenToWorldPoint(new Vector3(position.x, position.y, distance));
            world.z = tableCamera.transform.position.z + distance;
            return world;
        }
    }
}