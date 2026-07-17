# Adding a job

Define a request containing all inputs and a stateless transient job:

```csharp
public sealed class RebuildSummaryRequest
{
    public string DatabaseName { get; set; }
}

public sealed class RebuildSummaryJob
    : IBackgroundJob<RebuildSummaryRequest>
{
    public JobResult Execute(
        JobExecutionContext context,
        RebuildSummaryRequest request)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        for (var i = 1; i <= 1000; i++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            context.Progress.Report(
                new JobProgress(
                    i,
                    1000,
                    "Processing item " + i,
                    null));
        }

        return JobResult.Success("Summary rebuild completed.");
    }
}
```

Register `RebuildSummaryJob` as transient using the consuming feature's normal
Sitecore services configurator. Inject the DI-created instance into a feature
service and dispatch it:

```csharp
public sealed class RebuildSummaryService
{
    private readonly IJobDispatcher _dispatcher;
    private readonly RebuildSummaryJob _job;

    public RebuildSummaryService(
        IJobDispatcher dispatcher,
        RebuildSummaryJob job)
    {
        _dispatcher = dispatcher
            ?? throw new ArgumentNullException(nameof(dispatcher));
        _job = job
            ?? throw new ArgumentNullException(nameof(job));
    }

    public JobHandle Start(RebuildSummaryRequest request)
    {
        return _dispatcher.Start(
            _job,
            request,
            JobStartOptions.SingleInstance(
                "Rebuild Summary",
                "RebuildSummary"));
    }
}
```

Use a key that identifies the protected resource. For example,
`ContentCleanup.Master` and `ContentCleanup.Core` may run together, while two
active jobs using `ContentCleanup.Master` cannot.

Read status by framework job ID:

```csharp
JobStatusSnapshot status;
if (_jobStatusReader.TryGet(handle.JobId, out status))
{
    // Render or log the immutable snapshot.
}
```

Use `context.Progress.AddWarning(...)` for bounded warning details and return
`JobResult.SuccessWithWarnings(...)` when the overall outcome needs review.
Let unexpected exceptions escape the feature job; the execution wrapper logs
the complete exception and records a safe failure status.
