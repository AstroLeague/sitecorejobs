using System;

namespace SitecoreJobs.Foundation.State
{
    [Serializable]
    public sealed class JobAlreadyRunningException : InvalidOperationException
    {
        public JobAlreadyRunningException(
            string concurrencyKey,
            Guid existingJobId)
            : base(
                "A job is already running for concurrency key '"
                + concurrencyKey
                + "'.")
        {
            if (string.IsNullOrWhiteSpace(concurrencyKey))
            {
                throw new ArgumentException(
                    "Concurrency key cannot be empty.",
                    nameof(concurrencyKey));
            }

            if (existingJobId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Existing job ID cannot be empty.",
                    nameof(existingJobId));
            }

            ConcurrencyKey = concurrencyKey;
            ExistingJobId = existingJobId;
        }

        public string ConcurrencyKey { get; }

        public Guid ExistingJobId { get; }
    }
}
