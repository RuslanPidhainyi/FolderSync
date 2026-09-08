using FolderSync.Sync;
using FolderSync.Tests.Support;

namespace FolderSync.Tests;

public sealed class PeriodicSyncRunnerTests
{
    private sealed class FakeSynchronizer(Func<int, SyncResult> behaviour) : IFolderSynchronizer
    {
        public int Calls { get; private set; }

        public SyncResult Synchronize(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return behaviour(Calls);
        }
    }

    private static SyncResult Ok() => new(0, 0, 0, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Runs_immediately_and_then_periodically_until_cancelled()
    {
        var synchronizer = new FakeSynchronizer(_ => Ok());
        var log = new TestLogger();
        var runner = new PeriodicSyncRunner(synchronizer, TimeSpan.FromMilliseconds(20), log);
        using var cts = new CancellationTokenSource();

        var run = runner.RunAsync(cts.Token);
        await Task.Delay(250);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.InRange(synchronizer.Calls, 3, 20);
        Assert.Equal(synchronizer.Calls, runner.CompletedPasses);
        Assert.Contains(log.Infos, e => e.Contains("Synchronization #1 started"));
        Assert.Contains(log.Infos, e => e.Contains("Synchronization #1 finished"));
    }

    [Fact]
    public void RunOnce_logs_summary_and_reports_success()
    {
        var synchronizer = new FakeSynchronizer(_ => new SyncResult(2, 1, 3, 0, 0, 0, TimeSpan.FromMilliseconds(5)));
        var log = new TestLogger();
        var runner = new PeriodicSyncRunner(synchronizer, TimeSpan.FromSeconds(1), log);

        var succeeded = runner.RunOnce(CancellationToken.None);

        Assert.True(succeeded);
        Assert.Equal(1, runner.CompletedPasses);
        Assert.Contains(log.Infos, e => e.Contains("2 file(s) created, 1 updated, 3 deleted"));
    }

    [Fact]
    public void Failure_in_one_pass_is_logged_and_does_not_throw()
    {
        var synchronizer = new FakeSynchronizer(_ => throw new DirectoryNotFoundException("gone"));
        var log = new TestLogger();
        var runner = new PeriodicSyncRunner(synchronizer, TimeSpan.FromSeconds(1), log);

        var succeeded = runner.RunOnce(CancellationToken.None);

        Assert.False(succeeded);
        Assert.Contains(log.Errors, e => e.Contains("failed") && e.Contains("gone"));
        Assert.Equal(1, runner.CompletedPasses);
    }

    [Fact]
    public void Errors_in_result_are_reported_in_summary()
    {
        var synchronizer = new FakeSynchronizer(_ => new SyncResult(0, 0, 0, 0, 0, 2, TimeSpan.Zero));
        var log = new TestLogger();
        var runner = new PeriodicSyncRunner(synchronizer, TimeSpan.FromSeconds(1), log);

        var succeeded = runner.RunOnce(CancellationToken.None);

        Assert.False(succeeded);
        Assert.Contains(log.Infos, e => e.Contains("finished with errors") && e.Contains("2 error(s)"));
    }

    [Fact]
    public void Rejects_non_positive_interval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PeriodicSyncRunner(new FakeSynchronizer(_ => Ok()), TimeSpan.Zero, new TestLogger()));
    }
}
