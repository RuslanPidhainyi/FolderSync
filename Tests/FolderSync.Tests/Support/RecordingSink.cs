using FolderSync.Logging;

namespace FolderSync.Tests.Support;

public sealed class RecordingSink : ILogSink
{
    public List<string> Lines { get; } = [];

    public bool Disposed { get; private set; }

    public void Write(string line) => Lines.Add(line);

    public void Dispose() => Disposed = true;
}
