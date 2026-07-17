using System;
using System.Threading;
using SitecoreJobs.Foundation.Abstractions;

namespace SitecoreJobs.Foundation.Execution
{
    public sealed class JobExecutionContext
    {
        public JobExecutionContext(
            Guid jobId,
            IJobProgressReporter progress,
            CancellationToken cancellationToken)
        {
            if (jobId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Job ID cannot be empty.",
                    nameof(jobId));
            }

            JobId = jobId;
            Progress = progress
                ?? throw new ArgumentNullException(nameof(progress));
            CancellationToken = cancellationToken;
        }

        public Guid JobId { get; }

        public IJobProgressReporter Progress { get; }

        public CancellationToken CancellationToken { get; }
    }
}
