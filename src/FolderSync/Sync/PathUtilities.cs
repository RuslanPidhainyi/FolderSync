namespace FolderSync.Sync;

public static class PathUtilities
{
    /// <summary>
    /// File names are compared case-insensitively on Windows and macOS (their default file systems
    /// are case-insensitive) and case-sensitively elsewhere.
    /// </summary>
    public static StringComparer NameComparer { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static StringComparison NameComparison =>
        ReferenceEquals(NameComparer, StringComparer.Ordinal)
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Returns true when <paramref name="path"/> is the same folder as <paramref name="root"/>
    /// or is located anywhere inside it.
    /// </summary>
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
