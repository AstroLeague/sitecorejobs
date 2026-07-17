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
    public sealed class SitecoreJobDispatcherTests
    {
        private SitecoreJobSettings _settings;
        private InMemoryJobStateStore _store;
        private SitecoreJobDispatcher _dispatcher;

        [SetUp]
        public void SetUp()
        {
            _settings = new SitecoreJobSettings(
                "SitecoreJobs",
                "shell",
                100,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromHours(24),
                25);
            _store = new InMemoryJobStateStore(_settings);
            _dispatcher = new SitecoreJobDispatcher(_store, _settings);
        }

        [Test]
        public void StartRejectsNullJob()
        {
            Assert.Throws<ArgumentNullException>(
                () => _dispatcher.Start<object>(
                    null,
                    new object(),
                    JobStartOptions.AllowParallel("Test")));
        }

        [Test]
        public void StartRejectsNullRequest()
        {
            Assert.Throws<ArgumentNullException>(
                () => _dispatcher.Start(
                    new SuccessfulJob(),
                    null,
                    JobStartOptions.AllowParallel("Test")));
        }

        [Test]
        public void StartRejectsNullOptions()
        {
            Assert.Throws<ArgumentNullException>(
                () => _dispatcher.Start(
                    new SuccessfulJob(),
                    new object(),
                    null));
        }

        [Test]
        public void SitecoreStartFailureMarksFailedAndReleasesConcurrency()
        {
            var capturingStore = new CapturingStateStore(_store);
            var dispatcher = new SitecoreJobDispatcher(
                capturingStore,
                _settings,
                sitecoreOptions => throw new InvalidOperationException(
                    "Sitecore start failed."));
            var options = JobStartOptions.SingleInstance(
                "Test",
                "dispatcher-failure");

            Assert.Throws<InvalidOperationException>(
                () => dispatcher.Start(
                    new SuccessfulJob(),
                    new object(),
                    options));

            JobStatusSnapshot failed;
            Assert.That(
                _store.TryGet(capturingStore.LastHandle.JobId, out failed),
                Is.True);
            Assert.That(failed.State, Is.EqualTo(JobState.Failed));
            Assert.DoesNotThrow(
                () => _store.Create(
                    JobStartOptions.SingleInstance(
                        "Next",
                        "dispatcher-failure")));
        }

        private sealed class SuccessfulJob : IBackgroundJob<object>
        {
            public JobResult Execute(
                JobExecutionContext context,
                object request)
            {
                return JobResult.Success();
            }
        }

        private sealed class CapturingStateStore : IJobStateStore
        {
            private readonly IJobStateStore _inner;

            public CapturingStateStore(IJobStateStore inner)
            {
                _inner = inner;
            }

            public JobHandle LastHandle { get; private set; }

            public JobHandle Create(JobStartOptions options)
            {
                LastHandle = _inner.Create(options);
                return LastHandle;
            }

            public void MarkRunning(Guid jobId)
            {
                _inner.MarkRunning(jobId);
            }

            public void ReportProgress(Guid jobId, JobProgress progress)
            {
                _inner.ReportProgress(jobId, progress);
            }

            public void AddWarning(Guid jobId, string warning)
            {
                _inner.AddWarning(jobId, warning);
            }

            public void Complete(Guid jobId, JobResult result)
            {
                _inner.Complete(jobId, result);
            }

            public void Fail(Guid jobId, JobError error)
            {
                _inner.Fail(jobId, error);
            }

            public void Cancel(Guid jobId)
            {
                _inner.Cancel(jobId);
            }

            public void EnsureFailed(Guid jobId, JobError error)
            {
                _inner.EnsureFailed(jobId, error);
            }

            public bool TryGet(
                Guid jobId,
                out JobStatusSnapshot status)
            {
                return _inner.TryGet(jobId, out status);
            }
        }
    }
}
