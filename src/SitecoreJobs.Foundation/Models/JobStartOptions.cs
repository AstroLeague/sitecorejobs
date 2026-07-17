using System;

namespace SitecoreJobs.Foundation.Models
{
    public sealed class JobStartOptions
    {
        private JobStartOptions(
            string name,
            string category,
            string siteName,
            JobConcurrencyMode concurrencyMode,
            string concurrencyKey)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Job name cannot be empty.", nameof(name));
            }

            if (concurrencyMode == JobConcurrencyMode.SingleInstance
                && string.IsNullOrWhiteSpace(concurrencyKey))
            {
                throw new ArgumentException(
                    "A concurrency key is required for a single-instance job.",
                    nameof(concurrencyKey));
            }

            if (concurrencyMode != JobConcurrencyMode.AllowParallel
                && concurrencyMode != JobConcurrencyMode.SingleInstance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(concurrencyMode),
                    concurrencyMode,
                    "The concurrency mode is invalid.");
            }

            Name = name.Trim();
            Category = NormalizeOptional(category);
            SiteName = NormalizeOptional(siteName);
            ConcurrencyMode = concurrencyMode;
            ConcurrencyKey = concurrencyMode == JobConcurrencyMode.SingleInstance
                ? concurrencyKey.Trim()
                : null;
        }

        public string Name { get; }

        public string Category { get; }

        public string SiteName { get; }

        public JobConcurrencyMode ConcurrencyMode { get; }

        public string ConcurrencyKey { get; }

        public static JobStartOptions AllowParallel(
            string name,
            string category = null,
            string siteName = null)
        {
            return new JobStartOptions(
                name,
                category,
                siteName,
                JobConcurrencyMode.AllowParallel,
                null);
        }

        public static JobStartOptions SingleInstance(
            string name,
            string concurrencyKey,
            string category = null,
            string siteName = null)
        {
            return new JobStartOptions(
                name,
                category,
                siteName,
                JobConcurrencyMode.SingleInstance,
                concurrencyKey);
        }

        private static string NormalizeOptional(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
