namespace SitecoreJobs.Foundation.Models
{
    public enum JobState
    {
        Queued,
        Running,
        Succeeded,
        SucceededWithWarnings,
        Failed,
        Cancelled
    }
}
