using FolderSync.Sync.Comparison;
using FolderSync.Tests.Support;

namespace FolderSync.Tests.IntegrationTests;

/// <summary>Both hash-based strategies against real files on disk.</summary>
public sealed class HashFileComparerTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    public static TheoryData<ComparisonMode> HashModes => new() { ComparisonMode.Md5, ComparisonMode.Sha256 };

    [Theory]
    [MemberData(nameof(HashModes))]
    public void Identical_content_is_equal(ComparisonMode mode)
    {
        // Arrange
        var a = new FileInfo(_dir.WriteFile("a", "same content"));
        var b = new FileInfo(_dir.WriteFile("b", "same content"));
        var comparer = FileComparerFactory.Create(mode);

        // Act
        var equal = comparer.AreEqual(a, b);

        // Assert
        Assert.True(equal);
    }

    [Theory]
    [MemberData(nameof(HashModes))]
    public void Different_size_is_not_equal(ComparisonMode mode)
    {
        // Arrange
        var a = new FileInfo(_dir.WriteFile("a", "abc"));
        var b = new FileInfo(_dir.WriteFile("b", "abcd"));
        var comparer = FileComparerFactory.Create(mode);

        // Act
        var equal = comparer.AreEqual(a, b);

        // Assert
        Assert.False(equal);
    }

    [Theory]
    [MemberData(nameof(HashModes))]
    public void Same_size_with_different_content_is_not_equal(ComparisonMode mode)
    {
        // Arrange
        var a = new FileInfo(_dir.WriteFile("a", "abc"));
        var b = new FileInfo(_dir.WriteFile("b", "abd"));
        var comparer = FileComparerFactory.Create(mode);

        // Act
        var equal = comparer.AreEqual(a, b);

        // Assert
        Assert.False(equal);
    }

    [Theory]
    [MemberData(nameof(HashModes))]
    public void Empty_files_are_equal(ComparisonMode mode)
    {
        // Arrange
        var a = new FileInfo(_dir.WriteFile("a", string.Empty));
        var b = new FileInfo(_dir.WriteFile("b", string.Empty));
        var comparer = FileComparerFactory.Create(mode);

        // Act
        var equal = comparer.AreEqual(a, b);

        // Assert
        Assert.True(equal);
    }

    [Theory]
    [InlineData(ComparisonMode.Md5, "5eb63bbbe01eeed093cb22bb8f5acdc3")]
    [InlineData(ComparisonMode.Sha256, "b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9")]
    public void ComputeHash_matches_the_known_digest(ComparisonMode mode, string expectedHex)
    {
        // Arrange
        var file = _dir.WriteFile("hello", "hello world");
        var comparer = (HashFileComparer)FileComparerFactory.Create(mode);

        // Act
        var hash = comparer.ComputeHash(file);

        // Assert
        Assert.Equal(expectedHex, Convert.ToHexString(hash).ToLowerInvariant());
    }

    [Fact]
    public void Can_hash_a_file_that_another_process_holds_open_for_writing()
    {
        // Arrange
        var path = _dir.WriteFile("shared", "content");
        using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        var comparer = (HashFileComparer)FileComparerFactory.Create(ComparisonMode.Md5);

        // Act
        var hash = comparer.ComputeHash(path);

        // Assert
        Assert.NotEmpty(hash);
    }
}
