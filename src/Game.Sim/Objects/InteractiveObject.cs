using Game.Sim.Locations;

namespace Game.Sim.Objects;

public sealed class InteractiveObject
{
    public InteractiveObject(
        string id,
        LocationId location,
        string displayName,
        InteractiveObjectKind kind,
        bool isLocked = false,
        string? requiredKeyId = null,
        string? clueDescription = null,
        bool isSuspiciousToTamper = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (location.IsEmpty)
        {
            throw new ArgumentException("Object location cannot be empty.", nameof(location));
        }

        Id = id.Trim();
        Location = location;
        DisplayName = displayName.Trim();
        Kind = kind;
        IsLocked = isLocked;
        RequiredKeyId = string.IsNullOrWhiteSpace(requiredKeyId) ? null : requiredKeyId.Trim();
        ClueDescription = string.IsNullOrWhiteSpace(clueDescription) ? null : clueDescription.Trim();
        IsSuspiciousToTamper = isSuspiciousToTamper;
    }

    public string Id { get; }

    public LocationId Location { get; }

    public string DisplayName { get; }

    public InteractiveObjectKind Kind { get; }

    public bool IsLocked { get; private set; }

    public string? RequiredKeyId { get; }

    public string? ClueDescription { get; }

    public bool IsSuspiciousToTamper { get; }

    public bool IsTampered { get; private set; }

    public bool Unlock(string? providedKeyId = null)
    {
        if (!IsLocked)
        {
            return true;
        }

        if (RequiredKeyId is null || string.Equals(RequiredKeyId, providedKeyId, StringComparison.OrdinalIgnoreCase))
        {
            IsLocked = false;
            return true;
        }

        return false;
    }

    public void MarkTampered() => IsTampered = true;

    internal void RestoreState(bool isLocked, bool isTampered)
    {
        IsLocked = isLocked;
        IsTampered = isTampered;
    }
}
