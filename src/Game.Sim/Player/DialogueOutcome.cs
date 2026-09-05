using Game.Sim.Events;
using Game.Sim.Memory;

namespace Game.Sim.Player;

public sealed record DialogueOutcome(
    bool Succeeded,
    string Text,
    MemoryRecord? TransferredMemory = null,
    WorldEvent? GeneratedEvent = null,
    string? FailureReason = null,
    AlibiClaim? Claim = null,
    ConfrontationResult Confrontation = ConfrontationResult.None);
