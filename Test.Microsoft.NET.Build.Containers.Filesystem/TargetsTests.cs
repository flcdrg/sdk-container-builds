using static Microsoft.NET.Build.Containers.KnownStrings.Properties;

namespace Test.Microsoft.NET.Build.Containers.Targets;

[TestClass]
public class TargetsTests
{

    [DataRow(true, "/app/foo.exe")]
    [DataRow(false, "dotnet", "/app/foo.dll")]
    [TestMethod]
    public void CanSetEntrypointArgsToUseAppHost(bool useAppHost, params string[] entrypointArgs)
    {
        var (project, _) = Evaluator.InitProject(new()
        {
            [UseAppHost] = useAppHost.ToString()
        });
        Assert.IsTrue(project.Build(ComputeContainerConfig));
        var computedEntrypointArgs = project.GetItems(ContainerEntrypoint).Select(i => i.EvaluatedInclude).ToArray();
        foreach (var (First, Second) in entrypointArgs.Zip(computedEntrypointArgs))
        {
            Assert.AreEqual(First, Second);
        }
    }

    [DataRow("WebApplication44", "webapplication44", true)]
    [DataRow("friendly-suspicious-alligator", "friendly-suspicious-alligator", true)]
    [DataRow("*friendly-suspicious-alligator", "", false)]
    [DataRow("web/app2+7", "web/app2-7", true)]
    [DataRow("Microsoft.Apps.Demo.ContosoWeb", "microsoft.apps.demo.contosoweb", true)]
    [TestMethod]
    public void CanNormalizeInputContainerNames(string projectName, string expectedContainerImageName, bool shouldPass)
    {
        var (project, _) = Evaluator.InitProject(new()
        {
            [AssemblyName] = projectName
        });
        var instance = project.CreateProjectInstance(global::Microsoft.Build.Execution.ProjectInstanceSettings.None);
        Assert.AreEqual(shouldPass, instance.Build(new[]{ComputeContainerConfig}, null, null, out var outputs), "Build should have succeeded");
        Assert.AreEqual(expectedContainerImageName, instance.GetPropertyValue(ContainerImageName));
    }
}