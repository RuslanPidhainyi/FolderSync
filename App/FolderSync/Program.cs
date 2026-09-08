using FolderSync.Cli;

namespace FolderSync;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        SyncOptions? options;
        try
        {
            options = SyncOptionsParser.Parse(args);
            if (options is null)
            {
                Console.WriteLine(SyncOptionsParser.Usage);
                return ExitCodes.Success;
            }

            SyncOptionsValidator.Validate(options);
        }
        catch (OptionsException ex)
        {
            ReportError(ex.Message, showUsage: true);
            return ExitCodes.InvalidArguments;
        }

        SyncApplication application;
        try
        {
            application = SyncApplicationFactory.Create(options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ReportError($"cannot open log file '{options.LogFilePath}': {ex.Message}", showUsage: false);
            return ExitCodes.InvalidArguments;
        }

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            // Let the current pass finish its current operation instead of killing the process.
            e.Cancel = true;
            shutdown.Cancel();
        };

        using (application)
        {
            return await application.RunAsync(shutdown.Token);
        }
    }

    private static void ReportError(string message, bool showUsage)
    {
        Console.Error.WriteLine($"Error: {message}");
        if (showUsage)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(SyncOptionsParser.Usage);
        }
    }
}
