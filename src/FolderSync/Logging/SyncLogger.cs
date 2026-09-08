namespace FolderSync.Logging;

/// <summary>
/// Writes every log line both to the console and to a log file.
/// The file is opened in append mode so history survives restarts; each line is flushed
/// immediately so nothing is lost if the process is killed.
/// </summary>
public sealed class SyncLogger : ISyncLogger, IDisposable
{
    private readonly TextWriter _console;
    private readonly StreamWriter _file;
    private readonly object _gate = new();

    public SyncLogger(string logFilePath, TextWriter? console = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);

        var fullPath = Path.GetFullPath(logFilePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _file = new StreamWriter(stream) { AutoFlush = true };
        _console = console ?? Console.Out;
    }

    public void Info(string message) => Write("INFO ", message);

    public void Warning(string message) => Write("WARN ", message);

    public void Error(string message, Exception? exception = null)
    {
        var text = exception is null ? message : $"{message}: {exception.GetType().Name}: {exception.Message}";
        Write("ERROR", text);
    }

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";

        lock (_gate)
        {
            _console.WriteLine(line);
            _file.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _file.Dispose();
        }
    }
}
