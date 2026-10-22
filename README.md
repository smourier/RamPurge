# <img src="RamPurge/RamPurge.png" alt="RamPurge icon" width="128" align="middle"> RamPurge

A small Windows console tool that frees physical memory.
It is written in C# for .NET 10 and publishes as a single native AOT executable.

## Usage

```
RamPurge [area ...] [/exclude:name,name]
```

With no area, every area runs except `LowPriorityStandbyList`, which `StandbyList` already covers.
Areas run in the order below, since each one hands the pages it releases to the next.

| Area | What it does | Windows call | Privilege |
|---|---|---|---|
| `WorkingSet` | Empties the working set of every process. | `NtSetSystemInformation(SystemMemoryListInformation, MemoryEmptyWorkingSets)`, or `EmptyWorkingSet` per process | `SeProfileSingleProcessPrivilege` |
| `SystemFileCache` | Flushes the working set of the system file cache. | `SetSystemFileCacheSize(-1, -1, 0)` | `SeIncreaseQuotaPrivilege` |
| `ModifiedPageList` | Writes modified pages out and moves them to the standby list. | `MemoryFlushModifiedList` | `SeProfileSingleProcessPrivilege` |
| `StandbyList` | Purges the standby list. | `MemoryPurgeStandbyList` | `SeProfileSingleProcessPrivilege` |
| `LowPriorityStandbyList` | Purges only the lowest priority standby pages. | `MemoryPurgeLowPriorityStandbyList` | `SeProfileSingleProcessPrivilege` |
| `CombinedPageList` | Combines identical physical pages. | `NtSetSystemInformation(SystemCombinePhysicalMemoryInformation)` | `SeProfileSingleProcessPrivilege` |
| `RegistryCache` | Writes modified registry hive data out. | `NtSetSystemInformation(SystemRegistryReconciliationInformation)` | none |
| `VolumeCache` | Flushes the cached writes of every fixed volume. | `FlushFileBuffers` on `\\.\X:` | administrator |

`/exclude:name,name` trims working sets one process at a time and leaves the named processes alone.
Names are process names, with or without `.exe`. The option can be repeated, and `--exclude name,name` is the same.
Area names are not case sensitive, and `RamPurge -h` prints the help.

Examples:
* `RamPurge` runs every area.
* `RamPurge StandbyList` purges the standby list only.
* `RamPurge WorkingSet /exclude:explorer,devenv` trims every working set except those of Explorer and Visual Studio.

The exit code is 0 when every area succeeded, 1 otherwise.

## Elevation

Most areas need privileges that only an elevated administrator token holds, so run RamPurge from an elevated prompt.
It runs as invoker and never asks for elevation itself. An area that lacks its privilege reports it, and the others go on.

Without elevation, `WorkingSet` falls back to trimming each process it can open, which means the processes of the current user,
and `RegistryCache` works as well.

## What to expect

Purging moves pages to the free list, so the available memory figure goes up right away.
Those pages were cache, though. Processes fault their working sets back in, and file data that was in the standby list is read from disk again.
RamPurge is useful to measure cold cache behavior or to work around a driver that fills the standby list,
it does not make a healthy system faster.
