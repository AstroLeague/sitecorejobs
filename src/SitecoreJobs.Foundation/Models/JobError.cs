using System;

namespace SitecoreJobs.Foundation.Models
{
    public sealed class JobError
    {
        public JobError(string message, DateTimeOffset occurredAtUtc)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException(
                    "Error message cannot be empty.",
                    nameof(message));
            }

            if (occurredAtUtc.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "The error timestamp must be UTC.",
                    nameof(occurredAtUtc));
            }

            Message = message;
            OccurredAtUtc = occurredAtUtc;
        }

        public string Message { get; }

        public DateTimeOffset OccurredAtUtc { get; }
    }
}
