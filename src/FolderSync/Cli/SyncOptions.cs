using FolderSync.Sync.Comparison;

namespace FolderSync.Cli;

/// <summary>Validated, immutable configuration for one run of the program. Produced by <see cref="SyncOptionsParser"/>.</summary>
public sealed record SyncOptions(
    string Source,
    string Replica,
    TimeSpan Interval,
    string LogFilePath,
    ComparisonMode ComparisonMode,
    bool RunOnce);
