using Mejnun.Localization;
using NUnit.Framework;
using UnityEngine;

namespace Mejnun.Game.Tests
{
    public sealed class MatchFlowTimeTests
    {
        [Test]
        public void FormatDurationUsesMinutesAndHoursAndFloorsFractionalSeconds()
        {
            Assert.That(MatchFlow.FormatDuration(-1f), Is.EqualTo($"{Number(0)}:{TwoDigits(0)}"));
            Assert.That(MatchFlow.FormatDuration(61.9f), Is.EqualTo($"{Number(1)}:{TwoDigits(1)}"));
            Assert.That(MatchFlow.FormatDuration(3599f), Is.EqualTo($"{Number(59)}:{TwoDigits(59)}"));
            Assert.That(MatchFlow.FormatDuration(3600f),
                Is.EqualTo($"{Number(1)}:{TwoDigits(0)}:{TwoDigits(0)}"));
            Assert.That(MatchFlow.FormatDuration(3661f),
                Is.EqualTo($"{Number(1)}:{TwoDigits(1)}:{TwoDigits(1)}"));
        }

        [Test]
        public void MatchClockKeepsRunningInOnlinePauseMenuButStopsOffline()
        {
            Assert.That(MatchFlow.ShouldRunMatchClock(true, true, false, false), Is.True);
            Assert.That(MatchFlow.ShouldRunMatchClock(true, true, true, false), Is.False);
            Assert.That(MatchFlow.ShouldRunMatchClock(false, true, false, false), Is.False);
            Assert.That(MatchFlow.ShouldRunMatchClock(false, false, false, false), Is.True);
            Assert.That(MatchFlow.ShouldRunMatchClock(true, false, false, true), Is.False);
        }

        private static string Number(int value)
        {
            return Loc.Number(value);
        }

        private static string TwoDigits(int value)
        {
            return Loc.Number(value).PadLeft(2, Loc.Number(0)[0]);
        }
    }

    public sealed class TableCameraPanBoundsTests
    {
        [Test]
        public void ClampViewCenterKeepsViewportWithinBoardMargin()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(20f, 10f, 0f));

            var clamped = TableCamera.ClampViewCenter(new Vector2(100f, -100f), new Vector2(4f, 3f), bounds, 2f);

            Assert.That(clamped.x, Is.EqualTo(8f).Within(0.0001f));
            Assert.That(clamped.y, Is.EqualTo(-4f).Within(0.0001f));

            var oversizedViewport = TableCamera.ClampViewCenter(Vector2.one * 100f, new Vector2(20f, 20f), bounds,
                2f);

            Assert.That(oversizedViewport, Is.EqualTo(Vector2.zero));
        }
    }
}