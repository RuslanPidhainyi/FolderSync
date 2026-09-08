using FolderSync.Sync;
using FolderSync.Tests.Support;

namespace FolderSync.Tests;

public sealed class Md5FileComparerTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly Md5FileComparer _comparer = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Identical_content_is_equal()
    {
        var a = new FileInfo(_dir.WriteFile("a", "same content"));
        var b = new FileInfo(_dir.WriteFile("b", "same content"));

        Assert.True(_comparer.AreEqual(a, b));
    }

    [Fact]
    public void Different_size_is_not_equal()
    {
        var a = new FileInfo(_dir.WriteFile("a", "abc"));
        var b = new FileInfo(_dir.WriteFile("b", "abcd"));

        Assert.False(_comparer.AreEqual(a, b));
    }

    [Fact]
    public void Same_size_different_content_is_not_equal()
    {
        var a = new FileInfo(_dir.WriteFile("a", "abc"));
        var b = new FileInfo(_dir.WriteFile("b", "abd"));

        Assert.False(_comparer.AreEqual(a, b));
    }

    [Fact]
    public void Empty_files_are_equal()
    {
        var a = new FileInfo(_dir.WriteFile("a", string.Empty));
        var b = new FileInfo(_dir.WriteFile("b", string.Empty));

        Assert.True(_comparer.AreEqual(a, b));
    }

    [Fact]
    public void ComputeHash_matches_known_md5()
    {
        var file = _dir.WriteFile("hello", "hello world");

        var hash = Convert.ToHexString(Md5FileComparer.ComputeHash(file)).ToLowerInvariant();

        Assert.Equal("5eb63bbbe01eeed093cb22bb8f5acdc3", hash);
    }
}
