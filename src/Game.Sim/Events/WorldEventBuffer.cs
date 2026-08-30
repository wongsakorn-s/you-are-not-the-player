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
