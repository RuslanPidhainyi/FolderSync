using FolderSync.Cli;
using FolderSync.Sync.Comparison;
using FolderSync.Tests.Support;

namespace FolderSync.Tests.IntegrationTests;

/// <summary>Validation looks at the real file system, so every case needs actual folders.</summary>
public sealed class SyncOptionsValidatorTests : IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly string _source;
    private readonly string _replica;
    private readonly string _log;

    public SyncOptionsValidatorTests()
    {
        _source = _root.CreateDir("source");
        _replica = _root.Sub("replica");
        _log = _root.Sub("logs", "sync.log");
    }

    public void Dispose() => _root.Dispose();

    private SyncOptions Options(string? source = null, string? replica = null, string? log = null) =>
        new(source ?? _source, replica ?? _replica, TimeSpan.FromSeconds(1), log ?? _log, ComparisonMode.Md5, RunOnce: false);

    [Fact]
    public void Accepts_a_valid_configuration()
    {
        // Arrange
        var options = Options();

        // Act
        var exception = Record.Exception(() => SyncOptionsValidator.Validate(options));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Accepts_a_replica_that_does_not_exist_yet()
    {
        // Arrange
        var options = Options(replica: _root.Sub("not", "created", "yet"));

        // Act
        var exception = Record.Exception(() => SyncOptionsValidator.Validate(options));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Rejects_a_missing_source_folder()
    {
        // Arrange
        var options = Options(source: _root.Sub("nope"));
        Action act = () => SyncOptionsValidator.Validate(options);

        // Act
        var exception = Assert.Throws<OptionsException>(act);

        // Assert
        Assert.Contains("does not exist", exception.Message);
    }

    [Fact]
    public void Rejects_a_replica_path_that_is_a_file()
    {
        // Arrange
        var file = _root.WriteFile("replica-file", "x");
        var options = Options(replica: file);
        Action act = () => SyncOptionsValidator.Validate(options);

        // Act
        var exception = Assert.Throws<OptionsException>(act);

        // Assert
        Assert.Contains("not a folder", exception.Message);
    }

    [Fact]
    public void Rejects_a_replica_equal_to_the_source()
    {
        // Arrange
        var options = Options(replica: _source);
        Action act = () => SyncOptionsValidator.Validate(options);

        // Act & Assert
        Assert.Throws<OptionsException>(act);
    }

    [Fact]
    public void Rejects_a_replica_inside_the_source()
    {
        // Arrange
        var options = Options(replica: Path.Combine(_source, "replica"));
        Action act = () => SyncOptionsValidator.Validate(options);

        // Act & Assert
        Assert.Throws<OptionsException>(act);
    }

    [Fact]
    public void Rejects_a_source_inside_the_replica()
    {
        // Arrange
        var options = Options(replica: _root.Path, log: _root.Sub("..", "sync.log"));
        Action act = () => SyncOptionsValidator.Validate(options);

        // Act
        var exception = Assert.Throws<OptionsException>(act);

        // Assert
        Assert.Contains("would be deleted", exception.Message);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("replica")]
    public void Rejects_a_log_file_inside_a_synchronized_folder(string folder)
    {
        // Arrange
        var options = Options(log: _root.Sub(folder, "sync.log"));
        Action act = () => SyncOptionsValidator.Validate(options);

        // Act & Assert
        Assert.Throws<OptionsException>(act);
    }

    [Fact]
    public void Rejects_a_log_path_that_is_a_folder()
    {
        // Arrange
        var options = Options(log: _root.CreateDir("logdir"));
        Action act = () => SyncOptionsValidator.Validate(options);

        // Act
        var exception = Assert.Throws<OptionsException>(act);

        // Assert
        Assert.Contains("not a file", exception.Message);
    }
}
