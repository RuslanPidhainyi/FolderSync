namespace FolderSync.Tests.Support;

/// <summary>A unique folder under the system temp path, removed on dispose.</summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FolderSyncTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Sub(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public string CreateDir(params string[] parts)
    {
        var dir = Sub(parts);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string WriteFile(string relativePath, string content)
    {
        var file = Sub(relativePath.Split('/', '\\'));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
        return file;
    }

    public string ReadFile(string relativePath) => File.ReadAllText(Sub(relativePath.Split('/', '\\')));

    public bool FileExists(string relativePath) => File.Exists(Sub(relativePath.Split('/', '\\')));

    public bool DirExists(string relativePath) => Directory.Exists(Sub(relativePath.Split('/', '\\')));

    /// <summary>Relative paths of all files and folders inside, sorted, using '/' as separator.</summary>
    public IReadOnlyList<string> Snapshot()
    {
        var root = System.IO.Path.TrimEndingDirectorySeparator(Path);
        return Directory
            .EnumerateFileSystemEntries(root, "*", new EnumerationOptions { AttributesToSkip = 0, RecurseSubdirectories = true })
            .Select(p => System.IO.Path.GetRelativePath(root, p).Replace('\\', '/') + (Directory.Exists(p) ? "/" : string.Empty))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup.
        }
    }
}
