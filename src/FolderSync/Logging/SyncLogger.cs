namespace FolderSync.Logging;

/// <summary>
/// Formats log entries (timestamp + level + message) and fans each line out to every sink.
/// Formatting lives here; where the lines go is the sinks' concern.
/// </summary>
public sealed class SyncLogger : ISyncLogger, IDisposable
{
    private readonly ILogSink[] _sinks;
    private readonly object _gate = new();

    public SyncLogger(params ILogSink[] sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        if (sinks.Length == 0)
        {
            throw new ArgumentException("At least one log sink is required.", nameof(sinks));
        }

        _sinks = sinks;
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
            foreach (var sink in _sinks)
            {
                sink.Write(line);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var sink in _sinks)
            {
                sink.Dispose();
            }
        }
    }
}
