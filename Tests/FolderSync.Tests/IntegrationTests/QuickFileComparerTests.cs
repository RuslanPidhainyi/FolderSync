using FolderSync.Sync.Comparison;
using FolderSync.Tests.Support;

namespace FolderSync.Tests.IntegrationTests;

public sealed class QuickFileComparerTests : IDisposable
{
    private static readonly DateTime Stamp = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly TempDirectory _dir = new();
    private readonly QuickFileComparer _comparer = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Same_size_and_time_is_equal_even_if_the_content_differs()
    {
        // Arrange
        var a = CreateFile("a", "abc", Stamp);
        var b = CreateFile("b", "xyz", Stamp);

        // Act
        var equal = _comparer.AreEqual(a, b);

        // Assert
        Assert.True(equal);
    }

    [Fact]
    public void A_time_difference_within_two_seconds_is_still_equal()
    {
        // Arrange
        var a = CreateFile("a", "abc", Stamp);
        var b = CreateFile("b", "abc", Stamp.AddSeconds(2));

        // Act
        var equal = _comparer.AreEqual(a, b);

        // Assert
        Assert.True(equal);
    }

    [Fact]
    public void A_larger_time_difference_is_not_equal()
    {
        // Arrange
        var a = CreateFile("a", "abc", Stamp);
        var b = CreateFile("b", "abc", Stamp.AddSeconds(3));

        // Act
        var equal = _comparer.AreEqual(a, b);

        // Assert
        Assert.False(equal);
    }

    [Fact]
    public void Different_size_is_not_equal()
    {
        // Arrange
        var a = CreateFile("a", "abc", Stamp);
        var b = CreateFile("b", "abcd", Stamp);

        // Act
        var equal = _comparer.AreEqual(a, b);

        // Assert
        Assert.False(equal);
    }

    private FileInfo CreateFile(string name, string content, DateTime lastWriteUtc)
    {
        var path = _dir.WriteFile(name, content);
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
        return new FileInfo(path);
    }
}
