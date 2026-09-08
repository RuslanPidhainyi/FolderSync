using FolderSync.Sync;
using FolderSync.Tests.Support;

namespace FolderSync.Tests.UnitTests;

public sealed class PeriodicSyncRunnerTests
{
    [Fact]
    public async Task Runs_immediately_and_then_periodically_until_cancelled()
    {
        // Arrange
        var synchronizer = new FakeSynchronizer(_ => FakeSynchronizer.Clean());
        var log = new TestLogger();
        var runner = new PeriodicSyncRunner(synchronizer, TimeSpan.FromMilliseconds(20), log);
        using var cts = new CancellationTokenSource();

        // Act
        var run = runner.RunAsync(cts.Token);
        await Task.Delay(250);
        cts.Cancel();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.InRange(synchronizer.Calls, 3, 20);
        Assert.Equal(synchronizer.Calls, runner.CompletedPasses);
        Assert.Contains(log.Infos, e => e.Contains("Synchronization #1 started"));
        Assert.Contains(log.Infos, e => e.Contains("Synchronization #1 finished"));
    }

    [Fact]
    public void RunOnce_logs_the_summary_and_reports_success()
    {
        // Arrange
        var result = new SyncResult(2, 1, 3, 0, 0, 0, TimeSpan.FromMilliseconds(5));
        var synchronizer = new FakeSynchronizer(_ => result);
        var log = new TestLogger();
        var runner = new PeriodicSyncRunner(synchronizer, TimeSpan.FromSeconds(1), log);

        // Act
        var succeeded = runner.RunOnce(CancellationToken.None);

        // Assert
        Assert.True(succeeded);
        Assert.Equal(1, runner.CompletedPasses);
        Assert.Contains(log.Infos, e => e.Contains("Synchronization #1 finished: 2 file(s) created, 1 updated, 3 deleted"));
    }

    [Fact]
    public void RunOnce_reports_failure_when_the_pass_had_errors()
    {
        // Arrange
        var synchronizer = new FakeSynchronizer(_ => FakeSynchronizer.WithErrors(2));
        var log = new TestLogger();
        var runner = new PeriodicSyncRunner(synchronizer, TimeSpan.FromSeconds(1), log);

        // Act
        var succeeded = runner.RunOnce(CancellationToken.None);

        // Assert
        Assert.False(succeeded);
        Assert.Contains(log.Infos, e => e.Contains("finished with errors") && e.Contains("2 error(s)"));
    }

    [Fact]
    public void RunOnce_logs_an_exception_instead_of_throwing()
    {
        // Arrange
        var synchronizer = new FakeSynchronizer(_ => throw new DirectoryNotFoundException("gone"));
        var log = new TestLogger();
        var runner = new PeriodicSyncRunner(synchronizer, TimeSpan.FromSeconds(1), log);

        // Act
        var succeeded = runner.RunOnce(CancellationToken.None);

        // Assert
        Assert.False(succeeded);
        Assert.Equal(1, runner.CompletedPasses);
        Assert.Contains(log.Errors, e => e.Contains("Synchronization #1 failed") && e.Contains("gone"));
    }

    [Fact]
    public void RunOnce_propagates_cancellation()
    {
        // Arrange
        var synchronizer = new FakeSynchronizer(_ => FakeSynchronizer.Clean());
        var log = new TestLogger();
        var runner = new PeriodicSyncRunner(synchronizer, TimeSpan.FromSeconds(1), log);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Action act = () => runner.RunOnce(cts.Token);

        // Act & Assert
        Assert.Throws<OperationCanceledException>(act);
        Assert.Contains(log.Warnings, e => e.Contains("interrupted"));
    }

    [Fact]
    public void Rejects_non_positive_interval()
    {
        // Arrange
        var synchronizer = new FakeSynchronizer(_ => FakeSynchronizer.Clean());
        Action act = () => new PeriodicSyncRunner(synchronizer, TimeSpan.Zero, new TestLogger());

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }
}
