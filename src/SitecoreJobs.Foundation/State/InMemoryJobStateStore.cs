using System;
using System.Collections.Generic;
using SitecoreJobs.Foundation.Abstractions;
using SitecoreJobs.Foundation.Configuration;
using SitecoreJobs.Foundation.Models;

namespace SitecoreJobs.Foundation.State
{
    public sealed class InMemoryJobStateStore : IJobStateStore
    {
        private readonly object _syncRoot = new object();
        private readonly Dictionary<Guid, JobRecord> _jobs =
            new Dictionary<Guid, JobRecord>();
        private readonly Dictionary<string, Guid> _activeConcurrencyKeys =
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        private readonly SitecoreJobSettings _settings;
        private readonly Func<DateTimeOffset> _utcNow;

        public InMemoryJobStateStore(SitecoreJobSettings settings)
            : this(settings, () => DateTimeOffset.UtcNow)
        {
        }

        internal InMemoryJobStateStore(
            SitecoreJobSettings settings,
            Func<DateTimeOffset> utcNow)
        {
            _settings = settings
                ?? throw new ArgumentNullException(nameof(settings));
            _utcNow = utcNow
                ?? throw new ArgumentNullException(nameof(utcNow));
        }

        public JobHandle Create(JobStartOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            lock (_syncRoot)
            {
                var now = GetUtcNow();
                CleanupExpired(now);

                if (options.ConcurrencyMode == JobConcurrencyMode.SingleInstance)
                {
                    Guid existingJobId;
                    if (_activeConcurrencyKeys.TryGetValue(
                        options.ConcurrencyKey,
                        out existingJobId))
                    {
                        throw new JobAlreadyRunningException(
                            options.ConcurrencyKey,
                            existingJobId);
                    }
                }

                var jobId = Guid.NewGuid();
                var record = new JobRecord(jobId, options, now);
                _jobs.Add(jobId, record);

                if (options.ConcurrencyMode == JobConcurrencyMode.SingleInstance)
                {
                    _activeConcurrencyKeys.Add(
                        options.ConcurrencyKey,
                        jobId);
                }

                return new JobHandle(jobId);
            }
        }

        public void MarkRunning(Guid jobId)
        {
            lock (_syncRoot)
            {
                var record = GetRecord(jobId);
                RequireState(record, JobState.Queued);
                record.State = JobState.Running;
                record.StartedAtUtc = GetUtcNow();
            }
        }

        public void ReportProgress(Guid jobId, JobProgress progress)
        {
            if (progress == null)
            {
                throw new ArgumentNullException(nameof(progress));
            }

            lock (_syncRoot)
            {
                var record = GetRecord(jobId);
                RequireState(record, JobState.Running);

                if (record.Progress != null
                    && progress.Processed < record.Progress.Processed)
                {
                    throw new InvalidOperationException(
                        "Processed progress cannot move backward.");
                }

                record.Progress = progress;
            }
        }

        public void AddWarning(Guid jobId, string warning)
        {
            if (string.IsNullOrWhiteSpace(warning))
            {
                return;
            }

            lock (_syncRoot)
            {
                var record = GetRecord(jobId);
                RequireState(record, JobState.Running);
                record.WarningCount++;

                if (record.Warnings.Count < _settings.MaximumStoredWarnings)
                {
                    record.Warnings.Add(warning.Trim());
                }
            }
        }

        public void Complete(Guid jobId, JobResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            lock (_syncRoot)
            {
                var record = GetRecord(jobId);
                RequireState(record, JobState.Running);
                record.WarningCount = Math.Max(
                    record.WarningCount,
                    result.WarningCount);
                record.State = result.HasWarnings || record.WarningCount > 0
                    ? JobState.SucceededWithWarnings
                    : JobState.Succeeded;
                record.Summary = result.Summary;
                Finish(record);
            }
        }

        public void Fail(Guid jobId, JobError error)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            lock (_syncRoot)
            {
                var record = GetRecord(jobId);
                if (record.State != JobState.Queued
                    && record.State != JobState.Running)
                {
                    throw InvalidTransition(record.State, JobState.Failed);
                }

                record.State = JobState.Failed;
                record.Error = error;
                Finish(record);
            }
        }

        public void Cancel(Guid jobId)
        {
            lock (_syncRoot)
            {
                var record = GetRecord(jobId);
                RequireState(record, JobState.Running);
                record.State = JobState.Cancelled;
                Finish(record);
            }
        }

        public void EnsureFailed(Guid jobId, JobError error)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            lock (_syncRoot)
            {
                var record = GetRecord(jobId);
                if (!IsTerminal(record.State))
                {
                    record.State = JobState.Failed;
                    record.Error = error;
                    Finish(record);
                    return;
                }

                ReleaseConcurrencyKey(record);
            }
        }

        public bool TryGet(Guid jobId, out JobStatusSnapshot status)
        {
            if (jobId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Job ID cannot be empty.",
                    nameof(jobId));
            }

            lock (_syncRoot)
            {
                CleanupExpired(GetUtcNow());

                JobRecord record;
                if (!_jobs.TryGetValue(jobId, out record))
                {
                    status = null;
                    return false;
                }

                status = record.ToSnapshot();
                return true;
            }
        }

        private static bool IsTerminal(JobState state)
        {
            return state == JobState.Succeeded
                || state == JobState.SucceededWithWarnings
                || state == JobState.Failed
                || state == JobState.Cancelled;
        }

        private static void RequireState(
            JobRecord record,
            JobState expected)
        {
            if (record.State != expected)
            {
                throw InvalidTransition(record.State, expected);
            }
        }

        private static InvalidOperationException InvalidTransition(
            JobState current,
            JobState requested)
        {
            return new InvalidOperationException(
                "Cannot transition a job from "
                + current
                + " to "
                + requested
                + ".");
        }

        private JobRecord GetRecord(Guid jobId)
        {
            if (jobId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Job ID cannot be empty.",
                    nameof(jobId));
            }

            JobRecord record;
            if (!_jobs.TryGetValue(jobId, out record))
            {
                throw new KeyNotFoundException(
                    "No job exists with ID " + jobId + ".");
            }

            return record;
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

        private void Finish(JobRecord record)
        {
            record.CompletedAtUtc = GetUtcNow();
            ReleaseConcurrencyKey(record);
        }

        private void ReleaseConcurrencyKey(JobRecord record)
        {
            if (record.ConcurrencyKey == null)
            {
                return;
            }

            Guid owner;
            if (_activeConcurrencyKeys.TryGetValue(
                    record.ConcurrencyKey,
                    out owner)
                && owner == record.JobId)
            {
                _activeConcurrencyKeys.Remove(record.ConcurrencyKey);
            }
        }

        private void CleanupExpired(DateTimeOffset now)
        {
            var cutoff = now - _settings.CompletedStatusRetention;
            var expired = new List<Guid>();

            foreach (var pair in _jobs)
            {
                var record = pair.Value;
                if (IsTerminal(record.State)
                    && record.CompletedAtUtc.HasValue
                    && record.CompletedAtUtc.Value < cutoff)
                {
                    expired.Add(pair.Key);
                }
            }

            foreach (var jobId in expired)
            {
                _jobs.Remove(jobId);
            }
        }

        private sealed class JobRecord
        {
            public JobRecord(
                Guid jobId,
                JobStartOptions options,
                DateTimeOffset createdAtUtc)
            {
                JobId = jobId;
                Name = options.Name;
                State = JobState.Queued;
                CreatedAtUtc = createdAtUtc;
                ConcurrencyKey = options.ConcurrencyKey;
                Warnings = new List<string>();
                Summary = string.Empty;
            }

            public Guid JobId { get; }

            public string Name { get; }

            public JobState State { get; set; }

            public JobProgress Progress { get; set; }

            public DateTimeOffset CreatedAtUtc { get; }

            public DateTimeOffset? StartedAtUtc { get; set; }

            public DateTimeOffset? CompletedAtUtc { get; set; }

            public int WarningCount { get; set; }

            public List<string> Warnings { get; }

            public JobError Error { get; set; }

            public string Summary { get; set; }

            public string ConcurrencyKey { get; }

            public JobStatusSnapshot ToSnapshot()
            {
                return new JobStatusSnapshot(
                    JobId,
                    Name,
                    State,
                    Progress,
                    CreatedAtUtc,
                    StartedAtUtc,
                    CompletedAtUtc,
                    WarningCount,
                    Warnings,
                    Error,
                    Summary);
            }
        }
    }
}
