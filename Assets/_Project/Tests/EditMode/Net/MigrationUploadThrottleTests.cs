using Mejnun.Net;
using NUnit.Framework;

namespace Mejnun.Net.Tests
{
    public sealed class MigrationUploadThrottleTests
    {
        [Test]
        public void ShouldUpload_SkipsUnchangedDataAndHonorsMinimumInterval()
        {
            var throttle = new MigrationUploadThrottle(3);
            var original = new byte[] { 1, 2, 3 };
            var changed = new byte[] { 1, 2, 4 };

            Assert.That(throttle.ShouldUpload(original, 0), Is.True);
            throttle.OnUploadSucceeded(original, 0);

            Assert.That(throttle.ShouldUpload(original, 3), Is.False);
            Assert.That(throttle.ShouldUpload(changed, 2.9), Is.False);
            Assert.That(throttle.ShouldUpload(changed, 3), Is.True);
        }

        [Test]
        public void ShouldUpload_UsesExponentialBackoffAfterFailures()
        {
            var throttle = new MigrationUploadThrottle(3, 2, 8);
            var data = new byte[] { 1 };

            throttle.OnUploadFailed(0);
            Assert.That(throttle.ShouldUpload(data, 1.9), Is.False);
            Assert.That(throttle.ShouldUpload(data, 2), Is.True);

            throttle.OnUploadFailed(2);
            Assert.That(throttle.ShouldUpload(data, 5.9), Is.False);
            Assert.That(throttle.ShouldUpload(data, 6), Is.True);
        }
    }
}