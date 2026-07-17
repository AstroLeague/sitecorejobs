using SitecoreJobs.Foundation.Models;

namespace SitecoreJobs.Foundation.Abstractions
{
    public interface IJobProgressReporter
    {
        void Report(JobProgress progress);

        void AddWarning(string warning);
    }
}
