namespace Game.Sim.Events;

public interface IWorldEventBuffer
{
    int Count { get; }

    void Publish(WorldEvent worldEvent);

    IReadOnlyList<WorldEvent> Drain();
}
