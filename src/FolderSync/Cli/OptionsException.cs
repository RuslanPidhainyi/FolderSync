namespace FolderSync.Cli;

/// <summary>A user-facing problem with the command line arguments.</summary>
public sealed class OptionsException(string message) : Exception(message);
