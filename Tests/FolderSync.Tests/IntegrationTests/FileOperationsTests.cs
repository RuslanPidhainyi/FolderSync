using FolderSync.Sync.FileSystem;
using FolderSync.Tests.Support;

namespace FolderSync.Tests.IntegrationTests;

public sealed class FileOperationsTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly FileOperations _operations = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Enumerate_includes_hidden_entries_and_folders()
    {
        // Arrange
        var hidden = _dir.WriteFile("hidden.txt", "h");
        File.SetAttributes(hidden, File.GetAttributes(hidden) | FileAttributes.Hidden);
        _dir.WriteFile("visible.txt", "v");
        _dir.CreateDir("folder");

        // Act
        var entries = _operations.Enumerate(new DirectoryInfo(_dir.Path));

        // Assert
        var names = entries.Select(e => e.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(["folder", "hidden.txt", "visible.txt"], names);
        Assert.Single(entries.OfType<DirectoryInfo>());
    }

    [Fact]
    public void CopyFile_overwrites_a_read_only_destination()
    {
        // Arrange
        var source = new FileInfo(_dir.WriteFile("source.txt", "new"));
        var destination = new FileInfo(_dir.WriteFile("destination.txt", "old"));
        destination.IsReadOnly = true;

        // Act
        _operations.CopyFile(source, destination);

        // Assert
        Assert.Equal("new", _dir.ReadFile("destination.txt"));
    }

    [Fact]
    public void CopyFile_preserves_the_last_write_time()
    {
        // Arrange
        var stamp = new DateTime(2021, 6, 7, 8, 9, 10, DateTimeKind.Utc);
        var source = new FileInfo(_dir.WriteFile("source.txt", "x"));
        File.SetLastWriteTimeUtc(source.FullName, stamp);
        var destination = new FileInfo(_dir.Sub("destination.txt"));

        // Act
        _operations.CopyFile(source, destination);

        // Assert
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(destination.FullName));
    }

    [Fact]
    public void DeleteFile_removes_a_read_only_file()
    {
        // Arrange
        var file = new FileInfo(_dir.WriteFile("locked.txt", "x"));
        file.IsReadOnly = true;

        // Act
        _operations.DeleteFile(file);

        // Assert
        Assert.False(_dir.FileExists("locked.txt"));
    }

    [Fact]
    public void CreateDirectory_creates_missing_parents()
    {
        // Arrange
        var directory = new DirectoryInfo(_dir.Sub("a", "b", "c"));

        // Act
        _operations.CreateDirectory(directory);

        // Assert
        Assert.True(_dir.DirExists("a/b/c"));
    }

    [Fact]
    public void DeleteDirectory_refuses_a_folder_that_is_not_empty()
    {
        // Arrange
        _dir.WriteFile("full/child.txt", "x");
        var directory = new DirectoryInfo(_dir.Sub("full"));
        Action act = () => _operations.DeleteDirectory(directory);

        // Act & Assert
        Assert.Throws<IOException>(act);
        Assert.True(_dir.FileExists("full/child.txt"));
    }
}
