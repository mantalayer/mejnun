using Mejnun.Game;
using NUnit.Framework;
using UnityEngine;

namespace Mejnun.Game.EditMode.Tests
{
    public sealed class PointerRouterTests
    {
        [TestCase(2.5f, 0f, 15.74803f)]
        [TestCase(2.5f, 460f, 45.27559f)]
        [TestCase(0f, 460f, 0f)]
        public void MillimetersToPixelsUsesDeviceDpiOrFallback(float millimeters, float dpi, float expected)
        {
            Assert.That(PointerRouter.MillimetersToPixels(millimeters, dpi), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void GetEdgePanDirectionUsesAllScreenEdges()
        {
            Assert.That(PointerRouter.GetEdgePanDirection(new Vector2(500f, 400f), 1000f, 800f, 0.1f),
                Is.EqualTo(Vector2.zero));
            Assert.That(PointerRouter.GetEdgePanDirection(new Vector2(500f, 0f), 1000f, 800f, 0.1f),
                Is.EqualTo(new Vector2(0f, -1f)));
            Assert.That(PointerRouter.GetEdgePanDirection(new Vector2(0f, 0f), 1000f, 800f, 0.1f),
                Is.EqualTo(new Vector2(-1f, -1f)));
            Assert.That(PointerRouter.GetEdgePanDirection(new Vector2(0f, 400f), 1000f, 800f, 0.1f),
                Is.EqualTo(new Vector2(-1f, 0f)));
            Assert.That(PointerRouter.GetEdgePanDirection(new Vector2(1000f, 400f), 1000f, 800f, 0.1f),
                Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(PointerRouter.GetEdgePanDirection(new Vector2(500f, 800f), 1000f, 800f, 0.1f),
                Is.EqualTo(new Vector2(0f, 1f)));
        }

        [Test]
        public void GetEdgePanDirectionReachesFullSpeedHalfwayIntoTheZoneAndDisablesAtZeroWidth()
        {
            var deeper = PointerRouter.GetEdgePanDirection(new Vector2(20f, 400f), 1000f, 800f, 0.1f);
            var shallower = PointerRouter.GetEdgePanDirection(new Vector2(60f, 400f), 1000f, 800f, 0.1f);
            var halfDepth = PointerRouter.GetEdgePanDirection(new Vector2(40f, 400f), 1000f, 800f, 0.1f);

            Assert.That(Mathf.Abs(deeper.x), Is.GreaterThan(Mathf.Abs(shallower.x)));
            Assert.That(halfDepth, Is.EqualTo(new Vector2(-1f, 0f)));
            Assert.That(PointerRouter.GetEdgePanDirection(new Vector2(0f, 400f), 1000f, 800f, 0f),
                Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void GetEdgePanDirectionUsesBottomInsetAsEffectiveBottomEdge()
        {
            Assert.That(PointerRouter.GetEdgePanDirection(new Vector2(500f, 180f), 1000f, 800f, 0.1f, 100f),
                Is.EqualTo(Vector2.zero));
            Assert.That(PointerRouter.GetEdgePanDirection(new Vector2(500f, 140f), 1000f, 800f, 0.1f, 100f),
                Is.EqualTo(new Vector2(0f, -1f)));
            Assert.That(PointerRouter.GetEdgePanDirection(new Vector2(500f, 50f), 1000f, 800f, 0.1f, 100f),
                Is.EqualTo(new Vector2(0f, -1f)));
        }

        [TestCase(true, false, false, true)]
        [TestCase(true, false, true, false)]
        [TestCase(true, true, false, false)]
        [TestCase(false, false, false, false)]
        public void ShouldPanBoardDragRequiresUnselectedTouchWithoutLongPress(bool isTouch, bool isLongPress,
            bool isSelectedCard, bool expected)
        {
            Assert.That(PointerRouter.ShouldPanBoardDrag(isTouch, isLongPress, isSelectedCard),
                Is.EqualTo(expected));
        }

        [TestCase(0.1f, 0f)]
        [TestCase(0.15f, 0f)]
        [TestCase(0.375f, 0.5f)]
        [TestCase(0.6f, 1f)]
        [TestCase(0.75f, 1f)]
        public void TailHighlightProgressWaitsThenRampsToFull(float heldSeconds, float expected)
        {
            Assert.That(PointerRouter.GetTailHighlightProgress(heldSeconds,
                PointerRouter.TailHighlightDelaySeconds, PointerRouter.TailHoldSeconds),
                Is.EqualTo(expected).Within(0.001f));
        }
    }
}