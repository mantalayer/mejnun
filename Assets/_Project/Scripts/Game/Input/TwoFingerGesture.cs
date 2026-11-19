using UnityEngine;

namespace Mejnun.Game
{
    public enum TwoFingerGestureMode
    {
        Undecided,
        Panning,
        Zooming
    }

    public readonly struct TwoFingerGestureUpdate
    {
        public TwoFingerGestureUpdate(TwoFingerGestureMode mode, Vector2 delta, float scale)
        {
            Mode = mode;
            Delta = delta;
            Scale = scale;
        }

        public TwoFingerGestureMode Mode { get; }
        public Vector2 Delta { get; }
        public float Scale { get; }
    }

    public sealed class TwoFingerGesture
    {
        private TwoFingerGestureMode mode;
        private Vector2 startCenter;
        private Vector2 previousCenter;
        private float startDistance;
        private float previousDistance;
        private float previousTime;
        private float stillStartedAt;
        private bool initialized;
        private bool trackingStill;

        public TwoFingerGestureMode Mode => mode;

        public void Begin(Vector2 center, float distance, float time)
        {
            initialized = true;
            mode = TwoFingerGestureMode.Undecided;
            startCenter = center;
            previousCenter = center;
            startDistance = distance;
            previousDistance = distance;
            previousTime = time;
            trackingStill = false;
        }

        public TwoFingerGestureUpdate Update(Vector2 center, float distance, float time,
            GestureSettings settings)
        {
            settings = settings ?? new GestureSettings();
            if (!initialized)
            {
                Begin(center, distance, time);
                return new TwoFingerGestureUpdate(mode, Vector2.zero, 1f);
            }

            var delta = center - previousCenter;
            var elapsed = time - previousTime;
            var scale = 1f;
            var rebased = false;
            if (mode == TwoFingerGestureMode.Panning)
            {
                var speed = elapsed <= 0f
                    ? delta.sqrMagnitude <= 0.001f ? 0f : float.PositiveInfinity
                    : delta.magnitude / elapsed;
                if (speed <= settings.PanStillPixelsPerSecond)
                {
                    if (!trackingStill)
                    {
                        trackingStill = true;
                        stillStartedAt = time;
                    }

                    if (time - stillStartedAt >= settings.PanSettleSeconds)
                    {
                        mode = TwoFingerGestureMode.Undecided;
                        startCenter = center;
                        startDistance = distance;
                        trackingStill = false;
                        rebased = true;
                    }
                }
                else
                {
                    trackingStill = false;
                }
            }

            if (mode == TwoFingerGestureMode.Undecided && !rebased)
            {
                if (Vector2.Distance(center, startCenter) >= Mathf.Max(1f, settings.PanLockPixels))
                {
                    mode = TwoFingerGestureMode.Panning;
                    trackingStill = false;
                }
                else if (Mathf.Abs(distance - startDistance) >=
                         Mathf.Max(1f, settings.PinchThresholdPixels))
                {
                    mode = TwoFingerGestureMode.Zooming;
                }
            }
            else if (mode == TwoFingerGestureMode.Zooming && previousDistance > 0f && distance > 0f)
            {
                scale = distance / previousDistance;
            }

            previousCenter = center;
            previousDistance = distance;
            previousTime = time;
            return new TwoFingerGestureUpdate(mode, delta, scale);
        }

        public void Reset()
        {
            initialized = false;
            mode = TwoFingerGestureMode.Undecided;
            trackingStill = false;
        }
    }
}