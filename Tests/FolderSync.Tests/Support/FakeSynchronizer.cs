using FolderSync.Sync;

namespace FolderSync.Tests.Support;

public sealed class FakeSynchronizer(Func<int, SyncResult> behaviour) : IFolderSynchronizer
{
    public int Calls { get; private set; }

    public SyncResult Synchronize(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        return behaviour(Calls);
    }

    public static SyncResult Clean() => new(0, 0, 0, 0, 0, 0, TimeSpan.Zero);

    public static SyncResult WithErrors(int errors) => new(0, 0, 0, 0, 0, errors, TimeSpan.Zero);
}
