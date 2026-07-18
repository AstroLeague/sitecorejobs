using Microsoft.Extensions.DependencyInjection;
using Sitecore.DependencyInjection;
using SitecoreJobs.Foundation.Abstractions;
using SitecoreJobs.Foundation.Configuration;
using SitecoreJobs.Foundation.Execution;
using SitecoreJobs.Foundation.State;

namespace SitecoreJobs.Foundation.DependencyInjection
{
    public sealed class ServicesConfigurator : IServicesConfigurator
    {
        public void Configure(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(provider => SitecoreJobSettings.Load());
            serviceCollection.AddSingleton<InMemoryJobStateStore>();
            serviceCollection.AddSingleton<IJobStateStore>(
                provider => provider.GetRequiredService<InMemoryJobStateStore>());
            serviceCollection.AddSingleton<IJobStatusReader>(
                provider => provider.GetRequiredService<InMemoryJobStateStore>());
            serviceCollection.AddSingleton<IJobDispatcher, SitecoreJobDispatcher>();
        }
    }
}
