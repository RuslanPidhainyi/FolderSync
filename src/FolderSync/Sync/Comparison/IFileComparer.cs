namespace FolderSync.Sync.Comparison;

/// <summary>
/// Strategy that decides whether the replica copy of a file is still identical to its source.
/// Implementations are created by <see cref="FileComparerFactory"/>.
/// </summary>
public interface IFileComparer
{
    bool AreEqual(FileInfo source, FileInfo replica);
}
