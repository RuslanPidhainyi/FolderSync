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

Or produce a single self-contained executable:

```bash
dotnet publish src/FolderSync -c Release -o publish
```

## Usage

```
FolderSync --source <dir> --replica <dir> --interval <time> --log <file> [--once]
FolderSync <source> <replica> <interval> <log-file> [--once]
```

| Option                  | Meaning                                                                                              |
|-------------------------|------------------------------------------------------------------------------------------------------|
| `-s`, `--source <dir>`  | Folder to copy from. Must exist.                                                                     |
| `-r`, `--replica <dir>` | Folder to keep identical to the source. Created if missing. **Anything not in the source is deleted.** |
| `-i`, `--interval <t>`  | Time between passes. A plain number is seconds; suffixes `ms`, `s`, `m`, `h` are supported (`30s`, `5m`, `1h`). |
| `-l`, `--log <file>`    | Log file. Created if missing, appended to otherwise.                                                 |
| `--once`                | Run a single pass and exit (handy for scripts and cron jobs).                                        |
| `-h`, `--help`          | Show help.                                                                                           |

Examples:

```bash
# Windows
dotnet run --project src/FolderSync -- -s C:\Data -r D:\Backup\Data -i 60 -l C:\Logs\sync.log

# Linux / macOS, positional form, every 5 minutes
dotnet run --project src/FolderSync -- /home/me/docs /mnt/backup/docs 5m /var/log/foldersync.log
```

The program synchronizes immediately on start and then once per interval until you press `Ctrl+C`.
A pass that is already running is allowed to finish its current file operation before the process exits.

### Sample output

```
2026-09-08 18:40:01.120 [INFO ] FolderSync starting
2026-09-08 18:40:01.121 [INFO ]   Source:   C:\Data
2026-09-08 18:40:01.121 [INFO ]   Replica:  D:\Backup\Data
2026-09-08 18:40:01.121 [INFO ]   Interval: 00:01:00
2026-09-08 18:40:01.121 [INFO ]   Log file: C:\Logs\sync.log
2026-09-08 18:40:01.122 [INFO ] Press Ctrl+C to stop.
2026-09-08 18:40:01.123 [INFO ] Synchronization #1 started
2026-09-08 18:40:01.140 [INFO ] Created folder  photos
2026-09-08 18:40:01.152 [INFO ] Created file    photos\cat.jpg
2026-09-08 18:40:01.160 [INFO ] Updated file    notes.txt
2026-09-08 18:40:01.163 [INFO ] Deleted file    old.tmp
2026-09-08 18:40:01.165 [INFO ] Deleted folder  obsolete
2026-09-08 18:40:01.166 [INFO ] Synchronization #1 finished: 1 file(s) created, 1 updated, 1 deleted; 1 folder(s) created, 1 deleted; 0 error(s); took 43 ms
```

Exit codes: `0` success, `1` invalid arguments, `2` unexpected failure.

## How it works

Each pass walks the source and replica trees together, one directory at a time:

1. **Index** the entries of the current source directory.
2. **Delete** everything in the matching replica directory that is not in the source. A replica entry whose kind
   differs (file vs. folder) is deleted too, so it can be replaced.
3. **Copy** source files that are missing in the replica and **overwrite** files whose content differs.
   Files are compared by size first and then by MD5 digest (via `System.Security.Cryptography.MD5`),
   so a change that keeps the same size and timestamp is still detected. The replica file keeps the
   source file's last-write time.
4. **Recurse** into sub-folders, creating them in the replica when needed.

Deleting before copying keeps the replica's disk usage from temporarily doubling.

### Design decisions

* **No third-party packages.** Everything is built on the BCL: `System.IO` for the file system,
  `System.Security.Cryptography.MD5` for content fingerprints, `PeriodicTimer` for scheduling.
* **Resilient.** A failure on one entry (locked file, permission denied) is logged, counted and skipped;
  the rest of the pass continues and the entry is retried on the next pass. Read-only files in the replica
  are overwritten/deleted as required. Hidden and system files are synchronized like any other.
* **Safe by construction.** The CLI refuses configurations that would destroy data or loop forever:
  replica inside source, source inside replica, or the log file inside either folder.
* **Never overlapping.** Passes run sequentially. If one pass takes longer than the interval,
  the next one starts right after it instead of in parallel.
* **Graceful shutdown.** `Ctrl+C` cancels the timer and the running pass at the next safe point.
* **Directory symlinks** in the source are skipped with a warning to avoid cycles; in the replica they are
  removed as a single entry without descending into the target.
* **Case sensitivity** follows the platform: names are compared case-insensitively on Windows/macOS and
  case-sensitively on Linux.

### Known limitations

* Comparing by MD5 means every file is hashed on every pass. For very large trees a size + timestamp
  quick check (as `rsync` does by default) would be cheaper; it was left out in favour of guaranteed
  content equality.
* Copies are not atomic. If the process is killed mid-copy, the partial file is detected as different
  and re-copied on the next pass.
* File permissions/ACLs and extended attributes are not mirrored beyond what `File.Copy` preserves.

## Project layout

```
FolderSync.sln
src/FolderSync/
  Program.cs                   entry point: wiring, Ctrl+C handling, exit codes
  Cli/SyncOptions.cs           command line parsing and validation
  Logging/ISyncLogger.cs       logging abstraction
  Logging/SyncLogger.cs        console + file logger
  Sync/FolderSynchronizer.cs   the one-way synchronization algorithm
  Sync/Md5FileComparer.cs      size + MD5 content comparison
  Sync/PeriodicSyncRunner.cs   periodic execution
  Sync/PathUtilities.cs        path containment / name comparison helpers
tests/FolderSync.Tests/        xUnit tests (synchronizer, options, comparer, logger, runner)
```

## Tests

```bash
dotnet test
```

The tests create temporary folders under the system temp directory and cover: copying nested trees,
detecting same-size content changes, deleting stale files/folders, replacing a file with a folder and
vice versa, read-only and hidden files, timestamp preservation, error isolation, cancellation, argument
parsing and validation, logger output format, and the periodic runner.
