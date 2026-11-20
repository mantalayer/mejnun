using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Mejnun.Game
{
    [Serializable]
    public sealed class GestureSettings
    {
        [Min(1f)] public float DragThresholdPixels = 10f;
        [Min(1f)] public float PinchThresholdPixels = 24f;
        [Min(0.1f)] public float LongPressSeconds = 0.35f;
        [Min(0.1f)] public float DoubleTapSeconds = 0.3f;
        [Min(1f)] public float DoubleTapDistancePixels = 40f;
    }

    public enum PointerGestureKind
    {
        Tap,
        DoubleTap,
        LongPress,
        DragStarted,
        DragUpdated,
        DragEnded,
        Pinch,
        Cancelled
    }

    public readonly struct PointerGesture
    {
        public PointerGesture(PointerGestureKind kind, Vector2 startPosition, Vector2 position, Vector2 delta,
            bool isLongPress, float scale)
        {
            Kind = kind;
            StartPosition = startPosition;
            Position = position;
            Delta = delta;
            IsLongPress = isLongPress;
            Scale = scale;
        }

        public PointerGestureKind Kind { get; }
        public Vector2 StartPosition { get; }
        public Vector2 Position { get; }
        public Vector2 Delta { get; }
        public bool IsLongPress { get; }
        public float Scale { get; }
    }

    public sealed class PointerRouter : MonoBehaviour
    {
        [SerializeField] private GestureSettings settings = new GestureSettings();

        private bool touchSupportEnabled;
        private bool pointerDown;
        private bool pointerIsTouch;
        private int touchId;
        private int ignoredTouchId = -1;
        private bool ignoreTouchesUntilAllLifted;
        private bool dragging;
        private bool longPressStarted;
        private Vector2 pointerStart;
        private Vector2 lastPosition;
        private float pressStartedAt;
        private bool pendingTap;
        private Vector2 pendingTapPosition;
        private float pendingTapAt;
        private bool trackingPinch;
        private int firstPinchTouchId;
        private int secondPinchTouchId;
        private readonly TwoFingerGesture twoFingerGesture = new TwoFingerGesture();

        public event Action<PointerGesture> Gesture;
        public GestureSettings Settings => settings;

        public static bool IsInEdgePanZone(Vector2 position, float width, float height, float fraction)
        {
            if (width <= 0f || height <= 0f || fraction <= 0f)
            {
                return false;
            }

            var edgeSize = Mathf.Min(width, height) * fraction;
            return position.x <= edgeSize || position.x >= width - edgeSize || position.y >= height - edgeSize;
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                EnhancedTouchSupport.Enable();
                touchSupportEnabled = true;
            }
        }

        private void OnDisable()
        {
            if (touchSupportEnabled)
            {
                EnhancedTouchSupport.Disable();
                touchSupportEnabled = false;
            }

            CancelPointer();
            pendingTap = false;
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var touchActive = ProcessTouchInput();
            if (!touchActive)
            {
                ProcessMouseInput();
            }

            FlushPendingTap();
        }

        private bool ProcessTouchInput()
        {
            var touches = EnhancedTouch.activeTouches;
            if (ignoreTouchesUntilAllLifted && touches.Count < 2)
            {
                if (touches.Count == 0)
                {
                    ignoreTouchesUntilAllLifted = false;
                    ignoredTouchId = -1;
                    trackingPinch = false;
                    return false;
                }

                return true;
            }

            if (ignoredTouchId >= 0)
            {
                for (var index = 0; index < touches.Count; index++)
                {
                    var ignoredTouch = touches[index];
                    if (ignoredTouch.touchId != ignoredTouchId)
                    {
                        continue;
                    }

                    if (ignoredTouch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                        ignoredTouch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                    {
                        ignoredTouchId = -1;
                    }

                    trackingPinch = false;
                    return true;
                }

                ignoredTouchId = -1;
            }

            if (touches.Count >= 2)
            {
                ignoreTouchesUntilAllLifted = true;
                if (pointerDown)
                {
                    CancelPointer();
                }

                for (var index = 0; index < touches.Count; index++)
                {
                    if (!UiPointerBlocker.IsOverUi(touches[index].screenPosition))
                    {
                        continue;
                    }

                    ignoredTouchId = touches[index].touchId;
                    trackingPinch = false;
                    return true;
                }

                var first = touches[0].screenPosition;
                var second = touches[1].screenPosition;
                var center = (first + second) * 0.5f;
                var distance = Vector2.Distance(first, second);
                var firstTouchId = touches[0].touchId;
                var secondTouchId = touches[1].touchId;
                var touchIdsChanged = !trackingPinch || firstPinchTouchId != firstTouchId ||
                                      secondPinchTouchId != secondTouchId;
                if (touchIdsChanged)
                {
                    firstPinchTouchId = firstTouchId;
                    secondPinchTouchId = secondTouchId;
                    twoFingerGesture.Begin(center, distance);
                }
                else
                {
                    var update = twoFingerGesture.Update(center, distance, settings);
                    Emit(PointerGestureKind.Pinch, center, update.Delta, false, update.Scale);
                }

                trackingPinch = true;
                return true;
            }

            trackingPinch = false;
            twoFingerGesture.Reset();
            if (touches.Count == 0)
            {
                if (pointerDown && pointerIsTouch)
                {
                    FinishPointer(lastPosition);
                    return true;
                }

                return false;
            }

            var touch = touches[0];
            var position = touch.screenPosition;
            if (!pointerDown || !pointerIsTouch || touch.touchId != touchId)
            {
                if (pointerDown)
                {
                    CancelPointer();
                }

                if (UiPointerBlocker.IsOverUi(position))
                {
                    ignoredTouchId = touch.touchId;
                    return true;
                }

                BeginPointer(position, true, touch.touchId);
            }

            UpdatePointer(position);
            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                FinishPointer(position);
                return false;
            }

            return true;
        }

        private void ProcessMouseInput()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (UiPointerBlocker.IsOverUi(position))
                {
                    return;
                }

                BeginPointer(position, false, 0);
            }

            if (pointerDown && !pointerIsTouch)
            {
                UpdatePointer(position);
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    FinishPointer(position);
                }
            }
        }

        private void BeginPointer(Vector2 position, bool isTouch, int activeTouchId)
        {
            pointerDown = true;
            pointerIsTouch = isTouch;
            touchId = activeTouchId;
            pointerStart = position;
            lastPosition = position;
            pressStartedAt = Time.unscaledTime;
            dragging = false;
            longPressStarted = false;
        }

        private void UpdatePointer(Vector2 position)
        {
            if (!pointerDown)
            {
                return;
            }

            var delta = position - lastPosition;
            lastPosition = position;
            var distance = Vector2.Distance(pointerStart, position);
            if (!longPressStarted && !dragging && distance < settings.DragThresholdPixels &&
                Time.unscaledTime - pressStartedAt >= settings.LongPressSeconds)
            {
                longPressStarted = true;
                pendingTap = false;
                Emit(PointerGestureKind.LongPress, position, delta, true, 1f);
            }

            if (!dragging && distance >= settings.DragThresholdPixels)
            {
                dragging = true;
                pendingTap = false;
                Emit(PointerGestureKind.DragStarted, position, delta, longPressStarted, 1f);
            }
            else if (dragging && delta.sqrMagnitude > 0f)
            {
                Emit(PointerGestureKind.DragUpdated, position, delta, longPressStarted, 1f);
            }
        }

        private void FinishPointer(Vector2 position)
        {
            if (!pointerDown)
            {
                return;
            }

            if (dragging)
            {
                Emit(PointerGestureKind.DragEnded, position, position - lastPosition, longPressStarted, 1f);
            }
            else if (longPressStarted)
            {
                Emit(PointerGestureKind.Cancelled, position, Vector2.zero, true, 1f);
            }
            else
            {
                QueueTap(position);
            }

            pointerDown = false;
            dragging = false;
            longPressStarted = false;
            pointerIsTouch = false;
        }

        private void CancelPointer()
        {
            pendingTap = false;
            if (pointerDown && (dragging || longPressStarted))
            {
                Emit(PointerGestureKind.Cancelled, lastPosition, Vector2.zero, longPressStarted, 1f);
            }

            pointerDown = false;
            dragging = false;
            longPressStarted = false;
            pointerIsTouch = false;
        }

        private void QueueTap(Vector2 position)
        {
            if (pendingTap && Time.unscaledTime - pendingTapAt <= settings.DoubleTapSeconds &&
                Vector2.Distance(position, pendingTapPosition) <= settings.DoubleTapDistancePixels)
            {
                pendingTap = false;
                Emit(PointerGestureKind.DoubleTap, position, Vector2.zero, false, 1f);
                return;
            }

            pendingTap = true;
            pendingTapPosition = position;
            pendingTapAt = Time.unscaledTime;
            Emit(PointerGestureKind.Tap, position, Vector2.zero, false, 1f);
        }

        private void FlushPendingTap()
        {
            if (pendingTap && Time.unscaledTime - pendingTapAt > settings.DoubleTapSeconds)
            {
                pendingTap = false;
            }
        }

        private void Emit(PointerGestureKind kind, Vector2 position, Vector2 delta, bool isLongPress, float scale)
        {
            Gesture?.Invoke(new PointerGesture(kind, pointerStart, position, delta, isLongPress, scale));
        }
    }
}