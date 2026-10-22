namespace RamPurge;

// declared in the order the areas run, each one feeds the pages it releases to the next.
[Flags]
public enum MemoryAreas
{
    None = 0x0,
    WorkingSet = 0x1,
    SystemFileCache = 0x2,
    ModifiedPageList = 0x4,
    StandbyList = 0x8,
    LowPriorityStandbyList = 0x10,
    CombinedPageList = 0x20,
    RegistryCache = 0x40,
    VolumeCache = 0x80,
    All = WorkingSet | SystemFileCache | ModifiedPageList | StandbyList | CombinedPageList | RegistryCache | VolumeCache,
}
