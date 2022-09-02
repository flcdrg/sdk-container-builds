using System.Formats.Tar;
using System.Security.Cryptography;

namespace Microsoft.NET.Build.Containers;

public record struct Layer
{
    public Descriptor Descriptor { get; private set; }

    public string BackingFile { get; private set; }

    public static Layer FromDirectory(string directory, string containerPath, string userName)
    {
        var fileList =
            new DirectoryInfo(directory)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Select(fsi =>
                    {
                        string destinationPath = Path.Join(containerPath, Path.GetRelativePath(directory, fsi.FullName)).Replace(Path.DirectorySeparatorChar, '/');
                        return (fsi.FullName, destinationPath);
                    });
        return FromFiles(fileList, userName);
    }

    static IEnumerable<DirectoryInfo> Walk(DirectoryInfo start) {
        var current = start;
        yield return start;
        while (current.Parent is {} parentDir) {
            current = parentDir;
            yield return parentDir;
        }
    }

    public static Layer FromFiles(IEnumerable<(string path, string containerPath)> fileList, string userName)
    {
        long fileSize;
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];

        string tempTarballPath = ContentStore.GetTempFile();
        using (FileStream fs = File.Create(tempTarballPath))
        {
            var knownDirectoryPrefixes = new HashSet<string>();
            // using (GZipStream gz = new(fs, CompressionMode.Compress)) // TODO: https://github.com/dotnet/sdk-container-builds/issues/29
            using (TarWriter writer = new(fs, TarEntryFormat.Gnu, leaveOpen: true))
            {

                foreach (var item in fileList)
                {
                    var fileInfo = new FileInfo(item.path);
                    var containerFileInfo = new FileInfo(item.containerPath);
                    var containerDir = containerFileInfo.Directory!;
                    if (!knownDirectoryPrefixes.Contains(containerDir.FullName)) {
                        foreach (var directory in Walk(containerDir)) {
                            string containerDirPath = directory.FullName.Replace("c:\\", "").TrimStart(PathSeparators);
                            if (containerDirPath == "") {
                                break;
                            }
                            var dirEntry = new GnuTarEntry(TarEntryType.Directory, containerDirPath);
                            dirEntry.UserName = userName;
                            dirEntry.Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
                            writer.WriteEntry(dirEntry);
                            knownDirectoryPrefixes.Add(directory.FullName);
                        }
                    }
                    // Docker treats a COPY instruction that copies to a path like `/app` by
                    // including `app/` as a directory, with no leading slash. Emulate that here.
                    string containerPath = item.containerPath.TrimStart(PathSeparators);
                    var entry = new GnuTarEntry(TarEntryType.RegularFile, containerPath);
                    entry.UserName = userName;
                    entry.ModificationTime = fileInfo.LastWriteTimeUtc;
                    entry.ChangeTime = fileInfo.LastWriteTimeUtc;
                    entry.AccessTime = fileInfo.LastAccessTimeUtc;
                    entry.Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
                    entry.DataStream = fileInfo.OpenRead();
                    writer.WriteEntry(entry);
                }
            }

            fileSize = fs.Length;

            fs.Position = 0;

            SHA256.HashData(fs, hash);
        }

        string contentHash = Convert.ToHexString(hash).ToLowerInvariant();

        Descriptor descriptor = new()
        {
            MediaType = "application/vnd.docker.image.rootfs.diff.tar", // TODO: configurable? gzip always?
            Size = fileSize,
            Digest = $"sha256:{contentHash}"
        };

        string storedContent = ContentStore.PathForDescriptor(descriptor);

        Directory.CreateDirectory(ContentStore.ContentRoot);

        File.Move(tempTarballPath, storedContent, overwrite: true);

        Layer l = new()
        {
            Descriptor = descriptor,
            BackingFile = storedContent,
        };

        return l;
    }

    private readonly static char[] PathSeparators = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

}