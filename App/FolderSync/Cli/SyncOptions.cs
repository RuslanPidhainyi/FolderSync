using FolderSync.Sync.Comparison;

namespace FolderSync.Cli;

/// <summary>
/// Immutable configuration for one run of the program.
/// Produced by <see cref="SyncOptionsParser"/>, checked against the file system by <see cref="SyncOptionsValidator"/>.
/// </summary>
public sealed record SyncOptions(
    string Source,
    string Replica,
    TimeSpan Interval,
    string LogFilePath,
    ComparisonMode ComparisonMode,
    bool RunOnce);
