using Game.Sim.Entities;
using Game.Sim.Events;

namespace Game.Sim.Memory;

public sealed class MemoryStore
{
    private readonly Dictionary<MemoryId, MemoryRecord> _memories = [];
    private readonly Dictionary<EventId, MemoryId> _memoryByRootEvent = [];

    public MemoryStore(EntityId owner)
    {
        if (owner.IsEmpty)
        {
            throw new ArgumentException("Memory owner cannot be empty.", nameof(owner));
        }

        Owner = owner;
    }

    public EntityId Owner { get; }

    public IReadOnlyList<MemoryRecord> Memories => _memories.Values
        .OrderBy(memory => memory.Id.Value)
        .ToArray();

    public bool KnowsRootEvent(EventId rootEventId) => _memoryByRootEvent.ContainsKey(rootEventId);

    public MemoryRecord GetMemory(MemoryId id) =>
        _memories.TryGetValue(id, out MemoryRecord? memory)
            ? memory
            : throw new KeyNotFoundException($"Memory '{id}' does not exist for '{Owner}'.");

    internal void Add(MemoryRecord memory)
    {
        ArgumentNullException.ThrowIfNull(memory);

        if (_memoryByRootEvent.ContainsKey(memory.RootEventId))
        {
            throw new InvalidOperationException(
                $"'{Owner}' already has a memory rooted at event '{memory.RootEventId}'.");
        }

        if (!_memories.TryAdd(memory.Id, memory))
        {
            throw new InvalidOperationException($"Memory '{memory.Id}' already exists for '{Owner}'.");
        }

        _memoryByRootEvent.Add(memory.RootEventId, memory.Id);
    }
}
