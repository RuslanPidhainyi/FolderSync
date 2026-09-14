namespace FolderSync.Logging;

public sealed class FileLogSink : ILogSink
{
    private readonly StreamWriter _writer;

    public FileLogSink(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream) { AutoFlush = true };
    }

    public void Write(string line) => _writer.WriteLine(line);

    public void Dispose() => _writer.Dispose();
}
