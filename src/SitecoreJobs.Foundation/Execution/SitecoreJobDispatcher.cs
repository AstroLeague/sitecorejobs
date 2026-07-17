using System;
using Sitecore.Diagnostics;
using Sitecore.Jobs;
using SitecoreJobs.Foundation.Abstractions;
using SitecoreJobs.Foundation.Configuration;
using SitecoreJobs.Foundation.Models;
using SitecoreJobs.Foundation.State;

namespace SitecoreJobs.Foundation.Execution
{
    public sealed class SitecoreJobDispatcher : IJobDispatcher
    {
        private const string SafeFailureMessage =
            "The background job failed. See the Sitecore logs for details.";

        private readonly IJobStateStore _stateStore;
        private readonly SitecoreJobSettings _settings;
        private readonly Action<DefaultJobOptions> _startJob;

        public SitecoreJobDispatcher(
            IJobStateStore stateStore,
            SitecoreJobSettings settings)
            : this(
                stateStore,
                settings,
                options => JobManager.Start(options))
        {
        }

        internal SitecoreJobDispatcher(
            IJobStateStore stateStore,
            SitecoreJobSettings settings,
            Action<DefaultJobOptions> startJob)
        {
            _stateStore = stateStore
                ?? throw new ArgumentNullException(nameof(stateStore));
            _settings = settings
                ?? throw new ArgumentNullException(nameof(settings));
            _startJob = startJob
                ?? throw new ArgumentNullException(nameof(startJob));
        }

        public JobHandle Start<TRequest>(
            IBackgroundJob<TRequest> job,
            TRequest request,
            JobStartOptions options)
        {
            if (job == null)
            {
                throw new ArgumentNullException(nameof(job));
            }

            if (ReferenceEquals(request, null))
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var effectiveOptions = ApplyDefaults(options);
            JobHandle handle;

            try
            {
                handle = _stateStore.Create(effectiveOptions);
            }
            catch (JobAlreadyRunningException exception)
            {
                SafeLogWarning(
                    "Duplicate job start rejected. ExistingFrameworkJobId="
                    + exception.ExistingJobId
                    + "; Name="
                    + effectiveOptions.Name
                    + "; ConcurrencyKey="
                    + effectiveOptions.ConcurrencyKey
                    + ".");
                throw;
            }

            var execution = new SitecoreJobExecution<TRequest>(
                handle,
                effectiveOptions.Name,
                effectiveOptions.ConcurrencyKey,
                job,
                request,
                _stateStore,
                _settings);

            try
            {
                var sitecoreOptions = new DefaultJobOptions(
                    effectiveOptions.Name,
                    effectiveOptions.Category,
                    effectiveOptions.SiteName,
                    execution,
                    nameof(SitecoreJobExecution<TRequest>.Run),
                    new object[0]);

                _startJob(sitecoreOptions);
                SafeLogInfo(
                    "Job queued. FrameworkJobId="
                    + handle.JobId
                    + "; Name="
                    + effectiveOptions.Name
                    + FormatConcurrencyKey(effectiveOptions)
                    + ".");

                return handle;
            }
            catch (Exception exception)
            {
                SafeLogError(
                    "Failure while starting Sitecore JobManager. "
                    + "FrameworkJobId="
                    + handle.JobId
                    + "; Name="
                    + effectiveOptions.Name
                    + FormatConcurrencyKey(effectiveOptions)
                    + ".",
                    exception);

                try
                {
                    _stateStore.Fail(
                        handle.JobId,
                        new JobError(
                            SafeFailureMessage,
                            DateTimeOffset.UtcNow));
                }
                catch (Exception stateException)
                {
                    SafeLogError(
                        "Unable to mark the unstarted job as failed. "
                        + "FrameworkJobId="
                        + handle.JobId
                        + ".",
                        stateException);
                    TryEnsureFailed(handle);
                }

                throw;
            }
        }

        private void TryEnsureFailed(JobHandle handle)
        {
            try
            {
                _stateStore.EnsureFailed(
                    handle.JobId,
                    new JobError(
                        SafeFailureMessage,
                        DateTimeOffset.UtcNow));
            }
            catch (Exception exception)
            {
                SafeLogError(
                    "Unable to force a terminal failed job state. "
                    + "FrameworkJobId="
                    + handle.JobId
                    + ".",
                    exception);
            }
        }

        private JobStartOptions ApplyDefaults(JobStartOptions options)
        {
            var category = options.Category ?? _settings.DefaultCategory;
            var siteName = options.SiteName ?? _settings.DefaultSiteName;

            if (options.ConcurrencyMode == JobConcurrencyMode.AllowParallel)
            {
                return JobStartOptions.AllowParallel(
                    options.Name,
                    category,
                    siteName);
            }

            if (options.ConcurrencyMode == JobConcurrencyMode.SingleInstance)
            {
                return JobStartOptions.SingleInstance(
                    options.Name,
                    options.ConcurrencyKey,
                    category,
                    siteName);
            }

            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The concurrency mode is invalid.");
        }

        private static string FormatConcurrencyKey(
            JobStartOptions options)
        {
            return options.ConcurrencyKey == null
                ? string.Empty
                : "; ConcurrencyKey=" + options.ConcurrencyKey;
        }

        private void SafeLogInfo(string message)
        {
            try
            {
                Log.Info("[SitecoreJobs] " + message, this);
            }
            catch
            {
                // Logging must not alter dispatch behavior.
            }
        }

        private void SafeLogWarning(string message)
        {
            try
            {
                Log.Warn("[SitecoreJobs] " + message, this);
            }
            catch
            {
                // Logging must not alter dispatch behavior.
            }
        }

        private void SafeLogError(string message, Exception exception)
        {
            try
            {
                Log.Error("[SitecoreJobs] " + message, exception, this);
            }
            catch
            {
                // Logging must not hide dispatch failures.
            }
        }
    }
}
