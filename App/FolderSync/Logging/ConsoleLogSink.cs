namespace FolderSync.Logging;

public sealed class ConsoleLogSink : ILogSink
{
    private readonly TextWriter _writer;

    public ConsoleLogSink(TextWriter? writer = null) => _writer = writer ?? Console.Out;

    public void Write(string line) => _writer.WriteLine(line);

    public void Dispose()
    {
    }
}
