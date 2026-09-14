namespace FolderSync.Sync.Comparison;

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
