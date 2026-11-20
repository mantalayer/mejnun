using UnityEngine;

namespace Mejnun.Game
{
    public enum TwoFingerGestureMode
    {
        Undecided,
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
        private float startDistance;
        private float previousDistance;
        private bool initialized;

        public TwoFingerGestureMode Mode => mode;

        public void Begin(Vector2 center, float distance)
        {
            initialized = true;
            mode = TwoFingerGestureMode.Undecided;
            startDistance = distance;
            previousDistance = distance;
        }

        public TwoFingerGestureUpdate Update(Vector2 center, float distance, GestureSettings settings)
        {
            settings = settings ?? new GestureSettings();
            if (!initialized)
            {
                Begin(center, distance);
                return new TwoFingerGestureUpdate(mode, Vector2.zero, 1f);
            }

            var scale = 1f;
            if (mode == TwoFingerGestureMode.Undecided)
            {
                if (Mathf.Abs(distance - startDistance) >= Mathf.Max(1f, settings.PinchThresholdPixels))
                {
                    mode = TwoFingerGestureMode.Zooming;
                }
            }
            else if (mode == TwoFingerGestureMode.Zooming && previousDistance > 0f && distance > 0f)
            {
                scale = distance / previousDistance;
            }

            previousDistance = distance;
            return new TwoFingerGestureUpdate(mode, Vector2.zero, scale);
        }

        public void Reset()
        {
            initialized = false;
            mode = TwoFingerGestureMode.Undecided;
        }
    }
}