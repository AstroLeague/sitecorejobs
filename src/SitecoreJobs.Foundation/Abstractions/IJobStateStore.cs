using System;
using SitecoreJobs.Foundation.Models;

namespace SitecoreJobs.Foundation.Abstractions
{
    public interface IJobStateStore : IJobStatusReader
    {
        JobHandle Create(JobStartOptions options);

        void MarkRunning(Guid jobId);

        void ReportProgress(Guid jobId, JobProgress progress);

        void AddWarning(Guid jobId, string warning);

        void Complete(Guid jobId, JobResult result);

        void Fail(Guid jobId, JobError error);

        void Cancel(Guid jobId);

        void EnsureFailed(Guid jobId, JobError error);
    }
}
