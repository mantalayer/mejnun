using System;
using NUnit.Framework;

namespace Mejnun.Core.Tests
{
    public sealed class RngTests
    {
        [Test]
        public void Pcg32_SameSeedProducesTheSameSequence()
        {
            var first = new Pcg32(123456789UL);
            var second = new Pcg32(123456789UL);

            for (var index = 0; index < 100; index++)
            {
                Assert.That(first.NextUInt(), Is.EqualTo(second.NextUInt()));
            }
        }

        [Test]
        public void Pcg32_DifferentSeedsProduceDifferentSequences()
        {
            var first = new Pcg32(1UL);
            var second = new Pcg32(2UL);

            Assert.That(first.NextUInt(), Is.Not.EqualTo(second.NextUInt()));
        }

        [Test]
        public void Pcg32_StateRestoreResumesTheSequence()
        {
            var original = new Pcg32(987654321UL);
            original.NextUInt();
            original.NextUInt();
            var restored = Pcg32.FromState(original.State, original.Increment);

            for (var index = 0; index < 100; index++)
            {
                Assert.That(restored.NextUInt(), Is.EqualTo(original.NextUInt()));
            }
        }

        [Test]
        public void Pcg32_NextIntReturnsValuesWithinTheExclusiveBound()
        {
            var rng = new Pcg32(42UL);

            for (var index = 0; index < 1000; index++)
            {
                var value = rng.NextInt(7);
                Assert.That(value, Is.GreaterThanOrEqualTo(0));
                Assert.That(value, Is.LessThan(7));
            }
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Pcg32_NextIntRejectsNonPositiveBounds(int bound)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Pcg32(3UL).NextInt(bound));
        }

        [Test]
        public void Pcg32_FromStateRejectsEvenIncrement()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Pcg32.FromState(0UL, 2UL));
        }
    }
}