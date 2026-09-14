using FolderSync.Sync;

namespace FolderSync.Cli;

public static class SyncOptionsValidator
{
    public static void Validate(SyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Directory.Exists(options.Source))
        {
            throw new OptionsException($"Source folder does not exist: {options.Source}");
        }

        if (File.Exists(options.Replica))
        {
            throw new OptionsException($"Replica path points to a file, not a folder: {options.Replica}");
        }

        if (PathUtilities.IsSameOrInside(options.Replica, options.Source))
        {
            throw new OptionsException("Replica folder must not be the source folder or located inside it.");
        }

        if (PathUtilities.IsSameOrInside(options.Source, options.Replica))
        {
            throw new OptionsException("Source folder must not be located inside the replica folder (it would be deleted).");
        }

        if (Directory.Exists(options.LogFilePath))
        {
            throw new OptionsException($"Log path points to a folder, not a file: {options.LogFilePath}");
        }

        if (PathUtilities.IsSameOrInside(options.LogFilePath, options.Source)
            || PathUtilities.IsSameOrInside(options.LogFilePath, options.Replica))
        {
            throw new OptionsException("Log file must not be located inside the source or replica folder.");
        }
    }
}
