# FolderSync

A small, dependency-free .NET console tool that keeps a **replica** folder identical to a **source** folder.
Synchronization is one-way and runs periodically; every file/folder creation, update and removal is written
both to the console and to a log file.

## Requirements

* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer (the project targets `net8.0`).
* Works on Windows, Linux and macOS.

## Build

```bash
git clone https://github.com/<your-account>/FolderSync.git
cd FolderSync
dotnet build -c Release
```

Or produce a single executable:

```bash
dotnet publish src/FolderSync -c Release -o publish
```

## Usage

```
FolderSync --source <dir> --replica <dir> --interval <time> --log <file> [options]
FolderSync <source> <replica> <interval> <log-file> [options]
```

| Option                  | Meaning                                                                                              |
|-------------------------|------------------------------------------------------------------------------------------------------|
| `-s`, `--source <dir>`  | Folder to copy from. Must exist.                                                                     |
| `-r`, `--replica <dir>` | Folder to keep identical to the source. Created if missing. **Anything not in the source is deleted.** |
| `-i`, `--interval <t>`  | Time between passes. A plain number is seconds; suffixes `ms`, `s`, `m`, `h` are supported (`30s`, `5m`, `1h`). |
| `-l`, `--log <file>`    | Log file. Created if missing, appended to otherwise.                                                 |
| `-c`, `--compare <mode>`| How to detect a changed file: `md5` (default), `sha256`, or `quick` (size + last-write time, no content read). |
| `--once`                | Run a single pass and exit (handy for scripts and cron jobs).                                        |
| `-h`, `--help`          | Show help.                                                                                           |

Examples:

```bash
# Windows
dotnet run --project src/FolderSync -- -s C:\Data -r D:\Backup\Data -i 60 -l C:\Logs\sync.log

# Linux / macOS, positional form, every 5 minutes, cheap comparison for a large tree
dotnet run --project src/FolderSync -- /home/me/docs /mnt/backup/docs 5m /var/log/foldersync.log --compare quick
```

The program synchronizes immediately on start and then once per interval until you press `Ctrl+C`.
A pass that is already running is allowed to finish its current file operation before the process exits.

### Sample output

```
2026-09-08 18:40:01.120 [INFO ] FolderSync starting
2026-09-08 18:40:01.121 [INFO ]   Source:   C:\Data
2026-09-08 18:40:01.121 [INFO ]   Replica:  D:\Backup\Data
2026-09-08 18:40:01.121 [INFO ]   Interval: 00:01:00
2026-09-08 18:40:01.121 [INFO ]   Compare:  Md5
2026-09-08 18:40:01.121 [INFO ]   Log file: C:\Logs\sync.log
2026-09-08 18:40:01.122 [INFO ] Press Ctrl+C to stop.
2026-09-08 18:40:01.123 [INFO ] Synchronization #1 started
2026-09-08 18:40:01.140 [INFO ] Created folder photos
2026-09-08 18:40:01.152 [INFO ] Created file   photos\cat.jpg
2026-09-08 18:40:01.160 [INFO ] Updated file   notes.txt
2026-09-08 18:40:01.163 [INFO ] Deleted file   old.tmp
2026-09-08 18:40:01.165 [INFO ] Deleted folder obsolete
2026-09-08 18:40:01.166 [INFO ] Synchronization #1 finished: 1 file(s) created, 1 updated, 1 deleted; 1 folder(s) created, 1 deleted; 0 error(s); took 43 ms
```

Exit codes: `0` success, `1` invalid arguments or unusable log file, `2` a `--once` pass reported errors
or the program failed unexpectedly.

## How it works

Each pass walks the source and replica trees together, one directory at a time:

1. **Index** the entries of the current source directory.
2. **Delete** everything in the matching replica directory that is not in the source. A replica entry whose kind
   differs (file vs. folder) is deleted too, so it can be replaced.
3. **Copy** source files that are missing in the replica and **overwrite** files whose content differs
   according to the selected comparison mode. The replica file keeps the source file's last-write time.
4. **Recurse** into sub-folders, creating them in the replica when needed.

Deleting before copying keeps the replica's disk usage from temporarily doubling.

### Comparison modes

| Mode     | Check                                   | Reads content | Notes                                                        |
|----------|-----------------------------------------|---------------|--------------------------------------------------------------|
| `md5`    | size, then MD5 digest                   | yes           | Default. Detects any content change.                         |
| `sha256` | size, then SHA-256 digest               | yes           | Same guarantees, slower; for the collision-averse.           |
| `quick`  | size and last-write time (±2 s)         | no            | What `rsync` does by default. Fast on huge trees; misses an edit that keeps both size and timestamp. |

Digests come from `System.Security.Cryptography`; no third-party packages are used anywhere.

## Architecture

The code is organised so that each class has one reason to change and the algorithm never depends on
concrete infrastructure.

```
                 ┌──────────────────────┐   ┌────────────────────────┐
  args ────────▶ │  SyncOptionsParser   │──▶│  SyncOptionsValidator  │──▶ SyncOptions
                 └──────────────────────┘   └────────────────────────┘   (immutable record)
                                                                                │
                                                                                ▼
                 ┌──────────────────────────────────────────────┐
                 │ SyncApplicationFactory  (composition root)   │
                 │  ├ SyncLoggerFactory   ──▶ SyncLogger ─┬─ ConsoleLogSink
                 │  ├ FileComparerFactory ──▶ IFileComparer   └─ FileLogSink
                 │  ├ new FileOperations  ──▶ IFileOperations
                 │  └ new FolderSynchronizer(…), PeriodicSyncRunner(…)
                 └──────────────────────────────────────────────┘
                                                      │
                                                      ▼
                 SyncApplication.RunAsync ──▶ PeriodicSyncRunner ──▶ IFolderSynchronizer
```

Patterns and principles in use:

* **Factory** — `FileComparerFactory`, `SyncLoggerFactory` and `SyncApplicationFactory` are the only places
  that name concrete classes. Adding a comparison strategy or a log destination is a one-line change in a
  factory; nothing else is touched (Open/Closed).
* **Strategy** — `IFileComparer` encapsulates *how* two files are compared. `HashFileComparer` takes the hash
  function as a dependency, so MD5 and SHA-256 share one implementation instead of two near-identical classes (DRY).
* **Composite** — `SyncLogger` formats a line once and fans it out to any number of `ILogSink`s.
  Formatting and destinations are separate responsibilities (Single Responsibility).
* **Dependency Inversion** — `FolderSynchronizer` depends on `IFileComparer`, `IFileOperations` and
  `ISyncLogger`, never on `System.IO` details such as read-only attributes or timestamp preservation.
  Tests inject a failing `IFileOperations` to verify error isolation without provoking real I/O errors.
* **One error path** — every file system call in the synchronizer goes through a single `Guard` helper that
  logs the failure, counts it and lets the pass continue; the five kinds of change share one `Apply` method
  driven by a message table (DRY).
* **Table-driven CLI** — one `OptionSpec` table drives named parsing, positional fallback and the
  "not specified" errors, so each option is declared exactly once.

### Design decisions

* **Resilient.** A failure on one entry (locked file, permission denied) is logged, counted and skipped;
  the rest of the pass continues and the entry is retried on the next pass. Read-only files in the replica
  are overwritten/deleted as required. Hidden and system files are synchronized like any other.
* **Safe by construction.** The parser refuses configurations that would destroy data or loop forever:
  replica inside source, source inside replica, or the log file inside either folder.
* **Never overlapping.** Passes run sequentially. If one pass takes longer than the interval,
  the next one starts right after it instead of in parallel.
* **Graceful shutdown.** `Ctrl+C` cancels the timer and the running pass at the next safe point.
* **Directory symlinks** in the source are skipped with a warning to avoid cycles; in the replica they are
  removed as a single entry without descending into the target.
* **Case sensitivity** follows the platform: names are compared case-insensitively on Windows/macOS and
  case-sensitively on Linux.

### Known limitations

* Copies are not atomic. If the process is killed mid-copy, the partial file is detected as different
  and re-copied on the next pass.
* File permissions/ACLs and extended attributes are not mirrored beyond what `File.Copy` preserves.

## Project layout

```
FolderSync.sln
src/FolderSync/
  Program.cs                        entry point: parse, build, run, exit code
  ExitCodes.cs
  SyncApplication.cs                drives the runner in --once or periodic mode, owns the logger
  SyncApplicationFactory.cs         composition root
  Cli/
    SyncOptions.cs                  immutable options record
    SyncOptionsParser.cs            table-driven argument parsing (no file system access)
    SyncOptionsValidator.cs         checks options against the file system
    OptionsException.cs
  Logging/
    ISyncLogger.cs                  logging abstraction used by the domain
    ILogSink.cs, ConsoleLogSink.cs, FileLogSink.cs
    SyncLogger.cs                   formatting + fan-out to sinks
    SyncLoggerFactory.cs
  Sync/
    IFolderSynchronizer.cs
    FolderSynchronizer.cs           the one-way synchronization algorithm
    PeriodicSyncRunner.cs           periodic execution
    SyncOperation.cs, SyncStatistics.cs, SyncResult.cs
    PathUtilities.cs
    Comparison/
      IFileComparer.cs, ComparisonMode.cs
      HashFileComparer.cs           size + injected digest (MD5, SHA-256)
      QuickFileComparer.cs          size + last-write time
      FileComparerFactory.cs
    FileSystem/
      IFileOperations.cs
      FileOperations.cs             System.IO implementation (attributes, timestamps, enumeration)
tests/FolderSync.Tests/
  UnitTests/                        no disk, no real clock: fakes and in-memory doubles only
  IntegrationTests/                 real files, real folders, real System.IO
  EndToEndTests/                    parses real arguments and runs the composed application
  Support/                          shared test doubles (TempDirectory, TestLogger, FakeSynchronizer, ...)
```

## Tests

```bash
dotnet test
```

Every test follows Arrange / Act / Assert, and tests are split by what they touch:

* **`UnitTests/`** — one class in isolation, no disk and no real clock. Dependencies are fakes
  (`FakeSynchronizer`, `RecordingSink`, `TestLogger`) or values with no side effects
  (`SyncOptionsParser`, `PathUtilities`, the factories). Runs in well under a second.
* **`IntegrationTests/`** — real components against the real file system: `FolderSynchronizer` with
  `FileOperations` and the real hash comparers, the log sinks, and `SyncOptionsValidator` (which exists
  specifically to check paths on disk). Covers copying nested trees, same-size content changes, deleting
  stale files/folders, replacing a file with a folder and vice versa, read-only and hidden files,
  timestamp preservation, and error isolation against a genuinely locked file and an injected failing
  `IFileOperations`.
* **`EndToEndTests/`** — real command line arguments through `SyncOptionsParser` and
  `SyncOptionsValidator`, then `SyncApplicationFactory` wiring the whole graph, run against a real
  folder pair. This is the closest thing to running the published executable.

Run one category at a time with `dotnet test --filter FullyQualifiedName~UnitTests` (or
`IntegrationTests` / `EndToEndTests`).
