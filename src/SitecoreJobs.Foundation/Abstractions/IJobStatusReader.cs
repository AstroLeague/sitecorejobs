using System;
using SitecoreJobs.Foundation.Models;

namespace SitecoreJobs.Foundation.Abstractions
{
    public interface IJobStatusReader
    {
        bool TryGet(Guid jobId, out JobStatusSnapshot status);
    }
}
