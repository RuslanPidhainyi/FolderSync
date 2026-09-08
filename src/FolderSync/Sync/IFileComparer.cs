namespace FolderSync.Sync;

/// <summary>
/// Decides whether the replica copy of a file is still identical to its source.
/// </summary>
public interface IFileComparer
{
    bool AreEqual(FileInfo source, FileInfo replica);
}
