using SitecoreJobs.Foundation.Models;

namespace SitecoreJobs.Foundation.Abstractions
{
    public interface IJobDispatcher
    {
        JobHandle Start<TRequest>(
            IBackgroundJob<TRequest> job,
            TRequest request,
            JobStartOptions options);
    }
}
