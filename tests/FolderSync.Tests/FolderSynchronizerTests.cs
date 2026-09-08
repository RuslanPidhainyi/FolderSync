using FolderSync.Sync;
using FolderSync.Tests.Support;

namespace FolderSync.Tests;

public sealed class FolderSynchronizerTests : IDisposable
{
    private readonly TempDirectory _source = new();
    private readonly TempDirectory _replica = new();
    private readonly TestLogger _log = new();

    public void Dispose()
    {
        _source.Dispose();
        _replica.Dispose();
    }

    private SyncResult Sync() =>
        new FolderSynchronizer(_source.Path, _replica.Path, new Md5FileComparer(), _log).Synchronize();

    [Fact]
    public void Copies_new_files_and_nested_folders()
    {
        _source.WriteFile("a.txt", "A");
        _source.WriteFile("docs/b.txt", "B");
        _source.WriteFile("docs/inner/c.txt", "C");
        _source.CreateDir("empty");

        var result = Sync();

        Assert.Equal(_source.Snapshot(), _replica.Snapshot());
        Assert.Equal("A", _replica.ReadFile("a.txt"));
        Assert.Equal("C", _replica.ReadFile("docs/inner/c.txt"));
        Assert.Equal(3, result.FilesCreated);
        Assert.Equal(3, result.DirectoriesCreated);
        Assert.Equal(0, result.Errors);
        Assert.Contains(_log.Infos, e => e.Contains("Created file") && e.Contains("a.txt"));
        Assert.Contains(_log.Infos, e => e.Contains("Created folder") && e.Contains("empty"));
    }

    [Fact]
    public void Creates_replica_folder_when_it_does_not_exist()
    {
        _source.WriteFile("a.txt", "A");
        var replicaPath = _replica.Sub("nested", "replica");

        var result = new FolderSynchronizer(_source.Path, replicaPath, new Md5FileComparer(), _log).Synchronize();

        Assert.True(File.Exists(Path.Combine(replicaPath, "a.txt")));
        Assert.Equal(1, result.FilesCreated);
    }

    [Fact]
    public void Overwrites_file_whose_content_changed_but_size_did_not()
    {
        _source.WriteFile("a.txt", "AAAA");
        Sync();

        _source.WriteFile("a.txt", "BBBB");
        var result = Sync();

        Assert.Equal("BBBB", _replica.ReadFile("a.txt"));
        Assert.Equal(1, result.FilesUpdated);
        Assert.Equal(0, result.FilesCreated);
        Assert.Contains(_log.Infos, e => e.Contains("Updated file") && e.Contains("a.txt"));
    }

    [Fact]
    public void Overwrites_file_whose_size_changed()
    {
        _source.WriteFile("a.txt", "short");
        Sync();

        _source.WriteFile("a.txt", "much longer content");
        var result = Sync();

        Assert.Equal("much longer content", _replica.ReadFile("a.txt"));
        Assert.Equal(1, result.FilesUpdated);
    }

    [Fact]
    public void Deletes_files_and_folders_that_are_missing_in_source()
    {
        _source.WriteFile("keep.txt", "K");
        _replica.WriteFile("keep.txt", "K");
        _replica.WriteFile("stale.txt", "S");
        _replica.WriteFile("old/deep/x.txt", "X");
        _replica.CreateDir("old/emptyDir");

        var result = Sync();

        Assert.Equal(_source.Snapshot(), _replica.Snapshot());
        Assert.False(_replica.FileExists("stale.txt"));
        Assert.False(_replica.DirExists("old"));
        Assert.Equal(2, result.FilesDeleted);
        Assert.Equal(3, result.DirectoriesDeleted);
        Assert.Contains(_log.Infos, e => e.Contains("Deleted file") && e.Contains("stale.txt"));
        Assert.Contains(_log.Infos, e => e.Contains("Deleted file") && e.Contains("x.txt"));
        Assert.Contains(_log.Infos, e => e.Contains("Deleted folder") && e.EndsWith("old"));
    }

    [Fact]
    public void Replaces_folder_with_file_and_file_with_folder_when_kind_differs()
    {
        _source.WriteFile("item", "now a file");
        _source.WriteFile("other/inside.txt", "now a folder");
        _replica.WriteFile("item/was-a-folder.txt", "old");
        _replica.WriteFile("other", "was a file");

        Sync();

        Assert.Equal(_source.Snapshot(), _replica.Snapshot());
        Assert.Equal("now a file", _replica.ReadFile("item"));
        Assert.Equal("now a folder", _replica.ReadFile("other/inside.txt"));
    }

    [Fact]
    public void Second_pass_without_changes_does_nothing()
    {
        _source.WriteFile("a.txt", "A");
        _source.WriteFile("dir/b.txt", "B");
        Sync();

        var result = Sync();

        Assert.Equal(0, result.TotalChanges);
        Assert.Equal(0, result.Errors);
    }

    [Fact]
    public void Overwrites_and_deletes_read_only_files_in_replica()
    {
        _source.WriteFile("a.txt", "new");
        var replicaA = _replica.WriteFile("a.txt", "old");
        var replicaStale = _replica.WriteFile("stale.txt", "old");
        File.SetAttributes(replicaA, FileAttributes.ReadOnly);
        File.SetAttributes(replicaStale, FileAttributes.ReadOnly);

        var result = Sync();

        Assert.Equal("new", _replica.ReadFile("a.txt"));
        Assert.False(_replica.FileExists("stale.txt"));
        Assert.Equal(0, result.Errors);
    }

    [Fact]
    public void Copies_hidden_files()
    {
        var hidden = _source.WriteFile(".hidden", "h");
        File.SetAttributes(hidden, File.GetAttributes(hidden) | FileAttributes.Hidden);

        Sync();

        Assert.True(_replica.FileExists(".hidden"));
        Assert.Equal("h", _replica.ReadFile(".hidden"));
    }

    [Fact]
    public void Preserves_last_write_time_of_copied_files()
    {
        var file = _source.WriteFile("a.txt", "A");
        var stamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file, stamp);

        Sync();

        Assert.Equal(stamp, File.GetLastWriteTimeUtc(_replica.Sub("a.txt")));
    }

    [Fact]
    public void Reports_error_and_continues_when_a_file_cannot_be_copied()
    {
        _source.WriteFile("locked.txt", "L");
        _source.WriteFile("ok.txt", "OK");
        var replicaLocked = _replica.WriteFile("locked.txt", "different");

        // Hold an exclusive handle so the overwrite fails.
        using var handle = new FileStream(replicaLocked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var result = Sync();

        Assert.Equal(1, result.Errors);
        Assert.Equal("OK", _replica.ReadFile("ok.txt"));
        Assert.Contains(_log.Errors, e => e.Contains("locked.txt"));
    }

    [Fact]
    public void Throws_when_source_does_not_exist()
    {
        var missing = _source.Sub("missing");
        var synchronizer = new FolderSynchronizer(missing, _replica.Path, new Md5FileComparer(), _log);

        Assert.Throws<DirectoryNotFoundException>(() => synchronizer.Synchronize());
    }

    [Fact]
    public void Honours_cancellation()
    {
        _source.WriteFile("a.txt", "A");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var synchronizer = new FolderSynchronizer(_source.Path, _replica.Path, new Md5FileComparer(), _log);

        Assert.Throws<OperationCanceledException>(() => synchronizer.Synchronize(cts.Token));
        Assert.False(_replica.FileExists("a.txt"));
    }

    [Fact]
    public void Copies_large_file_correctly()
    {
        var bytes = new byte[3 * 1024 * 1024 + 17];
        new Random(42).NextBytes(bytes);
        File.WriteAllBytes(_source.Sub("big.bin"), bytes);

        Sync();

        Assert.Equal(bytes, File.ReadAllBytes(_replica.Sub("big.bin")));
    }
}
