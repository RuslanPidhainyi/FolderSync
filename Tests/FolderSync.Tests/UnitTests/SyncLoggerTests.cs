using FolderSync.Logging;
using FolderSync.Tests.Support;

namespace FolderSync.Tests.UnitTests;

public sealed class SyncLoggerTests
{
    [Fact]
    public void Formats_timestamp_level_and_message()
    {
        // Arrange
        var sink = new RecordingSink();
        var logger = new SyncLogger(sink);

        // Act
        logger.Info("hello");

        // Assert
        var line = Assert.Single(sink.Lines);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \[INFO \] hello$", line);
    }

    [Fact]
    public void Uses_a_fixed_width_level_tag_for_every_level()
    {
        // Arrange
        var sink = new RecordingSink();
        var logger = new SyncLogger(sink);

        // Act
        logger.Info("a");
        logger.Warning("b");
        logger.Error("c");

        // Assert
        Assert.Collection(
            sink.Lines,
            line => Assert.EndsWith("[INFO ] a", line),
            line => Assert.EndsWith("[WARN ] b", line),
            line => Assert.EndsWith("[ERROR] c", line));
    }

    [Fact]
    public void Appends_exception_type_and_message_to_errors()
    {
        // Arrange
        var sink = new RecordingSink();
        var logger = new SyncLogger(sink);
        var exception = new InvalidOperationException("details");

        // Act
        logger.Error("boom", exception);

        // Assert
        var line = Assert.Single(sink.Lines);
        Assert.EndsWith("[ERROR] boom: InvalidOperationException: details", line);
    }

    [Fact]
    public void Fans_every_line_out_to_all_sinks()
    {
        // Arrange
        var first = new RecordingSink();
        var second = new RecordingSink();
        var logger = new SyncLogger(first, second);

        // Act
        logger.Info("one");
        logger.Warning("two");

        // Assert
        Assert.Equal(2, first.Lines.Count);
        Assert.Equal(first.Lines, second.Lines);
    }

    [Fact]
    public void Dispose_disposes_all_sinks()
    {
        // Arrange
        var first = new RecordingSink();
        var second = new RecordingSink();
        var logger = new SyncLogger(first, second);

        // Act
        logger.Dispose();

        // Assert
        Assert.True(first.Disposed);
        Assert.True(second.Disposed);
    }

    [Fact]
    public void Requires_at_least_one_sink()
    {
        // Arrange
        Action act = () => new SyncLogger();

        // Act & Assert
        Assert.Throws<ArgumentException>(act);
    }
}
