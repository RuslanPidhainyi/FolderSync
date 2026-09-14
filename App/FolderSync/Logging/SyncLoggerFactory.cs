namespace FolderSync.Logging;

public static class SyncLoggerFactory
{
    public static SyncLogger Create(string logFilePath, TextWriter? console = null) =>
        new(new ConsoleLogSink(console), new FileLogSink(logFilePath));
}
