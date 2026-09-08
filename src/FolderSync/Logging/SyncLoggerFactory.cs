namespace FolderSync.Logging;

/// <summary>Builds the logger required by the task: every line goes to the console and to a file.</summary>
public static class SyncLoggerFactory
{
    public static SyncLogger Create(string logFilePath, TextWriter? console = null) =>
        new(new ConsoleLogSink(console), new FileLogSink(logFilePath));
}
