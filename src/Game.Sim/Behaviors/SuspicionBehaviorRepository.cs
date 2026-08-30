using Game.Sim.Entities;

namespace Game.Sim.Behaviors;

public sealed class SuspicionBehaviorRepository
{
    private readonly Dictionary<EntityId, SuspicionBehaviorProfile> _profiles;

    public SuspicionBehaviorRepository(IEnumerable<SuspicionBehaviorProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        SuspicionBehaviorProfile[] suppliedProfiles = profiles.ToArray();
        if (suppliedProfiles.Any(profile => profile is null))
        {
            throw new ArgumentException(
                "Suspicion behavior profiles cannot contain null values.",
                nameof(profiles));
        }

        if (suppliedProfiles.Select(profile => profile.Entity).Distinct().Count() !=
            suppliedProfiles.Length)
        {
            throw new ArgumentException(
                "Each entity can have only one suspicion behavior profile.",
                nameof(profiles));
        }

        _profiles = suppliedProfiles.ToDictionary(profile => profile.Entity);
    }

    public bool TryGet(EntityId entity, out SuspicionBehaviorProfile? profile) =>
        _profiles.TryGetValue(entity, out profile);
}
