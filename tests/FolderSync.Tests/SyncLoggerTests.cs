using FolderSync.Logging;
using FolderSync.Tests.Support;

namespace FolderSync.Tests;

public sealed class SyncLoggerTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Factory_builds_logger_that_writes_to_console_and_file()
    {
        var logPath = _dir.Sub("nested", "sync.log");
        var console = new StringWriter();

        using (var logger = SyncLoggerFactory.Create(logPath, console))
        {
            logger.Info("hello");
            logger.Warning("careful");
            logger.Error("boom", new InvalidOperationException("details"));
        }

        var fileLines = File.ReadAllLines(logPath);
        var consoleLines = console.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, fileLines.Length);
        Assert.Equal(fileLines, consoleLines);
        Assert.Contains("[INFO ] hello", fileLines[0]);
        Assert.Contains("[WARN ] careful", fileLines[1]);
        Assert.Contains("[ERROR] boom: InvalidOperationException: details", fileLines[2]);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} ", fileLines[0]);
    }

    [Fact]
    public void File_sink_appends_to_existing_file()
    {
        var logPath = _dir.Sub("sync.log");
        File.WriteAllText(logPath, "previous run" + Environment.NewLine);

        using (var logger = SyncLoggerFactory.Create(logPath, TextWriter.Null))
        {
            logger.Info("new run");
        }

        var lines = File.ReadAllLines(logPath);
        Assert.Equal(2, lines.Length);
        Assert.Equal("previous run", lines[0]);
        Assert.EndsWith("new run", lines[1]);
    }

    [Fact]
    public void File_sink_flushes_each_line_immediately()
    {
        var logPath = _dir.Sub("sync.log");
        using var logger = SyncLoggerFactory.Create(logPath, TextWriter.Null);

        logger.Info("first");

        using var reader = new StreamReader(new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        Assert.EndsWith("first", reader.ReadToEnd().TrimEnd());
    }

    [Fact]
    public void Fans_every_line_out_to_all_sinks_and_disposes_them()
    {
        var first = new RecordingSink();
        var second = new RecordingSink();

        using (var logger = new SyncLogger(first, second))
        {
            logger.Info("one");
            logger.Warning("two");
        }

        Assert.Equal(first.Lines, second.Lines);
        Assert.Equal(2, first.Lines.Count);
        Assert.EndsWith("[INFO ] one", first.Lines[0]);
        Assert.EndsWith("[WARN ] two", first.Lines[1]);
        Assert.True(first.Disposed);
        Assert.True(second.Disposed);
    }

    [Fact]
    public void Requires_at_least_one_sink()
    {
        Assert.Throws<ArgumentException>(() => new SyncLogger());
    }

    private sealed class RecordingSink : ILogSink
    {
        public List<string> Lines { get; } = [];

        public bool Disposed { get; private set; }

        public void Write(string line) => Lines.Add(line);

        public void Dispose() => Disposed = true;
    }
}
