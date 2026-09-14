using FolderSync.Cli;
using FolderSync.Logging;
using FolderSync.Sync;
using FolderSync.Sync.Comparison;
using FolderSync.Sync.FileSystem;

namespace FolderSync;

public static class SyncApplicationFactory
{
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
