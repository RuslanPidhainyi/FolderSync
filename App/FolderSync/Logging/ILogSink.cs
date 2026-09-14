namespace FolderSync.Logging;

public interface ILogSink : IDisposable
{
    void Write(string line);
}
