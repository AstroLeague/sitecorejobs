using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SitecoreJobs.Foundation.Abstractions;
using SitecoreJobs.Foundation.Configuration;
using SitecoreJobs.Foundation.DependencyInjection;

namespace SitecoreJobs.Foundation.Tests.DependencyInjection
{
    [TestFixture]
    public sealed class ServicesConfiguratorTests
    {
        [Test]
        public void ReaderAndStoreResolveToSameSingleton()
        {
            var services = new ServiceCollection();
            new ServicesConfigurator().Configure(services);

            using (var provider = services.BuildServiceProvider())
            {
                var store = provider.GetRequiredService<IJobStateStore>();
                var reader = provider.GetRequiredService<IJobStatusReader>();

                Assert.That(reader, Is.SameAs(store));
                Assert.That(
                    provider.GetRequiredService<IJobDispatcher>(),
                    Is.SameAs(
                        provider.GetRequiredService<IJobDispatcher>()));
            }
        }

        [Test]
        public void MissingSitecoreSettingsUseSafeDefaults()
        {
            var settings = SitecoreJobSettings.Load();

            Assert.That(
                settings.DefaultCategory,
                Is.EqualTo(SitecoreJobSettings.DefaultCategoryValue));
            Assert.That(
                settings.DefaultSiteName,
                Is.EqualTo(SitecoreJobSettings.DefaultSiteNameValue));
        }
    }
}
