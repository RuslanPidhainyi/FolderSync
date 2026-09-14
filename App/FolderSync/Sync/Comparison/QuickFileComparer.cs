namespace FolderSync.Sync.Comparison;

public sealed class QuickFileComparer : IFileComparer
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(2);

    public bool AreEqual(FileInfo source, FileInfo replica) =>
        source.Length == replica.Length
        && (source.LastWriteTimeUtc - replica.LastWriteTimeUtc).Duration() <= Tolerance;
}
