# Build Specification: Reusable Sitecore Jobs Foundation

> This document is intended to be given directly to Cursor, Codex, or another coding agent.
>
> Read the entire specification before creating or changing files.

---

## 1. Objective

Build a small, reusable foundation for running and tracking background jobs in Sitecore.

The foundation must use Sitecore's out-of-the-box job engine under the hood:

```text
Sitecore.Jobs.JobOptions
Sitecore.Jobs.JobManager.Start(...)
Sitecore.Context.Job
```

Do **not** build a replacement job engine.

Do **not** use:

```text
Task.Run
Thread
ThreadPool
A custom worker service
A custom queue processor
Hangfire
Quartz
```

This phase is only for the universal jobs foundation.

Do **not** implement Bulk Retention in this phase. Bulk Retention will be added later as a separate feature that consumes this foundation.

---

## 2. Target Technology

Build for:

```text
Sitecore XP/XM: 10.3
.NET Framework: 4.8
Language compatibility: C# 7.3-compatible syntax
Runtime topology: Single CM instance
Dependency injection: Sitecore built-in DI
```

Avoid syntax and framework features that may require modern .NET or a newer compiler, including:

```text
records
init-only properties
file-scoped namespaces
required properties
top-level statements
nullable reference type dependence
```

Use normal classes, constructor guards, and immutable public models.

---

## 3. Repository and Project Naming

Use the following neutral project name so the repository can be reused across different Sitecore solutions:

```text
SitecoreJobs.Foundation
```

Recommended repository name:

```text
sitecore-jobs-foundation
```

Do not introduce client-specific, company-specific, or feature-specific names into the reusable project.

If the host solution requires a different namespace, keep the implementation organized so the root namespace can be renamed without changing behavior.

---

## 4. Required Repository Structure

Create the following structure:

```text
sitecore-jobs-foundation
|
|-- src
|   `-- SitecoreJobs.Foundation
|       |
|       |-- Abstractions
|       |   |-- IBackgroundJob.cs
|       |   |-- IJobDispatcher.cs
|       |   |-- IJobProgressReporter.cs
|       |   |-- IJobStateStore.cs
|       |   `-- IJobStatusReader.cs
|       |
|       |-- Execution
|       |   |-- JobExecutionContext.cs
|       |   |-- JobProgressReporter.cs
|       |   |-- SitecoreJobDispatcher.cs
|       |   `-- SitecoreJobExecution.cs
|       |
|       |-- Models
|       |   |-- JobConcurrencyMode.cs
|       |   |-- JobError.cs
|       |   |-- JobHandle.cs
|       |   |-- JobProgress.cs
|       |   |-- JobResult.cs
|       |   |-- JobStartOptions.cs
|       |   |-- JobState.cs
|       |   `-- JobStatusSnapshot.cs
|       |
|       |-- State
|       |   |-- InMemoryJobStateStore.cs
|       |   `-- JobAlreadyRunningException.cs
|       |
|       |-- Configuration
|       |   `-- SitecoreJobSettings.cs
|       |
|       |-- DependencyInjection
|       |   `-- ServicesConfigurator.cs
|       |
|       |-- App_Config
|       |   `-- Include
|       |       `-- Foundation
|       |           `-- SitecoreJobs.Foundation.config
|       |
|       `-- SitecoreJobs.Foundation.csproj
|
|-- tests
|   `-- SitecoreJobs.Foundation.Tests
|       |-- State
|       |   `-- InMemoryJobStateStoreTests.cs
|       |
|       |-- Execution
|       |   |-- JobProgressReporterTests.cs
|       |   `-- SitecoreJobExecutionTests.cs
|       |
|       `-- SitecoreJobs.Foundation.Tests.csproj
|
|-- docs
|   |-- architecture.md
|   `-- adding-a-job.md
|
|-- README.md
|-- .gitignore
`-- SitecoreJobs.Foundation.sln
```

Do not add more projects or folders unless the existing repository structure requires them.

Do not create a WebForms control, API endpoint, database project, sample website, or Bulk Retention project in this phase.

---

## 5. Architectural Principle

The final execution flow must be:

```text
Feature or application code
          |
          v
IJobDispatcher
          |
          v
SitecoreJobDispatcher
          |
          v
Sitecore.Jobs.JobOptions
          |
          v
Sitecore.Jobs.JobManager.Start(...)
          |
          v
SitecoreJobExecution
          |
          v
IBackgroundJob<TRequest>
```

Responsibilities must remain separated:

| Component | Responsibility |
|---|---|
| Sitecore OOTB Jobs | Runs the work in the background |
| `IJobDispatcher` | Provides one safe way to start a job |
| `SitecoreJobExecution` | Owns lifecycle, exception handling, and final status |
| `IBackgroundJob<TRequest>` | Defines feature-specific work |
| `IJobProgressReporter` | Reports progress without exposing mutable state |
| `IJobStateStore` | Stores active and recent job status |
| `IJobStatusReader` | Provides read-only status access to callers |

---

## 6. Keep the Design Small

Do not create:

```text
JobManager
JobRunner
JobCoordinator
JobOrchestrator
JobPipeline
JobProcessor
JobHandler
JobFactory
JobRepository
JobEventBus
JobPluginLoader
BatchedJobBase
SitecoreJobBase
```

unless a class is explicitly required by this specification.

There must be:

- One public dispatcher
- One execution wrapper
- One in-memory state store
- One progress reporter
- Small immutable models
- Small interfaces at real architectural boundaries

Prefer composition over inheritance.

Do not create a base class for feature jobs.

---

## 7. Public Job Contract

Create:

```csharp
public interface IBackgroundJob<in TRequest>
{
    JobResult Execute(
        JobExecutionContext context,
        TRequest request);
}
```

Rules:

- `TRequest` contains all input required by the job.
- The request must not be null.
- The job implementation should be stateless.
- All execution-specific state must remain local to `Execute`.
- An unhandled exception means the job failed.
- A successful return value determines whether the job completed normally or with warnings.
- The interface must not expose Sitecore's `Job` type to feature code.

---

## 8. Job Dispatcher Contract

Create:

```csharp
public interface IJobDispatcher
{
    JobHandle Start<TRequest>(
        IBackgroundJob<TRequest> job,
        TRequest request,
        JobStartOptions options);
}
```

This design intentionally accepts the DI-created job instance.

This avoids:

- Reflection-based job discovery
- A custom service locator
- A generic job factory
- Hard-coded type-name resolution
- Request serialization

The dispatcher must reject:

- A null job
- A null request
- Null start options
- Empty or whitespace job names
- Invalid concurrency configuration

Feature jobs must be registered as transient and must be stateless.

---

## 9. Sitecore Dispatcher Implementation

Create `SitecoreJobDispatcher`.

Its responsibilities are:

1. Validate inputs.
2. Create an initial framework job record through `IJobStateStore`.
3. Create a `SitecoreJobExecution<TRequest>` object.
4. Create Sitecore `JobOptions`.
5. Call `Sitecore.Jobs.JobManager.Start(...)`.
6. Return the framework `JobHandle`.
7. If Sitecore fails to start the job, mark the framework job as failed and release its concurrency key.

Conceptual implementation:

```csharp
public JobHandle Start<TRequest>(
    IBackgroundJob<TRequest> job,
    TRequest request,
    JobStartOptions options)
{
    // Validate inputs.

    JobHandle handle = _stateStore.Create(options);

    var execution = new SitecoreJobExecution<TRequest>(
        handle,
        job,
        request,
        _stateStore,
        _settings);

    var sitecoreOptions = new Sitecore.Jobs.JobOptions(
        options.Name,
        options.Category,
        options.SiteName,
        execution,
        nameof(SitecoreJobExecution<TRequest>.Run),
        Array.Empty<object>());

    Sitecore.Jobs.JobManager.Start(sitecoreOptions);

    return handle;
}
```

### Important Sitecore API Rule

Do not blindly copy the constructor above.

Inspect the `Sitecore.Kernel` assembly referenced by the target Sitecore 10.3 solution and use the exact available `Sitecore.Jobs.JobOptions` constructor or factory API.

The implementation must compile against the actual Sitecore 10.3 assemblies.

Do not invent types such as:

```text
Sitecore.Jobs.JobBase
Sitecore.Jobs.IJob
Sitecore.Jobs.JobRunner
```

unless they are confirmed in the referenced Sitecore assemblies.

The final production code must still call:

```csharp
Sitecore.Jobs.JobManager.Start(sitecoreOptions);
```

---

## 10. Execution Wrapper

Create internal class:

```text
SitecoreJobExecution<TRequest>
```

This is the target object invoked by Sitecore's OOTB job engine.

It must expose one public parameterless execution method:

```csharp
public void Run()
```

The constructor may receive:

- `JobHandle`
- `IBackgroundJob<TRequest>`
- `TRequest`
- `IJobStateStore`
- `SitecoreJobSettings`

`Run()` must:

1. Mark the job as `Running`.
2. Create `JobProgressReporter`.
3. Create `JobExecutionContext`.
4. Call the feature job's `Execute`.
5. Convert the returned `JobResult` into the correct final status.
6. Catch `OperationCanceledException` separately.
7. Catch all other unhandled exceptions.
8. Log full exceptions through Sitecore logging.
9. Store only a safe error message in the status snapshot.
10. Always release the concurrency key through the state store.
11. Ensure a completed job cannot be completed a second time.

Do not allow exceptions from final logging or cleanup to hide the original job failure.

---

## 11. Execution Context

Create:

```csharp
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
```

Cancellation is not implemented in this phase.

Use:

```csharp
CancellationToken.None
```

The token is included now so future cancellation does not require changing every job contract.

Do not add cancellation endpoints or cancellation storage in this phase.

---

## 12. Job States

Create:

```csharp
public enum JobState
{
    Queued,
    Running,
    Succeeded,
    SucceededWithWarnings,
    Failed,
    Cancelled
}
```

Allowed transitions:

```text
Queued -> Running
Queued -> Failed
Running -> Succeeded
Running -> SucceededWithWarnings
Running -> Failed
Running -> Cancelled
```

All other transitions must be rejected or ignored safely.

Examples of invalid transitions:

```text
Succeeded -> Running
Failed -> Succeeded
Cancelled -> Running
Succeeded -> Failed
```

Lifecycle validation may be implemented inside `InMemoryJobStateStore`.

Do not create a separate state-machine class unless the implementation becomes genuinely difficult to read without it.

---

## 13. Job Start Options

Create an immutable public model similar to:

```csharp
public sealed class JobStartOptions
{
    public string Name { get; }

    public string Category { get; }

    public string SiteName { get; }

    public JobConcurrencyMode ConcurrencyMode { get; }

    public string ConcurrencyKey { get; }
}
```

Provide simple factory methods:

```csharp
JobStartOptions.AllowParallel(
    string name,
    string category = null,
    string siteName = null);

JobStartOptions.SingleInstance(
    string name,
    string concurrencyKey,
    string category = null,
    string siteName = null);
```

Defaults must come from `SitecoreJobSettings`.

Do not add a large fluent builder.

Do not add arbitrary dictionaries of Sitecore options.

---

## 14. Concurrency

Create:

```csharp
public enum JobConcurrencyMode
{
    AllowParallel,
    SingleInstance
}
```

Do not add per-user, per-site, or distributed concurrency in this phase.

For `SingleInstance`:

- `ConcurrencyKey` is required.
- Only one non-terminal job may own the key.
- Key acquisition must be atomic.
- Starting a second active job with the same key must throw `JobAlreadyRunningException`.
- The exception must expose the existing framework job ID.
- The key must be released when the job succeeds, fails, or is cancelled.
- A failure during `JobManager.Start(...)` must also release the key.

Different keys may run simultaneously.

Example:

```text
ContentCleanup.Master
ContentCleanup.Core
```

may run simultaneously.

Two jobs using:

```text
ContentCleanup.Master
```

may not run simultaneously.

Use the framework's concurrency key as the source of truth.

Do not rely only on the Sitecore job name or a disabled UI button.

---

## 15. Job Handle

Create an immutable model:

```csharp
public sealed class JobHandle
{
    public JobHandle(Guid jobId)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException(
                "Job ID cannot be empty.",
                nameof(jobId));
        }

        JobId = jobId;
    }

    public Guid JobId { get; }
}
```

Do not expose mutable state through the handle.

Do not place a Sitecore `Job` object on the handle.

---

## 16. Progress Model

Create an immutable `JobProgress` model.

Required properties:

```csharp
public long Processed { get; }

public long? Total { get; }

public string Message { get; }

public IReadOnlyDictionary<string, long> Metrics { get; }
```

Rules:

- `Processed` cannot be negative.
- `Total`, when present, cannot be negative.
- `Processed` cannot exceed `Total` when total is known.
- `Message` must become `string.Empty` when omitted.
- `Metrics` must never be null.
- Copy the supplied metrics into a read-only collection.
- Metric names cannot be null or whitespace.
- Metric values cannot be negative.
- The state store must prevent `Processed` from moving backward.
- `Total` may be null for traversal jobs where the total is unknown.

Example:

```csharp
var progress = new JobProgress(
    processed: 1250,
    total: null,
    message: "/sitecore/media library/example",
    metrics: new Dictionary<string, long>
    {
        ["updated"] = 900,
        ["skipped"] = 340,
        ["failed"] = 10
    });
```

The generic framework does not assign meaning to metric names.

---

## 17. Progress Reporter

Create:

```csharp
public interface IJobProgressReporter
{
    void Report(JobProgress progress);

    void AddWarning(string warning);
}
```

Create internal or sealed implementation:

```text
JobProgressReporter
```

It writes through `IJobStateStore`.

It must throttle status updates to avoid excessive locking or future persistence writes.

Use both thresholds:

```text
Item threshold
Time threshold
```

Default behavior:

```text
Report after 100 additional processed items
or after 2 seconds,
whichever happens first.
```

Also provide a way for the execution wrapper to force the final pending progress update before completion.

Warnings:

- Null, empty, or whitespace warnings must be ignored or rejected consistently.
- Store the total warning count.
- Store only the first configurable number of warning messages.
- Do not allow unlimited warning storage.

---

## 18. Job Result

Create a small immutable result model.

Required behavior:

```csharp
JobResult.Success(string summary = null);

JobResult.SuccessWithWarnings(
    string summary = null,
    int warningCount = 1);
```

Recommended properties:

```csharp
public bool HasWarnings { get; }

public int WarningCount { get; }

public string Summary { get; }
```

Do not represent unhandled failure with `JobResult`.

Unhandled exceptions are handled by `SitecoreJobExecution`.

Do not create a generic result payload or serialize arbitrary objects in this phase.

Feature-specific output should be written by the feature itself, such as a generated report path.

---

## 19. Status Snapshot

Create an immutable `JobStatusSnapshot`.

Required properties:

```csharp
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
```

Rules:

- Return a new snapshot for each read.
- Never return the mutable internal record.
- Collections must be copied and read-only.
- UTC timestamps only.
- `Progress` may be null before the first progress update.
- `Error` must be null unless the job failed.
- Full stack traces must never be placed in the snapshot.
- `Summary` must default to `string.Empty`.

---

## 20. Safe Error Model

Create:

```csharp
public sealed class JobError
{
    public string Message { get; }

    public DateTimeOffset OccurredAtUtc { get; }
}
```

The status snapshot must receive a safe message.

The full exception must be logged using Sitecore logging:

```csharp
Sitecore.Diagnostics.Log.Error(...)
```

Do not expose:

- Stack trace
- Connection strings
- File-system secrets
- Raw configuration values
- Complete inner-exception chains

The initial safe message may be:

```text
The background job failed. See the Sitecore logs for details.
```

A job may provide a known user-safe validation message by throwing a dedicated application exception later, but do not build a full exception taxonomy in this phase.

---

## 21. State Store Contracts

Create:

```csharp
public interface IJobStatusReader
{
    bool TryGet(
        Guid jobId,
        out JobStatusSnapshot status);
}
```

Create:

```csharp
public interface IJobStateStore : IJobStatusReader
{
    JobHandle Create(JobStartOptions options);

    void MarkRunning(Guid jobId);

    void ReportProgress(
        Guid jobId,
        JobProgress progress);

    void AddWarning(
        Guid jobId,
        string warning);

    void Complete(
        Guid jobId,
        JobResult result);

    void Fail(
        Guid jobId,
        JobError error);

    void Cancel(Guid jobId);
}
```

The exact method names may be adjusted slightly for clarity, but the behavior must remain the same.

Do not expose methods that return mutable state.

---

## 22. In-Memory State Store

Create:

```text
InMemoryJobStateStore
```

This is the only state-store implementation required now.

Use a simple lock-based implementation.

Recommended internal storage:

```csharp
private readonly object _syncRoot = new object();

private readonly Dictionary<Guid, JobRecord> _jobs;

private readonly Dictionary<string, Guid> _activeConcurrencyKeys;
```

Use one private nested mutable `JobRecord` class inside `InMemoryJobStateStore`.

Do not create a public mutable job entity.

A single lock is acceptable because:

- The application uses one CM instance.
- Progress updates are throttled.
- State operations are small.
- Atomic correctness is more important than unnecessary concurrency complexity.

The store must atomically handle:

- Job creation
- Concurrency-key acquisition
- State transitions
- Progress updates
- Completion
- Failure
- Cancellation
- Concurrency-key release
- Snapshot creation

Do not mix `ConcurrentDictionary` and complex locking unless tests prove it is needed.

---

## 23. Completed Status Retention

Running jobs do not need to survive an application restart.

Completed job statuses should remain available in memory for a configurable period.

Default:

```text
24 hours
```

Use lazy cleanup.

Perform cleanup during safe store operations such as:

```text
Create
TryGet
```

Do not create a timer, background cleanup job, or scheduled task in this phase.

Only remove terminal jobs whose `CompletedAtUtc` is older than the configured retention period.

Do not remove active jobs.

---

## 24. Configuration

Create immutable settings model:

```csharp
public sealed class SitecoreJobSettings
{
    public string DefaultCategory { get; }

    public string DefaultSiteName { get; }

    public int ProgressItemInterval { get; }

    public TimeSpan ProgressTimeInterval { get; }

    public TimeSpan CompletedStatusRetention { get; }

    public int MaximumStoredWarnings { get; }
}
```

Recommended defaults:

```text
DefaultCategory: SitecoreJobs
DefaultSiteName: shell
ProgressItemInterval: 100
ProgressTimeInterval: 2 seconds
CompletedStatusRetention: 24 hours
MaximumStoredWarnings: 25
```

Register one settings instance through `ServicesConfigurator`.

Read values from Sitecore settings in the config patch.

Suggested setting names:

```text
SitecoreJobs.DefaultCategory
SitecoreJobs.DefaultSiteName
SitecoreJobs.ProgressItemInterval
SitecoreJobs.ProgressTimeIntervalSeconds
SitecoreJobs.CompletedStatusRetentionMinutes
SitecoreJobs.MaximumStoredWarnings
```

Validate configuration values and fall back to safe defaults when values are invalid.

Do not add an `ISettingsProvider` unless existing solution conventions require it.

---

## 25. Dependency Injection

Use Sitecore built-in dependency injection.

Create:

```text
DependencyInjection/ServicesConfigurator.cs
```

Implement Sitecore's supported services configurator interface available in the referenced Sitecore 10.3 assemblies.

Register:

```text
SitecoreJobSettings          Singleton instance
IJobStateStore               Singleton
IJobStatusReader             Same singleton instance as IJobStateStore
IJobDispatcher               Singleton
```

`JobProgressReporter` and `SitecoreJobExecution<TRequest>` are created internally and do not need open-generic registrations.

Feature jobs will be registered as transient by the consuming feature.

### Important registration requirement

`IJobStateStore` and `IJobStatusReader` must resolve to the same singleton `InMemoryJobStateStore` instance.

Do not accidentally register two different stores.

Use the exact DI registration pattern already supported by the host Sitecore 10.3 solution.

Add the configurator to the Sitecore config through the appropriate `<services>` patch.

---

## 26. Sitecore References

The reusable repository must not commit proprietary Sitecore binaries.

The project should reference the Sitecore 10.3 assemblies using the host solution's normal approach, such as:

- Existing solution references
- A local Sitecore NuGet/feed configuration
- A documented environment property for assembly paths

Keep Sitecore references non-deploying when the host solution already supplies them.

Follow the host repository's existing `Copy Local` or `Private` convention.

Do not upgrade Microsoft dependency injection packages independently from the versions used by the target Sitecore 10.3 solution.

The coding agent must inspect the existing solution before changing package versions.

---

## 27. Null-Safety and Defensive Rules

Apply these rules consistently:

### Constructor dependencies

```csharp
_dependency = dependency
    ?? throw new ArgumentNullException(nameof(dependency));
```

### Public inputs

Reject null immediately.

### Strings

Validate identifiers and names with:

```csharp
string.IsNullOrWhiteSpace(...)
```

### GUIDs

Reject `Guid.Empty`.

### Collections

Never return null.

Use empty read-only collections.

### Time

Use:

```csharp
DateTimeOffset.UtcNow
```

Do not use local server time for stored job timestamps.

### State

Do not permit updates after a terminal state.

### Progress

Do not permit processed counts to decrease.

### Error handling

Do not swallow exceptions silently.

Log infrastructure problems clearly.

---

## 28. Logging

Use Sitecore logging.

Log at minimum:

```text
Job queued
Job started
Job completed
Job completed with warnings
Job failed
Job cancelled
Duplicate job start rejected
Failure while starting Sitecore JobManager
```

Include:

```text
Framework job ID
Job name
Concurrency key when present
```

Do not log the full request object by default because requests may contain sensitive or very large data.

---

## 29. Tests

Use the unit-test framework already used by the host repository.

Do not introduce a second testing framework if one already exists.

At minimum, implement tests for the following.

### `InMemoryJobStateStoreTests`

- Creating a job returns a non-empty job ID.
- New job state is `Queued`.
- `MarkRunning` sets `StartedAtUtc`.
- A job completes successfully.
- A job completes with warnings.
- A job fails with a safe error.
- A job can be cancelled from `Running`.
- Invalid state transitions are rejected.
- Progress cannot move backward.
- Snapshots cannot mutate internal state.
- Collections returned by snapshots cannot mutate internal state.
- `SingleInstance` rejects a duplicate active concurrency key.
- The duplicate exception exposes the existing job ID.
- Different concurrency keys can run simultaneously.
- `AllowParallel` jobs can run simultaneously.
- Completion releases the concurrency key.
- Failure releases the concurrency key.
- Cancellation releases the concurrency key.
- Expired completed statuses are cleaned up.
- Active statuses are never removed by cleanup.

### `JobProgressReporterTests`

- It publishes after the item threshold.
- It publishes after the time threshold.
- It does not publish every item.
- It can force the final pending update.
- It limits stored warning messages.
- It still tracks the full warning count.

### `SitecoreJobExecutionTests`

- It changes state from `Queued` to `Running`.
- Successful execution finishes as `Succeeded`.
- Warning result finishes as `SucceededWithWarnings`.
- Unhandled exception finishes as `Failed`.
- Full exception is logged.
- Safe error is stored.
- `OperationCanceledException` finishes as `Cancelled`.
- Concurrency is released in every terminal path.
- Final progress is flushed before completion.

Use fakes for feature jobs and the state store where helpful.

Do not require a live Sitecore instance for pure state and lifecycle unit tests.

If the static Sitecore `JobManager` makes dispatcher unit testing disproportionately complex, test the dispatcher through a focused integration or compile-time smoke test rather than adding several abstractions solely for mocking one static call.

---

## 30. Documentation

Create a concise `README.md`.

It must contain:

1. What the library does.
2. Supported Sitecore and .NET versions.
3. Confirmation that Sitecore OOTB Jobs are used underneath.
4. Installation and reference guidance.
5. Required Sitecore config patch.
6. DI registration behavior.
7. How to create a new job.
8. How to start a job.
9. How to read status.
10. Concurrency behavior.
11. Known limitations.
12. Deferred features.

Create `docs/adding-a-job.md` with one small example.

Example feature job:

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
            context.CancellationToken
                .ThrowIfCancellationRequested();

            context.Progress.Report(
                new JobProgress(
                    processed: i,
                    total: 1000,
                    message: "Processing item " + i,
                    metrics: null));
        }

        return JobResult.Success(
            "Summary rebuild completed.");
    }
}
```

Example feature service:

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

    public JobHandle Start(
        RebuildSummaryRequest request)
    {
        return _dispatcher.Start(
            _job,
            request,
            JobStartOptions.SingleInstance(
                name: "Rebuild Summary",
                concurrencyKey: "RebuildSummary"));
    }
}
```

Example status read:

```csharp
JobStatusSnapshot status;

if (_jobStatusReader.TryGet(
    handle.JobId,
    out status))
{
    // Display or log the immutable snapshot.
}
```

---

## 31. Explicitly Deferred Features

Do not implement these now:

```text
Bulk Retention
ASPX pages
ASCX controls
HTTP polling endpoints
Persistent SQL job history
Redis state
Multi-CM coordination
Distributed locking
Job restart after application recycle
User authorization
Per-user concurrency
Automatic retries
Job scheduling
Job chaining
Job priorities
Generic batching framework
Job result serialization
Dynamic plugin discovery
Dashboard UI
Cancellation requests
```

The architecture must allow the following later without redesigning feature jobs:

```text
Cancellation
Persistent state store
Generic status ASCX
Feature-specific ASCX controls
Additional background jobs
```

---

## 32. Integration Boundary for Future UI

No UI is built now.

Future UI code will depend only on:

```text
IJobDispatcher
IJobStatusReader
JobHandle
JobStatusSnapshot
```

A future generic ASCX should store only the framework job ID and retrieve fresh snapshots through `IJobStatusReader`.

The framework must not depend on:

```text
System.Web.UI.UserControl
Page
HttpContext
ViewState
Session
```

This keeps the core library usable from:

- ASPX pages
- ASCX controls
- Scheduled tasks
- Commands
- Pipelines
- Event handlers
- Administrative utilities

---

## 33. Acceptance Criteria

The implementation is complete only when all of the following are true:

- The solution builds for .NET Framework 4.8.
- The code uses C# 7.3-compatible syntax.
- The production library references the actual Sitecore 10.3 assemblies.
- The dispatcher starts jobs through `Sitecore.Jobs.JobManager.Start(...)`.
- No custom background execution engine exists.
- No Bulk Retention code exists.
- No WebForms or HTTP code exists in the core project.
- No client-specific or company-specific code exists.
- A developer can implement a new job with one request class, one job class, and one feature service.
- Duplicate single-instance jobs are rejected atomically.
- Different concurrency keys can run simultaneously.
- Progress is immutable to callers.
- Status is immutable to callers.
- Completed state releases concurrency.
- Failed state releases concurrency.
- Cancelled state releases concurrency.
- Full exceptions are logged.
- Only safe errors are stored.
- Completed status retention is configurable.
- Unit tests pass.
- README integration instructions are complete.
- There are no unnecessary abstractions or duplicate manager/runner layers.
- There are no unresolved compiler errors.
- There are no placeholder implementations.
- There are no `TODO` comments except for explicitly deferred cancellation and persistence extension points.

---

## 34. Coding-Agent Instructions

When implementing this specification:

1. Inspect the existing repository and follow its project style.
2. Inspect the referenced Sitecore 10.3 assemblies before writing Sitecore API calls.
3. Do not guess constructor overloads.
4. Build after each small group of files.
5. Fix compile errors before continuing.
6. Run all tests before completing the task.
7. Keep each class focused and readable.
8. Do not add a class merely to wrap another class without adding a real boundary.
9. Do not change unrelated projects.
10. Do not implement Bulk Retention.
11. Do not add UI.
12. Do not add persistence.
13. Do not add custom threading.
14. Document any unavoidable deviation from this specification in the final summary.
15. Provide a final file-by-file summary and the commands used to build and test.

When an existing host solution convention conflicts with a naming or configuration detail in this document, preserve the architectural behavior and follow the host convention.

The most important non-negotiable rule is:

> This is a reusable safety and consistency layer over Sitecore OOTB Jobs, not a replacement for Sitecore OOTB Jobs.
