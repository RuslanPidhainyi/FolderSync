namespace FolderSync.Logging;

/// <summary>
/// Minimal logging abstraction used by the synchronizer.
/// Kept separate from the concrete implementation so the sync logic can be unit tested.
/// </summary>
public interface ISyncLogger
{
    void Info(string message);

    void Warning(string message);

    void Error(string message, Exception? exception = null);
}
