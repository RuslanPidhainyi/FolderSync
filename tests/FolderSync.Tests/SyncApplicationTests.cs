using FolderSync.Cli;
using FolderSync.Sync.Comparison;
using FolderSync.Tests.Support;

namespace FolderSync.Tests;

/// <summary>End-to-end: the composition root wires real components and a single pass mirrors the folder.</summary>
public sealed class SyncApplicationTests : IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly string _source;
    private readonly string _replica;
    private readonly string _log;

    public SyncApplicationTests()
    {
        _source = _root.CreateDir("source");
        _replica = _root.Sub("replica");
        _log = _root.Sub("logs", "sync.log");
    }

    public void Dispose() => _root.Dispose();

    private SyncOptions Options(ComparisonMode mode = ComparisonMode.Md5) =>
        new(_source, _replica, TimeSpan.FromSeconds(1), _log, mode, RunOnce: true);

    [Theory]
    [InlineData(ComparisonMode.Md5)]
    [InlineData(ComparisonMode.Sha256)]
    [InlineData(ComparisonMode.Quick)]
    public async Task Single_pass_mirrors_source_and_writes_log(ComparisonMode mode)
    {
        File.WriteAllText(Path.Combine(_source, "a.txt"), "A");
        Directory.CreateDirectory(Path.Combine(_source, "sub"));
        File.WriteAllText(Path.Combine(_source, "sub", "b.txt"), "B");
        var console = new StringWriter();

        int exitCode;
        using (var app = SyncApplicationFactory.Create(Options(mode), console))
        {
            exitCode = await app.RunAsync(CancellationToken.None);
        }

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal("A", File.ReadAllText(Path.Combine(_replica, "a.txt")));
        Assert.Equal("B", File.ReadAllText(Path.Combine(_replica, "sub", "b.txt")));

        var logText = File.ReadAllText(_log);
        Assert.Contains($"Compare:  {mode}", logText);
        Assert.Contains("Synchronization #1 finished", logText);
        Assert.Contains("FolderSync stopped", logText);
        Assert.Equal(logText, console.ToString());
    }

    [Fact]
    public async Task Single_pass_returns_failure_exit_code_when_source_disappears()
    {
        var options = Options();
        Directory.Delete(_source);

        int exitCode;
        using (var app = SyncApplicationFactory.Create(options, TextWriter.Null))
        {
            exitCode = await app.RunAsync(CancellationToken.None);
        }

        Assert.Equal(ExitCodes.SyncFailed, exitCode);
        Assert.Contains("Synchronization #1 failed", File.ReadAllText(_log));
    }

    [Fact]
    public async Task Periodic_mode_stops_cleanly_on_cancellation()
    {
        var options = Options() with { RunOnce = false, Interval = TimeSpan.FromMilliseconds(50) };
        using var cts = new CancellationTokenSource();

        int exitCode;
        using (var app = SyncApplicationFactory.Create(options, TextWriter.Null))
        {
            var run = app.RunAsync(cts.Token);
            await Task.Delay(200);
            cts.Cancel();
            exitCode = await run;
        }

        Assert.Equal(ExitCodes.Success, exitCode);
        var logText = File.ReadAllText(_log);
        Assert.Contains("Synchronization #2 started", logText);
        Assert.Contains("Stopped by user.", logText);
    }

    [Fact]
    public void Factory_releases_log_file_when_wiring_fails()
    {
        var options = Options() with { Interval = TimeSpan.Zero };

        Assert.Throws<ArgumentOutOfRangeException>(() => SyncApplicationFactory.Create(options, TextWriter.Null));

        // If the logger had leaked, the file would still be locked for exclusive access.
        using var exclusive = new FileStream(_log, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }
}
