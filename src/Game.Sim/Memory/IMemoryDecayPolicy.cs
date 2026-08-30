using Game.Sim.Time;

namespace Game.Sim.Memory;

public interface IMemoryDecayPolicy
{
    float CalculateRetainedConfidence(MemoryRecord memory, SimTime now);
}
