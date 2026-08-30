using Game.Sim.Events;

namespace Game.Sim.Logging;

public interface IWorldEventLogger
{
    void Write(WorldEvent worldEvent);

    void Flush();
}
