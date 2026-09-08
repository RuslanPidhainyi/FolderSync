namespace FolderSync.Sync.Comparison;

/// <summary>
/// Compares files by size first (cheap) and then by a digest of their content.
/// The hash function is injected, so MD5, SHA-256 or any other algorithm share this one implementation.
/// The digest is used purely as a content fingerprint, not for security.
/// </summary>
public sealed class HashFileComparer : IFileComparer
{
    private const int BufferSize = 1 << 16;

    private readonly Func<Stream, byte[]> _hash;

    public HashFileComparer(string algorithmName, Func<Stream, byte[]> hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithmName);
        AlgorithmName = algorithmName;
        _hash = hash ?? throw new ArgumentNullException(nameof(hash));
    }

    public string AlgorithmName { get; }

    public bool AreEqual(FileInfo source, FileInfo replica)
    {
        if (source.Length != replica.Length)
        {
            return false;
        }

        if (source.Length == 0)
        {
            return true;
        }

        return ComputeHash(source.FullName).AsSpan().SequenceEqual(ComputeHash(replica.FullName));
    }

    public byte[] ComputeHash(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            BufferSize,
            FileOptions.SequentialScan);

        return _hash(stream);
    }
}
