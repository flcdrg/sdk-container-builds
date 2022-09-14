using System.Diagnostics;

namespace Test.Microsoft.NET.Build.Containers.Filesystem;

[TestClass]
public class DockerRegistryManager
{
    public const string BaseImage = "dotnet/runtime";
    public const string BaseImageSource = "mcr.microsoft.com/";
    public const string BaseImageTag = "6.0";

    public const string ChiseledImage = "dotnet/nightly/aspnet";
    public const string ChiseledImageTag = "6.0-jammy-chiseled";

    public const string LocalRegistry = "localhost:5010";

    public static readonly string FullyQualifiedBaseImageDefault = FullImageUrl(BaseImageSource, BaseImage, BaseImageTag);
    public static string FullImageUrl(string source, string image, string tag) => $"https://{source.TrimEnd('/')}/{image}:{tag}";

    private static string s_registryContainerId;

    public static void Pull(string sourceRegistry, string image, string tag, string targetRegistry) {
        Process pullBase = Process.Start("docker", $"pull {sourceRegistry}{image}:{tag}");
        Assert.IsNotNull(pullBase);
        pullBase.WaitForExit();
        Assert.AreEqual(0, pullBase.ExitCode);

        Process tagger = Process.Start("docker", $"tag {sourceRegistry}{image}:{tag} {targetRegistry}/{image}:{tag}");
        Assert.IsNotNull(tagger);
        tagger.WaitForExit();
        Assert.AreEqual(0, tagger.ExitCode);

        Process pushBase = Process.Start("docker", $"push {targetRegistry}/{image}:{tag}");
        Assert.IsNotNull(pushBase);
        pushBase.WaitForExit();
        Assert.AreEqual(0, pushBase.ExitCode);
    }

    [AssemblyInitialize]
    public static void StartAndPopulateDockerRegistry(TestContext context)
    {
        Console.WriteLine(nameof(StartAndPopulateDockerRegistry));

        ProcessStartInfo startRegistry = new("docker", "run --rm --publish 5010:5000 --detach registry:2")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using Process registryProcess = Process.Start(startRegistry);
        Assert.IsNotNull(registryProcess);
        string registryContainerId = registryProcess.StandardOutput.ReadLine();
        // debugging purposes
        string everythingElse = registryProcess.StandardOutput.ReadToEnd();
        string errStream = registryProcess.StandardError.ReadToEnd();
        Assert.IsNotNull(registryContainerId);
        registryProcess.WaitForExit();
        Assert.AreEqual(0, registryProcess.ExitCode, $"Could not start Docker registry. Are you running one for manual testing?{Environment.NewLine}{errStream}");

        s_registryContainerId = registryContainerId;

        Pull(BaseImageSource, BaseImage, BaseImageTag, LocalRegistry);
        Pull(BaseImageSource, ChiseledImage, ChiseledImageTag, LocalRegistry);
    }

    [AssemblyCleanup]
    public static void ShutdownDockerRegistry()
    {
        Assert.IsNotNull(s_registryContainerId);

        Process shutdownRegistry = Process.Start("docker", $"stop {s_registryContainerId}");
        Assert.IsNotNull(shutdownRegistry);
        shutdownRegistry.WaitForExit();
        Assert.AreEqual(0, shutdownRegistry.ExitCode);
    }
}