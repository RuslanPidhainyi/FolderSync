using FolderSync.Logging;

namespace FolderSync.Tests.Support;

public sealed class TestLogger : ISyncLogger
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    public IEnumerable<string> Infos => _entries.Where(e => e.StartsWith("INFO ", StringComparison.Ordinal));

    public IEnumerable<string> Warnings => _entries.Where(e => e.StartsWith("WARN ", StringComparison.Ordinal));

    public IEnumerable<string> Errors => _entries.Where(e => e.StartsWith("ERROR ", StringComparison.Ordinal));

    public void Info(string message) => _entries.Add($"INFO {message}");

    public void Warning(string message) => _entries.Add($"WARN {message}");

    public void Error(string message, Exception? exception = null) =>
        _entries.Add($"ERROR {message}{(exception is null ? string.Empty : $": {exception.Message}")}");
}
