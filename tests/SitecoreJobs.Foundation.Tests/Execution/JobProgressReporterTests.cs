using System;
using NUnit.Framework;
using SitecoreJobs.Foundation.Abstractions;
using SitecoreJobs.Foundation.Configuration;
using SitecoreJobs.Foundation.Execution;
using SitecoreJobs.Foundation.Models;
using SitecoreJobs.Foundation.State;

namespace SitecoreJobs.Foundation.Tests.Execution
{
    [TestFixture]
    public sealed class JobProgressReporterTests
    {
        private DateTimeOffset _now;
        private RecordingStateStore _stateStore;
        private JobProgressReporter _reporter;

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
            _stateStore = new RecordingStateStore();
            _reporter = new JobProgressReporter(
                Guid.NewGuid(),
                _stateStore,
                CreateSettings(100, TimeSpan.FromSeconds(2), 25),
                () => _now);
        }

        [Test]
        public void PublishesAtItemThreshold()
        {
            _reporter.Report(new JobProgress(99, 200, null, null));
            Assert.That(_stateStore.ProgressReports, Is.EqualTo(0));

            _reporter.Report(new JobProgress(100, 200, null, null));

            Assert.That(_stateStore.ProgressReports, Is.EqualTo(1));
            Assert.That(
                _stateStore.LastProgress.Processed,
                Is.EqualTo(100));
        }

        [Test]
        public void PublishesAtTimeThreshold()
        {
            _reporter.Report(new JobProgress(1, 200, null, null));
            _now = _now.AddSeconds(2);
            _reporter.Report(new JobProgress(2, 200, null, null));

            Assert.That(_stateStore.ProgressReports, Is.EqualTo(1));
            Assert.That(_stateStore.LastProgress.Processed, Is.EqualTo(2));
        }

        [Test]
        public void DoesNotPublishEveryReport()
        {
            for (var processed = 1; processed < 100; processed++)
            {
                _reporter.Report(
                    new JobProgress(processed, 100, null, null));
            }

            Assert.That(_stateStore.ProgressReports, Is.EqualTo(0));
        }

        [Test]
        public void FlushPublishesFinalPendingProgress()
        {
            _reporter.Report(new JobProgress(25, 100, "pending", null));
            _reporter.Flush();
            _reporter.Flush();

            Assert.That(_stateStore.ProgressReports, Is.EqualTo(1));
            Assert.That(
                _stateStore.LastProgress.Message,
                Is.EqualTo("pending"));
        }

        [Test]
        public void WarningsAreBoundedWhileFullCountIsTracked()
        {
            var settings = CreateSettings(
                100,
                TimeSpan.FromSeconds(2),
                2);
            var store = new InMemoryJobStateStore(
                settings,
                () => _now);
            var handle = store.Create(
                JobStartOptions.AllowParallel("Warnings"));
            store.MarkRunning(handle.JobId);
            var reporter = new JobProgressReporter(
                handle.JobId,
                store,
                settings,
                () => _now);

            reporter.AddWarning("one");
            reporter.AddWarning("two");
            reporter.AddWarning("three");
            reporter.AddWarning(" ");

            JobStatusSnapshot status;
            store.TryGet(handle.JobId, out status);
            Assert.That(status.WarningCount, Is.EqualTo(3));
            Assert.That(status.Warnings, Is.EqualTo(new[] { "one", "two" }));
        }

        private static SitecoreJobSettings CreateSettings(
            int itemInterval,
            TimeSpan timeInterval,
            int maximumWarnings)
        {
            return new SitecoreJobSettings(
                "SitecoreJobs",
                "shell",
                itemInterval,
                timeInterval,
                TimeSpan.FromHours(24),
                maximumWarnings);
        }

        private sealed class RecordingStateStore : IJobStateStore
        {
            public int ProgressReports { get; private set; }

            public JobProgress LastProgress { get; private set; }

            public JobHandle Create(JobStartOptions options)
            {
                throw new NotSupportedException();
            }

            public void MarkRunning(Guid jobId)
            {
            }

            public void ReportProgress(Guid jobId, JobProgress progress)
            {
                ProgressReports++;
                LastProgress = progress;
            }

            public void AddWarning(Guid jobId, string warning)
            {
            }

            public void Complete(Guid jobId, JobResult result)
            {
            }

            public void Fail(Guid jobId, JobError error)
            {
            }

            public void Cancel(Guid jobId)
            {
            }

            public void EnsureFailed(Guid jobId, JobError error)
            {
            }

            public bool TryGet(Guid jobId, out JobStatusSnapshot status)
            {
                status = null;
                return false;
            }
        }
    }
}
