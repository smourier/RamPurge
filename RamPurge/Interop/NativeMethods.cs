namespace RamPurge.Interop;

internal static partial class NativeMethods
{
    public const string SE_DEBUG_NAME = "SeDebugPrivilege";
    public const string SE_INCREASE_QUOTA_NAME = "SeIncreaseQuotaPrivilege";
    public const string SE_PROF_SINGLE_PROCESS_NAME = "SeProfileSingleProcessPrivilege";

    public const uint SE_PRIVILEGE_ENABLED = 0x2;
    public const uint TOKEN_QUERY = 0x8;
    public const uint TOKEN_ADJUST_PRIVILEGES = 0x20;
    public const uint PROCESS_SET_QUOTA = 0x100;
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    public const int STATUS_SUCCESS = 0;
    public const int ERROR_NOT_ALL_ASSIGNED = 1300;
    public const int ERROR_PRIVILEGE_NOT_HELD = 1314;

    [LibraryImport("ntdll")]
    public static unsafe partial int NtSetSystemInformation(SYSTEM_INFORMATION_CLASS SystemInformationClass, void* SystemInformation, uint SystemInformationLength);

    [LibraryImport("ntdll")]
    public static partial uint RtlNtStatusToDosError(int Status);

    [LibraryImport("advapi32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenProcessToken(nint ProcessHandle, uint DesiredAccess, out nint TokenHandle);

    [LibraryImport("advapi32", EntryPoint = "LookupPrivilegeValueW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

    [LibraryImport("advapi32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AdjustTokenPrivileges(
        nint TokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges,
        in TOKEN_PRIVILEGES NewState,
        uint BufferLength,
        nint PreviousState,
        nint ReturnLength);

    [LibraryImport("kernel32")]
    public static partial nint GetCurrentProcess();

    [LibraryImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint hObject);

    [LibraryImport("kernel32", SetLastError = true)]
    public static partial SafeProcessHandle OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [LibraryImport("kernel32", EntryPoint = "K32EmptyWorkingSet", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EmptyWorkingSet(SafeProcessHandle hProcess);

    [LibraryImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetSystemFileCacheSize(nuint MinimumFileCacheSize, nuint MaximumFileCacheSize, uint Flags);

    [LibraryImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [LibraryImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool FlushFileBuffers(SafeFileHandle hFile);
}