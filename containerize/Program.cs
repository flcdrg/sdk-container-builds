using System.CommandLine;
using Microsoft.NET.Build.Containers;
using System.Text.Json;


/*
var fileOption = new Argument<DirectoryInfo>(
    name: "folder",
    description: "The folder to pack.")
    .LegalFilePathsOnly().ExistingOnly();

Option<string> registryUri = new(
    name: "--registry",
    description: "Location of the registry to push to.",
    getDefaultValue: () => "localhost:5010");

Option<string> baseImageName = new(
    name: "--base",
    description: "Base image name.",
    getDefaultValue: () => "dotnet/runtime");

Option<string> baseImageTag = new(
    name: "--baseTag",
    description: "Base image tag.",
    getDefaultValue: () => $"{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription[5]}.0");

Option<string[]> entrypoint = new(
    name: "--entrypoint",
    description: "Entrypoint application command.");

Option<string> imageName = new(
    name: "--name",
    description: "Name of the new image.");

var imageTag = new Option<string>("--tag", description: "Tag of the new image.", getDefaultValue: () => "latest");

var workingDir = new Option<string>("--working-dir", description: "The working directory of the application", getDefaultValue: () => "/app");
*/

var baseRegistryArg = new Argument<string>(
    name: "--baseregistry",
    description: "The base registry to use");

var baseImageNameArg = new Argument<string>(
    name: "--baseimagename",
    description: "The base image to pull.");

// Add validator here
var baseImageTagOption = new Option<string>(
    name: "--baseimagetag",
    description: "The base image tag. Ex: 6.0");

var outputRegistryArg = new Argument<string>(
    name: "--outputregistry",
    description: "The registry to push to.");

var imageNameArg = new Argument<string>(
    name: "--imagename",
    description: "The name of the output image that will be pushed to the registry.");

var imageTagsArg = new Argument<string[]>(
    name: "--imagetags",
    description: "The tags to associate with the new image.");

var publishDirectoryArg = new Argument<DirectoryInfo>(
    name: "--publishdirectory",
    description: "The directory for the build outputs to be published.")
    .LegalFilePathsOnly().ExistingOnly();

var workingDirectoryArg = new Argument<string>(
    name: "--workingdirectory",
    description: "The working directory of the container.");

var entrypointArg = new Argument<string[]>(
    name: "--entrypoint",
    description: "The entrypoint application of the container.");

var entrypointArgsOption = new Option<string[]>(
    name: "--entrypointargs",
    description: "Arguments to pass alongside Entrypoint.");

var labelsOption = new Option<string[]>(
    name: "--labels",
    description: "Labels that the image configuration will include in metadata.");

RootCommand root = new RootCommand("Containerize an application without Docker.")
{
    baseRegistryArg,
    baseImageNameArg,
    baseImageTagOption,
    outputRegistryArg,
    imageNameArg,
    imageTagsArg,
    publishDirectoryArg,
    workingDirectoryArg,
    entrypointArg,
    entrypointArgsOption,
    labelsOption
};
/////


/*
RootCommand rootCommand = new("Containerize an application without Docker."){
    fileOption,
    registryUri,
    baseImageName,
    baseImageTag,
    entrypoint,
    imageName,
    imageTag,
    workingDir
};
rootCommand.SetHandler(async (folder, containerWorkingDir, uri, baseImageName, baseTag, entrypoint, imageName, imageTag) =>
{
    await Containerize(folder, containerWorkingDir, uri, baseImageName, baseTag, entrypoint, imageName, imageTag);
},
    fileOption,
    workingDir,
    registryUri,
    baseImageName,
    baseImageTag, 
    entrypoint,
    imageName,
    imageTag
    );

return await rootCommand.InvokeAsync(args);
*/
async Task Containerize(DirectoryInfo folder, string workingDir, string registryName, string baseName, string baseTag, string[] entrypoint, string imageName, string imageTag)
{
    Registry registry = new Registry(new Uri($"http://{registryName}"));

    Console.WriteLine($"Reading from {registry.BaseUri}");

    Image x = await registry.GetImageManifest(baseName, baseTag);
    x.WorkingDirectory = workingDir;

    JsonSerializerOptions options = new()
    {
        WriteIndented = true,
    };

    Console.WriteLine($"Copying from {folder.FullName} to {workingDir}");
    Layer l = Layer.FromDirectory(folder.FullName, workingDir);

    x.AddLayer(l);

    x.SetEntrypoint(entrypoint);

    // File.WriteAllTextAsync("manifest.json", x.manifest.ToJsonString(options));
    // File.WriteAllTextAsync("config.json", x.config.ToJsonString(options));

    await LocalDocker.Load(x, imageName, imageTag, baseName);

    Console.WriteLine($"Loaded image into local Docker daemon. Use 'docker run --rm -it --name {imageName} {registryName}/{imageName}:{imageTag}' to run the application.");
}
