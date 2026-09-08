using FolderSync.Logging;

namespace FolderSync.Tests.Support;

/// <summary>In-memory <see cref="ILogSink"/> that keeps every line and remembers whether it was disposed.</summary>
public sealed class RecordingSink : ILogSink
{
    public List<string> Lines { get; } = [];

    public bool Disposed { get; private set; }

    public void Write(string line) => Lines.Add(line);

    public void Dispose() => Disposed = true;
}
