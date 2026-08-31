using Game.Sim.Entities;
using Game.Sim.Locations;

namespace Game.Sim.Actions;

public interface ILocationAccessPolicy
{
    bool CanTraverse(EntityId actor, LocationConnection connection);
}
