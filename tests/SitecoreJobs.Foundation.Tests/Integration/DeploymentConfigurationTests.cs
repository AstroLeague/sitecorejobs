using System.IO;
using NUnit.Framework;

namespace SitecoreJobs.Foundation.Tests.Integration
{
    [TestFixture]
    public sealed class DeploymentConfigurationTests
    {
        [Test]
        public void FoundationConfigIsRestrictedToContentManagement()
        {
            var config = File.ReadAllText(
                RepositoryPath(
                    "src",
                    "SitecoreJobs.Foundation",
                    "App_Config",
                    "Include",
                    "Foundation",
                    "SitecoreJobs.Foundation.config"));

            Assert.That(
                config,
                Does.Contain("role:require=\"ContentManagement\""));
        }

        [Test]
        public void PublishingProjectCannotReplaceSitecoreWebConfig()
        {
            var projectDirectory = RepositoryPath(
                "src",
                "Build",
                "HelixPublishingPipeline",
                "HPP.Platform");
            var project = File.ReadAllText(
                Path.Combine(projectDirectory, "HPP.Platform.csproj"));

            Assert.That(
                project,
                Does.Contain(
                    "<ExcludeFilesFromDeployment>Web.config;"));
            Assert.That(
                File.Exists(Path.Combine(projectDirectory, "Web.config")),
                Is.False);
        }

        private static string RepositoryPath(params string[] parts)
        {
            var path = Path.GetFullPath(
                Path.Combine(
                    TestContext.CurrentContext.TestDirectory,
                    @"..\..\..\..\..\"));

            foreach (var part in parts)
            {
                path = Path.Combine(path, part);
            }

            return path;
        }
    }
}
