using FolderSync.Cli;
using FolderSync.Logging;
using FolderSync.Sync;

namespace FolderSync;

public sealed class SyncApplication : IDisposable
{
    private readonly SyncOptions _options;
    private readonly ISyncLogger _logger;
    private readonly PeriodicSyncRunner _runner;

    public SyncApplication(SyncOptions options, ISyncLogger logger, PeriodicSyncRunner runner)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        LogStartup();

        try
        {
            if (_options.RunOnce)
            {
                return Finish(_runner.RunOnce(cancellationToken) ? ExitCodes.Success : ExitCodes.SyncFailed);
            }

            _logger.Info("Press Ctrl+C to stop.");
            await _runner.RunAsync(cancellationToken).ConfigureAwait(false);
            return Finish(ExitCodes.Success);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.Info("Stopped by user.");
            return Finish(ExitCodes.Success);
        }
        catch (Exception ex)
        {
            _logger.Error("Unexpected failure", ex);
            return Finish(ExitCodes.SyncFailed);
        }
    }

    public void Dispose() => (_logger as IDisposable)?.Dispose();

    private void LogStartup()
    {
        _logger.Info("FolderSync starting");
        _logger.Info($"  Source:   {_options.Source}");
        _logger.Info($"  Replica:  {_options.Replica}");
        _logger.Info($"  Interval: {_options.Interval}");
        _logger.Info($"  Compare:  {_options.ComparisonMode}");
        _logger.Info($"  Log file: {_options.LogFilePath}");
    }

    private int Finish(int exitCode)
    {
        _logger.Info("FolderSync stopped");
        return exitCode;
    }
}
