using System.Globalization;
using System.Text.RegularExpressions;
using FolderSync.Sync.Comparison;

namespace FolderSync.Cli;

public static partial class SyncOptionsParser
{
    public const string Usage = """
        FolderSync - one-way periodic folder synchronization (source -> replica).

        Usage:
          FolderSync --source <dir> --replica <dir> --interval <time> --log <file> [options]
          FolderSync <source> <replica> <interval> <log-file> [options]

        Required:
          -s, --source <dir>     Folder to copy from. Must exist.
          -r, --replica <dir>    Folder to keep identical to the source. Created if missing.
                                 Everything in it that is not in the source will be deleted!
          -i, --interval <time>  Time between synchronization passes. A plain number means
                                 seconds; suffixes ms, s, m, h are supported (e.g. 30s, 5m, 1h).
          -l, --log <file>       Log file. Created if missing, appended to otherwise.

        Options:
          -c, --compare <mode>   How to decide whether a file changed:
                                   md5     size, then MD5 of the content (default)
                                   sha256  size, then SHA-256 of the content
                                   quick   size and last-write time only (fastest)
              --once             Run a single synchronization pass and exit.
          -h, --help             Show this help.

        Examples:
          FolderSync -s C:\Data -r D:\Backup\Data -i 60 -l C:\Logs\sync.log
          FolderSync /home/me/docs /mnt/backup/docs 5m /var/log/foldersync.log --compare quick
        """;

    private const string SupportedComparisonModes = "md5, sha256, quick";

    private sealed class Draft
    {
        public string? Source;
        public string? Replica;
        public string? Interval;
        public string? Log;
        public string? Compare;
        public bool RunOnce;
    }

    private sealed record OptionSpec(
        string ShortName,
        string LongName,
        string Description,
        bool Positional,
        Func<Draft, string?> Get,
        Action<Draft, string> Set);

    private static readonly OptionSpec[] Specs =
    [
        new("-s", "--source", "source folder", true, d => d.Source, (d, v) => d.Source = v),
        new("-r", "--replica", "replica folder", true, d => d.Replica, (d, v) => d.Replica = v),
        new("-i", "--interval", "synchronization interval", true, d => d.Interval, (d, v) => d.Interval = v),
        new("-l", "--log", "log file path", true, d => d.Log, (d, v) => d.Log = v),
        new("-c", "--compare", "comparison mode", false, d => d.Compare, (d, v) => d.Compare = v),
    ];

    public static SyncOptions? Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var draft = new Draft();
        var positional = new List<string>();

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-h" or "--help" or "/?":
                    return null;
                case "--once":
                    draft.RunOnce = true;
                    break;
                default:
                    var spec = Array.Find(Specs, s => s.ShortName == arg || s.LongName == arg);
                    if (spec is not null)
                    {
                        if (i + 1 >= args.Count)
                        {
                            throw new OptionsException($"Option '{arg}' requires a value.");
                        }

                        spec.Set(draft, args[++i]);
                    }
                    else if (LooksLikeOption(arg))
                    {
                        throw new OptionsException($"Unknown option '{arg}'.");
                    }
                    else
                    {
                        positional.Add(arg);
                    }

                    break;
            }
        }

        AssignPositional(draft, positional);
        RequireMandatory(draft);

        return new SyncOptions(
            Path.GetFullPath(draft.Source!),
            Path.GetFullPath(draft.Replica!),
            ParseInterval(draft.Interval!),
            Path.GetFullPath(draft.Log!),
            ParseComparisonMode(draft.Compare),
            draft.RunOnce);
    }

    public static TimeSpan ParseInterval(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

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

    public static ComparisonMode ParseComparisonMode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ComparisonMode.Md5;
        }

        return text.Trim().ToLowerInvariant() switch
        {
            "md5" => ComparisonMode.Md5,
            "sha256" => ComparisonMode.Sha256,
            "quick" => ComparisonMode.Quick,
            _ => throw new OptionsException(
                $"Invalid comparison mode '{text}'. Supported values: {SupportedComparisonModes}."),
        };
    }

    private static void AssignPositional(Draft draft, List<string> positional)
    {
        var queue = new Queue<string>(positional);

        foreach (var spec in Specs.Where(s => s.Positional))
        {
            if (queue.Count == 0)
            {
                break;
            }

            if (spec.Get(draft) is null)
            {
                spec.Set(draft, queue.Dequeue());
            }
        }

        if (queue.Count > 0)
        {
            throw new OptionsException($"Unexpected argument '{queue.Peek()}'.");
        }
    }

    private static void RequireMandatory(Draft draft)
    {
        foreach (var spec in Specs.Where(s => s.Positional))
        {
            if (string.IsNullOrWhiteSpace(spec.Get(draft)))
            {
                throw new OptionsException($"The {spec.Description} is not specified.");
            }
        }
    }

    private static bool LooksLikeOption(string arg) => arg.Length > 1 && arg[0] == '-';

    [GeneratedRegex(@"^(?<value>\d+(?:\.\d+)?)\s*(?<unit>ms|s|m|h)?$", RegexOptions.IgnoreCase)]
    private static partial Regex IntervalRegex();
}
