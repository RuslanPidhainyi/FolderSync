namespace FolderSync.Logging;

public interface ISyncLogger
{
    void Info(string message);

    void Warning(string message);

    void Error(string message, Exception? exception = null);
}
