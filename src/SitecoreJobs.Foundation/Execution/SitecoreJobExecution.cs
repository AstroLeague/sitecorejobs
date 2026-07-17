using System;
using System.Threading;
using Sitecore.Diagnostics;
using SitecoreJobs.Foundation.Abstractions;
using SitecoreJobs.Foundation.Configuration;
using SitecoreJobs.Foundation.Models;

namespace SitecoreJobs.Foundation.Execution
{
    internal sealed class SitecoreJobExecution<TRequest>
    {
        private const string SafeFailureMessage =
            "The background job failed. See the Sitecore logs for details.";

        private readonly JobHandle _handle;
        private readonly string _jobName;
        private readonly string _concurrencyKey;
        private readonly IBackgroundJob<TRequest> _job;
        private readonly TRequest _request;
        private readonly IJobStateStore _stateStore;
        private readonly SitecoreJobSettings _settings;
        private readonly Action<string, Exception> _errorLogger;
        private int _hasRun;

        public SitecoreJobExecution(
            JobHandle handle,
            string jobName,
            string concurrencyKey,
            IBackgroundJob<TRequest> job,
            TRequest request,
            IJobStateStore stateStore,
            SitecoreJobSettings settings,
            Action<string, Exception> errorLogger = null)
        {
            _handle = handle
                ?? throw new ArgumentNullException(nameof(handle));

            if (string.IsNullOrWhiteSpace(jobName))
            {
                throw new ArgumentException(
                    "Job name cannot be empty.",
                    nameof(jobName));
            }

            _jobName = jobName;
            _concurrencyKey = string.IsNullOrWhiteSpace(concurrencyKey)
                ? null
                : concurrencyKey.Trim();
            _job = job
                ?? throw new ArgumentNullException(nameof(job));

            if (ReferenceEquals(request, null))
            {
                throw new ArgumentNullException(nameof(request));
            }

            _request = request;
            _stateStore = stateStore
                ?? throw new ArgumentNullException(nameof(stateStore));
            _settings = settings
                ?? throw new ArgumentNullException(nameof(settings));
            _errorLogger = errorLogger ?? DefaultLogError;
        }

        public void Run()
        {
            if (Interlocked.Exchange(ref _hasRun, 1) != 0)
            {
                SafeLogWarning(
                    "Sitecore job execution was invoked more than once.");
                return;
            }

            var reporter = new JobProgressReporter(
                _handle.JobId,
                _stateStore,
                _settings);

            try
            {
                _stateStore.MarkRunning(_handle.JobId);
                SafeLogInfo("Job started.");
                SafeLogToSitecoreJob("Job started.");

                var context = new JobExecutionContext(
                    _handle.JobId,
                    reporter,
                    CancellationToken.None);
                var result = _job.Execute(context, _request);

                if (result == null)
                {
                    throw new InvalidOperationException(
                        "The background job returned a null result.");
                }

                reporter.Flush();
                _stateStore.Complete(_handle.JobId, result);

                SafeLogInfo(
                    result.HasWarnings
                        ? "Job completed with warnings."
                        : "Job completed.");
                SafeLogToSitecoreJob(
                    result.HasWarnings
                        ? "Job completed with warnings."
                        : "Job completed.");
            }
            catch (OperationCanceledException exception)
            {
                SafeFlush(reporter);
                LogError("Job was cancelled.", exception);
                TryCancel();
                SafeLogToSitecoreJob("Job cancelled.");
            }
            catch (Exception exception)
            {
                SafeFlush(reporter);
                LogError("Job failed.", exception);
                TryFail();
                SafeLogToSitecoreJob("Job failed.");
            }
        }

        private void TryCancel()
        {
            try
            {
                _stateStore.Cancel(_handle.JobId);
            }
            catch (Exception exception)
            {
                LogError(
                    "Unable to persist the cancelled job state.",
                    exception);
                TryEnsureFailed();
            }
        }

        private void TryFail()
        {
            try
            {
                _stateStore.Fail(
                    _handle.JobId,
                    new JobError(
                        SafeFailureMessage,
                        DateTimeOffset.UtcNow));
            }
            catch (Exception exception)
            {
                LogError(
                    "Unable to persist the failed job state.",
                    exception);
                TryEnsureFailed();
            }
        }

        private void TryEnsureFailed()
        {
            try
            {
                _stateStore.EnsureFailed(
                    _handle.JobId,
                    new JobError(
                        SafeFailureMessage,
                        DateTimeOffset.UtcNow));
            }
            catch (Exception exception)
            {
                LogError(
                    "Unable to force a terminal failed job state.",
                    exception);
            }
        }

        private void SafeFlush(JobProgressReporter reporter)
        {
            try
            {
                reporter.Flush();
            }
            catch (Exception exception)
            {
                LogError(
                    "Unable to flush final job progress.",
                    exception);
            }
        }

        private void SafeLogInfo(string message)
        {
            try
            {
                Log.Info(FormatMessage(message), this);
            }
            catch
            {
                // Logging must not alter the job lifecycle.
            }
        }

        private void SafeLogWarning(string message)
        {
            try
            {
                Log.Warn(FormatMessage(message), this);
            }
            catch
            {
                // Logging must not alter the job lifecycle.
            }
        }

        private void LogError(string message, Exception exception)
        {
            try
            {
                _errorLogger(message, exception);
            }
            catch
            {
                // Logging must not hide the original lifecycle result.
            }
        }

        private void DefaultLogError(string message, Exception exception)
        {
            Log.Error(FormatMessage(message), exception, this);
        }

        private void SafeLogToSitecoreJob(string message)
        {
            try
            {
                var sitecoreJob = Sitecore.Context.Job;
                if (sitecoreJob != null && sitecoreJob.Status != null)
                {
                    sitecoreJob.Status.LogInfo(
                        FormatMessage(message));
                }
            }
            catch (Exception exception)
            {
                LogError(
                    "Unable to update Sitecore job status.",
                    exception);
            }
        }

        private string FormatMessage(string message)
        {
            return "[SitecoreJobs] "
                + message
                + " FrameworkJobId="
                + _handle.JobId
                + "; Name="
                + _jobName
                + (_concurrencyKey == null
                    ? string.Empty
                    : "; ConcurrencyKey=" + _concurrencyKey)
                + ".";
        }
    }
}
