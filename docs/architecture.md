# Architecture

## Execution flow

```text
Feature service
    -> IJobDispatcher
    -> SitecoreJobDispatcher
    -> Sitecore.Jobs.DefaultJobOptions
    -> Sitecore.Jobs.JobManager.Start(...)
    -> SitecoreJobExecution<TRequest>.Run()
    -> IBackgroundJob<TRequest>.Execute(...)
```

`SitecoreJobDispatcher` validates input, applies configured defaults, reserves
the concurrency key through `IJobStateStore`, and gives Sitecore a target
object with a public parameterless `Run` method. Sitecore's job engine owns
background execution. The foundation does not schedule or run work itself.

`SitecoreJobExecution<TRequest>` owns lifecycle transitions. It changes the
framework record from queued to running, creates the progress reporter and
execution context, executes feature code, flushes pending progress, and stores
the final result. It logs complete exceptions through Sitecore logging while
placing only a fixed safe message in status.

## State and concurrency

`InMemoryJobStateStore` uses one lock to make creation, state changes,
concurrency-key ownership, snapshots, and lazy cleanup atomic. Its only mutable
job record is private. Every read returns a new `JobStatusSnapshot` whose
collections are copied and read-only.

Allowed transitions are:

```text
Queued -> Running
Queued -> Failed
Running -> Succeeded
Running -> SucceededWithWarnings
Running -> Failed
Running -> Cancelled
```

Other transitions throw `InvalidOperationException`. Progress cannot decrease.
Only one non-terminal job can own a single-instance key. Terminal transitions
release that key while still holding the store lock.

## Progress and retention

`JobProgressReporter` publishes after either the configured processed-item
interval or time interval, whichever occurs first. The execution wrapper
forces the final pending update. Warning count is complete, but only the first
configured number of messages is retained.

Terminal status is cleaned lazily during create and read operations. Active
status is never expired. The store deliberately has no timer or cleanup
background task.

## Extension boundaries

Feature and future UI code depend on `IJobDispatcher`, `IJobStatusReader`,
`JobHandle`, and immutable models. A future persistent store can implement
`IJobStateStore` without changing feature jobs. The cancellation token is
already present in `JobExecutionContext`, but cancellation requests and storage
are not implemented.
