using FolderSync.Sync;
using FolderSync.Sync.Comparison;
using FolderSync.Sync.FileSystem;
using FolderSync.Tests.Support;

namespace FolderSync.Tests.IntegrationTests;

public sealed class FolderSynchronizerTests : IDisposable
{
    private readonly TempDirectory _source = new();
    private readonly TempDirectory _replica = new();
    private readonly TestLogger _log = new();
    private readonly IFileComparer _comparer = FileComparerFactory.Create(ComparisonMode.Md5);

    public void Dispose()
    {
        _source.Dispose();
        _replica.Dispose();
    }

    private FolderSynchronizer CreateSynchronizer(IFileOperations? fileSystem = null) =>
        new(_source.Path, _replica.Path, _comparer, fileSystem ?? new FileOperations(), _log);

    [Fact]
    public void Copies_new_files_and_nested_folders()
    {
        // Arrange
        _source.WriteFile("a.txt", "A");
        _source.WriteFile("docs/b.txt", "B");
        _source.WriteFile("docs/inner/c.txt", "C");
        _source.CreateDir("empty");
        var synchronizer = CreateSynchronizer();

        // Act
        var result = synchronizer.Synchronize();

        // Assert
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
    public void Creates_the_replica_folder_when_it_does_not_exist()
    {
        // Arrange
        _source.WriteFile("a.txt", "A");
        var replicaPath = _replica.Sub("nested", "replica");
        var synchronizer = new FolderSynchronizer(_source.Path, replicaPath, _comparer, _log);

        // Act
        var result = synchronizer.Synchronize();

        // Assert
        Assert.True(File.Exists(Path.Combine(replicaPath, "a.txt")));
        Assert.Equal(1, result.FilesCreated);
        Assert.Contains(_log.Infos, e => e.Contains("Created folder") && e.Contains("<root>"));
    }

    [Fact]
    public void Overwrites_a_file_whose_content_changed_but_size_did_not()
    {
        // Arrange
        _source.WriteFile("a.txt", "AAAA");
        var synchronizer = CreateSynchronizer();
        synchronizer.Synchronize();
        _source.WriteFile("a.txt", "BBBB");

        // Act
        var result = synchronizer.Synchronize();

        // Assert
        Assert.Equal("BBBB", _replica.ReadFile("a.txt"));
        Assert.Equal(1, result.FilesUpdated);
        Assert.Equal(0, result.FilesCreated);
        Assert.Contains(_log.Infos, e => e.Contains("Updated file") && e.Contains("a.txt"));
    }

    [Fact]
    public void Overwrites_a_file_whose_size_changed()
    {
        // Arrange
        _source.WriteFile("a.txt", "short");
        var synchronizer = CreateSynchronizer();
        synchronizer.Synchronize();
        _source.WriteFile("a.txt", "much longer content");

        // Act
        var result = synchronizer.Synchronize();

        // Assert
        Assert.Equal("much longer content", _replica.ReadFile("a.txt"));
        Assert.Equal(1, result.FilesUpdated);
    }

    [Fact]
    public void Deletes_files_and_folders_that_are_missing_in_the_source()
    {
        // Arrange
        _source.WriteFile("keep.txt", "K");
        _replica.WriteFile("keep.txt", "K");
        _replica.WriteFile("stale.txt", "S");
        _replica.WriteFile("old/deep/x.txt", "X");
        _replica.CreateDir("old/emptyDir");
        var synchronizer = CreateSynchronizer();

        // Act
        var result = synchronizer.Synchronize();

        // Assert
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
    public void Replaces_a_folder_with_a_file_and_a_file_with_a_folder_when_the_kind_differs()
    {
        // Arrange
        _source.WriteFile("item", "now a file");
        _source.WriteFile("other/inside.txt", "now a folder");
        _replica.WriteFile("item/was-a-folder.txt", "old");
        _replica.WriteFile("other", "was a file");
        var synchronizer = CreateSynchronizer();

        // Act
        synchronizer.Synchronize();

        // Assert
        Assert.Equal(_source.Snapshot(), _replica.Snapshot());
        Assert.Equal("now a file", _replica.ReadFile("item"));
        Assert.Equal("now a folder", _replica.ReadFile("other/inside.txt"));
    }

    [Fact]
    public void A_second_pass_without_changes_does_nothing()
    {
        // Arrange
        _source.WriteFile("a.txt", "A");
        _source.WriteFile("dir/b.txt", "B");
        var synchronizer = CreateSynchronizer();
        synchronizer.Synchronize();

        // Act
        var result = synchronizer.Synchronize();

        // Assert
        Assert.Equal(0, result.TotalChanges);
        Assert.Equal(0, result.Errors);
    }

    [Fact]
    public void Overwrites_and_deletes_read_only_files_in_the_replica()
    {
        // Arrange
        _source.WriteFile("a.txt", "new");
        var replicaA = _replica.WriteFile("a.txt", "old");
        var replicaStale = _replica.WriteFile("stale.txt", "old");
        File.SetAttributes(replicaA, FileAttributes.ReadOnly);
        File.SetAttributes(replicaStale, FileAttributes.ReadOnly);
        var synchronizer = CreateSynchronizer();

        // Act
        var result = synchronizer.Synchronize();

        // Assert
        Assert.Equal("new", _replica.ReadFile("a.txt"));
        Assert.False(_replica.FileExists("stale.txt"));
        Assert.Equal(0, result.Errors);
    }

    [Fact]
    public void Copies_hidden_files()
    {
        // Arrange
        var hidden = _source.WriteFile(".hidden", "h");
        File.SetAttributes(hidden, File.GetAttributes(hidden) | FileAttributes.Hidden);
        var synchronizer = CreateSynchronizer();

        // Act
        synchronizer.Synchronize();

        // Assert
        Assert.True(_replica.FileExists(".hidden"));
        Assert.Equal("h", _replica.ReadFile(".hidden"));
    }

    [Fact]
    public void Preserves_the_last_write_time_of_copied_files()
    {
        // Arrange
        var file = _source.WriteFile("a.txt", "A");
        var stamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file, stamp);
        var synchronizer = CreateSynchronizer();

        // Act
        synchronizer.Synchronize();

        // Assert
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(_replica.Sub("a.txt")));
    }

    [Fact]
    public void Logs_a_locked_file_as_an_error_and_continues_with_the_rest()
    {
        // Arrange
        _source.WriteFile("locked.txt", "L");
        _source.WriteFile("ok.txt", "OK");
        var replicaLocked = _replica.WriteFile("locked.txt", "different");
        using var exclusiveHandle = new FileStream(replicaLocked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var synchronizer = CreateSynchronizer();

        // Act
        var result = synchronizer.Synchronize();

        // Assert
        Assert.Equal(1, result.Errors);
        Assert.Equal("OK", _replica.ReadFile("ok.txt"));
        Assert.Contains(_log.Errors, e => e.Contains("locked.txt"));
    }

    [Fact]
    public void Isolates_failures_reported_by_the_file_operations()
    {
        // Arrange
        _source.WriteFile("bad.txt", "B");
        _source.WriteFile("good.txt", "G");
        var synchronizer = CreateSynchronizer(new FailingFileOperations { FailCopyOf = "bad.txt" });

        // Act
        var result = synchronizer.Synchronize();

        // Assert
        Assert.Equal(1, result.Errors);
        Assert.Equal(1, result.FilesCreated);
        Assert.True(_replica.FileExists("good.txt"));
        Assert.False(_replica.FileExists("bad.txt"));
        Assert.Contains(_log.Errors, e => e.Contains("bad.txt") && e.Contains("disk full"));
    }

    [Fact]
    public void Throws_when_the_source_does_not_exist()
    {
        // Arrange
        var missing = _source.Sub("missing");
        var synchronizer = new FolderSynchronizer(missing, _replica.Path, _comparer, _log);
        Action act = () => synchronizer.Synchronize();

        // Act & Assert
        Assert.Throws<DirectoryNotFoundException>(act);
    }

    [Fact]
    public void Honours_cancellation_before_touching_the_replica()
    {
        // Arrange
        _source.WriteFile("a.txt", "A");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var synchronizer = CreateSynchronizer();
        Action act = () => synchronizer.Synchronize(cts.Token);

        // Act & Assert
        Assert.Throws<OperationCanceledException>(act);
        Assert.False(_replica.FileExists("a.txt"));
    }

    [Fact]
    public void Copies_a_large_file_byte_for_byte()
    {
        // Arrange
        var bytes = new byte[3 * 1024 * 1024 + 17];
        new Random(42).NextBytes(bytes);
        File.WriteAllBytes(_source.Sub("big.bin"), bytes);
        var synchronizer = CreateSynchronizer();

        // Act
        synchronizer.Synchronize();

        // Assert
        Assert.Equal(bytes, File.ReadAllBytes(_replica.Sub("big.bin")));
    }

    private sealed class FailingFileOperations : IFileOperations
    {
        private readonly FileOperations _inner = new();

        public required string FailCopyOf { get; init; }

        public IReadOnlyList<FileSystemInfo> Enumerate(DirectoryInfo directory) => _inner.Enumerate(directory);

        public void CopyFile(FileInfo source, FileInfo destination)
        {
            if (source.Name == FailCopyOf)
            {
                throw new IOException("disk full");
            }

            _inner.CopyFile(source, destination);
        }

        public void DeleteFile(FileInfo file) => _inner.DeleteFile(file);

        public void CreateDirectory(DirectoryInfo directory) => _inner.CreateDirectory(directory);

        public void DeleteDirectory(DirectoryInfo directory) => _inner.DeleteDirectory(directory);
    }
}
