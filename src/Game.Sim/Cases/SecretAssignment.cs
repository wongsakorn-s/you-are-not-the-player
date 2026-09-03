using Game.Sim.Entities;
using Game.Sim.Secrets;

namespace Game.Sim.Cases;

/// <summary>
/// One NPC's private reason to behave oddly. Secrets exist to produce false
/// positives: a thief and a Completionist both look wrong, and the player has to
/// tell them apart from evidence rather than from a flag.
/// </summary>
public sealed record SecretAssignment
{
    public SecretAssignment(
        EntityId owner,
        SecretBehaviorKind behavior,
        EntityId? accomplice = null)
    {
        if (owner.IsEmpty)
        {
            throw new ArgumentException("Secret owner cannot be empty.", nameof(owner));
        }

        if (!Enum.IsDefined(behavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(behavior),
                behavior,
                "Unknown secret behavior.");
        }

        bool needsAccomplice = behavior == SecretBehaviorKind.SecretMeeting;
        if (needsAccomplice && (accomplice is null || accomplice.Value.IsEmpty))
        {
            throw new ArgumentException(
                $"Behavior '{behavior}' requires an accomplice.",
                nameof(accomplice));
        }

        if (!needsAccomplice && accomplice is not null)
        {
            throw new ArgumentException(
                $"Behavior '{behavior}' does not take an accomplice.",
                nameof(accomplice));
        }

        if (accomplice == owner)
        {
            throw new ArgumentException(
                "An accomplice cannot be the secret owner.",
                nameof(accomplice));
        }

        Owner = owner;
        Behavior = behavior;
        Accomplice = accomplice;
    }

    public EntityId Owner { get; }

    public SecretBehaviorKind Behavior { get; }

    public EntityId? Accomplice { get; }
}
