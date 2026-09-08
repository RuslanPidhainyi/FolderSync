namespace FolderSync.Sync.Comparison;

/// <summary>How the synchronizer decides whether a replica file still matches its source.</summary>
public enum ComparisonMode
{
    /// <summary>Size first, then MD5 digest of the content. Default.</summary>
    Md5,

    /// <summary>Size first, then SHA-256 digest of the content.</summary>
    Sha256,

    /// <summary>Size and last-write time only. Fastest, but a change that keeps both is missed.</summary>
    Quick,
}
