using System;
using System.Collections.Generic;
using NUnit.Framework;
using SitecoreJobs.Foundation.Configuration;
using SitecoreJobs.Foundation.Models;
using SitecoreJobs.Foundation.State;

namespace SitecoreJobs.Foundation.Tests.State
{
    [TestFixture]
    public sealed class InMemoryJobStateStoreTests
    {
        private DateTimeOffset _now;
        private InMemoryJobStateStore _store;

        [SetUp]
        public void SetUp()
        {
            _now = new DateTimeOffset(
                2026,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero);
            _store = new InMemoryJobStateStore(
                CreateSettings(TimeSpan.FromHours(1), 2),
                () => _now);
        }

        [Test]
        public void CreateReturnsQueuedJobWithNonEmptyId()
        {
            var handle = _store.Create(
                JobStartOptions.AllowParallel("Test"));

            JobStatusSnapshot status;
            Assert.That(handle.JobId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(_store.TryGet(handle.JobId, out status), Is.True);
            Assert.That(status.State, Is.EqualTo(JobState.Queued));
            Assert.That(status.CreatedAtUtc, Is.EqualTo(_now));
            Assert.That(status.Summary, Is.Empty);
            Assert.That(status.Warnings, Is.Empty);
        }

        [Test]
        public void MarkRunningSetsStartedTimestamp()
        {
            var handle = CreateRunningJob("Test");

            JobStatusSnapshot status;
            _store.TryGet(handle.JobId, out status);

            Assert.That(status.State, Is.EqualTo(JobState.Running));
            Assert.That(status.StartedAtUtc, Is.EqualTo(_now));
        }

        [Test]
        public void CompleteStoresSuccess()
        {
            var handle = CreateRunningJob("Test");
            _store.Complete(handle.JobId, JobResult.Success("done"));

            var status = Get(handle);
            Assert.That(status.State, Is.EqualTo(JobState.Succeeded));
            Assert.That(status.CompletedAtUtc, Is.EqualTo(_now));
            Assert.That(status.Summary, Is.EqualTo("done"));
        }

        [Test]
        public void CompleteStoresWarningResult()
        {
            var handle = CreateRunningJob("Test");
            _store.Complete(
                handle.JobId,
                JobResult.SuccessWithWarnings("done", 3));

            var status = Get(handle);
            Assert.That(
                status.State,
                Is.EqualTo(JobState.SucceededWithWarnings));
            Assert.That(status.WarningCount, Is.EqualTo(3));
        }

        [Test]
        public void FailStoresOnlySafeError()
        {
            var handle = CreateRunningJob("Test");
            var error = new JobError("Safe message", _now);
            _store.Fail(handle.JobId, error);

            var status = Get(handle);
            Assert.That(status.State, Is.EqualTo(JobState.Failed));
            Assert.That(status.Error.Message, Is.EqualTo("Safe message"));
            Assert.That(status.Error.OccurredAtUtc, Is.EqualTo(_now));
        }

        [Test]
        public void CancelTransitionsRunningJob()
        {
            var handle = CreateRunningJob("Test");
            _store.Cancel(handle.JobId);

            Assert.That(Get(handle).State, Is.EqualTo(JobState.Cancelled));
        }

        [Test]
        public void InvalidTerminalTransitionIsRejected()
        {
            var handle = CreateRunningJob("Test");
            _store.Complete(handle.JobId, JobResult.Success());

            Assert.Throws<InvalidOperationException>(
                () => _store.MarkRunning(handle.JobId));
            Assert.Throws<InvalidOperationException>(
                () => _store.Fail(
                    handle.JobId,
                    new JobError("Safe", _now)));
        }

        [Test]
        public void ProgressCannotMoveBackward()
        {
            var handle = CreateRunningJob("Test");
            _store.ReportProgress(
                handle.JobId,
                new JobProgress(10, 20, null, null));

            Assert.Throws<InvalidOperationException>(
                () => _store.ReportProgress(
                    handle.JobId,
                    new JobProgress(9, 20, null, null)));
        }

        [Test]
        public void SnapshotsAndTheirCollectionsCannotMutateStoredState()
        {
            var handle = CreateRunningJob("Test");
            var sourceMetrics = new Dictionary<string, long>
            {
                { "updated", 1 }
            };
            _store.ReportProgress(
                handle.JobId,
                new JobProgress(1, 2, "one", sourceMetrics));
            _store.AddWarning(handle.JobId, "first");

            var first = Get(handle);
            sourceMetrics["updated"] = 99;
            var warnings = (ICollection<string>)first.Warnings;
            var metrics =
                (IDictionary<string, long>)first.Progress.Metrics;

            Assert.Throws<NotSupportedException>(
                () => warnings.Add("injected"));
            Assert.Throws<NotSupportedException>(
                () => metrics["updated"] = 99);

            var second = Get(handle);
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.Warnings, Is.EqualTo(new[] { "first" }));
            Assert.That(second.Progress.Metrics["updated"], Is.EqualTo(1));
        }

        [Test]
        public void SingleInstanceRejectsDuplicateAndExposesExistingId()
        {
            var first = _store.Create(
                JobStartOptions.SingleInstance("Test", "shared"));

            var exception = Assert.Throws<JobAlreadyRunningException>(
                () => _store.Create(
                    JobStartOptions.SingleInstance("Test", "SHARED")));

            Assert.That(exception.ExistingJobId, Is.EqualTo(first.JobId));
            Assert.That(exception.ConcurrencyKey, Is.EqualTo("SHARED"));
        }

        [Test]
        public void DifferentKeysAndParallelJobsCanRunTogether()
        {
            var first = _store.Create(
                JobStartOptions.SingleInstance("One", "one"));
            var second = _store.Create(
                JobStartOptions.SingleInstance("Two", "two"));
            var parallelOne = _store.Create(
                JobStartOptions.AllowParallel("Parallel"));
            var parallelTwo = _store.Create(
                JobStartOptions.AllowParallel("Parallel"));

            Assert.That(
                new[]
                {
                    first.JobId,
                    second.JobId,
                    parallelOne.JobId,
                    parallelTwo.JobId
                },
                Is.Unique);
        }

        [TestCase(JobState.Succeeded)]
        [TestCase(JobState.Failed)]
        [TestCase(JobState.Cancelled)]
        public void TerminalStateReleasesConcurrencyKey(JobState terminalState)
        {
            var handle = _store.Create(
                JobStartOptions.SingleInstance("Test", "shared"));
            _store.MarkRunning(handle.JobId);

            if (terminalState == JobState.Succeeded)
            {
                _store.Complete(handle.JobId, JobResult.Success());
            }
            else if (terminalState == JobState.Failed)
            {
                _store.Fail(
                    handle.JobId,
                    new JobError("Safe", _now));
            }
            else
            {
                _store.Cancel(handle.JobId);
            }

            Assert.DoesNotThrow(
                () => _store.Create(
                    JobStartOptions.SingleInstance("Next", "shared")));
        }

        [Test]
        public void ExpiredTerminalJobsAreRemovedLazily()
        {
            var handle = CreateRunningJob("Expired");
            _store.Complete(handle.JobId, JobResult.Success());
            _now = _now.AddHours(2);

            JobStatusSnapshot status;
            Assert.That(_store.TryGet(handle.JobId, out status), Is.False);
            Assert.That(status, Is.Null);
        }

        [Test]
        public void ActiveJobsAreNeverRemovedByCleanup()
        {
            var handle = CreateRunningJob("Active");
            _now = _now.AddDays(30);

            Assert.That(_store.TryGet(handle.JobId, out _), Is.True);
            Assert.That(Get(handle).State, Is.EqualTo(JobState.Running));
        }

        [Test]
        public void WarningStorageIsBoundedButCountIsComplete()
        {
            var handle = CreateRunningJob("Warnings");
            _store.AddWarning(handle.JobId, "one");
            _store.AddWarning(handle.JobId, "two");
            _store.AddWarning(handle.JobId, "three");

            var status = Get(handle);
            Assert.That(status.WarningCount, Is.EqualTo(3));
            Assert.That(status.Warnings, Is.EqualTo(new[] { "one", "two" }));
        }

        private JobHandle CreateRunningJob(string name)
        {
            var handle = _store.Create(
                JobStartOptions.AllowParallel(name));
            _store.MarkRunning(handle.JobId);
            return handle;
        }

        private JobStatusSnapshot Get(JobHandle handle)
        {
            JobStatusSnapshot status;
            Assert.That(_store.TryGet(handle.JobId, out status), Is.True);
            return status;
        }

        private static SitecoreJobSettings CreateSettings(
            TimeSpan retention,
            int maximumWarnings)
        {
            return new SitecoreJobSettings(
                "SitecoreJobs",
                "shell",
                100,
                TimeSpan.FromSeconds(2),
                retention,
                maximumWarnings);
        }
    }
}
