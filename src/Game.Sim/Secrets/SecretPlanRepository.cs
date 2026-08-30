using Game.Sim.Entities;
using Game.Sim.Time;

namespace Game.Sim.Secrets;

public sealed class SecretPlanRepository
{
    private readonly Dictionary<string, SecretPlan> _plansById;

    public SecretPlanRepository(IEnumerable<SecretPlan> plans)
    {
        ArgumentNullException.ThrowIfNull(plans);
        SecretPlan[] suppliedPlans = plans.ToArray();
        if (suppliedPlans.Any(plan => plan is null))
        {
            throw new ArgumentException("Secret plans cannot contain null values.", nameof(plans));
        }

        SecretPlan[] materializedPlans = suppliedPlans
            .OrderBy(plan => plan.Id, StringComparer.Ordinal)
            .ToArray();

        if (materializedPlans.Select(plan => plan.Id).Distinct(StringComparer.Ordinal).Count() !=
            materializedPlans.Length)
        {
            throw new ArgumentException("Secret plan IDs must be unique.", nameof(plans));
        }

        Plans = Array.AsReadOnly(materializedPlans);
        _plansById = materializedPlans.ToDictionary(plan => plan.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<SecretPlan> Plans { get; }

    public IReadOnlyList<SecretPlan> GetActivePlans(EntityId participant, SimMinuteOfDay time) =>
        Plans
            .Where(plan => plan.Participants.Contains(participant) && plan.IsActive(time))
            .ToArray();

    public bool TryGet(string id, out SecretPlan? plan) => _plansById.TryGetValue(id, out plan);
}
