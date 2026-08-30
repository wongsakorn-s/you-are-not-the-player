using Game.Sim.Entities;
using Game.Sim.Locations;

namespace Game.Sim.Behaviors;

public sealed class SuspicionBehaviorProfile
{
    public SuspicionBehaviorProfile(
        EntityId entity,
        IEnumerable<EntityId> contacts,
        LocationId safeLocation)
    {
        if (entity.IsEmpty)
        {
            throw new ArgumentException("Behavior entity cannot be empty.", nameof(entity));
        }

        ArgumentNullException.ThrowIfNull(contacts);
        EntityId[] materializedContacts = contacts
            .Distinct()
            .OrderBy(contact => contact.Value, StringComparer.Ordinal)
            .ToArray();
        if (materializedContacts.Any(contact => contact.IsEmpty || contact == entity))
        {
            throw new ArgumentException(
                "Behavior contacts must be valid entities other than the profile owner.",
                nameof(contacts));
        }

        if (safeLocation.IsEmpty)
        {
            throw new ArgumentException("Safe location cannot be empty.", nameof(safeLocation));
        }

        Entity = entity;
        Contacts = Array.AsReadOnly(materializedContacts);
        SafeLocation = safeLocation;
    }

    public EntityId Entity { get; }

    public IReadOnlyList<EntityId> Contacts { get; }

    public LocationId SafeLocation { get; }
}
