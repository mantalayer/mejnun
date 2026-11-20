using Mejnun.Game;
using NUnit.Framework;
using UnityEngine;

namespace Mejnun.Game.Tests
{
    public sealed class TwoFingerGestureTests
    {
        private readonly GestureSettings settings = new GestureSettings();

        [Test]
        public void MovingFingerCenterDoesNotPanOrZoom()
        {
            var gesture = new TwoFingerGesture();
            gesture.Begin(Vector2.zero, 100f);

            var update = gesture.Update(new Vector2(20f, 0f), 100f, settings);
            Assert.That(update.Mode, Is.EqualTo(TwoFingerGestureMode.Undecided));
            Assert.That(update.Scale, Is.EqualTo(1f));
            Assert.That(update.Delta, Is.EqualTo(Vector2.zero));

            update = gesture.Update(new Vector2(40f, 0f), 100f, settings);
            Assert.That(update.Mode, Is.EqualTo(TwoFingerGestureMode.Undecided));
            Assert.That(update.Scale, Is.EqualTo(1f));
            Assert.That(update.Delta, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void PureSpreadChangeZooms()
        {
            var gesture = new TwoFingerGesture();
            gesture.Begin(Vector2.zero, 100f);

            var update = gesture.Update(Vector2.zero, 130f, settings);
            Assert.That(update.Mode, Is.EqualTo(TwoFingerGestureMode.Zooming));
            Assert.That(update.Scale, Is.EqualTo(1f));
            Assert.That(update.Delta, Is.EqualTo(Vector2.zero));

            update = gesture.Update(Vector2.zero, 150f, settings);
            Assert.That(update.Scale, Is.EqualTo(150f / 130f).Within(0.001f));
            Assert.That(update.Delta, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void DeltaIsAlwaysZero()
        {
            var gesture = new TwoFingerGesture();
            gesture.Begin(Vector2.zero, 100f);

            var update = gesture.Update(new Vector2(50f, 20f), 130f, settings);
            Assert.That(update.Delta, Is.EqualTo(Vector2.zero));

            update = gesture.Update(new Vector2(75f, 40f), 155f, settings);
            Assert.That(update.Mode, Is.EqualTo(TwoFingerGestureMode.Zooming));
            Assert.That(update.Delta, Is.EqualTo(Vector2.zero));
        }
    }
}