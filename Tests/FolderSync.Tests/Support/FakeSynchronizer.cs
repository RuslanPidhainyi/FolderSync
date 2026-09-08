using FolderSync.Sync;

namespace FolderSync.Tests.Support;

/// <summary>Scripted <see cref="IFolderSynchronizer"/>: returns (or throws) whatever the behaviour says for each call.</summary>
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
