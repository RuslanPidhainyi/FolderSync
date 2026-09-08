using FolderSync.Logging;
using FolderSync.Tests.Support;

namespace FolderSync.Tests.IntegrationTests;

public sealed class SyncLoggerFactoryTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Builds_a_logger_that_writes_identical_lines_to_console_and_file()
    {
        // Arrange
        var logPath = _dir.Sub("logs", "sync.log");
        var console = new StringWriter();

        // Act
        using (var logger = SyncLoggerFactory.Create(logPath, console))
        {
            logger.Info("hello");
            logger.Warning("careful");
            logger.Error("boom", new InvalidOperationException("details"));
        }

        // Assert
        var fileLines = File.ReadAllLines(logPath);
        var consoleLines = console.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(fileLines, consoleLines);
        Assert.Collection(
            fileLines,
            line => Assert.EndsWith("[INFO ] hello", line),
            line => Assert.EndsWith("[WARN ] careful", line),
            line => Assert.EndsWith("[ERROR] boom: InvalidOperationException: details", line));
    }

    [Fact]
    public void Fails_fast_when_the_log_path_is_a_folder()
    {
        // Arrange
        var folder = _dir.CreateDir("not-a-file");
        Action act = () => SyncLoggerFactory.Create(folder, TextWriter.Null);

        // Act
        var exception = Record.Exception(act);

        // Assert: Program.cs treats both as "log file could not be opened", so either is correct here.
        Assert.True(
            exception is IOException or UnauthorizedAccessException,
            $"Expected IOException or UnauthorizedAccessException but got {exception?.GetType()}");
    }
}
