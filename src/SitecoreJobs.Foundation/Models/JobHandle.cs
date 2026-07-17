using System;

namespace SitecoreJobs.Foundation.Models
{
    public sealed class JobHandle
    {
        public JobHandle(Guid jobId)
        {
            if (jobId == Guid.Empty)
            {
                throw new ArgumentException("Job ID cannot be empty.", nameof(jobId));
            }

            JobId = jobId;
        }

        public Guid JobId { get; }
    }
}
