using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SitecoreJobs.Foundation.Models
{
    public sealed class JobStatusSnapshot
    {
        public JobStatusSnapshot(
            Guid jobId,
            string name,
            JobState state,
            JobProgress progress,
            DateTimeOffset createdAtUtc,
            DateTimeOffset? startedAtUtc,
            DateTimeOffset? completedAtUtc,
            int warningCount,
            IEnumerable<string> warnings,
            JobError error,
            string summary)
        {
            if (jobId == Guid.Empty)
            {
                throw new ArgumentException("Job ID cannot be empty.", nameof(jobId));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Job name cannot be empty.", nameof(name));
            }

            if (warningCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(warningCount));
            }

            EnsureUtc(createdAtUtc, nameof(createdAtUtc));
            EnsureUtc(startedAtUtc, nameof(startedAtUtc));
            EnsureUtc(completedAtUtc, nameof(completedAtUtc));

            if (state != JobState.Failed && error != null)
            {
                throw new ArgumentException(
                    "Only a failed job can contain an error.",
                    nameof(error));
            }

            JobId = jobId;
            Name = name;
            State = state;
            Progress = progress;
            CreatedAtUtc = createdAtUtc;
            StartedAtUtc = startedAtUtc;
            CompletedAtUtc = completedAtUtc;
            WarningCount = warningCount;
            Warnings = CopyWarnings(warnings);
            Error = error;
            Summary = summary ?? string.Empty;
        }

        public Guid JobId { get; }

        public string Name { get; }

        public JobState State { get; }

        public JobProgress Progress { get; }

        public DateTimeOffset CreatedAtUtc { get; }

        public DateTimeOffset? StartedAtUtc { get; }

        public DateTimeOffset? CompletedAtUtc { get; }

        public int WarningCount { get; }

        public IReadOnlyCollection<string> Warnings { get; }

        public JobError Error { get; }

        public string Summary { get; }

        private static IReadOnlyCollection<string> CopyWarnings(
            IEnumerable<string> warnings)
        {
            var copy = warnings == null
                ? new List<string>()
                : new List<string>(warnings);

            return new ReadOnlyCollection<string>(copy);
        }

        private static void EnsureUtc(
            DateTimeOffset value,
            string parameterName)
        {
            if (value.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Timestamps must be UTC.",
                    parameterName);
            }
        }

        private static void EnsureUtc(
            DateTimeOffset? value,
            string parameterName)
        {
            if (value.HasValue)
            {
                EnsureUtc(value.Value, parameterName);
            }
        }
    }
}
