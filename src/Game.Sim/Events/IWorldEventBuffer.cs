namespace Game.Sim.Events;

public interface IWorldEventBuffer
{
    int Count { get; }

    void Publish(WorldEvent worldEvent);

    void PublishBatch(IReadOnlyCollection<WorldEvent> worldEvents);

    IReadOnlyList<WorldEvent> Drain();
}
