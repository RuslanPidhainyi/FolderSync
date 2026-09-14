namespace FolderSync.Sync;

public static class PathUtilities
{
    public static StringComparer NameComparer { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static StringComparison NameComparison =>
        ReferenceEquals(NameComparer, StringComparer.Ordinal)
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

    public static bool IsSameOrInside(string path, string root)
    {
        var fullPath = Normalize(path);
        var fullRoot = Normalize(root);

        if (string.Equals(fullPath, fullRoot, NameComparison))
        {
            return true;
        }

        return fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, NameComparison);
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
