using System.Diagnostics;

namespace Test.Microsoft.NET.Build.Containers.Filesystem;

[TestClass]
public class DockerRegistryManager
{
    public const string BaseImage = "dotnet/runtime";
    public const string ChiseledImage = "dotnet/nightly/aspnet";
    public const string BaseImageSource = "mcr.microsoft.com/";
    public const string ChiseledImageSource = "mcr.microsoft.com/";
    public const string BaseImageTag = "6.0";
    public const string ChiseledImageTag = "6.0-jammy-chiseled";
    public const string LocalRegistry = "localhost:5010";

    public const string FullyQualifiedBaseImageDefault = $"https://{BaseImageSource}{BaseImage}:{BaseImageTag}";

    private static string s_registryContainerId;

    public static void Ingest(string source, string dest, string image, string tag) {
        Process pullBase = Process.Start("docker", $"pull {source}{image}:{tag}");
        Assert.IsNotNull(pullBase);
        pullBase.WaitForExit();
        Assert.AreEqual(0, pullBase.ExitCode);

        Process tagger = Process.Start("docker", $"tag {source}{image}:{tag} {dest}/{image}:{tag}");
        Assert.IsNotNull(tagger);
        tagger.WaitForExit();
        Assert.AreEqual(0, tagger.ExitCode);

        Process pushBase = Process.Start("docker", $"push {dest}/{image}:{tag}");
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

        Ingest(BaseImageSource, LocalRegistry, BaseImage, BaseImageTag);
        Ingest(ChiseledImageSource, LocalRegistry, ChiseledImage, ChiseledImageTag);
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