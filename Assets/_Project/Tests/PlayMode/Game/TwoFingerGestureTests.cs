using Mejnun.Game;
using NUnit.Framework;
using UnityEngine;

namespace Mejnun.Game.Tests
{
    public sealed class TwoFingerGestureTests
    {
        private readonly GestureSettings settings = new GestureSettings();

        [Test]
        public void PanWithChangingFingerSpreadNeverZooms()
        {
            var gesture = new TwoFingerGesture();
            gesture.Begin(Vector2.zero, 100f, 0f);

            var update = gesture.Update(new Vector2(20f, 0f), 130f, 0.1f, settings);
            Assert.That(update.Mode, Is.EqualTo(TwoFingerGestureMode.Panning));
            Assert.That(update.Scale, Is.EqualTo(1f));

            update = gesture.Update(new Vector2(35f, 0f), 155f, 0.2f, settings);
            Assert.That(update.Mode, Is.EqualTo(TwoFingerGestureMode.Panning));
            Assert.That(update.Scale, Is.EqualTo(1f));
        }

        [Test]
        public void PureSpreadChangeZooms()
        {
            var gesture = new TwoFingerGesture();
            gesture.Begin(Vector2.zero, 100f, 0f);

            var update = gesture.Update(Vector2.zero, 130f, 0.1f, settings);
            Assert.That(update.Mode, Is.EqualTo(TwoFingerGestureMode.Zooming));
            Assert.That(update.Scale, Is.EqualTo(1f));

            update = gesture.Update(Vector2.zero, 150f, 0.2f, settings);
            Assert.That(update.Scale, Is.EqualTo(150f / 130f).Within(0.001f));
        }

        [Test]
        public void PanCanZoomAfterCenterSettlesAndPinchBegins()
        {
            var gesture = new TwoFingerGesture();
            gesture.Begin(Vector2.zero, 100f, 0f);

            var update = gesture.Update(new Vector2(20f, 0f), 130f, 0.1f, settings);
            Assert.That(update.Mode, Is.EqualTo(TwoFingerGestureMode.Panning));

            gesture.Update(new Vector2(20f, 0f), 130f, 0.2f, settings);
            update = gesture.Update(new Vector2(20f, 0f), 130f, 0.46f, settings);
            Assert.That(update.Mode, Is.EqualTo(TwoFingerGestureMode.Undecided));

            update = gesture.Update(new Vector2(20f, 0f), 155f, 0.5f, settings);
            Assert.That(update.Mode, Is.EqualTo(TwoFingerGestureMode.Zooming));
            update = gesture.Update(new Vector2(20f, 0f), 160f, 0.6f, settings);
            Assert.That(update.Scale, Is.EqualTo(160f / 155f).Within(0.001f));
        }
    }
}