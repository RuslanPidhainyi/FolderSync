using FolderSync.Sync.Comparison;

namespace FolderSync.Tests.UnitTests;

/// <summary>Constructor guards only; hashing real files is covered by the integration tests.</summary>
public sealed class HashFileComparerArgumentTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Requires_an_algorithm_name(string? name)
    {
        // Arrange
        Action act = () => new HashFileComparer(name!, _ => []);

        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(act);
    }

    [Fact]
    public void Requires_a_hash_function()
    {
        // Arrange
        Action act = () => new HashFileComparer("MD5", null!);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Exposes_the_algorithm_name()
    {
        // Act
        var comparer = new HashFileComparer("XXH3", _ => []);

        // Assert
        Assert.Equal("XXH3", comparer.AlgorithmName);
    }
}
