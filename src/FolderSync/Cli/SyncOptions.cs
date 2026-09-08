using System.Globalization;
using System.Text.RegularExpressions;
using FolderSync.Sync;

namespace FolderSync.Cli;

/// <summary>
/// Command line options. Both named (<c>--source dir</c>) and positional
/// (<c>source replica interval log</c>) forms are accepted.
/// </summary>
public sealed partial class SyncOptions
{
    public const string Usage = """
        FolderSync - one-way periodic folder synchronization (source -> replica).

        Usage:
          FolderSync --source <dir> --replica <dir> --interval <time> --log <file> [--once]
          FolderSync <source> <replica> <interval> <log-file> [--once]

        Options:
          -s, --source <dir>     Folder to copy from. Must exist.
          -r, --replica <dir>    Folder to keep identical to the source. Created if missing.
                                 Everything in it that is not in the source will be deleted!
          -i, --interval <time>  Time between synchronization passes. A plain number means
                                 seconds; suffixes ms, s, m, h are supported (e.g. 30s, 5m, 1h).
          -l, --log <file>       Log file. Created if missing, appended to otherwise.
              --once             Run a single synchronization pass and exit.
          -h, --help             Show this help.

        Examples:
          FolderSync -s C:\Data -r D:\Backup\Data -i 60 -l C:\Logs\sync.log
          FolderSync /home/me/docs /mnt/backup/docs 5m /var/log/foldersync.log
        """;

    private SyncOptions(string source, string replica, TimeSpan interval, string logFilePath, bool runOnce)
    {
        Source = source;
        Replica = replica;
        Interval = interval;
        LogFilePath = logFilePath;
        RunOnce = runOnce;
    }

    public string Source { get; }

    public string Replica { get; }

    public TimeSpan Interval { get; }

    public string LogFilePath { get; }

    public bool RunOnce { get; }

    /// <summary>
    /// Parses and validates the arguments. Returns <c>null</c> when help was requested.
    /// Throws <see cref="OptionsException"/> with a user-friendly message on any problem.
    /// </summary>
    public static SyncOptions? Parse(IReadOnlyList<string> args)
    {
        string? source = null;
        string? replica = null;
        string? interval = null;
        string? log = null;
        var runOnce = false;
        var positional = new List<string>();

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-h" or "--help" or "/?":
                    return null;
                case "--once":
                    runOnce = true;
                    break;
                case "-s" or "--source":
                    source = RequireValue(args, ref i);
                    break;
                case "-r" or "--replica":
                    replica = RequireValue(args, ref i);
                    break;
                case "-i" or "--interval":
                    interval = RequireValue(args, ref i);
                    break;
                case "-l" or "--log":
                    log = RequireValue(args, ref i);
                    break;
                default:
                    if (arg.StartsWith('-') && arg.Length > 1)
                    {
                        throw new OptionsException($"Unknown option '{arg}'.");
                    }

                    positional.Add(arg);
                    break;
            }
        }

        // Positional values fill whatever the named options did not provide, in the documented order.
        var queue = new Queue<string>(positional);
        source ??= queue.Count > 0 ? queue.Dequeue() : null;
        replica ??= queue.Count > 0 ? queue.Dequeue() : null;
        interval ??= queue.Count > 0 ? queue.Dequeue() : null;
        log ??= queue.Count > 0 ? queue.Dequeue() : null;

        if (queue.Count > 0)
        {
            throw new OptionsException($"Unexpected argument '{queue.Peek()}'.");
        }

        if (string.IsNullOrWhiteSpace(source)) throw new OptionsException("Source folder is not specified.");
        if (string.IsNullOrWhiteSpace(replica)) throw new OptionsException("Replica folder is not specified.");
        if (string.IsNullOrWhiteSpace(interval)) throw new OptionsException("Synchronization interval is not specified.");
        if (string.IsNullOrWhiteSpace(log)) throw new OptionsException("Log file path is not specified.");

        var options = new SyncOptions(
            Path.GetFullPath(source),
            Path.GetFullPath(replica),
            ParseInterval(interval),
            Path.GetFullPath(log),
            runOnce);

        options.Validate();
        return options;
    }

    public static TimeSpan ParseInterval(string text)
    {
        var match = IntervalRegex().Match(text.Trim());
        if (!match.Success)
        {
            throw new OptionsException(
                $"Invalid interval '{text}'. Use a number of seconds or a value with a suffix: 500ms, 30s, 5m, 1h.");
        }

        var value = double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
        var interval = match.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "ms" => TimeSpan.FromMilliseconds(value),
            "m" => TimeSpan.FromMinutes(value),
            "h" => TimeSpan.FromHours(value),
            _ => TimeSpan.FromSeconds(value),
        };

        if (interval <= TimeSpan.Zero)
        {
            throw new OptionsException("Interval must be greater than zero.");
        }

        if (interval > TimeSpan.FromDays(30))
        {
            throw new OptionsException("Interval must not exceed 30 days.");
        }

        return interval;
    }

    private void Validate()
    {
        if (!Directory.Exists(Source))
        {
            throw new OptionsException($"Source folder does not exist: {Source}");
        }

        if (File.Exists(Replica))
        {
            throw new OptionsException($"Replica path points to a file, not a folder: {Replica}");
        }

        if (PathUtilities.IsSameOrInside(Replica, Source))
        {
            throw new OptionsException("Replica folder must not be the source folder or located inside it.");
        }

        if (PathUtilities.IsSameOrInside(Source, Replica))
        {
            throw new OptionsException("Source folder must not be located inside the replica folder (it would be deleted).");
        }

        if (Directory.Exists(LogFilePath))
        {
            throw new OptionsException($"Log path points to a folder, not a file: {LogFilePath}");
        }

        if (PathUtilities.IsSameOrInside(LogFilePath, Source) || PathUtilities.IsSameOrInside(LogFilePath, Replica))
        {
            throw new OptionsException("Log file must not be located inside the source or replica folder.");
        }
    }

    private static string RequireValue(IReadOnlyList<string> args, ref int index)
    {
        if (index + 1 >= args.Count)
        {
            throw new OptionsException($"Option '{args[index]}' requires a value.");
        }

        return args[++index];
    }

    [GeneratedRegex(@"^(?<value>\d+(?:\.\d+)?)\s*(?<unit>ms|s|m|h)?$", RegexOptions.IgnoreCase)]
    private static partial Regex IntervalRegex();
}

public sealed class OptionsException(string message) : Exception(message);
