using System;

namespace SitecoreJobs.Foundation.Models
{
    public sealed class JobResult
    {
        private JobResult(bool hasWarnings, int warningCount, string summary)
        {
            if (warningCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(warningCount),
                    "Warning count cannot be negative.");
            }

            if (hasWarnings && warningCount == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(warningCount),
                    "A warning result must contain at least one warning.");
            }

            HasWarnings = hasWarnings;
            WarningCount = warningCount;
            Summary = summary ?? string.Empty;
        }

        public bool HasWarnings { get; }

        public int WarningCount { get; }

        public string Summary { get; }

        public static JobResult Success(string summary = null)
        {
            return new JobResult(false, 0, summary);
        }

        public static JobResult SuccessWithWarnings(
            string summary = null,
            int warningCount = 1)
        {
            return new JobResult(true, warningCount, summary);
        }
    }
}
