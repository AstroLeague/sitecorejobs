# Sitecore Jobs Foundation

A small, reusable safety and consistency layer over Sitecore's built-in background job engine. It dispatches work through `Sitecore.Jobs.JobManager`, tracks immutable status and progress in memory, and enforces optional single-instance concurrency.

## Support

- Sitecore XM/XP 10.3
- .NET Framework 4.8
- C# 7.3
- Single content-management instance
- Sitecore built-in dependency injection

The library does not create a worker service, custom queue, scheduler, or replacement job engine.

## Repository layout

- `src/SitecoreJobs.Foundation` — production library and Sitecore config patch
- `tests/SitecoreJobs.Foundation.Tests` — unit tests
- `docs/architecture.md` — design and execution flow
- `docs/adding-a-job.md` — integration example
- `docker` — local Sitecore 10.3 XM1 + SXA environment, tooling, Traefik, deploy, and data folders

## Build and test

Use a Visual Studio Developer PowerShell with the .NET Framework 4.8 SDK:

```powershell
dotnet restore .\SitecoreJobs.Foundation.sln
dotnet build .\SitecoreJobs.Foundation.sln --configuration Release --no-restore
dotnet test .\tests\SitecoreJobs.Foundation.Tests\SitecoreJobs.Foundation.Tests.csproj --configuration Release --no-build
```

`Sitecore.Kernel` is resolved from the configured Sitecore NuGet feed and is not copied into the output.

## Sitecore installation

Deploy these outputs to the Sitecore CM website:

- `bin/SitecoreJobs.Foundation.dll`
- `App_Config/Include/Foundation/SitecoreJobs.Foundation.config`

The config patch registers one shared in-memory state store, the status reader, dispatcher, and validated settings. Feature-specific jobs should be transient and stateless.

## Basic usage

Implement `IBackgroundJob<TRequest>`, then inject the job and `IJobDispatcher` into the feature service that starts it:

```csharp
public sealed class RebuildJob : IBackgroundJob<RebuildRequest>
{
    public JobResult Execute(
        JobExecutionContext context,
        RebuildRequest request)
    {
        context.Progress.Report(
            new JobProgress(1, 1, "Complete", null));

        return JobResult.Success("Rebuild completed.");
    }
}
```

```csharp
JobHandle handle = dispatcher.Start(
    job,
    request,
    JobStartOptions.SingleInstance(
        "Rebuild",
        "Rebuild.Master"));
```

Read current state through `IJobStatusReader.TryGet(handle.JobId, out status)`.

Single-instance keys are acquired atomically and released on success, failure, cancellation, or a Sitecore dispatch failure. Parallel jobs do not acquire a key.

## Local Sitecore environment

Copy `docker/.env.example` to `docker/.env`, generate local secrets there, place
`license.xml` in `license`, then run an elevated Windows PowerShell 5.1 prompt:

```powershell
.\init.ps1
.\up.ps1
```

The retained environment exposes:

- CM: `https://cm.sitecorejobs.localhost`
- Identity: `https://id.sitecorejobs.localhost`

The Docker topology uses a local `sitecorejobs-local` NAT network with static
container IPs configured in `docker/.env`. Sitecore connection strings use
those IPs for SQL, Solr, Redis, and the internal Identity authority.

Use `.\down.ps1` to stop it. The environment intentionally contains no JSS rendering host, styleguide, demo content, Azure AD customization, or Coveo assets.

## Limitations

State is process-local and is lost on application restart. This release has no distributed locking, persistence, scheduling, retries, cancellation requests, HTTP/UI endpoints, or Bulk Retention feature.
