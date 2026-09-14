namespace FolderSync.Sync.FileSystem;

public interface IFileOperations
{
    IReadOnlyList<FileSystemInfo> Enumerate(DirectoryInfo directory);

    void CopyFile(FileInfo source, FileInfo destination);

    void DeleteFile(FileInfo file);

    void CreateDirectory(DirectoryInfo directory);

    void DeleteDirectory(DirectoryInfo directory);
}
