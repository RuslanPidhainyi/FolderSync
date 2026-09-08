using FolderSync.Sync.Comparison;
using FolderSync.Tests.Support;

namespace FolderSync.Tests;

public sealed class FileComparerFactoryTests
{
    [Theory]
    [InlineData(ComparisonMode.Md5, typeof(HashFileComparer))]
    [InlineData(ComparisonMode.Sha256, typeof(HashFileComparer))]
    [InlineData(ComparisonMode.Quick, typeof(QuickFileComparer))]
    public void Creates_comparer_for_each_mode(ComparisonMode mode, Type expected)
    {
        Assert.IsType(expected, FileComparerFactory.Create(mode));
    }

    [Fact]
    public void Hash_comparers_use_the_requested_algorithm()
    {
        Assert.Equal("MD5", ((HashFileComparer)FileComparerFactory.Create(ComparisonMode.Md5)).AlgorithmName);
        Assert.Equal("SHA-256", ((HashFileComparer)FileComparerFactory.Create(ComparisonMode.Sha256)).AlgorithmName);
    }

    [Fact]
    public void Rejects_unknown_mode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FileComparerFactory.Create((ComparisonMode)42));
    }
}

public sealed class HashFileComparerTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    public static TheoryData<ComparisonMode> HashModes => new() { ComparisonMode.Md5, ComparisonMode.Sha256 };

    [Theory]
    [MemberData(nameof(HashModes))]
    public void Identical_content_is_equal(ComparisonMode mode)
    {
        var a = new FileInfo(_dir.WriteFile("a", "same content"));
        var b = new FileInfo(_dir.WriteFile("b", "same content"));

        Assert.True(FileComparerFactory.Create(mode).AreEqual(a, b));
    }

    [Theory]
    [MemberData(nameof(HashModes))]
    public void Different_size_is_not_equal(ComparisonMode mode)
    {
        var a = new FileInfo(_dir.WriteFile("a", "abc"));
        var b = new FileInfo(_dir.WriteFile("b", "abcd"));

        Assert.False(FileComparerFactory.Create(mode).AreEqual(a, b));
    }

    [Theory]
    [MemberData(nameof(HashModes))]
    public void Same_size_different_content_is_not_equal(ComparisonMode mode)
    {
        var a = new FileInfo(_dir.WriteFile("a", "abc"));
        var b = new FileInfo(_dir.WriteFile("b", "abd"));

        Assert.False(FileComparerFactory.Create(mode).AreEqual(a, b));
    }

    [Fact]
    public void Empty_files_are_equal()
    {
        var a = new FileInfo(_dir.WriteFile("a", string.Empty));
        var b = new FileInfo(_dir.WriteFile("b", string.Empty));

        Assert.True(FileComparerFactory.Create(ComparisonMode.Md5).AreEqual(a, b));
    }

    [Theory]
    [InlineData(ComparisonMode.Md5, "5eb63bbbe01eeed093cb22bb8f5acdc3")]
    [InlineData(ComparisonMode.Sha256, "b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9")]
    public void ComputeHash_matches_known_digest(ComparisonMode mode, string expectedHex)
    {
        var file = _dir.WriteFile("hello", "hello world");
        var comparer = (HashFileComparer)FileComparerFactory.Create(mode);

        var hash = Convert.ToHexString(comparer.ComputeHash(file)).ToLowerInvariant();

        Assert.Equal(expectedHex, hash);
    }

    [Fact]
    public void Requires_algorithm_name_and_hash_function()
    {
        Assert.Throws<ArgumentException>(() => new HashFileComparer(" ", _ => []));
        Assert.Throws<ArgumentNullException>(() => new HashFileComparer("X", null!));
    }
}

public sealed class QuickFileComparerTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly QuickFileComparer _comparer = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Same_size_and_time_is_equal_even_if_content_differs()
    {
        var a = Create("a", "abc", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var b = Create("b", "xyz", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.True(_comparer.AreEqual(a, b));
    }

    [Fact]
    public void Time_within_two_seconds_is_still_equal()
    {
        var stamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var a = Create("a", "abc", stamp);
        var b = Create("b", "abc", stamp.AddSeconds(2));

        Assert.True(_comparer.AreEqual(a, b));
    }

    [Fact]
    public void Different_time_is_not_equal()
    {
        var stamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var a = Create("a", "abc", stamp);
        var b = Create("b", "abc", stamp.AddSeconds(3));

        Assert.False(_comparer.AreEqual(a, b));
    }

    [Fact]
    public void Different_size_is_not_equal()
    {
        var stamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var a = Create("a", "abc", stamp);
        var b = Create("b", "abcd", stamp);

        Assert.False(_comparer.AreEqual(a, b));
    }

    private FileInfo Create(string name, string content, DateTime lastWriteUtc)
    {
        var path = _dir.WriteFile(name, content);
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
        return new FileInfo(path);
    }
}
