using System;

namespace Mejnun.Net
{
    public sealed class MigrationUploadThrottle
    {
        private readonly double minimumIntervalSeconds;
        private readonly double initialFailureBackoffSeconds;
        private readonly double maximumFailureBackoffSeconds;
        private byte[] lastUploadedData;
        private double nextUploadAt;
        private int consecutiveFailures;

        public double NextUploadAt => nextUploadAt;

        public MigrationUploadThrottle(double minimumIntervalSeconds = 3,
            double initialFailureBackoffSeconds = 2, double maximumFailureBackoffSeconds = 30)
        {
            ValidatePositiveFinite(minimumIntervalSeconds, nameof(minimumIntervalSeconds));
            ValidatePositiveFinite(initialFailureBackoffSeconds, nameof(initialFailureBackoffSeconds));
            ValidatePositiveFinite(maximumFailureBackoffSeconds, nameof(maximumFailureBackoffSeconds));
            if (maximumFailureBackoffSeconds < initialFailureBackoffSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumFailureBackoffSeconds));
            }

            this.minimumIntervalSeconds = minimumIntervalSeconds;
            this.initialFailureBackoffSeconds = initialFailureBackoffSeconds;
            this.maximumFailureBackoffSeconds = maximumFailureBackoffSeconds;
        }

        public bool ShouldUpload(byte[] data, double nowSeconds)
        {
            ValidateTime(nowSeconds);
            return data != null && data.Length > 0 && !AreEqual(lastUploadedData, data) && nowSeconds >= nextUploadAt;
        }

        public bool IsUnchanged(byte[] data)
        {
            return AreEqual(lastUploadedData, data);
        }

        public void OnUploadSucceeded(byte[] data, double nowSeconds)
        {
            ValidateTime(nowSeconds);
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Migration data must not be empty.", nameof(data));
            }

            lastUploadedData = (byte[])data.Clone();
            consecutiveFailures = 0;
            nextUploadAt = nowSeconds + minimumIntervalSeconds;
        }

        public void OnUploadFailed(double nowSeconds)
        {
            ValidateTime(nowSeconds);
            consecutiveFailures = Math.Min(consecutiveFailures + 1, 30);
            var backoff = initialFailureBackoffSeconds * Math.Pow(2, consecutiveFailures - 1);
            nextUploadAt = nowSeconds + Math.Min(backoff, maximumFailureBackoffSeconds);
        }

        private static bool AreEqual(byte[] first, byte[] second)
        {
            if (first == null || second == null || first.Length != second.Length)
            {
                return false;
            }

            for (var index = 0; index < first.Length; index++)
            {
                if (first[index] != second[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static void ValidatePositiveFinite(double value, string parameterName)
        {
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void ValidateTime(double nowSeconds)
        {
            if (nowSeconds < 0 || double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(nowSeconds));
            }
        }
    }
}