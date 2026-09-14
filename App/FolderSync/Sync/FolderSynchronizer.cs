using System.Diagnostics;
using FolderSync.Logging;
using FolderSync.Sync.Comparison;
using FolderSync.Sync.FileSystem;

namespace FolderSync.Sync;

public sealed class FolderSynchronizer : IFolderSynchronizer
{
    private static readonly IReadOnlyDictionary<SyncOperation, (string Done, string Verb)> Messages =
        new Dictionary<SyncOperation, (string, string)>
        {
            [SyncOperation.CreateFile] = ("Created file", "copy file"),
            [SyncOperation.UpdateFile] = ("Updated file", "update file"),
            [SyncOperation.DeleteFile] = ("Deleted file", "delete file"),
            [SyncOperation.CreateDirectory] = ("Created folder", "create folder"),
            [SyncOperation.DeleteDirectory] = ("Deleted folder", "delete folder"),
        };

    private readonly string _sourceRoot;
    private readonly string _replicaRoot;
    private readonly IFileComparer _comparer;
    private readonly IFileOperations _fileSystem;
    private readonly ISyncLogger _log;
    private readonly StringComparer _nameComparer = PathUtilities.NameComparer;

    public FolderSynchronizer(string sourceRoot, string replicaRoot, IFileComparer comparer, ISyncLogger log)
        : this(sourceRoot, replicaRoot, comparer, new FileOperations(), log)
    {
    }

    public FolderSynchronizer(
        string sourceRoot,
        string replicaRoot,
        IFileComparer comparer,
        IFileOperations fileSystem,
        ISyncLogger log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaRoot);

        _sourceRoot = Path.GetFullPath(sourceRoot);
        _replicaRoot = Path.GetFullPath(replicaRoot);
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public SyncResult Synchronize(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var stats = new SyncStatistics();

        var source = new DirectoryInfo(_sourceRoot);
        if (!source.Exists)
        {
            throw new DirectoryNotFoundException($"Source folder does not exist: {_sourceRoot}");
        }

        var replica = new DirectoryInfo(_replicaRoot);
        if (!replica.Exists)
        {
            Apply(SyncOperation.CreateDirectory, string.Empty, () => _fileSystem.CreateDirectory(replica), stats);
        }

        SyncDirectory(source, replica, relativePath: string.Empty, stats, cancellationToken);

        return stats.ToResult(stopwatch.Elapsed);
    }

    private void SyncDirectory(
        DirectoryInfo source,
        DirectoryInfo replica,
        string relativePath,
        SyncStatistics stats,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryEnumerate(source, relativePath, stats, out var sourceEntries))
        {
            return;
        }

        var sourceFiles = new Dictionary<string, FileInfo>(_nameComparer);
        var sourceDirs = new Dictionary<string, DirectoryInfo>(_nameComparer);

        foreach (var entry in sourceEntries)
        {
            switch (entry)
            {
                case DirectoryInfo { LinkTarget: not null } link:
                    _log.Warning($"Skipping folder symlink {Display(Path.Combine(relativePath, link.Name))}");
                    break;
                case DirectoryInfo dir:
                    sourceDirs[dir.Name] = dir;
                    break;
                case FileInfo file:
                    sourceFiles[file.Name] = file;
                    break;
            }
        }

        RemoveExtraneousEntries(replica, relativePath, sourceFiles, sourceDirs, stats, cancellationToken);
        CopyChangedFiles(sourceFiles.Values, replica, relativePath, stats, cancellationToken);
        SyncSubdirectories(sourceDirs.Values, replica, relativePath, stats, cancellationToken);
    }

    private void RemoveExtraneousEntries(
        DirectoryInfo replica,
        string relativePath,
        IReadOnlyDictionary<string, FileInfo> sourceFiles,
        IReadOnlyDictionary<string, DirectoryInfo> sourceDirs,
        SyncStatistics stats,
        CancellationToken cancellationToken)
    {
        if (!TryEnumerate(replica, relativePath, stats, out var replicaEntries))
        {
            return;
        }

        foreach (var entry in replicaEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entryPath = Path.Combine(relativePath, entry.Name);

            switch (entry)
            {
                case DirectoryInfo dir when !sourceDirs.ContainsKey(dir.Name):
                    DeleteDirectoryTree(dir, entryPath, stats, cancellationToken);
                    break;
                case FileInfo file when !sourceFiles.ContainsKey(file.Name):
                    Apply(SyncOperation.DeleteFile, entryPath, () => _fileSystem.DeleteFile(file), stats);
                    break;
            }
        }
    }

    private void CopyChangedFiles(
        IEnumerable<FileInfo> sourceFiles,
        DirectoryInfo replica,
        string relativePath,
        SyncStatistics stats,
        CancellationToken cancellationToken)
    {
        foreach (var sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entryPath = Path.Combine(relativePath, sourceFile.Name);
            var replicaFile = new FileInfo(Path.Combine(replica.FullName, sourceFile.Name));

            if (!replicaFile.Exists)
            {
                Apply(SyncOperation.CreateFile, entryPath, () => _fileSystem.CopyFile(sourceFile, replicaFile), stats);
            }
            else if (TryCompare(sourceFile, replicaFile, entryPath, stats, out var equal) && !equal)
            {
                Apply(SyncOperation.UpdateFile, entryPath, () => _fileSystem.CopyFile(sourceFile, replicaFile), stats);
            }
        }
    }

    private void SyncSubdirectories(
        IEnumerable<DirectoryInfo> sourceDirs,
        DirectoryInfo replica,
        string relativePath,
        SyncStatistics stats,
        CancellationToken cancellationToken)
    {
        foreach (var sourceDir in sourceDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entryPath = Path.Combine(relativePath, sourceDir.Name);
            var replicaDir = new DirectoryInfo(Path.Combine(replica.FullName, sourceDir.Name));

            if (!replicaDir.Exists
                && !Apply(SyncOperation.CreateDirectory, entryPath, () => _fileSystem.CreateDirectory(replicaDir), stats))
            {
                continue;
            }

            SyncDirectory(sourceDir, replicaDir, entryPath, stats, cancellationToken);
        }
    }

    private void DeleteDirectoryTree(
        DirectoryInfo directory,
        string relativePath,
        SyncStatistics stats,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (directory.LinkTarget is null && TryEnumerate(directory, relativePath, stats, out var entries))
        {
            foreach (var entry in entries)
            {
                var entryPath = Path.Combine(relativePath, entry.Name);
                switch (entry)
                {
                    case DirectoryInfo dir:
                        DeleteDirectoryTree(dir, entryPath, stats, cancellationToken);
                        break;
                    case FileInfo file:
                        Apply(SyncOperation.DeleteFile, entryPath, () => _fileSystem.DeleteFile(file), stats);
                        break;
                }
            }
        }

        Apply(SyncOperation.DeleteDirectory, relativePath, () => _fileSystem.DeleteDirectory(directory), stats);
    }

    private bool Apply(SyncOperation operation, string relativePath, Action action, SyncStatistics stats)
    {
        var (done, verb) = Messages[operation];
        if (!Guard(action, $"Failed to {verb} {Display(relativePath)}", stats))
        {
            return false;
        }

        stats.Record(operation);
        _log.Info($"{done,-14} {Display(relativePath)}");
        return true;
    }

    private bool TryEnumerate(
        DirectoryInfo directory,
        string relativePath,
        SyncStatistics stats,
        out IReadOnlyList<FileSystemInfo> entries)
    {
        IReadOnlyList<FileSystemInfo> result = [];
        var succeeded = Guard(
            () => result = _fileSystem.Enumerate(directory),
            $"Failed to read folder {Display(relativePath)}",
            stats);

        entries = result;
        return succeeded;
    }

    private bool TryCompare(FileInfo source, FileInfo replica, string relativePath, SyncStatistics stats, out bool equal)
    {
        var result = false;
        var succeeded = Guard(
            () => result = _comparer.AreEqual(source, replica),
            $"Failed to compare {Display(relativePath)}",
            stats);

        equal = result;
        return succeeded;
    }

    private bool Guard(Action action, string failureMessage, SyncStatistics stats)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            stats.RecordError();
            _log.Error(failureMessage, ex);
            return false;
        }
    }

    private static string Display(string relativePath) =>
        relativePath.Length == 0 ? "<root>" : relativePath;
}
