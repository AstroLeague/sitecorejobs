using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SitecoreJobs.Foundation.Models
{
    public sealed class JobProgress
    {
        private static readonly IReadOnlyDictionary<string, long> EmptyMetrics =
            new ReadOnlyDictionary<string, long>(
                new Dictionary<string, long>());

        public JobProgress(
            long processed,
            long? total,
            string message,
            IDictionary<string, long> metrics)
        {
            if (processed < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(processed),
                    "Processed cannot be negative.");
            }

            if (total.HasValue && total.Value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(total),
                    "Total cannot be negative.");
            }

            if (total.HasValue && processed > total.Value)
            {
                throw new ArgumentException(
                    "Processed cannot exceed total.",
                    nameof(processed));
            }

            Processed = processed;
            Total = total;
            Message = message ?? string.Empty;
            Metrics = CopyMetrics(metrics);
        }

        public long Processed { get; }

        public long? Total { get; }

        public string Message { get; }

        public IReadOnlyDictionary<string, long> Metrics { get; }

        private static IReadOnlyDictionary<string, long> CopyMetrics(
            IDictionary<string, long> metrics)
        {
            if (metrics == null || metrics.Count == 0)
            {
                return EmptyMetrics;
            }

            var copy = new Dictionary<string, long>(
                metrics.Count,
                StringComparer.OrdinalIgnoreCase);

            foreach (var metric in metrics)
            {
                if (string.IsNullOrWhiteSpace(metric.Key))
                {
                    throw new ArgumentException(
                        "Metric names cannot be empty.",
                        nameof(metrics));
                }

                if (metric.Value < 0)
                {
                    throw new ArgumentException(
                        "Metric values cannot be negative.",
                        nameof(metrics));
                }

                copy.Add(metric.Key, metric.Value);
            }

            return new ReadOnlyDictionary<string, long>(copy);
        }
    }
}
