using FolderSync.Cli;
using FolderSync.Sync.Comparison;
using FolderSync.Tests.Support;

namespace FolderSync.Tests;

public sealed class SyncOptionsParserTests : IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly string _source;
    private readonly string _replica;
    private readonly string _log;

    public SyncOptionsParserTests()
    {
        _source = _root.CreateDir("source");
        _replica = _root.Sub("replica");
        _log = _root.Sub("logs", "sync.log");
    }

    public void Dispose() => _root.Dispose();

    [Fact]
    public void Parses_named_options()
    {
        var options = SyncOptionsParser.Parse(["--source", _source, "--replica", _replica, "--interval", "30", "--log", _log]);

        Assert.NotNull(options);
        Assert.Equal(_source, options.Source);
        Assert.Equal(_replica, options.Replica);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Interval);
        Assert.Equal(_log, options.LogFilePath);
        Assert.Equal(ComparisonMode.Md5, options.ComparisonMode);
        Assert.False(options.RunOnce);
    }

    [Fact]
    public void Parses_short_options_and_flags()
    {
        var options = SyncOptionsParser.Parse(["-s", _source, "-r", _replica, "-i", "5m", "-l", _log, "-c", "quick", "--once"]);

        Assert.NotNull(options);
        Assert.Equal(TimeSpan.FromMinutes(5), options.Interval);
        Assert.Equal(ComparisonMode.Quick, options.ComparisonMode);
        Assert.True(options.RunOnce);
    }

    [Fact]
    public void Parses_positional_arguments()
    {
        var options = SyncOptionsParser.Parse([_source, _replica, "10", _log]);

        Assert.NotNull(options);
        Assert.Equal(_source, options.Source);
        Assert.Equal(_replica, options.Replica);
        Assert.Equal(TimeSpan.FromSeconds(10), options.Interval);
        Assert.Equal(_log, options.LogFilePath);
    }

    [Fact]
    public void Mixes_named_and_positional_arguments()
    {
        var options = SyncOptionsParser.Parse(["--interval", "2h", _source, _replica, _log, "--compare", "SHA256"]);

        Assert.NotNull(options);
        Assert.Equal(TimeSpan.FromHours(2), options.Interval);
        Assert.Equal(_log, options.LogFilePath);
        Assert.Equal(ComparisonMode.Sha256, options.ComparisonMode);
    }

    [Fact]
    public void Help_returns_null()
    {
        Assert.Null(SyncOptionsParser.Parse(["--help"]));
        Assert.Null(SyncOptionsParser.Parse(["-h"]));
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
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), SyncOptionsParser.ParseInterval(text));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("5d")]
    [InlineData("")]
    public void Rejects_invalid_intervals(string text)
    {
        Assert.Throws<OptionsException>(() => SyncOptionsParser.ParseInterval(text));
    }

    [Theory]
    [InlineData(null, ComparisonMode.Md5)]
    [InlineData("md5", ComparisonMode.Md5)]
    [InlineData("MD5", ComparisonMode.Md5)]
    [InlineData("sha256", ComparisonMode.Sha256)]
    [InlineData(" quick ", ComparisonMode.Quick)]
    public void Parses_comparison_modes(string? text, ComparisonMode expected)
    {
        Assert.Equal(expected, SyncOptionsParser.ParseComparisonMode(text));
    }

    [Fact]
    public void Rejects_unknown_comparison_mode()
    {
        var ex = Assert.Throws<OptionsException>(() => SyncOptionsParser.ParseComparisonMode("crc32"));
        Assert.Contains("md5, sha256, quick", ex.Message);
    }

    [Fact]
    public void Rejects_missing_arguments()
    {
        var ex = Assert.Throws<OptionsException>(() => SyncOptionsParser.Parse([_source, _replica]));
        Assert.Contains("interval", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_unknown_option()
    {
        Assert.Throws<OptionsException>(() => SyncOptionsParser.Parse(["--bogus", _source, _replica, "1", _log]));
    }

    [Fact]
    public void Rejects_option_without_value()
    {
        Assert.Throws<OptionsException>(() => SyncOptionsParser.Parse([_source, _replica, "1", "--log"]));
    }

    [Fact]
    public void Rejects_extra_positional_argument()
    {
        Assert.Throws<OptionsException>(() => SyncOptionsParser.Parse([_source, _replica, "1", _log, "extra"]));
    }

    [Fact]
    public void Rejects_missing_source_folder()
    {
        var ex = Assert.Throws<OptionsException>(() => SyncOptionsParser.Parse([_root.Sub("nope"), _replica, "1", _log]));
        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public void Rejects_replica_equal_to_source()
    {
        Assert.Throws<OptionsException>(() => SyncOptionsParser.Parse([_source, _source, "1", _log]));
    }

    [Fact]
    public void Rejects_replica_inside_source()
    {
        Assert.Throws<OptionsException>(() => SyncOptionsParser.Parse([_source, Path.Combine(_source, "replica"), "1", _log]));
    }

    [Fact]
    public void Rejects_source_inside_replica()
    {
        Assert.Throws<OptionsException>(() => SyncOptionsParser.Parse([_source, _root.Path, "1", _root.Sub("..", "sync.log")]));
    }

    [Fact]
    public void Rejects_log_file_inside_source_or_replica()
    {
        Assert.Throws<OptionsException>(() => SyncOptionsParser.Parse([_source, _replica, "1", Path.Combine(_source, "sync.log")]));
        Assert.Throws<OptionsException>(() => SyncOptionsParser.Parse([_source, _replica, "1", Path.Combine(_replica, "sync.log")]));
    }

    [Fact]
    public void Rejects_log_path_that_is_a_folder()
    {
        var folder = _root.CreateDir("logdir");
        Assert.Throws<OptionsException>(() => SyncOptionsParser.Parse([_source, _replica, "1", folder]));
    }
}
