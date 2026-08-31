using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Memory;
using Game.Sim.Time;

namespace Game.Sim.Player;

public sealed record PlayerJournalEntry(
    MemoryId Id,
    MemoryKind Kind,
    EntityId? Subject,
    EventType EventType,
    LocationId? Location,
    SimTime EventTime,
    float Confidence,
    EntityId? InformationSource,
    EventId RootEventId,
    string Summary);
