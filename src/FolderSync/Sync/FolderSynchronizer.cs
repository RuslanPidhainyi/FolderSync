using System.Diagnostics;
using FolderSync.Logging;

namespace FolderSync.Sync;

/// <summary>
/// One-way synchronizer: after <see cref="Synchronize"/> completes, the replica folder is an exact
/// copy of the source folder. Files missing in the replica are copied, files that differ are
/// overwritten, and anything that exists only in the replica is removed.
/// </summary>
public sealed class FolderSynchronizer : IFolderSynchronizer
{
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        // The default EnumerationOptions skip hidden and system entries; we want everything.
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
    };

    private readonly string _sourceRoot;
    private readonly string _replicaRoot;
    private readonly IFileComparer _comparer;
    private readonly ISyncLogger _log;
    private readonly StringComparer _nameComparer;

    public FolderSynchronizer(string sourceRoot, string replicaRoot, IFileComparer comparer, ISyncLogger log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaRoot);

        _sourceRoot = Path.GetFullPath(sourceRoot);
        _replicaRoot = Path.GetFullPath(replicaRoot);
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _nameComparer = PathUtilities.NameComparer;
    }

    public SyncResult Synchronize(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var stats = new Counters();

        var source = new DirectoryInfo(_sourceRoot);
        if (!source.Exists)
        {
            throw new DirectoryNotFoundException($"Source folder does not exist: {_sourceRoot}");
        }

        var replica = new DirectoryInfo(_replicaRoot);
        if (!replica.Exists)
        {
            replica.Create();
            stats.DirectoriesCreated++;
            _log.Info($"Created folder  {Display(string.Empty)}");
        }

        SyncDirectory(source, replica, relativePath: string.Empty, stats, cancellationToken);

        return stats.ToResult(stopwatch.Elapsed);
    }

    private void SyncDirectory(
        DirectoryInfo source,
        DirectoryInfo replica,
        string relativePath,
        Counters stats,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1. Index the source directory.
        var sourceFiles = new Dictionary<string, FileInfo>(_nameComparer);
        var sourceDirs = new Dictionary<string, DirectoryInfo>(_nameComparer);

        if (!TryEnumerate(source, relativePath, stats, out var sourceEntries))
        {
            return;
        }

        foreach (var entry in sourceEntries)
        {
            switch (entry)
            {
                case DirectoryInfo dir when dir.LinkTarget is not null:
                    _log.Warning($"Skipping folder symlink {Display(Combine(relativePath, dir.Name))}");
                    break;
                case DirectoryInfo dir:
                    sourceDirs[dir.Name] = dir;
                    break;
                case FileInfo file:
                    sourceFiles[file.Name] = file;
                    break;
            }
        }

        // 2. Remove everything from the replica that no longer exists in the source
        //    (or exists with a different kind: file vs. folder).
        if (TryEnumerate(replica, relativePath, stats, out var replicaEntries))
        {
            foreach (var entry in replicaEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entryPath = Combine(relativePath, entry.Name);

                switch (entry)
                {
                    case DirectoryInfo dir when !sourceDirs.ContainsKey(dir.Name):
                        DeleteDirectory(dir, entryPath, stats, cancellationToken);
                        break;
                    case FileInfo file when !sourceFiles.ContainsKey(file.Name):
                        DeleteFile(file, entryPath, stats);
                        break;
                }
            }
        }

        // 3. Copy new files and overwrite files whose content changed.
        foreach (var sourceFile in sourceFiles.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entryPath = Combine(relativePath, sourceFile.Name);
            var replicaFile = new FileInfo(Path.Combine(replica.FullName, sourceFile.Name));

            try
            {
                if (!replicaFile.Exists)
                {
                    CopyFile(sourceFile, replicaFile);
                    stats.FilesCreated++;
                    _log.Info($"Created file    {Display(entryPath)}");
                }
                else if (!_comparer.AreEqual(sourceFile, replicaFile))
                {
                    CopyFile(sourceFile, replicaFile);
                    stats.FilesUpdated++;
                    _log.Info($"Updated file    {Display(entryPath)}");
                }
            }
            catch (Exception ex) when (IsRecoverable(ex))
            {
                stats.Errors++;
                _log.Error($"Failed to copy {Display(entryPath)}", ex);
            }
        }

        // 4. Recurse into sub-folders, creating them in the replica when needed.
        foreach (var sourceDir in sourceDirs.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entryPath = Combine(relativePath, sourceDir.Name);
            var replicaDir = new DirectoryInfo(Path.Combine(replica.FullName, sourceDir.Name));

            if (!replicaDir.Exists)
            {
                try
                {
                    replicaDir.Create();
                    stats.DirectoriesCreated++;
                    _log.Info($"Created folder  {Display(entryPath)}");
                }
                catch (Exception ex) when (IsRecoverable(ex))
                {
                    stats.Errors++;
                    _log.Error($"Failed to create folder {Display(entryPath)}", ex);
                    continue;
                }
            }

            SyncDirectory(sourceDir, replicaDir, entryPath, stats, cancellationToken);
        }
    }

    private bool TryEnumerate(
        DirectoryInfo directory,
        string relativePath,
        Counters stats,
        out List<FileSystemInfo> entries)
    {
        try
        {
            entries = directory.EnumerateFileSystemInfos("*", EnumerationOptions).ToList();
            return true;
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            stats.Errors++;
            _log.Error($"Failed to read folder {Display(relativePath)}", ex);
            entries = [];
            return false;
        }
    }

    private static void CopyFile(FileInfo source, FileInfo destination)
    {
        if (destination.Exists && destination.IsReadOnly)
        {
            destination.IsReadOnly = false;
        }

        source.CopyTo(destination.FullName, overwrite: true);

        // Keep the replica timestamp identical to the source so the copy is a faithful mirror.
        try
        {
            File.SetLastWriteTimeUtc(destination.FullName, source.LastWriteTimeUtc);
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            // Not fatal: the content is already in place, only the timestamp differs.
        }
    }

    private void DeleteFile(FileInfo file, string relativePath, Counters stats)
    {
        try
        {
            if (file.IsReadOnly)
            {
                file.IsReadOnly = false;
            }

            file.Delete();
            stats.FilesDeleted++;
            _log.Info($"Deleted file    {Display(relativePath)}");
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            stats.Errors++;
            _log.Error($"Failed to delete file {Display(relativePath)}", ex);
        }
    }

    private void DeleteDirectory(
        DirectoryInfo directory,
        string relativePath,
        Counters stats,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // A folder symlink in the replica is removed as a single entry; we never descend into it.
        if (directory.LinkTarget is null && TryEnumerate(directory, relativePath, stats, out var entries))
        {
            foreach (var entry in entries)
            {
                var entryPath = Combine(relativePath, entry.Name);
                switch (entry)
                {
                    case DirectoryInfo dir:
                        DeleteDirectory(dir, entryPath, stats, cancellationToken);
                        break;
                    case FileInfo file:
                        DeleteFile(file, entryPath, stats);
                        break;
                }
            }
        }

        try
        {
            directory.Delete(recursive: false);
            stats.DirectoriesDeleted++;
            _log.Info($"Deleted folder  {Display(relativePath)}");
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            stats.Errors++;
            _log.Error($"Failed to delete folder {Display(relativePath)}", ex);
        }
    }

    private static string Combine(string relativePath, string name) =>
        relativePath.Length == 0 ? name : Path.Combine(relativePath, name);

    private static string Display(string relativePath) =>
        relativePath.Length == 0 ? "<root>" : relativePath;

    private static bool IsRecoverable(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or NotSupportedException;

    private sealed class Counters
    {
        public int FilesCreated;
        public int FilesUpdated;
        public int FilesDeleted;
        public int DirectoriesCreated;
        public int DirectoriesDeleted;
        public int Errors;

        public SyncResult ToResult(TimeSpan duration) => new(
            FilesCreated,
            FilesUpdated,
            FilesDeleted,
            DirectoriesCreated,
            DirectoriesDeleted,
            Errors,
            duration);
    }
}
