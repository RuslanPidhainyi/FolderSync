namespace FolderSync.Sync.FileSystem;

/// <summary>
/// The file system primitives the synchronizer needs. Keeping them behind an interface separates
/// the synchronization algorithm from platform details (attributes, timestamps, enumeration flags)
/// and lets tests inject failures without touching the disk.
/// </summary>
public interface IFileOperations
{
    /// <summary>Lists the direct children of a folder, including hidden and system entries.</summary>
    IReadOnlyList<FileSystemInfo> Enumerate(DirectoryInfo directory);

    /// <summary>Copies a file, overwriting the destination if it exists.</summary>
    void CopyFile(FileInfo source, FileInfo destination);

    void DeleteFile(FileInfo file);

    void CreateDirectory(DirectoryInfo directory);

    /// <summary>Deletes a folder that is already empty.</summary>
    void DeleteDirectory(DirectoryInfo directory);
}
