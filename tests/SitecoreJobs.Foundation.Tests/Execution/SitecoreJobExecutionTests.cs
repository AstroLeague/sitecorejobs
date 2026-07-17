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
    public sealed class SitecoreJobExecutionTests
    {
        private SitecoreJobSettings _settings;
        private InMemoryJobStateStore _store;

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
        }

        [Test]
        public void SuccessfulExecutionTransitionsThroughRunningToSucceeded()
        {
            JobState stateDuringExecution = JobState.Queued;
            var handle = CreateHandle("Success");
            var job = new DelegateJob(
                context =>
                {
                    stateDuringExecution = Get(handle).State;
                    return JobResult.Success("done");
                });

            CreateExecution(handle, job).Run();

            Assert.That(stateDuringExecution, Is.EqualTo(JobState.Running));
            Assert.That(Get(handle).State, Is.EqualTo(JobState.Succeeded));
            Assert.That(Get(handle).Summary, Is.EqualTo("done"));
        }

        [Test]
        public void WarningResultFinishesWithWarnings()
        {
            var handle = CreateHandle("Warnings");
            var job = new DelegateJob(
                context => JobResult.SuccessWithWarnings("review", 2));

            CreateExecution(handle, job).Run();

            Assert.That(
                Get(handle).State,
                Is.EqualTo(JobState.SucceededWithWarnings));
            Assert.That(Get(handle).WarningCount, Is.EqualTo(2));
        }

        [Test]
        public void ExceptionIsLoggedInFullButOnlySafeErrorIsStored()
        {
            var handle = CreateHandle("Failure");
            var original = new InvalidOperationException(
                "secret connection detail");
            Exception logged = null;
            var job = new DelegateJob(context => throw original);

            CreateExecution(
                handle,
                job,
                (message, exception) => logged = exception).Run();

            var status = Get(handle);
            Assert.That(logged, Is.SameAs(original));
            Assert.That(status.State, Is.EqualTo(JobState.Failed));
            Assert.That(
                status.Error.Message,
                Is.EqualTo(
                    "The background job failed. "
                    + "See the Sitecore logs for details."));
            Assert.That(
                status.Error.Message,
                Does.Not.Contain("secret"));
        }

        [Test]
        public void OperationCanceledExceptionFinishesAsCancelled()
        {
            var handle = CreateHandle("Cancelled");
            var job = new DelegateJob(
                context => throw new OperationCanceledException());

            CreateExecution(handle, job).Run();

            Assert.That(Get(handle).State, Is.EqualTo(JobState.Cancelled));
        }

        [Test]
        public void FinalPendingProgressIsFlushedBeforeCompletion()
        {
            var handle = CreateHandle("Progress");
            var job = new DelegateJob(
                context =>
                {
                    context.Progress.Report(
                        new JobProgress(12, 100, "last", null));
                    return JobResult.Success();
                });

            CreateExecution(handle, job).Run();

            Assert.That(Get(handle).Progress.Processed, Is.EqualTo(12));
            Assert.That(Get(handle).Progress.Message, Is.EqualTo("last"));
        }

        [TestCase("success")]
        [TestCase("failure")]
        [TestCase("cancel")]
        public void EveryTerminalPathReleasesConcurrency(string outcome)
        {
            var handle = _store.Create(
                JobStartOptions.SingleInstance("First", "shared"));
            var job = new DelegateJob(
                context =>
                {
                    if (outcome == "failure")
                    {
                        throw new InvalidOperationException("failure");
                    }

                    if (outcome == "cancel")
                    {
                        throw new OperationCanceledException();
                    }

                    return JobResult.Success();
                });

            CreateExecution(handle, job).Run();

            Assert.DoesNotThrow(
                () => _store.Create(
                    JobStartOptions.SingleInstance("Next", "shared")));
        }

        [Test]
        public void ExecutionCannotCompleteTwice()
        {
            var calls = 0;
            var handle = CreateHandle("Once");
            var job = new DelegateJob(
                context =>
                {
                    calls++;
                    return JobResult.Success();
                });
            var execution = CreateExecution(handle, job);

            execution.Run();
            execution.Run();

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(Get(handle).State, Is.EqualTo(JobState.Succeeded));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TerminalPersistenceFailureDoesNotEscape(bool cancelled)
        {
            var handle = _store.Create(
                JobStartOptions.SingleInstance(
                    "Persistence failure",
                    "persistence-failure"));
            var job = new DelegateJob(
                context =>
                {
                    if (cancelled)
                    {
                        throw new OperationCanceledException();
                    }

                    throw new InvalidOperationException("failure");
                });
            var throwingStore = new ThrowingTerminalStore(_store);

            Assert.DoesNotThrow(
                () => CreateExecution(
                    handle,
                    job,
                    null,
                    throwingStore).Run());
            Assert.That(
                Get(handle).State,
                Is.EqualTo(JobState.Failed));
            Assert.DoesNotThrow(
                () => _store.Create(
                    JobStartOptions.SingleInstance(
                        "Next",
                        "persistence-failure")));
        }

        private JobHandle CreateHandle(string name)
        {
            return _store.Create(JobStartOptions.AllowParallel(name));
        }

        private SitecoreJobExecution<object> CreateExecution(
            JobHandle handle,
            IBackgroundJob<object> job,
            Action<string, Exception> errorLogger = null,
            IJobStateStore stateStore = null)
        {
            return new SitecoreJobExecution<object>(
                handle,
                "Test",
                null,
                job,
                new object(),
                stateStore ?? _store,
                _settings,
                errorLogger);
        }

        private JobStatusSnapshot Get(JobHandle handle)
        {
            JobStatusSnapshot status;
            Assert.That(_store.TryGet(handle.JobId, out status), Is.True);
            return status;
        }

        private sealed class DelegateJob : IBackgroundJob<object>
        {
            private readonly Func<JobExecutionContext, JobResult> _execute;

            public DelegateJob(
                Func<JobExecutionContext, JobResult> execute)
            {
                _execute = execute;
            }

            public JobResult Execute(
                JobExecutionContext context,
                object request)
            {
                return _execute(context);
            }
        }

        private sealed class ThrowingTerminalStore : IJobStateStore
        {
            private readonly IJobStateStore _inner;

            public ThrowingTerminalStore(IJobStateStore inner)
            {
                _inner = inner;
            }

            public JobHandle Create(JobStartOptions options)
            {
                return _inner.Create(options);
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
                throw new InvalidOperationException("store failure");
            }

            public void Cancel(Guid jobId)
            {
                throw new InvalidOperationException("store failure");
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
