using static Microsoft.NET.Build.Containers.KnownStrings;
using static Microsoft.NET.Build.Containers.KnownStrings.Properties;

namespace Test.Microsoft.NET.Build.Containers.Tasks
{
    [TestClass]
    public class ParseContainerPropertiesTests
    {
        [TestMethod]
        public void Baseline()
        {
            var (project, _) = Evaluator.InitProject(new () {
                [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:6.0",
                [ContainerRegistry] = "localhost:5010",
                [ContainerImageName] = "dotnet/testimage",
                [ContainerImageTag] = "5.0"
            });
            var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
            Assert.IsTrue(instance.Build(new[]{ComputeContainerConfig}, null, null, out var outputs));

            Assert.AreEqual("https://mcr.microsoft.com", instance.GetPropertyValue(ContainerBaseRegistry));
            Assert.AreEqual("dotnet/runtime", instance.GetPropertyValue(ContainerBaseName));
            Assert.AreEqual("6.0", instance.GetPropertyValue(ContainerBaseTag));

            Assert.AreEqual("dotnet/testimage", instance.GetPropertyValue(ContainerImageName));
            new []{ "5.0"}.SequenceEqual(instance.GetItems(ContainerImageTags).Select(x => x.EvaluatedInclude));
            Assert.AreNotEqual("localhost:5010", instance.GetPropertyValue(ContainerRegistry));
        }

        [TestMethod]
        public void SpacesGetReplacedWithDashes()
        {
             var (project, _) = Evaluator.InitProject(new () {
                [ContainerBaseImage] = "mcr microsoft com/dotnet runtime:6.0",
                [ContainerRegistry] = "localhost:5010"
            });

            var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
            Assert.IsTrue(instance.Build(new[]{ComputeContainerConfig}, null, null, out var outputs));

            Assert.AreEqual("https://mcr-microsoft-com", instance.GetPropertyValue(ContainerBaseRegistry));
            Assert.AreEqual("dotnet-runtime", instance.GetPropertyValue(ContainerBaseName));
            Assert.AreEqual("6.0", instance.GetPropertyValue(ContainerBaseTag));
        }

        [TestMethod]
        public void RegexCatchesInvalidContainerNames()
        {
             var (project, logs) = Evaluator.InitProject(new () {
                [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:6.0",
                [ContainerRegistry] = "localhost:5010",
                [ContainerImageName] = "dotnet testimage",
                [ContainerImageTag] = "5.0"
            }, captureLogs: true);
            
            var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
            Assert.IsTrue(instance.Build(new[]{ComputeContainerConfig}, new [] { logs }, null, out var outputs));
            Assert.IsTrue(logs.Warnings.Count > 0);
            Assert.AreEqual(logs.Warnings[0].Code, ErrorCodes.CONTAINER001);
        }

        [TestMethod]
        public void RegexCatchesInvalidContainerTags()
        {
            var (project, logs) = Evaluator.InitProject(new () {
                [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:6.0",
                [ContainerRegistry] = "localhost:5010",
                [ContainerImageName] = "dotnet/testimage",
                [ContainerImageTag] = "5 0"
            }, captureLogs: true);

            var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
            Assert.IsFalse(instance.Build(new[]{ComputeContainerConfig},  new [] { logs }, null, out var outputs));

            Assert.IsTrue(logs.Errors.Count > 0);
            Assert.AreEqual(logs.Errors[0].Code, ErrorCodes.CONTAINER004);
        }

        [TestMethod]
        public void CanOnlySupplyOneOfTagAndTags()
        {
            var (project, logs) = Evaluator.InitProject(new () {
                [ContainerBaseImage] = "mcr.microsoft.com/dotnet/runtime:6.0",
                [ContainerRegistry] = "localhost:5010",
                [ContainerImageName] = "dotnet/testimage",
                [ContainerImageTag] = "5.0",
                [ContainerImageTags] = "latest;oldest"
            }, captureLogs: true);

            var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
            Assert.IsFalse(instance.Build(new[]{ComputeContainerConfig},  new [] { logs }, null, out var outputs));

            Assert.IsTrue(logs.Errors.Count > 0);
            Assert.AreEqual(logs.Errors[0].Code, ErrorCodes.CONTAINER005);
        }
    }
}