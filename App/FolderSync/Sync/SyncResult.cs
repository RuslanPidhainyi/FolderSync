namespace FolderSync.Sync;

/// <summary>
/// Summary of a single synchronization pass.
/// </summary>
public sealed record SyncResult(
    int FilesCreated,
    int FilesUpdated,
    int FilesDeleted,
    int DirectoriesCreated,
    int DirectoriesDeleted,
    int Errors,
    TimeSpan Duration)
{
    public int TotalChanges => FilesCreated + FilesUpdated + FilesDeleted + DirectoriesCreated + DirectoriesDeleted;

    public override string ToString() =>
        $"{FilesCreated} file(s) created, {FilesUpdated} updated, {FilesDeleted} deleted; " +
        $"{DirectoriesCreated} folder(s) created, {DirectoriesDeleted} deleted; " +
        $"{Errors} error(s); took {Duration.TotalMilliseconds:F0} ms";
}
