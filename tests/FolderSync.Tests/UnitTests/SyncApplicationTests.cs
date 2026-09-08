using FolderSync.Cli;
using FolderSync.Logging;
using FolderSync.Sync;
using FolderSync.Sync.Comparison;
using FolderSync.Tests.Support;

namespace FolderSync.Tests.UnitTests;

/// <summary>Exit-code mapping and lifecycle of <see cref="SyncApplication"/> with a scripted synchronizer.</summary>
public sealed class SyncApplicationTests
{
    private static SyncOptions Options(bool runOnce, TimeSpan? interval = null) =>
        new("source", "replica", interval ?? TimeSpan.FromSeconds(1), "sync.log", ComparisonMode.Md5, runOnce);

    private static SyncApplication Build(SyncOptions options, IFolderSynchronizer synchronizer, ISyncLogger log) =>
        new(options, log, new PeriodicSyncRunner(synchronizer, options.Interval, log));

    [Fact]
    public async Task Single_pass_without_errors_returns_success()
    {
        // Arrange
        var log = new TestLogger();
        var app = Build(Options(runOnce: true), new FakeSynchronizer(_ => FakeSynchronizer.Clean()), log);

        // Act
        var exitCode = await app.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains(log.Infos, e => e.Contains("FolderSync stopped"));
    }

    [Fact]
    public async Task Single_pass_with_errors_returns_sync_failed()
    {
        // Arrange
        var app = Build(Options(runOnce: true), new FakeSynchronizer(_ => FakeSynchronizer.WithErrors(1)), new TestLogger());

        // Act
        var exitCode = await app.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(ExitCodes.SyncFailed, exitCode);
    }

    [Fact]
    public async Task Single_pass_that_throws_returns_sync_failed()
    {
        // Arrange
        var synchronizer = new FakeSynchronizer(_ => throw new DirectoryNotFoundException("gone"));
        var log = new TestLogger();
        var app = Build(Options(runOnce: true), synchronizer, log);

        // Act
        var exitCode = await app.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(ExitCodes.SyncFailed, exitCode);
        Assert.Contains(log.Errors, e => e.Contains("gone"));
    }

    [Fact]
    public async Task Periodic_mode_returns_success_when_cancelled()
    {
        // Arrange
        var synchronizer = new FakeSynchronizer(_ => FakeSynchronizer.Clean());
        var log = new TestLogger();
        var app = Build(Options(runOnce: false, TimeSpan.FromMilliseconds(20)), synchronizer, log);
        using var cts = new CancellationTokenSource();

        // Act
        var run = app.RunAsync(cts.Token);
        await Task.Delay(150);
        cts.Cancel();
        var exitCode = await run;

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.InRange(synchronizer.Calls, 2, 20);
        Assert.Contains(log.Infos, e => e.Contains("Press Ctrl+C to stop."));
        Assert.Contains(log.Infos, e => e.Contains("Stopped by user."));
    }

    [Fact]
    public async Task Logs_the_configuration_at_startup()
    {
        // Arrange
        var options = Options(runOnce: true) with { ComparisonMode = ComparisonMode.Quick };
        var log = new TestLogger();
        var app = Build(options, new FakeSynchronizer(_ => FakeSynchronizer.Clean()), log);

        // Act
        await app.RunAsync(CancellationToken.None);

        // Assert
        Assert.Contains(log.Infos, e => e.Contains("Source:") && e.Contains(options.Source));
        Assert.Contains(log.Infos, e => e.Contains("Replica:") && e.Contains(options.Replica));
        Assert.Contains(log.Infos, e => e.Contains("Interval:") && e.Contains("00:00:01"));
        Assert.Contains(log.Infos, e => e.Contains("Compare:") && e.Contains("Quick"));
        Assert.Contains(log.Infos, e => e.Contains("Log file:") && e.Contains(options.LogFilePath));
    }

    [Fact]
    public void Dispose_disposes_a_disposable_logger()
    {
        // Arrange
        var sink = new RecordingSink();
        var logger = new SyncLogger(sink);
        var app = Build(Options(runOnce: true), new FakeSynchronizer(_ => FakeSynchronizer.Clean()), logger);

        // Act
        app.Dispose();

        // Assert
        Assert.True(sink.Disposed);
    }

    [Fact]
    public void Requires_all_dependencies()
    {
        // Arrange
        var options = Options(runOnce: true);
        var log = new TestLogger();
        var runner = new PeriodicSyncRunner(new FakeSynchronizer(_ => FakeSynchronizer.Clean()), options.Interval, log);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SyncApplication(null!, log, runner));
        Assert.Throws<ArgumentNullException>(() => new SyncApplication(options, null!, runner));
        Assert.Throws<ArgumentNullException>(() => new SyncApplication(options, log, null!));
    }
}
