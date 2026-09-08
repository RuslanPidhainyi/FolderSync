namespace FolderSync.Sync;

/// <summary>The kinds of change a synchronization pass can make to the replica.</summary>
public enum SyncOperation
{
    CreateFile,
    UpdateFile,
    DeleteFile,
    CreateDirectory,
    DeleteDirectory,
}
