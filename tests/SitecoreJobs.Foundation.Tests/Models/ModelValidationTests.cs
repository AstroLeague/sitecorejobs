using System;
using System.Collections.Generic;
using NUnit.Framework;
using SitecoreJobs.Foundation.Models;

namespace SitecoreJobs.Foundation.Tests.Models
{
    [TestFixture]
    public sealed class ModelValidationTests
    {
        [TestCase(-1, null)]
        [TestCase(2, 1)]
        public void JobProgressRejectsInvalidCounts(long processed, long? total)
        {
            Assert.Catch<ArgumentException>(
                () => new JobProgress(processed, total, null, null));
        }

        [Test]
        public void JobProgressDefensivelyCopiesMetrics()
        {
            var metrics = new Dictionary<string, long>
            {
                { "updated", 2 }
            };
            var progress = new JobProgress(2, 2, null, metrics);

            metrics["updated"] = 9;

            Assert.That(progress.Message, Is.Empty);
            Assert.That(progress.Metrics["updated"], Is.EqualTo(2));
            Assert.Throws<NotSupportedException>(
                () => ((IDictionary<string, long>)progress.Metrics)
                    .Add("new", 1));
        }

        [TestCase(null, 1)]
        [TestCase("", 1)]
        [TestCase("valid", -1)]
        public void JobProgressRejectsInvalidMetrics(string name, long value)
        {
            var metrics = new Dictionary<string, long>
            {
                { name ?? string.Empty, value }
            };

            Assert.Throws<ArgumentException>(
                () => new JobProgress(0, null, null, metrics));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void JobStartOptionsRejectsEmptyName(string name)
        {
            Assert.Throws<ArgumentException>(
                () => JobStartOptions.AllowParallel(name));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void SingleInstanceRejectsEmptyConcurrencyKey(string key)
        {
            Assert.Throws<ArgumentException>(
                () => JobStartOptions.SingleInstance("Job", key));
        }

        [Test]
        public void JobResultValidatesWarningsAndDefaultsSummary()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => JobResult.SuccessWithWarnings(null, 0));
            Assert.That(JobResult.Success().Summary, Is.Empty);
        }

        [Test]
        public void JobErrorRequiresSafeMessageAndUtcTimestamp()
        {
            Assert.Throws<ArgumentException>(
                () => new JobError(
                    string.Empty,
                    DateTimeOffset.UtcNow));
            Assert.Throws<ArgumentException>(
                () => new JobError(
                    "safe",
                    new DateTimeOffset(
                        2026,
                        1,
                        1,
                        0,
                        0,
                        0,
                        TimeSpan.FromHours(1))));
        }
    }
}
