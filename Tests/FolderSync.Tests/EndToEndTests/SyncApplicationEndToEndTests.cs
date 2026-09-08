using FolderSync.Cli;
using FolderSync.Sync.Comparison;
using FolderSync.Tests.Support;

namespace FolderSync.Tests.EndToEndTests;

/// <summary>
/// The whole program as the user runs it, minus the process boundary: arguments are parsed and
/// validated, the composition root wires real components, and a real folder gets mirrored.
/// </summary>
public sealed class SyncApplicationEndToEndTests : IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly string _source;
    private readonly string _replica;
    private readonly string _log;

    public SyncApplicationEndToEndTests()
    {
        _source = _root.CreateDir("source");
        _replica = _root.Sub("replica");
        _log = _root.Sub("logs", "sync.log");
    }

    public void Dispose() => _root.Dispose();

    /// <summary>
    /// Builds options entirely from named arguments so extra flags (--once, --compare, --interval)
    /// never collide with a positional slot; the positional form itself is covered by the parser's
    /// own unit tests.
    /// </summary>
    private SyncOptions ParseAndValidate(string interval, params string[] extraArgs)
    {
        var options = SyncOptionsParser.Parse(
            ["--source", _source, "--replica", _replica, "--interval", interval, "--log", _log, .. extraArgs])!;
        SyncOptionsValidator.Validate(options);
        return options;
    }

    [Theory]
    [InlineData("md5")]
    [InlineData("sha256")]
    [InlineData("quick")]
    public async Task A_single_pass_mirrors_the_source_and_writes_the_log(string compareMode)
    {
        // Arrange
        File.WriteAllText(Path.Combine(_source, "a.txt"), "A");
        Directory.CreateDirectory(Path.Combine(_source, "sub"));
        File.WriteAllText(Path.Combine(_source, "sub", "b.txt"), "B");
        var options = ParseAndValidate("1", "--once", "--compare", compareMode);
        var console = new StringWriter();

        // Act
        int exitCode;
        using (var app = SyncApplicationFactory.Create(options, console))
        {
            exitCode = await app.RunAsync(CancellationToken.None);
        }

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal("A", File.ReadAllText(Path.Combine(_replica, "a.txt")));
        Assert.Equal("B", File.ReadAllText(Path.Combine(_replica, "sub", "b.txt")));
        var logText = File.ReadAllText(_log);
        Assert.Contains($"Compare:  {options.ComparisonMode}", logText);
        Assert.Contains("Synchronization #1 finished", logText);
        Assert.Contains("FolderSync stopped", logText);
        Assert.Equal(logText, console.ToString());
    }

    [Fact]
    public async Task A_single_pass_returns_the_failure_exit_code_when_the_source_disappears()
    {
        // Arrange
        var options = ParseAndValidate("1", "--once");
        Directory.Delete(_source);

        // Act
        int exitCode;
        using (var app = SyncApplicationFactory.Create(options, TextWriter.Null))
        {
            exitCode = await app.RunAsync(CancellationToken.None);
        }

        // Assert
        Assert.Equal(ExitCodes.SyncFailed, exitCode);
        Assert.Contains("Synchronization #1 failed", File.ReadAllText(_log));
    }

    [Fact]
    public async Task Periodic_mode_picks_up_changes_and_stops_cleanly_on_cancellation()
    {
        // Arrange
        var options = ParseAndValidate("50ms");
        using var cts = new CancellationTokenSource();

        // Act
        int exitCode;
        using (var app = SyncApplicationFactory.Create(options, TextWriter.Null))
        {
            var run = app.RunAsync(cts.Token);
            await Task.Delay(120);
            File.WriteAllText(Path.Combine(_source, "late.txt"), "L");
            await Task.Delay(200);
            cts.Cancel();
            exitCode = await run;
        }

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal("L", File.ReadAllText(Path.Combine(_replica, "late.txt")));
        var logText = File.ReadAllText(_log);
        Assert.Contains("Synchronization #2 started", logText);
        Assert.Contains("late.txt", logText);
        Assert.Contains("Stopped by user.", logText);
    }

    [Fact]
    public void The_factory_releases_the_log_file_when_wiring_fails()
    {
        // Arrange
        var options = ParseAndValidate("1") with { Interval = TimeSpan.Zero };
        Action act = () => SyncApplicationFactory.Create(options, TextWriter.Null);

        // Act
        Assert.Throws<ArgumentOutOfRangeException>(act);

        // Assert: if the logger had leaked, opening the file exclusively would fail.
        using var exclusive = new FileStream(_log, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }
}
