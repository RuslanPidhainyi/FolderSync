using FolderSync.Logging;
using FolderSync.Tests.Support;

namespace FolderSync.Tests.IntegrationTests;

public sealed class FileLogSinkTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Creates_the_missing_folder_and_file()
    {
        // Arrange
        var path = _dir.Sub("nested", "deeper", "sync.log");

        // Act
        using (var sink = new FileLogSink(path))
        {
            sink.Write("first");
        }

        // Assert
        Assert.Equal(["first"], File.ReadAllLines(path));
    }

    [Fact]
    public void Appends_to_an_existing_file()
    {
        // Arrange
        var path = _dir.Sub("sync.log");
        File.WriteAllText(path, "previous run" + Environment.NewLine);

        // Act
        using (var sink = new FileLogSink(path))
        {
            sink.Write("new run");
        }

        // Assert
        Assert.Equal(["previous run", "new run"], File.ReadAllLines(path));
    }

    [Fact]
    public void Flushes_each_line_immediately()
    {
        // Arrange
        var path = _dir.Sub("sync.log");
        using var sink = new FileLogSink(path);

        // Act
        sink.Write("first");

        // Assert
        using var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        Assert.Equal("first", reader.ReadToEnd().TrimEnd());
    }

    [Fact]
    public void Dispose_releases_the_file()
    {
        // Arrange
        var path = _dir.Sub("sync.log");
        var sink = new FileLogSink(path);

        // Act
        sink.Dispose();

        // Assert
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }
}
