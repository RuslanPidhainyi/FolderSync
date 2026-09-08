using System.Security.Cryptography;

namespace FolderSync.Sync.Comparison;

/// <summary>
/// Factory that maps a <see cref="ComparisonMode"/> chosen on the command line to a concrete
/// <see cref="IFileComparer"/>. The synchronizer only ever sees the interface, so new strategies
/// are added here without touching the synchronization algorithm.
/// </summary>
public static class FileComparerFactory
{
    public static IFileComparer Create(ComparisonMode mode) => mode switch
    {
        ComparisonMode.Md5 => new HashFileComparer("MD5", MD5.HashData),
        ComparisonMode.Sha256 => new HashFileComparer("SHA-256", SHA256.HashData),
        ComparisonMode.Quick => new QuickFileComparer(),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported comparison mode."),
    };
}
