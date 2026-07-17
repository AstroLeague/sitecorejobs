using System;
using SitecoreJobs.Foundation.Abstractions;
using SitecoreJobs.Foundation.Configuration;
using SitecoreJobs.Foundation.Models;

namespace SitecoreJobs.Foundation.Execution
{
    internal sealed class JobProgressReporter : IJobProgressReporter
    {
        private readonly Guid _jobId;
        private readonly IJobStateStore _stateStore;
        private readonly SitecoreJobSettings _settings;
        private readonly Func<DateTimeOffset> _utcNow;
        private long _lastObservedProcessed;
        private long _lastPublishedProcessed;
        private DateTimeOffset _lastPublishedAtUtc;
        private JobProgress _pending;

        public JobProgressReporter(
            Guid jobId,
            IJobStateStore stateStore,
            SitecoreJobSettings settings)
            : this(jobId, stateStore, settings, () => DateTimeOffset.UtcNow)
        {
        }

        internal JobProgressReporter(
            Guid jobId,
            IJobStateStore stateStore,
            SitecoreJobSettings settings,
            Func<DateTimeOffset> utcNow)
        {
            if (jobId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Job ID cannot be empty.",
                    nameof(jobId));
            }

            _jobId = jobId;
            _stateStore = stateStore
                ?? throw new ArgumentNullException(nameof(stateStore));
            _settings = settings
                ?? throw new ArgumentNullException(nameof(settings));
            _utcNow = utcNow
                ?? throw new ArgumentNullException(nameof(utcNow));
            _lastPublishedAtUtc = GetUtcNow();
        }

        public void Report(JobProgress progress)
        {
            if (progress == null)
            {
                throw new ArgumentNullException(nameof(progress));
            }

            if (progress.Processed < _lastObservedProcessed)
            {
                throw new InvalidOperationException(
                    "Processed progress cannot move backward.");
            }

            _lastObservedProcessed = progress.Processed;
            _pending = progress;

            var now = GetUtcNow();
            if (progress.Processed - _lastPublishedProcessed
                    >= _settings.ProgressItemInterval
                || now - _lastPublishedAtUtc
                    >= _settings.ProgressTimeInterval)
            {
                Publish(now);
            }
        }

        public void AddWarning(string warning)
        {
            if (string.IsNullOrWhiteSpace(warning))
            {
                return;
            }

            _stateStore.AddWarning(_jobId, warning);
        }

        internal void Flush()
        {
            if (_pending != null)
            {
                Publish(GetUtcNow());
            }
        }

        private void Publish(DateTimeOffset now)
        {
            var progress = _pending;
            if (progress == null)
            {
                return;
            }

            _stateStore.ReportProgress(_jobId, progress);
            _lastPublishedProcessed = progress.Processed;
            _lastPublishedAtUtc = now;
            _pending = null;
        }

        private DateTimeOffset GetUtcNow()
        {
            var now = _utcNow();
            if (now.Offset != TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    "The configured clock must return UTC values.");
            }

            return now;
        }
    }
}
