namespace FolderSync.Sync.FileSystem;

public sealed class FileOperations : IFileOperations
{
    private static readonly EnumerationOptions Everything = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
    };

    public IReadOnlyList<FileSystemInfo> Enumerate(DirectoryInfo directory) =>
        directory.EnumerateFileSystemInfos("*", Everything).ToList();

    public void CopyFile(FileInfo source, FileInfo destination)
    {
        ClearReadOnly(destination);
        source.CopyTo(destination.FullName, overwrite: true);
        TryPreserveLastWriteTime(source, destination);
    }

    public void DeleteFile(FileInfo file)
    {
        ClearReadOnly(file);
        file.Delete();
    }

    public void CreateDirectory(DirectoryInfo directory) => directory.Create();

    public void DeleteDirectory(DirectoryInfo directory) => directory.Delete(recursive: false);

    private static void ClearReadOnly(FileInfo file)
    {
        if (file.Exists && file.IsReadOnly)
        {
            file.IsReadOnly = false;
        }
    }

    private static void TryPreserveLastWriteTime(FileInfo source, FileInfo destination)
    {
        try
        {
            File.SetLastWriteTimeUtc(destination.FullName, source.LastWriteTimeUtc);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
