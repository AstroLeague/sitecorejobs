using SitecoreJobs.Foundation.Execution;
using SitecoreJobs.Foundation.Models;

namespace SitecoreJobs.Foundation.Abstractions
{
    public interface IBackgroundJob<in TRequest>
    {
        JobResult Execute(JobExecutionContext context, TRequest request);
    }
}
