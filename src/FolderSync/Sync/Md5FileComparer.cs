using System.Security.Cryptography;

namespace FolderSync.Sync;

/// <summary>
/// Compares files by size first (cheap) and then by MD5 digest of their content.
/// MD5 is not used for security here, only as a fast fingerprint of the file content.
/// </summary>
public sealed class Md5FileComparer : IFileComparer
{
    private const int BufferSize = 1 << 16;

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

        var sourceHash = ComputeHash(source.FullName);
        var replicaHash = ComputeHash(replica.FullName);
        return sourceHash.AsSpan().SequenceEqual(replicaHash);
    }

    public static byte[] ComputeHash(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            BufferSize,
            FileOptions.SequentialScan);

        return MD5.HashData(stream);
    }
}
