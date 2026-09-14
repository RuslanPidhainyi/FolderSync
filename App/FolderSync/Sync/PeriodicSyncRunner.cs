using FolderSync.Logging;

namespace FolderSync.Sync;

public sealed class PeriodicSyncRunner
{
    private readonly IFolderSynchronizer _synchronizer;
    private readonly TimeSpan _interval;
    private readonly ISyncLogger _log;

    public PeriodicSyncRunner(IFolderSynchronizer synchronizer, TimeSpan interval, ISyncLogger log)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be positive.");
        }

        _synchronizer = synchronizer ?? throw new ArgumentNullException(nameof(synchronizer));
        _interval = interval;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public int CompletedPasses { get; private set; }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_interval);

        do
        {
            RunOnce(cancellationToken);
        }
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
    }

    public bool RunOnce(CancellationToken cancellationToken)
    {
        var pass = CompletedPasses + 1;
        _log.Info($"Synchronization #{pass} started");

        try
        {
            var result = _synchronizer.Synchronize(cancellationToken);
            var status = result.Errors == 0 ? "finished" : "finished with errors";
            _log.Info($"Synchronization #{pass} {status}: {result}");
            return result.Errors == 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _log.Warning($"Synchronization #{pass} interrupted");
            throw;
        }
        catch (Exception ex)
        {
            _log.Error($"Synchronization #{pass} failed", ex);
            return false;
        }
        finally
        {
            CompletedPasses = pass;
        }
    }
}
