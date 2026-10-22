namespace RamPurge.Interop;

internal struct MEMORY_COMBINE_INFORMATION_EX
{
    public nint Handle;
    public nuint PagesCombined;
    public uint Flags;
}