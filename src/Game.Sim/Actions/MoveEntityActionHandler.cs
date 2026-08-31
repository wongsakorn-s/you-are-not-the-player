using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.World;

namespace Game.Sim.Actions;

public sealed class MoveEntityActionHandler
{
    private static readonly EventTag[] MovementTags = [
        EventTag.Movement,
        EventTag.Visible,
    ];
    private static readonly EventTag[] RestrictedMovementTags = [
        EventTag.Movement,
        EventTag.Visible,
        EventTag.Restricted,
    ];

    private readonly WorldState _world;
    private readonly WorldEventFactory _events;
    private readonly IWorldEventBuffer _eventBuffer;

    public MoveEntityActionHandler(
        WorldState world,
        WorldEventFactory events,
        IWorldEventBuffer eventBuffer)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(eventBuffer);
        _world = world;
        _events = events;
        _eventBuffer = eventBuffer;
    }

    public bool Execute(MoveEntityCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        EntityState actor = _world.GetEntity(command.Actor);
        LocationState destinationState = _world.GetLocation(command.Destination);
        LocationId origin = actor.LogicalLocation;

        if (origin == command.Destination)
        {
            return false;
        }

        var payload = new LocationTransitionPayload(origin, command.Destination);
        EventTag[] leaveTags = _world.GetLocation(origin).IsRestricted
            ? RestrictedMovementTags
            : MovementTags;
        EventTag[] enterTags = destinationState.IsRestricted
            ? RestrictedMovementTags
            : MovementTags;
        WorldEvent leaveEvent = _events.Create(
            command.Actor,
            EventType.LeaveLocation,
            origin,
            tags: leaveTags,
            payload: payload);
        WorldEvent enterEvent = _events.Create(
            command.Actor,
            EventType.EnterLocation,
            command.Destination,
            tags: enterTags,
            payload: payload);

        _eventBuffer.PublishBatch([leaveEvent, enterEvent]);
        _world.RelocateEntity(command.Actor, command.Destination);
        return true;
    }
}
