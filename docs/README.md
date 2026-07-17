# Sitecore Jobs Foundation

Sitecore Jobs Foundation is a reusable safety and status layer over Sitecore's
out-of-the-box job engine. It targets Sitecore XP/XM 10.3, .NET Framework 4.8,
and C# 7.3. Work is started by `Sitecore.Jobs.JobManager.Start`; this library
does not create threads, queues, schedulers, or another execution engine.

## Installation and configuration

Reference `src/SitecoreJobs.Foundation/SitecoreJobs.Foundation.csproj`, keep
the `Sitecore.Kernel` reference at the host's Sitecore 10.3 version, and deploy
the assembly plus `SitecoreJobs.Foundation.config`.

The config registers one settings instance, one singleton state store, and one
singleton dispatcher. `IJobStateStore` and `IJobStatusReader` resolve to the
same store. Feature jobs must be stateless transient services.

Defaults are category `SitecoreJobs`, site `shell`, progress every 100 items or
2 seconds, 24-hour completed-status retention, and 25 stored warning messages.
Invalid configured values fall back to these defaults.

## Usage

Implement `IBackgroundJob<TRequest>`, inject that job and `IJobDispatcher`,
then use `JobStartOptions.AllowParallel` or `SingleInstance`. The returned
`JobHandle` contains only the framework ID. Read immutable status with
`IJobStatusReader.TryGet`. See [adding-a-job.md](adding-a-job.md).

Single-instance keys are acquired atomically. Duplicate active keys throw
`JobAlreadyRunningException` with the existing ID. Every terminal path and a
Sitecore start failure release the key.

## Limitations

State and concurrency are process-local for one CM. They do not survive an
application recycle. Cancellation requests, persistence, distributed locking,
retries, scheduling, UI/HTTP endpoints, generic batching, and Bulk Retention
are deferred.
