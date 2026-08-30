using Game.Sim.Entities;
using Game.Sim.Perception;
using Game.Sim.Time;
using Game.Sim.World;

namespace Game.Sim.Memory;

public sealed class MemorySystem
{
    private readonly WorldState _world;
    private readonly IMemoryIdGenerator _ids;
    private readonly IMemoryDecayPolicy _decayPolicy;
    private readonly Dictionary<EntityId, MemoryStore> _stores = [];

    public MemorySystem(
        WorldState world,
        IMemoryIdGenerator ids,
        IMemoryDecayPolicy decayPolicy)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(decayPolicy);
        _world = world;
        _ids = ids;
        _decayPolicy = decayPolicy;
    }

    public MemoryStore GetStore(EntityId owner)
    {
        _ = _world.GetEntity(owner);

        if (!_stores.TryGetValue(owner, out MemoryStore? store))
        {
            store = new MemoryStore(owner);
            _stores.Add(owner, store);
        }

        return store;
    }

    public MemoryRecord? Remember(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        MemoryStore store = GetStore(observation.Observer);

        if (observation.PerceivedActor is EntityId subject)
        {
            _ = _world.GetEntity(subject);
        }

        if (store.KnowsRootEvent(observation.SourceEvent))
        {
            return null;
        }

        MemoryRecord memory = MemoryRecord.FromObservation(_ids.NextId(), observation);
        store.Add(memory);
        return memory;
    }

    public MemoryRecord? ShareMemory(
        EntityId informationSource,
        EntityId recipient,
        MemoryId sourceMemoryId,
        SimTime sharedAt,
        float transmissionConfidence)
    {
        if (informationSource == recipient)
        {
            throw new ArgumentException("An entity cannot share a memory with itself.", nameof(recipient));
        }

        ValidateUnitInterval(transmissionConfidence, nameof(transmissionConfidence));
        MemoryStore sourceStore = GetStore(informationSource);
        MemoryRecord sourceMemory = sourceStore.GetMemory(sourceMemoryId);
        MemoryStore recipientStore = GetStore(recipient);

        if (recipientStore.KnowsRootEvent(sourceMemory.RootEventId))
        {
            return null;
        }

        float retainedConfidence = _decayPolicy.CalculateRetainedConfidence(sourceMemory, sharedAt);
        float receivedConfidence = retainedConfidence * transmissionConfidence;
        MemoryRecord socialMemory = MemoryRecord.FromSharedMemory(
            _ids.NextId(),
            sourceMemory,
            informationSource,
            sharedAt,
            receivedConfidence);
        recipientStore.Add(socialMemory);
        return socialMemory;
    }

    public float GetRetainedConfidence(EntityId owner, MemoryId memoryId, SimTime now) =>
        _decayPolicy.CalculateRetainedConfidence(GetStore(owner).GetMemory(memoryId), now);

    private static void ValidateUnitInterval(float value, string parameterName)
    {
        if (float.IsNaN(value) || value is < 0.0f or > 1.0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Value must be between 0 and 1 inclusive.");
        }
    }
}
