using UnityEngine;
using UnityEngine.InputSystem;

namespace Mejnun.Game
{
    [RequireComponent(typeof(Camera))]
    public sealed class TableCamera : MonoBehaviour
    {
        private const float FitDuration = 0.4f;
        private const float PanInertiaTimeConstant = 0.12f;
        private const float PanVelocitySmoothingTime = 0.04f;
        private const float MaxPanInertiaSpeed = 8f;
        private const float MinPanInertiaSpeed = 0.03f;
        private const float RecentPanMovementWindow = 0.08f;
        private const float BoardPanMargin = 2f;

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
        private bool holdFeltAnchor;
        private Vector3 panVelocity;
        private float lastPanMovementTime;
        private bool hasPanMovement;
        private bool panTracking;
        private bool panInertiaActive;
        private bool mousePanActive;

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

            CancelPanInertia();
            mousePanActive = false;
        }

        private void Update()
        {
            HandleMouseCamera();
        }

        private void LateUpdate()
        {
            if (tableCamera == null)
            {
                return;
            }

            if (fitting)
            {
                if (GameSettings.ReducedMotion)
                {
                    ApplyFitTarget();
                }
                else
                {
                    var progress = Mathf.Clamp01((Time.unscaledTime - fitStartTime) / FitDuration);
                    if (progress >= 1f)
                    {
                        ApplyFitTarget();
                    }
                    else
                    {
                        var remaining = 1f - progress;
                        var eased = 1f - remaining * remaining * remaining;
                        FrameRateGovernor.NotifyActivity();
                        tableCamera.transform.position =
                            Vector3.LerpUnclamped(fitStartPosition, fitTargetPosition, eased);
                        tableCamera.orthographicSize = Mathf.LerpUnclamped(fitStartSize, fitTargetSize, eased);
                        baseOrthographicSize = tableCamera.orthographicSize;
                    }
                }
            }

            if (!panInertiaActive)
            {
                return;
            }

            if (GameSettings.ReducedMotion)
            {
                CancelPanInertia();
                return;
            }

            var deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            tableCamera.transform.position += panVelocity * deltaTime;
            FrameRateGovernor.NotifyActivity();
            if (ClampCameraToBoard())
            {
                CancelPanInertia();
                return;
            }

            panVelocity *= Mathf.Exp(-deltaTime / PanInertiaTimeConstant);
            if (panVelocity.sqrMagnitude <= MinPanInertiaSpeed * MinPanInertiaSpeed)
            {
                CancelPanInertia();
                return;
            }
        }

        public void SetBoardBounds(Bounds bounds)
        {
            boardBounds = bounds;
            hasBoardBounds = true;
            AutoFit(bounds);
        }

        public void AutoFit(Bounds bounds)
        {
            if (!holdFeltAnchor && !manuallyAdjusted)
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

        public static Vector2 ClampViewCenter(Vector2 center, Vector2 halfVisible, Bounds bounds, float margin)
        {
            margin = Mathf.Max(0f, margin);
            halfVisible.x = Mathf.Max(0f, halfVisible.x);
            halfVisible.y = Mathf.Max(0f, halfVisible.y);
            var minX = bounds.min.x - margin + halfVisible.x;
            var maxX = bounds.max.x + margin - halfVisible.x;
            var minY = bounds.min.y - margin + halfVisible.y;
            var maxY = bounds.max.y + margin - halfVisible.y;
            if (minX > maxX)
            {
                minX = maxX = bounds.center.x;
            }

            if (minY > maxY)
            {
                minY = maxY = bounds.center.y;
            }

            return new Vector2(Mathf.Clamp(center.x, minX, maxX), Mathf.Clamp(center.y, minY, maxY));
        }

        private bool ClampCameraToBoard()
        {
            if (!hasBoardBounds)
            {
                return false;
            }

            var visibleRect = GetVisibleBoardWorldRect();
            var targetCenter = ClampViewCenter(visibleRect.center, visibleRect.size * 0.5f, boardBounds,
                BoardPanMargin);
            var offset = targetCenter - visibleRect.center;
            if (offset.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            tableCamera.transform.position += new Vector3(offset.x, offset.y, 0f);
            return true;
        }

        public void FitBoard()
        {
            if (!holdFeltAnchor && hasBoardBounds)
            {
                FitBounds(boardBounds, fitPadding, true);
            }
        }

        public void FitFeltAnchor()
        {
            holdFeltAnchor = true;
            CancelPanInertia();
            if (tableCamera == null)
            {
                tableCamera = GetComponent<Camera>();
            }

            tableCamera.orthographic = true;
            var animate = Application.isPlaying && hasFittedBounds && !GameSettings.ReducedMotion;
            var targetChanged = !fitting ||
                                Vector3.SqrMagnitude(fitTargetPosition - feltAnchorPosition) > 0.000001f ||
                                Mathf.Abs(fitTargetSize - feltAnchorSize) > 0.0001f;
            if (animate && targetChanged)
            {
                fitStartPosition = tableCamera.transform.position;
                fitStartSize = tableCamera.orthographicSize;
                fitStartTime = Time.unscaledTime;
            }

            fitTargetPosition = feltAnchorPosition;
            fitTargetSize = feltAnchorSize;
            fitting = animate;
            manuallyAdjusted = false;
            if (!fitting)
            {
                ApplyFitTarget();
            }
        }

        public void ReleaseFeltAnchor()
        {
            holdFeltAnchor = false;
            FitBoard();
        }

        public void FitBounds(Bounds bounds, float padding = 1.1f, bool resetManualAdjustment = true)
        {
            CancelPanInertia();
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
            PanByScreenDelta(screenDelta, true, true);
        }

        public void Pan(Vector2 screenDelta, Vector2 screenPosition, bool trackForInertia = true)
        {
            PanByScreenDelta(screenDelta, screenPosition, true, trackForInertia);
        }

        public void BeginPan()
        {
            CancelPanInertia();
            panTracking = true;
        }

        public void ReleasePan()
        {
            var canCoast = panTracking && hasPanMovement && !GameSettings.ReducedMotion &&
                           Time.unscaledTime - lastPanMovementTime < RecentPanMovementWindow &&
                           panVelocity.sqrMagnitude > MinPanInertiaSpeed * MinPanInertiaSpeed;
            panTracking = false;
            hasPanMovement = false;
            panInertiaActive = canCoast;
            if (!canCoast)
            {
                panVelocity = Vector3.zero;
            }
        }

        public void CancelPanInertia()
        {
            panVelocity = Vector3.zero;
            lastPanMovementTime = 0f;
            hasPanMovement = false;
            panTracking = false;
            panInertiaActive = false;
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
                if (mousePanActive)
                {
                    CancelPanInertia();
                    mousePanActive = false;
                }

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

            var isPanning = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
            if (isPanning)
            {
                if (!mousePanActive)
                {
                    BeginPan();
                    mousePanActive = true;
                }

                PanByScreenDelta(mouse.delta.ReadValue(), pointer, true, true);
            }
            else if (mousePanActive)
            {
                ReleasePan();
                mousePanActive = false;
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
                CancelPanInertia();
                FrameRateGovernor.NotifyActivity();
                fitting = false;
            }

            var worldBefore = ScreenToWorld(screenPosition);
            tableCamera.orthographicSize = Mathf.Clamp(tableCamera.orthographicSize / zoomFactor,
                minOrthographicSize, maxOrthographicSize);
            var worldAfter = ScreenToWorld(screenPosition);
            tableCamera.transform.position += worldBefore - worldAfter;
            ClampCameraToBoard();
            if (markManual)
            {
                manuallyAdjusted = true;
            }
        }

        private void PanByScreenDelta(Vector2 screenDelta, bool markManual, bool trackForInertia)
        {
            var mouse = Mouse.current;
            var pointer = mouse != null
                ? mouse.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            PanByScreenDelta(screenDelta, pointer, markManual, trackForInertia);
        }

        private void PanByScreenDelta(Vector2 screenDelta, Vector2 pointer, bool markManual,
            bool trackForInertia)
        {
            if (screenDelta.sqrMagnitude <= 0.001f)
            {
                return;
            }

            if (markManual)
            {
                if (!trackForInertia)
                {
                    CancelPanInertia();
                }

                FrameRateGovernor.NotifyActivity();
                fitting = false;
            }

            var current = ScreenToWorld(pointer);
            var previous = ScreenToWorld(pointer - screenDelta);
            var worldDelta = previous - current;
            tableCamera.transform.position += worldDelta;
            if (trackForInertia)
            {
                if (!panTracking)
                {
                    BeginPan();
                }

                var deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.001f);
                var smoothing = 1f - Mathf.Exp(-deltaTime / PanVelocitySmoothingTime);
                var sampledVelocity = worldDelta / deltaTime;
                panVelocity = Vector3.ClampMagnitude(
                    Vector3.Lerp(panVelocity, sampledVelocity, smoothing), MaxPanInertiaSpeed);
                lastPanMovementTime = Time.unscaledTime;
                hasPanMovement = true;
            }

            if (ClampCameraToBoard())
            {
                panVelocity = Vector3.zero;
            }

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