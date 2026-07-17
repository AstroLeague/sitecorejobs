using System;
using System.Globalization;
using Sitecore.Configuration;

namespace SitecoreJobs.Foundation.Configuration
{
    public sealed class SitecoreJobSettings
    {
        public const string DefaultCategoryValue = "SitecoreJobs";
        public const string DefaultSiteNameValue = "shell";
        public const int DefaultProgressItemInterval = 100;
        public const int DefaultProgressTimeIntervalSeconds = 2;
        public const int DefaultCompletedStatusRetentionMinutes = 1440;
        public const int DefaultMaximumStoredWarnings = 25;

        public SitecoreJobSettings(
            string defaultCategory,
            string defaultSiteName,
            int progressItemInterval,
            TimeSpan progressTimeInterval,
            TimeSpan completedStatusRetention,
            int maximumStoredWarnings)
        {
            if (string.IsNullOrWhiteSpace(defaultCategory))
            {
                throw new ArgumentException(
                    "Default category cannot be empty.",
                    nameof(defaultCategory));
            }

            if (string.IsNullOrWhiteSpace(defaultSiteName))
            {
                throw new ArgumentException(
                    "Default site name cannot be empty.",
                    nameof(defaultSiteName));
            }

            if (progressItemInterval <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(progressItemInterval));
            }

            if (progressTimeInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(progressTimeInterval));
            }

            if (completedStatusRetention <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(completedStatusRetention));
            }

            if (maximumStoredWarnings < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumStoredWarnings));
            }

            DefaultCategory = defaultCategory.Trim();
            DefaultSiteName = defaultSiteName.Trim();
            ProgressItemInterval = progressItemInterval;
            ProgressTimeInterval = progressTimeInterval;
            CompletedStatusRetention = completedStatusRetention;
            MaximumStoredWarnings = maximumStoredWarnings;
        }

        public string DefaultCategory { get; }

        public string DefaultSiteName { get; }

        public int ProgressItemInterval { get; }

        public TimeSpan ProgressTimeInterval { get; }

        public TimeSpan CompletedStatusRetention { get; }

        public int MaximumStoredWarnings { get; }

        public static SitecoreJobSettings Load()
        {
            return new SitecoreJobSettings(
                ReadString(
                    "SitecoreJobs.DefaultCategory",
                    DefaultCategoryValue),
                ReadString(
                    "SitecoreJobs.DefaultSiteName",
                    DefaultSiteNameValue),
                ReadPositiveInt(
                    "SitecoreJobs.ProgressItemInterval",
                    DefaultProgressItemInterval),
                TimeSpan.FromSeconds(
                    ReadPositiveInt(
                        "SitecoreJobs.ProgressTimeIntervalSeconds",
                        DefaultProgressTimeIntervalSeconds)),
                TimeSpan.FromMinutes(
                    ReadPositiveInt(
                        "SitecoreJobs.CompletedStatusRetentionMinutes",
                        DefaultCompletedStatusRetentionMinutes)),
                ReadNonNegativeInt(
                    "SitecoreJobs.MaximumStoredWarnings",
                    DefaultMaximumStoredWarnings));
        }

        private static string ReadString(string name, string fallback)
        {
            var value = Settings.GetSetting(name);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static int ReadPositiveInt(string name, int fallback)
        {
            int value;
            return int.TryParse(
                       Settings.GetSetting(name),
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out value)
                   && value > 0
                ? value
                : fallback;
        }

        private static int ReadNonNegativeInt(string name, int fallback)
        {
            int value;
            return int.TryParse(
                       Settings.GetSetting(name),
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out value)
                   && value >= 0
                ? value
                : fallback;
        }
    }
}
