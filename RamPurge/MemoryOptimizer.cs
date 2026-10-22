namespace RamPurge;

internal static class MemoryOptimizer
{
    private const string _volumePathPrefix = @"\\.\";

    public static bool IsElevated => Environment.IsPrivilegedProcess;

    public static unsafe MEMORYSTATUSEX GetMemoryStatus()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)sizeof(MEMORYSTATUSEX) };
        if (!GlobalMemoryStatusEx(ref status))
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        return status;
    }

    public static void EmptySystemWorkingSets() => SetMemoryListCommand(SYSTEM_MEMORY_LIST_COMMAND.MemoryEmptyWorkingSets);

    public static TrimResult TrimProcessWorkingSets(IReadOnlySet<string> exclusions)
    {
        // without SeDebugPrivilege only the processes of the current user can be opened, which is still worth doing.
        _ = TryEnablePrivilege(SE_DEBUG_NAME);
        var trimmed = 0;
        var skipped = 0;
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (exclusions.Contains(process.ProcessName))
                {
                    skipped++;
                    continue;
                }

                using var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_SET_QUOTA, false, (uint)process.Id);
                if (!handle.IsInvalid && EmptyWorkingSet(handle))
                {
                    trimmed++;
                }
                else
                {
                    skipped++;
                }
            }
        }

        return new TrimResult(trimmed, skipped);
    }

    public static void FlushSystemFileCache()
    {
        EnablePrivilege(SE_INCREASE_QUOTA_NAME);

        // a minimum and a maximum of (SIZE_T)-1 is the documented request to flush the cache working set.
        if (!SetSystemFileCacheSize(nuint.MaxValue, nuint.MaxValue, 0))
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    public static void FlushModifiedPageList() => SetMemoryListCommand(SYSTEM_MEMORY_LIST_COMMAND.MemoryFlushModifiedList);

    public static void PurgeStandbyList(bool lowPriorityOnly) => SetMemoryListCommand(lowPriorityOnly
        ? SYSTEM_MEMORY_LIST_COMMAND.MemoryPurgeLowPriorityStandbyList
        : SYSTEM_MEMORY_LIST_COMMAND.MemoryPurgeStandbyList);

    public static unsafe ulong CombinePages()
    {
        EnablePrivilege(SE_PROF_SINGLE_PROCESS_NAME);
        var information = new MEMORY_COMBINE_INFORMATION_EX();
        SetSystemInformation(SYSTEM_INFORMATION_CLASS.SystemCombinePhysicalMemoryInformation, &information, (uint)sizeof(MEMORY_COMBINE_INFORMATION_EX));
        return information.PagesCombined;
    }

    public static unsafe void ReconcileRegistry() => SetSystemInformation(SYSTEM_INFORMATION_CLASS.SystemRegistryReconciliationInformation, null, 0);

    public static int FlushVolumes()
    {
        var count = 0;
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                continue;

            // flushing a volume handle writes out every modified cached page of that volume, it needs write access.
            using var handle = File.OpenHandle(_volumePathPrefix + drive.Name.TrimEnd(Path.DirectorySeparatorChar), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            if (!FlushFileBuffers(handle))
                throw new Win32Exception(Marshal.GetLastPInvokeError());

            count++;
        }

        return count;
    }

    public static bool TryEnablePrivilege(string name)
    {
        if (!LookupPrivilegeValue(null, name, out var luid))
            return false;

        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var token))
            return false;

        try
        {
            var privileges = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privileges = new LUID_AND_ATTRIBUTES { Luid = luid, Attributes = SE_PRIVILEGE_ENABLED },
            };

            // AdjustTokenPrivileges succeeds even when the token does not hold the privilege, only the last error tells.
            return AdjustTokenPrivileges(token, false, privileges, 0, 0, 0) && Marshal.GetLastPInvokeError() != ERROR_NOT_ALL_ASSIGNED;
        }
        finally
        {
            CloseHandle(token);
        }
    }

    private static void EnablePrivilege(string name)
    {
        if (!TryEnablePrivilege(name))
            throw new Win32Exception(ERROR_PRIVILEGE_NOT_HELD, string.Format(Resources.PrivilegeNotHeld, name));
    }

    private static unsafe void SetMemoryListCommand(SYSTEM_MEMORY_LIST_COMMAND command)
    {
        EnablePrivilege(SE_PROF_SINGLE_PROCESS_NAME);
        SetSystemInformation(SYSTEM_INFORMATION_CLASS.SystemMemoryListInformation, &command, sizeof(SYSTEM_MEMORY_LIST_COMMAND));
    }

    private static unsafe void SetSystemInformation(SYSTEM_INFORMATION_CLASS informationClass, void* information, uint length)
    {
        var status = NtSetSystemInformation(informationClass, information, length);
        if (status != STATUS_SUCCESS)
            throw new Win32Exception((int)RtlNtStatusToDosError(status));
    }
}
