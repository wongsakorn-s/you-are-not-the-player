namespace Game.Sim.Snapshots;

public sealed record EntityMemoryStoreSnapshot(
    string Owner,
    IReadOnlyList<MemoryRecordSnapshot> Memories);
