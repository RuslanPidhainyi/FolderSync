using FolderSync.Cli;
using FolderSync.Logging;
using FolderSync.Sync;
using FolderSync.Sync.Comparison;
using FolderSync.Sync.FileSystem;

namespace FolderSync;

/// <summary>
/// Composition root. This is the only place that knows which concrete classes make up the program;
/// everything else depends on interfaces.
/// </summary>
public static class SyncApplicationFactory
{
    /// <param name="options">Validated options from <see cref="SyncOptionsParser"/>.</param>
    /// <param name="console">Where console log lines go; defaults to <see cref="Console.Out"/>.</param>
    /// <exception cref="IOException">The log file cannot be opened.</exception>
    /// <exception cref="UnauthorizedAccessException">The log file cannot be opened.</exception>
    public static SyncApplication Create(SyncOptions options, TextWriter? console = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var logger = SyncLoggerFactory.Create(options.LogFilePath, console);
        try
        {
            var comparer = FileComparerFactory.Create(options.ComparisonMode);
            var synchronizer = new FolderSynchronizer(
                options.Source,
                options.Replica,
                comparer,
                new FileOperations(),
                logger);
            var runner = new PeriodicSyncRunner(synchronizer, options.Interval, logger);

            return new SyncApplication(options, logger, runner);
        }
        catch
        {
            logger.Dispose();
            throw;
        }
    }
}
