namespace Game.Sim.Events;

public sealed class WorldEventBuffer : IWorldEventBuffer
{
    private readonly List<WorldEvent> _pending = [];

    public int Count => _pending.Count;

    public void Publish(WorldEvent worldEvent)
    {
        ArgumentNullException.ThrowIfNull(worldEvent);
        _pending.Add(worldEvent);
    }

    public void PublishBatch(IReadOnlyCollection<WorldEvent> worldEvents)
    {
        ArgumentNullException.ThrowIfNull(worldEvents);

        if (worldEvents.Any(worldEvent => worldEvent is null))
        {
            throw new ArgumentException("An event batch cannot contain null values.", nameof(worldEvents));
        }

        _pending.AddRange(worldEvents);
    }

    public IReadOnlyList<WorldEvent> Drain()
    {
        if (_pending.Count == 0)
        {
            return [];
        }

        WorldEvent[] drained = [.. _pending];
        _pending.Clear();
        return drained;
    }
}
