namespace FolderSync.Sync;

public interface IFolderSynchronizer
{
    /// <summary>
    /// Performs one full synchronization pass and returns a summary of what was done.
    /// </summary>
    SyncResult Synchronize(CancellationToken cancellationToken = default);
}
