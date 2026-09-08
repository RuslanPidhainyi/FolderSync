namespace FolderSync;

public static class ExitCodes
{
    public const int Success = 0;

    /// <summary>The command line could not be parsed or validated, or the log file could not be opened.</summary>
    public const int InvalidArguments = 1;

    /// <summary>A <c>--once</c> pass reported errors, or the program failed unexpectedly.</summary>
    public const int SyncFailed = 2;
}
