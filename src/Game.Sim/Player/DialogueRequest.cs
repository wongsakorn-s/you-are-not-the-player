using Game.Sim.Entities;
using Game.Sim.Memory;

namespace Game.Sim.Player;

public sealed class DialogueRequest
{
    public DialogueRequest(
        DialogueActionKind kind,
        EntityId requester,
        EntityId partner,
        EntityId? subject = null,
        MemoryId? memoryToShare = null,
        string? targetObjectId = null,
        MemoryId? confrontingMemoryId = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown dialogue action kind.");
        }

        if (requester.IsEmpty)
        {
            throw new ArgumentException("Requester cannot be empty.", nameof(requester));
        }

        if (partner.IsEmpty)
        {
            throw new ArgumentException("Partner cannot be empty.", nameof(partner));
        }

        if (requester == partner)
        {
            throw new ArgumentException("Requester and partner cannot be the same entity.", nameof(partner));
        }

        if (kind == DialogueActionKind.AskAboutSubject && (subject is null || subject.Value.IsEmpty))
        {
            throw new ArgumentException("Subject is required when asking about a target.", nameof(subject));
        }

        if (kind == DialogueActionKind.ShareRumor && (memoryToShare is null || memoryToShare.Value.IsEmpty))
        {
            throw new ArgumentException("Memory to share is required when sharing a rumor.", nameof(memoryToShare));
        }

        if (kind == DialogueActionKind.InquireAboutObject && string.IsNullOrWhiteSpace(targetObjectId))
        {
            throw new ArgumentException("Target object ID is required when inquiring about an object.", nameof(targetObjectId));
        }

        if (kind == DialogueActionKind.ConfrontEvidence && (confrontingMemoryId is null || confrontingMemoryId.Value.IsEmpty))
        {
            throw new ArgumentException("Confronting memory ID is required when confronting with evidence.", nameof(confrontingMemoryId));
        }

        Kind = kind;
        Requester = requester;
        Partner = partner;
        Subject = subject;
        MemoryToShare = memoryToShare;
        TargetObjectId = targetObjectId?.Trim();
        ConfrontingMemoryId = confrontingMemoryId;
    }

    public DialogueActionKind Kind { get; }

    public EntityId Requester { get; }

    public EntityId Partner { get; }

    public EntityId? Subject { get; }

    public MemoryId? MemoryToShare { get; }

    public string? TargetObjectId { get; }

    public MemoryId? ConfrontingMemoryId { get; }
}
