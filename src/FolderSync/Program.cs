using FolderSync.Cli;
using FolderSync.Logging;
using FolderSync.Sync;

namespace FolderSync;

public static class Program
{
    private const int ExitOk = 0;
    private const int ExitInvalidArguments = 1;
    private const int ExitFailure = 2;

    public static async Task<int> Main(string[] args)
    {
        SyncOptions? options;
        try
        {
            options = SyncOptions.Parse(args);
        }
        catch (OptionsException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(SyncOptions.Usage);
            return ExitInvalidArguments;
        }

        if (options is null)
        {
            Console.WriteLine(SyncOptions.Usage);
            return ExitOk;
        }

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            // Let the current pass finish its current operation instead of killing the process.
            e.Cancel = true;
            shutdown.Cancel();
        };

        SyncLogger logger;
        try
        {
            logger = new SyncLogger(options.LogFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error: cannot open log file '{options.LogFilePath}': {ex.Message}");
            return ExitInvalidArguments;
        }

        using (logger)
        {
            logger.Info("FolderSync starting");
            logger.Info($"  Source:   {options.Source}");
            logger.Info($"  Replica:  {options.Replica}");
            logger.Info($"  Interval: {options.Interval}");
            logger.Info($"  Log file: {options.LogFilePath}");

            var synchronizer = new FolderSynchronizer(options.Source, options.Replica, new Md5FileComparer(), logger);
            var runner = new PeriodicSyncRunner(synchronizer, options.Interval, logger);

            try
            {
                if (options.RunOnce)
                {
                    runner.RunOnce(shutdown.Token);
                }
                else
                {
                    logger.Info("Press Ctrl+C to stop.");
                    await runner.RunAsync(shutdown.Token);
                }
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
            {
                logger.Info("Stopped by user.");
            }
            catch (Exception ex)
            {
                logger.Error("Unexpected failure", ex);
                return ExitFailure;
            }

            logger.Info("FolderSync stopped");
            return ExitOk;
        }
    }
}
