using FolderSync.Cli;
using FolderSync.Sync.Comparison;

namespace FolderSync.Tests.UnitTests;

public sealed class SyncOptionsParserTests
{
    private const string Source = "source";
    private const string Replica = "replica";
    private const string Log = "logs/sync.log";

    [Fact]
    public void Parses_named_options()
    {
        // Arrange
        string[] args = ["--source", Source, "--replica", Replica, "--interval", "30", "--log", Log];

        // Act
        var options = SyncOptionsParser.Parse(args);

        // Assert
        Assert.NotNull(options);
        Assert.Equal(Path.GetFullPath(Source), options.Source);
        Assert.Equal(Path.GetFullPath(Replica), options.Replica);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Interval);
        Assert.Equal(Path.GetFullPath(Log), options.LogFilePath);
        Assert.Equal(ComparisonMode.Md5, options.ComparisonMode);
        Assert.False(options.RunOnce);
    }

    [Fact]
    public void Parses_short_options_and_flags()
    {
        // Arrange
        string[] args = ["-s", Source, "-r", Replica, "-i", "5m", "-l", Log, "-c", "quick", "--once"];

        // Act
        var options = SyncOptionsParser.Parse(args);

        // Assert
        Assert.NotNull(options);
        Assert.Equal(TimeSpan.FromMinutes(5), options.Interval);
        Assert.Equal(ComparisonMode.Quick, options.ComparisonMode);
        Assert.True(options.RunOnce);
    }

    [Fact]
    public void Parses_positional_arguments()
    {
        // Arrange
        string[] args = [Source, Replica, "10", Log];

        // Act
        var options = SyncOptionsParser.Parse(args);

        // Assert
        Assert.NotNull(options);
        Assert.Equal(Path.GetFullPath(Source), options.Source);
        Assert.Equal(Path.GetFullPath(Replica), options.Replica);
        Assert.Equal(TimeSpan.FromSeconds(10), options.Interval);
        Assert.Equal(Path.GetFullPath(Log), options.LogFilePath);
    }

    [Fact]
    public void Positional_arguments_fill_only_the_options_not_given_by_name()
    {
        // Arrange
        string[] args = ["--interval", "2h", Source, Replica, Log, "--compare", "SHA256"];

        // Act
        var options = SyncOptionsParser.Parse(args);

        // Assert
        Assert.NotNull(options);
        Assert.Equal(Path.GetFullPath(Source), options.Source);
        Assert.Equal(Path.GetFullPath(Replica), options.Replica);
        Assert.Equal(TimeSpan.FromHours(2), options.Interval);
        Assert.Equal(Path.GetFullPath(Log), options.LogFilePath);
        Assert.Equal(ComparisonMode.Sha256, options.ComparisonMode);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public void Help_returns_null(string flag)
    {
        // Arrange
        string[] args = [Source, flag, Replica];

        // Act
        var options = SyncOptionsParser.Parse(args);

        // Assert
        Assert.Null(options);
    }

    [Theory]
    [InlineData("45", 45_000)]
    [InlineData("500ms", 500)]
    [InlineData("1.5s", 1_500)]
    [InlineData("2m", 120_000)]
    [InlineData("1H", 3_600_000)]
    [InlineData(" 10 s ", 10_000)]
    public void Parses_interval_formats(string text, double expectedMilliseconds)
    {
        // Arrange
        var expected = TimeSpan.FromMilliseconds(expectedMilliseconds);

        // Act
        var interval = SyncOptionsParser.ParseInterval(text);

        // Assert
        Assert.Equal(expected, interval);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("5d")]
    [InlineData("")]
    [InlineData("999999h")]
    public void Rejects_invalid_intervals(string text)
    {
        // Arrange
        Action act = () => SyncOptionsParser.ParseInterval(text);

        // Act & Assert
        Assert.Throws<OptionsException>(act);
    }

    [Theory]
    [InlineData(null, ComparisonMode.Md5)]
    [InlineData("", ComparisonMode.Md5)]
    [InlineData("md5", ComparisonMode.Md5)]
    [InlineData("MD5", ComparisonMode.Md5)]
    [InlineData("sha256", ComparisonMode.Sha256)]
    [InlineData(" quick ", ComparisonMode.Quick)]
    public void Parses_comparison_modes(string? text, ComparisonMode expected)
    {
        // Act
        var mode = SyncOptionsParser.ParseComparisonMode(text);

        // Assert
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void Rejects_unknown_comparison_mode()
    {
        // Arrange
        Action act = () => SyncOptionsParser.ParseComparisonMode("crc32");

        // Act
        var exception = Assert.Throws<OptionsException>(act);

        // Assert
        Assert.Contains("crc32", exception.Message);
        Assert.Contains("md5, sha256, quick", exception.Message);
    }

    [Theory]
    [InlineData(0, "source")]
    [InlineData(1, "replica")]
    [InlineData(2, "interval")]
    [InlineData(3, "log")]
    public void Reports_the_first_missing_mandatory_option(int provided, string expectedWord)
    {
        // Arrange
        string[] all = [Source, Replica, "1", Log];
        var args = all.Take(provided).ToArray();
        Action act = () => SyncOptionsParser.Parse(args);

        // Act
        var exception = Assert.Throws<OptionsException>(act);

        // Assert
        Assert.Contains(expectedWord, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not specified", exception.Message);
    }

    [Fact]
    public void Rejects_blank_value()
    {
        // Arrange
        string[] args = ["--source", "  ", Replica, "1", Log];
        Action act = () => SyncOptionsParser.Parse(args);

        // Act
        var exception = Assert.Throws<OptionsException>(act);

        // Assert
        Assert.Contains("source folder", exception.Message);
    }

    [Fact]
    public void Rejects_unknown_option()
    {
        // Arrange
        string[] args = ["--bogus", Source, Replica, "1", Log];
        Action act = () => SyncOptionsParser.Parse(args);

        // Act
        var exception = Assert.Throws<OptionsException>(act);

        // Assert
        Assert.Contains("--bogus", exception.Message);
    }

    [Fact]
    public void Rejects_option_without_value()
    {
        // Arrange
        string[] args = [Source, Replica, "1", "--log"];
        Action act = () => SyncOptionsParser.Parse(args);

        // Act
        var exception = Assert.Throws<OptionsException>(act);

        // Assert
        Assert.Contains("--log", exception.Message);
        Assert.Contains("requires a value", exception.Message);
    }

    [Fact]
    public void Rejects_extra_positional_argument()
    {
        // Arrange
        string[] args = [Source, Replica, "1", Log, "extra"];
        Action act = () => SyncOptionsParser.Parse(args);

        // Act
        var exception = Assert.Throws<OptionsException>(act);

        // Assert
        Assert.Contains("extra", exception.Message);
    }

    [Fact]
    public void Usage_documents_every_option()
    {
        // Arrange
        string[] expected = ["--source", "--replica", "--interval", "--log", "--compare", "--once", "--help"];

        // Act
        var usage = SyncOptionsParser.Usage;

        // Assert
        Assert.All(expected, option => Assert.Contains(option, usage));
    }
}
