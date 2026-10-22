namespace RamPurge.Interop;

internal struct TOKEN_PRIVILEGES
{
    public uint PrivilegeCount;

    // declared as an ANYSIZE_ARRAY, one entry is all this program ever passes.
    public LUID_AND_ATTRIBUTES Privileges;
}