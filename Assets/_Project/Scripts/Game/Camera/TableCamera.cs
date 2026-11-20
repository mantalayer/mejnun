using UnityEngine;
using UnityEngine.InputSystem;

namespace Mejnun.Game
{
    [RequireComponent(typeof(Camera))]
    public sealed class TableCamera : MonoBehaviour
    {
        private const float FitDuration = 0.4f;

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
        private float topInsetFraction;
        private float leftInsetFraction;
        private float rightInsetFraction;
        private float bottomInsetFloor;
        private Vector3 fitStartPosition;
        private float fitStartSize;
        private float fitStartTime;
        private Vector3 fitTargetPosition;
        private float fitTargetSize;
        private Vector3 feltAnchorPosition;
        private float feltAnchorSize;
        private bool fitting;
        private bool hasFittedBounds;
        private bool hasFeltAnchor;

        public Camera Camera => tableCamera;
        public float BottomInsetFraction => bottomInsetFraction;
        public float BottomInsetFloor => bottomInsetFloor;
        public float TopInsetFraction => topInsetFraction;
        public float LeftInsetFraction => leftInsetFraction;
        public float RightInsetFraction => rightInsetFraction;
        public float ViewportAspect => tableCamera == null ? 1f : tableCamera.aspect;
        public float MaxOrthographicSize => maxOrthographicSize;
        public bool ManuallyAdjusted => manuallyAdjusted;
        public Vector3 FeltAnchorPosition => feltAnchorPosition;
        public float FeltAnchorSize => feltAnchorSize;
        public float ZoomFactor => baseOrthographicSize <= 0f
            ? 1f
            : baseOrthographicSize / Mathf.Max(tableCamera.orthographicSize, 0.0001f);

        private void Awake()
        {
            tableCamera = GetComponent<Camera>();
            tableCamera.orthographic = true;
            baseOrthographicSize = tableCamera.orthographicSize;
            feltAnchorPosition = tableCamera.transform.position;
            feltAnchorSize = tableCamera.orthographicSize;
            bottomInsetFloor = bottomInsetFraction;
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

        private void LateUpdate()
        {
            if (!fitting || tableCamera == null)
            {
                return;
            }

            if (GameSettings.ReducedMotion)
            {
                ApplyFitTarget();
                return;
            }

            var progress = Mathf.Clamp01((Time.unscaledTime - fitStartTime) / FitDuration);
            if (progress >= 1f)
            {
                ApplyFitTarget();
                return;
            }

            var remaining = 1f - progress;
            var eased = 1f - remaining * remaining * remaining;
            FrameRateGovernor.NotifyActivity();
            tableCamera.transform.position = Vector3.LerpUnclamped(fitStartPosition, fitTargetPosition, eased);
            tableCamera.orthographicSize = Mathf.LerpUnclamped(fitStartSize, fitTargetSize, eased);
            baseOrthographicSize = tableCamera.orthographicSize;
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

        public void SetViewportInsets(float top, float bottom, float left, float right)
        {
            var nextTop = Mathf.Clamp(top, 0f, 0.45f);
            var nextBottom = Mathf.Max(bottomInsetFloor, Mathf.Clamp(bottom, 0f, 0.45f));
            var nextLeft = Mathf.Clamp(left, 0f, 0.45f);
            var nextRight = Mathf.Clamp(right, 0f, 0.45f);
            if (Mathf.Approximately(topInsetFraction, nextTop) &&
                Mathf.Approximately(bottomInsetFraction, nextBottom) &&
                Mathf.Approximately(leftInsetFraction, nextLeft) &&
                Mathf.Approximately(rightInsetFraction, nextRight))
            {
                return;
            }

            topInsetFraction = nextTop;
            bottomInsetFraction = nextBottom;
            leftInsetFraction = nextLeft;
            rightInsetFraction = nextRight;
            if (hasBoardBounds)
            {
                AutoFit(boardBounds);
            }
        }

        public Rect GetVisibleBoardWorldRect()
        {
            if (tableCamera == null)
            {
                tableCamera = GetComponent<Camera>();
            }

            var distance = Mathf.Abs(tableCamera.transform.position.z);
            var bottomLeft = tableCamera.ViewportToWorldPoint(
                new Vector3(leftInsetFraction, bottomInsetFraction, distance));
            var bottomRight = tableCamera.ViewportToWorldPoint(
                new Vector3(1f - rightInsetFraction, bottomInsetFraction, distance));
            var topLeft = tableCamera.ViewportToWorldPoint(
                new Vector3(leftInsetFraction, 1f - topInsetFraction, distance));
            var topRight = tableCamera.ViewportToWorldPoint(
                new Vector3(1f - rightInsetFraction, 1f - topInsetFraction, distance));
            return Rect.MinMaxRect(
                Mathf.Min(bottomLeft.x, bottomRight.x, topLeft.x, topRight.x),
                Mathf.Min(bottomLeft.y, bottomRight.y, topLeft.y, topRight.y),
                Mathf.Max(bottomLeft.x, bottomRight.x, topLeft.x, topRight.x),
                Mathf.Max(bottomLeft.y, bottomRight.y, topLeft.y, topRight.y));
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
            var availableHeight = Mathf.Max(1f - topInsetFraction - bottomInsetFraction, 0.1f);
            var availableWidth = Mathf.Max(1f - leftInsetFraction - rightInsetFraction, 0.1f);
            var verticalSize = Mathf.Max(bounds.size.y * 0.5f / availableHeight, 0.5f);
            var horizontalSize = Mathf.Max(bounds.size.x * 0.5f / (aspect * availableWidth), 0.5f);
            var fitSize = Mathf.Clamp(Mathf.Max(verticalSize, horizontalSize) * padding,
                minOrthographicSize, maxOrthographicSize);
            tableCamera.orthographic = true;
            var position = tableCamera.transform.position;
            position.x = bounds.center.x + fitSize * aspect * (rightInsetFraction - leftInsetFraction);
            position.y = bounds.center.y + fitSize * (topInsetFraction - bottomInsetFraction);
            var animate = Application.isPlaying && hasFittedBounds && !GameSettings.ReducedMotion;
            var targetChanged = !fitting ||
                                Vector3.SqrMagnitude(fitTargetPosition - position) > 0.000001f ||
                                Mathf.Abs(fitTargetSize - fitSize) > 0.0001f;
            if (animate && targetChanged)
            {
                fitStartPosition = tableCamera.transform.position;
                fitStartSize = tableCamera.orthographicSize;
                fitStartTime = Time.unscaledTime;
            }

            fitTargetPosition = position;
            fitTargetSize = fitSize;
            fitting = animate;
            hasFittedBounds = true;
            if (resetManualAdjustment)
            {
                manuallyAdjusted = false;
            }

            if (!fitting)
            {
                ApplyFitTarget();
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
            if (zoomFactor <= 0f || Mathf.Approximately(zoomFactor, 1f))
            {
                return;
            }

            if (markManual)
            {
                FrameRateGovernor.NotifyActivity();
                fitting = false;
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

            if (markManual)
            {
                FrameRateGovernor.NotifyActivity();
                fitting = false;
            }

            var current = ScreenToWorld(pointer);
            var previous = ScreenToWorld(pointer - screenDelta);
            tableCamera.transform.position += previous - current;
            if (markManual)
            {
                manuallyAdjusted = true;
            }
        }

        private void ApplyFitTarget()
        {
            tableCamera.transform.position = fitTargetPosition;
            tableCamera.orthographicSize = fitTargetSize;
            if (!hasFeltAnchor)
            {
                feltAnchorPosition = fitTargetPosition;
                feltAnchorSize = fitTargetSize;
                hasFeltAnchor = true;
            }

            baseOrthographicSize = fitTargetSize;
            fitting = false;
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