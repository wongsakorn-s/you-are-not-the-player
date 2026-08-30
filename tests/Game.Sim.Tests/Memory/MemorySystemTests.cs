using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Perception;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Tests.Memory;

public sealed class MemorySystemTests
{
    private static readonly EntityId Anna = new("anna");
    private static readonly EntityId Bob = new("bob");
    private static readonly EntityId Charlie = new("charlie");
    private static readonly EntityId George = new("george");
    private static readonly LocationId Basement = new("basement");

    [Fact]
    public void Remember_CreatesEpisodicMemoryFromObservation()
    {
        MemorySystem system = CreateSystem();
        Observation observation = CreateObservation(Anna, sourceEvent: 9, time: 12, confidence: 0.8f);

        MemoryRecord memory = Assert.IsType<MemoryRecord>(system.Remember(observation));

        Assert.Equal(new MemoryId(1), memory.Id);
        Assert.Equal(MemoryKind.Episodic, memory.Kind);
        Assert.Equal(George, memory.Subject);
        Assert.Equal(new EventId(9), memory.RootEventId);
        Assert.Equal(observation.Id, memory.SourceObservationId);
        Assert.Null(memory.InformationSource);
        Assert.Equal(new SimTime(12), memory.EventTime);
        Assert.Equal(memory.EventTime, memory.CreatedAt);
    }

    [Fact]
    public void ShareMemory_CreatesSocialMemoryAndPreservesRootLineage()
    {
        MemorySystem system = CreateSystem(
            new ExponentialMemoryDecayPolicy(episodicDecayRatePerTick: 0.1, socialDecayRatePerTick: 0.2));
        MemoryRecord source = Assert.IsType<MemoryRecord>(
            system.Remember(CreateObservation(Anna, sourceEvent: 9, time: 10, confidence: 0.8f)));

        MemoryRecord social = Assert.IsType<MemoryRecord>(system.ShareMemory(
            Anna,
            Bob,
            source.Id,
            sharedAt: new SimTime(12),
            transmissionConfidence: 0.5f));

        float expectedConfidence = 0.8f * MathF.Exp(-0.1f * 2) * 0.5f;
        Assert.Equal(MemoryKind.Social, social.Kind);
        Assert.Equal(source.RootEventId, social.RootEventId);
        Assert.Equal(source.Id, social.SourceMemoryId);
        Assert.Equal(Anna, social.InformationSource);
        Assert.Equal(source.EventTime, social.EventTime);
        Assert.Equal(new SimTime(12), social.CreatedAt);
        Assert.Equal(expectedConfidence, social.InitialConfidence, precision: 6);
    }

    [Fact]
    public void ShareMemory_PreservesRootAcrossMultipleHopsAndStopsRumorLoop()
    {
        MemorySystem system = CreateSystem();
        MemoryRecord annaMemory = Assert.IsType<MemoryRecord>(
            system.Remember(CreateObservation(Anna, sourceEvent: 9, time: 10)));
        MemoryRecord bobMemory = Assert.IsType<MemoryRecord>(system.ShareMemory(
            Anna,
            Bob,
            annaMemory.Id,
            new SimTime(11),
            transmissionConfidence: 1.0f));
        MemoryRecord charlieMemory = Assert.IsType<MemoryRecord>(system.ShareMemory(
            Bob,
            Charlie,
            bobMemory.Id,
            new SimTime(12),
            transmissionConfidence: 1.0f));

        MemoryRecord? loopedMemory = system.ShareMemory(
            Charlie,
            Anna,
            charlieMemory.Id,
            new SimTime(13),
            transmissionConfidence: 1.0f);
        MemoryRecord nextMemory = Assert.IsType<MemoryRecord>(
            system.Remember(CreateObservation(Bob, sourceEvent: 10, time: 13)));

        Assert.Null(loopedMemory);
        Assert.Single(system.GetStore(Anna).Memories);
        Assert.Equal(annaMemory.RootEventId, bobMemory.RootEventId);
        Assert.Equal(annaMemory.RootEventId, charlieMemory.RootEventId);
        Assert.Equal(bobMemory.Id, charlieMemory.SourceMemoryId);
        Assert.Equal(new MemoryId(4), nextMemory.Id);
    }

    [Fact]
    public void Remember_IgnoresDuplicateObservationOfSameRootEvent()
    {
        MemorySystem system = CreateSystem();
        Observation first = CreateObservation(Anna, sourceEvent: 9, time: 10);
        Observation duplicate = CreateObservation(Anna, sourceEvent: 9, time: 11);

        MemoryRecord original = Assert.IsType<MemoryRecord>(system.Remember(first));
        MemoryRecord? ignored = system.Remember(duplicate);

        Assert.Null(ignored);
        Assert.Equal([original], system.GetStore(Anna).Memories);
    }

    [Fact]
    public void GetRetainedConfidence_UsesMemoryKindDecayRate()
    {
        MemorySystem system = CreateSystem(
            new ExponentialMemoryDecayPolicy(episodicDecayRatePerTick: 0.1, socialDecayRatePerTick: 0.2));
        MemoryRecord memory = Assert.IsType<MemoryRecord>(
            system.Remember(CreateObservation(Anna, sourceEvent: 9, time: 10, confidence: 1.0f)));

        float retained = system.GetRetainedConfidence(Anna, memory.Id, new SimTime(15));

        Assert.Equal(MathF.Exp(-0.5f), retained, precision: 6);
    }

    [Fact]
    public void GetRetainedConfidence_RejectsTimeBeforeMemoryCreation()
    {
        MemorySystem system = CreateSystem();
        MemoryRecord memory = Assert.IsType<MemoryRecord>(
            system.Remember(CreateObservation(Anna, sourceEvent: 9, time: 10)));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => system.GetRetainedConfidence(Anna, memory.Id, new SimTime(9)));
    }

    [Fact]
    public void GetRetainedConfidence_UsesSocialDecayRateAfterSharing()
    {
        MemorySystem system = CreateSystem(
            new ExponentialMemoryDecayPolicy(episodicDecayRatePerTick: 0.0, socialDecayRatePerTick: 0.2));
        MemoryRecord source = Assert.IsType<MemoryRecord>(
            system.Remember(CreateObservation(Anna, sourceEvent: 9, time: 10, confidence: 1.0f)));
        MemoryRecord social = Assert.IsType<MemoryRecord>(system.ShareMemory(
            Anna,
            Bob,
            source.Id,
            new SimTime(12),
            transmissionConfidence: 1.0f));

        float retained = system.GetRetainedConfidence(Bob, social.Id, new SimTime(14));

        Assert.Equal(MathF.Exp(-0.4f), retained, precision: 6);
    }

    private static MemorySystem CreateSystem(IMemoryDecayPolicy? decayPolicy = null)
    {
        var world = new WorldState();
        world.AddLocation(new LocationState(Basement));
        world.AddEntity(new EntityState(Anna, Basement));
        world.AddEntity(new EntityState(Bob, Basement));
        world.AddEntity(new EntityState(Charlie, Basement));
        world.AddEntity(new EntityState(George, Basement));
        return new MemorySystem(
            world,
            new SequentialMemoryIdGenerator(),
            decayPolicy ?? new ExponentialMemoryDecayPolicy(0.0, 0.0));
    }

    private static Observation CreateObservation(
        EntityId observer,
        long sourceEvent,
        long time,
        float confidence = 0.9f) =>
        new(
            new ObservationId(sourceEvent),
            new EventId(sourceEvent),
            observer,
            George,
            EventType.EnterLocation,
            Basement,
            new SimTime(time),
            confidence,
            salience: 0.5f,
            PerceptionChannel.Visual);
}
