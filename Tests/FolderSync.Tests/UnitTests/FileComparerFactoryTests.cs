using FolderSync.Sync.Comparison;

namespace FolderSync.Tests.UnitTests;

public sealed class FileComparerFactoryTests
{
    [Theory]
    [InlineData(ComparisonMode.Md5, typeof(HashFileComparer))]
    [InlineData(ComparisonMode.Sha256, typeof(HashFileComparer))]
    [InlineData(ComparisonMode.Quick, typeof(QuickFileComparer))]
    public void Creates_the_comparer_for_each_mode(ComparisonMode mode, Type expectedType)
    {
        // Act
        var comparer = FileComparerFactory.Create(mode);

        // Assert
        Assert.IsType(expectedType, comparer);
    }

    [Theory]
    [InlineData(ComparisonMode.Md5, "MD5")]
    [InlineData(ComparisonMode.Sha256, "SHA-256")]
    public void Hash_comparers_use_the_requested_algorithm(ComparisonMode mode, string expectedAlgorithm)
    {
        // Act
        var comparer = (HashFileComparer)FileComparerFactory.Create(mode);

        // Assert
        Assert.Equal(expectedAlgorithm, comparer.AlgorithmName);
    }

    [Fact]
    public void Rejects_unknown_mode()
    {
        // Arrange
        var unknown = (ComparisonMode)42;
        Action act = () => FileComparerFactory.Create(unknown);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }
}
