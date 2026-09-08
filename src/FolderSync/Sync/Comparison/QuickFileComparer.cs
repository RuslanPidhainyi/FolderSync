namespace FolderSync.Sync.Comparison;

/// <summary>
/// Cheap heuristic used by <c>rsync</c> by default: two files are considered equal when they have
/// the same size and (almost) the same last-write time. No file content is read.
/// </summary>
public sealed class QuickFileComparer : IFileComparer
{
    // FAT/exFAT store timestamps with a 2-second resolution, so an exact match would
    // re-copy every file when the replica lives on such a volume.
    private static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(2);

    public bool AreEqual(FileInfo source, FileInfo replica) =>
        source.Length == replica.Length
        && (source.LastWriteTimeUtc - replica.LastWriteTimeUtc).Duration() <= Tolerance;
}
