using NUnit.Framework;

namespace Mejnun.Game.Tests
{
    public sealed class OpeningSeedFinderTests
    {
        [TestCase(2)]
        [TestCase(6)]
        public void Find_ProducesASeedWithAnOpeningMeldForEveryHandAndDealerPath(int seatCount)
        {
            var seed = OpeningSeedFinder.Find(20261117UL, seatCount, 128);

            Assert.That(OpeningSeedFinder.EveryHandCanGoDown(seed, seatCount, false), Is.True);
            Assert.That(OpeningSeedFinder.EveryHandCanGoDown(seed, seatCount, true), Is.True);
        }
    }
}