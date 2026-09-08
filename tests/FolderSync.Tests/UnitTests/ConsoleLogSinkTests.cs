using FolderSync.Logging;

namespace FolderSync.Tests.UnitTests;

public sealed class ConsoleLogSinkTests
{
    [Fact]
    public void Writes_each_line_to_the_writer()
    {
        // Arrange
        var writer = new StringWriter();
        var sink = new ConsoleLogSink(writer);

        // Act
        sink.Write("hello");

        // Assert
        Assert.Equal("hello" + Environment.NewLine, writer.ToString());
    }

    [Fact]
    public void Dispose_does_not_close_the_writer_it_was_given()
    {
        // Arrange
        var writer = new StringWriter();
        var sink = new ConsoleLogSink(writer);

        // Act
        sink.Dispose();

        // Assert
        writer.Write("still usable");
        Assert.Equal("still usable", writer.ToString());
    }
}
