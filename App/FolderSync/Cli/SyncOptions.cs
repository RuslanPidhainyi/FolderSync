using FolderSync.Sync.Comparison;

namespace FolderSync.Cli;

public sealed record SyncOptions(
    string Source,
    string Replica,
    TimeSpan Interval,
    string LogFilePath,
    ComparisonMode ComparisonMode,
    bool RunOnce);
