namespace FolderSync.Sync;

/// <summary>Mutable counters for one pass; turned into an immutable <see cref="SyncResult"/> at the end.</summary>
internal sealed class SyncStatistics
{
    private readonly Dictionary<SyncOperation, int> _counts = [];
    private int _errors;

    public void Record(SyncOperation operation) => _counts[operation] = Count(operation) + 1;

    public void RecordError() => _errors++;

    public SyncResult ToResult(TimeSpan duration) => new(
        Count(SyncOperation.CreateFile),
        Count(SyncOperation.UpdateFile),
        Count(SyncOperation.DeleteFile),
        Count(SyncOperation.CreateDirectory),
        Count(SyncOperation.DeleteDirectory),
        _errors,
        duration);

    private int Count(SyncOperation operation) => _counts.GetValueOrDefault(operation);
}
