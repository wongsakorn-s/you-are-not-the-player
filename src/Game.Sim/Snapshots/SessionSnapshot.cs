namespace Game.Sim.Snapshots;

public sealed record SessionSnapshot(
    SnapshotMetadata Metadata,
    IReadOnlyList<EntityStateSnapshot> Entities,
    IReadOnlyList<WorldEventSnapshot> Events,
    IReadOnlyList<EntityMemoryStoreSnapshot> Memories,
    IReadOnlyList<SuspicionCaseSnapshot> Suspicions,
    IReadOnlyList<MovementRequestSnapshot> PendingMovements);
