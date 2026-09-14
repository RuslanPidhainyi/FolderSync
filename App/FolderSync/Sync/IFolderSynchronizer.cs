namespace FolderSync.Sync;

public interface IFolderSynchronizer
{
    SyncResult Synchronize(CancellationToken cancellationToken = default);
}
