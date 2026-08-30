using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Perception;
using Game.Sim.Suspicion;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Tests.Suspicion;

public sealed class SuspicionSystemTests
{
    private static readonly EntityId Anna = new("anna");
    private static readonly EntityId George = new("george");
    private static readonly LocationId Basement = new("basement");

    [Fact]
    public void ProcessMemory_DerivesMultiDimensionalScoresFromEvidence()
    {
        (MemorySystem memories, SuspicionSystem suspicion) = CreateSystems(decayRate: 0.0);
        MemoryRecord memory = RememberRestrictedEntry(memories, confidence: 0.8f);

        int contributionsAdded = suspicion.ProcessMemory(Anna, memory);
        SuspicionSnapshot snapshot = suspicion.GetSnapshot(Anna, George, SimTime.Zero);

        Assert.Equal(2, contributionsAdded);
        Assert.Equal(16.0f, snapshot.Vector.RoleDeviation, precision: 5);
        Assert.Equal(6.4f, snapshot.Vector.Secrecy, precision: 5);
        Assert.Equal(0.0f, snapshot.Vector.Criminality);
        Assert.Equal(2, snapshot.Evidence.Count);
        Assert.All(
            snapshot.Evidence,
            evidence => Assert.Equal(memory.Id, evidence.Contribution.SourceMemory));
        Assert.All(
            snapshot.Evidence,
            evidence => Assert.Equal("restricted_area_entry", evidence.Contribution.RuleId));
    }

    [Fact]
    public void Snapshot_RecalculatesScoresFromDecayedMemoryConfidence()
    {
        (MemorySystem memories, SuspicionSystem suspicion) = CreateSystems(decayRate: 0.1);
        MemoryRecord memory = RememberRestrictedEntry(memories, confidence: 0.8f);
        _ = suspicion.ProcessMemory(Anna, memory);

        SuspicionSnapshot initial = suspicion.GetSnapshot(Anna, George, SimTime.Zero);
        SuspicionSnapshot later = suspicion.GetSnapshot(Anna, George, new SimTime(10));

        float retention = MathF.Exp(-1.0f);
        Assert.Equal(16.0f, initial.Vector.RoleDeviation, precision: 5);
        Assert.Equal(16.0f * retention, later.Vector.RoleDeviation, precision: 5);
        Assert.Equal(0.8f * retention, later.Evidence[1].RetainedConfidence, precision: 5);
    }

    [Fact]
    public void ProcessMemory_IsIdempotentForSameRuleAndMemory()
    {
        (MemorySystem memories, SuspicionSystem suspicion) = CreateSystems(decayRate: 0.0);
        MemoryRecord memory = RememberRestrictedEntry(memories, confidence: 1.0f);

        int firstCount = suspicion.ProcessMemory(Anna, memory);
        int secondCount = suspicion.ProcessMemory(Anna, memory);
        SuspicionSnapshot snapshot = suspicion.GetSnapshot(Anna, George, SimTime.Zero);

        Assert.Equal(2, firstCount);
        Assert.Equal(0, secondCount);
        Assert.Equal(2, snapshot.Evidence.Count);
    }

    [Fact]
    public void ProcessMemory_WithoutKnownSubjectCreatesNoEvidence()
    {
        (MemorySystem memories, SuspicionSystem suspicion) = CreateSystems(decayRate: 0.0);
        var observation = new Observation(
            new ObservationId(1),
            new EventId(1),
            Anna,
            perceivedActor: null,
            EventType.EnterLocation,
            Basement,
            [EventTag.Restricted],
            SimTime.Zero,
            confidence: 0.8f,
            salience: 0.9f,
            PerceptionChannel.Audio);
        MemoryRecord memory = Assert.IsType<MemoryRecord>(memories.Remember(observation));

        int added = suspicion.ProcessMemory(Anna, memory);

        Assert.Equal(0, added);
    }

    [Fact]
    public void RuleMatching_DoesNotCollapseCrimeIntoRoleDeviation()
    {
        var crimeRule = new SuspicionRule(
            "visible_crime",
            EventType.EnterLocation,
            [EventTag.Restricted],
            memoryKind: null,
            [new SuspicionEffect(SuspicionDimension.Criminality, 5.0f)]);
        (MemorySystem memories, SuspicionSystem suspicion) = CreateSystems(
            decayRate: 0.0,
            additionalRules: [crimeRule]);
        MemoryRecord memory = RememberRestrictedEntry(memories, confidence: 1.0f);

        _ = suspicion.ProcessMemory(Anna, memory);
        SuspicionVector vector = suspicion.GetSnapshot(Anna, George, SimTime.Zero).Vector;

        Assert.Equal(5.0f, vector.Criminality);
        Assert.Equal(20.0f, vector.RoleDeviation);
        Assert.Equal(8.0f, vector.Secrecy);
        Assert.Equal(0.0f, vector.MetaBehavior);
    }

    [Fact]
    public void SuspicionVector_RejectsInvalidScores()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SuspicionVector(0, 0, -1, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SuspicionVector(0, 0, 0, float.PositiveInfinity, 0, 0));
    }

    private static (MemorySystem Memories, SuspicionSystem Suspicion) CreateSystems(
        double decayRate,
        IReadOnlyCollection<SuspicionRule>? additionalRules = null)
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Basement));
        world.AddEntity(new EntityState(Anna, Basement));
        world.AddEntity(new EntityState(George, Basement));
        var decay = new ExponentialMemoryDecayPolicy(decayRate, decayRate);
        var memories = new MemorySystem(world, new SequentialMemoryIdGenerator(), decay);

        string rulesPath = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "SuspicionRules",
            "mvp.json");
        var parsedRules = JsonSuspicionRuleParser
            .Parse(File.ReadAllText(rulesPath))
            .Rules;
        SuspicionRule[] allRules = [.. parsedRules, .. additionalRules ?? []];
        var repository = new InMemorySuspicionRuleRepository(allRules);
        return (memories, new SuspicionSystem(memories, repository));
    }

    private static MemoryRecord RememberRestrictedEntry(
        MemorySystem memories,
        float confidence)
    {
        var observation = new Observation(
            new ObservationId(1),
            new EventId(1),
            Anna,
            George,
            EventType.EnterLocation,
            Basement,
            [EventTag.Visible, EventTag.Restricted],
            SimTime.Zero,
            confidence,
            salience: 0.9f,
            PerceptionChannel.Visual);
        return Assert.IsType<MemoryRecord>(memories.Remember(observation));
    }
}
