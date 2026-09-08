namespace FolderSync.Logging;

/// <summary>A destination for formatted log lines (console, file, ...).</summary>
public interface ILogSink : IDisposable
{
    void Write(string line);
}
