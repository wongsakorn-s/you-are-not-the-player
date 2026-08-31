using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Routines;
using Game.Sim.Suspicion;
using Game.Sim.Time;

namespace Game.Sim.Scenarios;

public sealed class BasementScenarioResult
{
    public BasementScenarioResult(
        ulong seed,
        SimTime completedAt,
        IEnumerable<WorldEvent> events,
        IEnumerable<NpcRoutineDecision> decisions,
        IEnumerable<OwnedMemory> memories,
        IEnumerable<ScenarioActorSnapshot> actors,
        int observationCount,
        WorldEvent restrictedEntry,
        MemoryRecord annaMemory,
        MemoryRecord bobRumor,
        SuspicionSnapshot annaSuspicion,
        SuspicionSnapshot bobSuspicion,
        SimTime annaFirstSuspicionAt,
        SimTime bobFirstSuspicionAt,
        NpcRoutineDecision annaInitialDecision,
        NpcRoutineDecision bobInitialDecision,
        LocationId annaFinalLocation,
        LocationId bobFinalLocation,
        LocationId georgeFinalLocation)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(memories);
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(restrictedEntry);
        ArgumentNullException.ThrowIfNull(annaMemory);
        ArgumentNullException.ThrowIfNull(bobRumor);
        ArgumentNullException.ThrowIfNull(annaSuspicion);
        ArgumentNullException.ThrowIfNull(bobSuspicion);
        ArgumentNullException.ThrowIfNull(annaInitialDecision);
        ArgumentNullException.ThrowIfNull(bobInitialDecision);
        ArgumentOutOfRangeException.ThrowIfNegative(observationCount);

        Seed = seed;
        CompletedAt = completedAt;
        Events = Array.AsReadOnly(events.ToArray());
        Decisions = Array.AsReadOnly(decisions.ToArray());
        Memories = Array.AsReadOnly(memories.ToArray());
        Actors = Array.AsReadOnly(actors.ToArray());
        ObservationCount = observationCount;
        RestrictedEntry = restrictedEntry;
        AnnaMemory = annaMemory;
        BobRumor = bobRumor;
        AnnaSuspicion = annaSuspicion;
        BobSuspicion = bobSuspicion;
        AnnaFirstSuspicionAt = annaFirstSuspicionAt;
        BobFirstSuspicionAt = bobFirstSuspicionAt;
        AnnaInitialDecision = annaInitialDecision;
        BobInitialDecision = bobInitialDecision;
        AnnaFinalLocation = annaFinalLocation;
        BobFinalLocation = bobFinalLocation;
        GeorgeFinalLocation = georgeFinalLocation;
    }

    public ulong Seed { get; }

    public SimTime CompletedAt { get; }

    public IReadOnlyList<WorldEvent> Events { get; }

    public IReadOnlyList<NpcRoutineDecision> Decisions { get; }

    public IReadOnlyList<OwnedMemory> Memories { get; }

    public IReadOnlyList<ScenarioActorSnapshot> Actors { get; }

    public int ObservationCount { get; }

    public WorldEvent RestrictedEntry { get; }

    public MemoryRecord AnnaMemory { get; }

    public MemoryRecord BobRumor { get; }

    public SuspicionSnapshot AnnaSuspicion { get; }

    public SuspicionSnapshot BobSuspicion { get; }

    public SimTime AnnaFirstSuspicionAt { get; }

    public SimTime BobFirstSuspicionAt { get; }

    public NpcRoutineDecision AnnaInitialDecision { get; }

    public NpcRoutineDecision BobInitialDecision { get; }

    public LocationId AnnaFinalLocation { get; }

    public LocationId BobFinalLocation { get; }

    public LocationId GeorgeFinalLocation { get; }
}
