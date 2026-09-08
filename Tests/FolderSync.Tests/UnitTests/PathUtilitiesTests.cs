using FolderSync.Sync;

namespace FolderSync.Tests.UnitTests;

/// <summary><see cref="PathUtilities"/> only manipulates strings; no folder in these tests exists on disk.</summary>
public sealed class PathUtilitiesTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine("unit-tests", "root"));

    [Fact]
    public void A_folder_is_inside_itself()
    {
        // Act
        var result = PathUtilities.IsSameOrInside(Root, Root);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Trailing_separator_is_ignored()
    {
        // Arrange
        var withSeparator = Root + Path.DirectorySeparatorChar;

        // Act
        var result = PathUtilities.IsSameOrInside(withSeparator, Root);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Nested_folder_is_inside()
    {
        // Arrange
        var nested = Path.Combine(Root, "a", "b");

        // Act
        var result = PathUtilities.IsSameOrInside(nested, Root);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Parent_folder_is_not_inside()
    {
        // Arrange
        var parent = Path.GetDirectoryName(Root)!;

        // Act
        var result = PathUtilities.IsSameOrInside(parent, Root);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Sibling_sharing_a_name_prefix_is_not_inside()
    {
        // Arrange
        var sibling = Root + "2";

        // Act
        var result = PathUtilities.IsSameOrInside(sibling, Root);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Relative_segments_are_resolved_before_comparing()
    {
        // Arrange
        var escapes = Path.Combine(Root, "a", "..", "..");

        // Act
        var result = PathUtilities.IsSameOrInside(escapes, Root);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Name_comparison_follows_the_platform()
    {
        // Arrange
        var expected = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        // Act
        var comparer = PathUtilities.NameComparer;

        // Assert
        Assert.Same(expected, comparer);
    }

    [Fact]
    public void Case_differences_follow_the_platform()
    {
        // Arrange
        var upperCased = Root.ToUpperInvariant();
        var caseInsensitive = ReferenceEquals(PathUtilities.NameComparer, StringComparer.OrdinalIgnoreCase);

        // Act
        var result = PathUtilities.IsSameOrInside(upperCased, Root);

        // Assert
        Assert.Equal(caseInsensitive, result);
    }
}
