using Game.Sim.Cases;
using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.PlayerAi;
using Game.Sim.Roles;
using Game.Sim.Scenarios;
using Game.Sim.Schedules;
using Game.Sim.Secrets;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Roles;

/// <summary>
/// False positives are the point. An ordinary character with something to hide
/// has to be able to look exactly as wrong as the Player does, or "acting
/// strangely means Player" is a rule that simply works.
/// </summary>
public sealed class HotelSecretStagingTests
{
    private static readonly EntityId[] Roster =
    [
        BasementScenario.Anna,
        BasementScenario.Bob,
        BasementScenario.George,
        BasementScenario.Charlie,
        BasementScenario.Dana,
        BasementScenario.Evelyn,
    ];

    private static readonly (EntityId Entity, RoleId Role)[] CastRoles =
    [
        (BasementScenario.George, HotelNightRoutines.Receptionist),
        (BasementScenario.Anna, HotelNightRoutines.Cleaner),
        (BasementScenario.Bob, HotelNightRoutines.Security),
        (BasementScenario.Charlie, HotelNightRoutines.Guest),
        (BasementScenario.Dana, HotelNightRoutines.Cook),
        (BasementScenario.Evelyn, HotelNightRoutines.Manager),
    ];

    [Fact]
    public void EverySecretIsStagedSomewhereItsOwnerCanActuallyGo()
    {
        // The bug this exists for: a manager was staged into a guest room she has
        // no permission to enter, so the goal was never taken, the event never
        // fired, and the secret quietly did not exist.
        for (ulong seed = 0; seed < 120; seed++)
        {
            SessionTruth truth = Generate(seed);
            foreach (SecretPlan plan in HotelSecretStaging.Stage(truth.Secrets, RoleOf).Plans)
            {
                if (plan.IgnoresRolePermissions)
                {
                    continue;
                }

                foreach (EntityId participant in plan.Participants)
                {
                    RoleId role = CastRoles.First(item => item.Entity == participant).Role;
                    Assert.True(
                        HotelNightRoutines.Permissions(role).CanEnter(plan.Location),
                        $"seed {seed}: {participant} ({role}) cannot reach {plan.Location} " +
                        $"for '{plan.Id}'.");
                }
            }
        }
    }

    [Fact]
    public void EverySecretFallsInsideTheNight()
    {
        // A plan that opens after dawn is a plan that never happens.
        for (ulong seed = 0; seed < 60; seed++)
        {
            foreach (SecretPlan plan in HotelSecretStaging.Stage(Generate(seed).Secrets, RoleOf).Plans)
            {
                int start = plan.Start.Value;
                Assert.True(
                    start >= 23 * 60 || start < 5 * 60,
                    $"seed {seed}: '{plan.Id}' starts at {plan.Start}, outside the shift.");
            }
        }
    }

    [Fact]
    public void SecretsNeverLandOnTheHostOrTheHiddenPlayer()
    {
        // Neither is driven by the NPC routine system, so a secret given to them
        // could never be acted on - and the hidden player must stand out through
        // Player-like behaviour alone.
        for (ulong seed = 0; seed < 120; seed++)
        {
            SessionTruth truth = Generate(seed);
            foreach (SecretAssignment secret in truth.Secrets)
            {
                Assert.NotEqual(truth.HumanHost, secret.Owner);
                Assert.NotEqual(truth.HiddenPlayer, secret.Owner);
                if (secret.Accomplice is { } accomplice)
                {
                    Assert.NotEqual(truth.HumanHost, accomplice);
                    Assert.NotEqual(truth.HiddenPlayer, accomplice);
                }
            }
        }
    }

    [Fact]
    public void AcrossSeedsTheHotelHidesMoreThanOneKindOfThing()
    {
        SecretBehaviorKind[] staged = Enumerable.Range(0, 120)
            .SelectMany(seed => Generate((ulong)seed).Secrets)
            .Select(secret => secret.Behavior)
            .Distinct()
            .ToArray();

        Assert.True(
            staged.Length >= 2,
            $"Only {staged.Length} kind(s) of secret ever appear: {string.Join(",", staged)}.");
    }

    [Fact]
    public void AStagedSecretActuallyHappensDuringTheNight()
    {
        // End to end: the seed hands out a secret, the staging gives it a time and
        // a place, the goal outranks the character's shift, and the world records
        // an event an onlooker could have seen.
        var seen = new HashSet<EventType>();
        for (ulong seed = 0; seed < 12 && seen.Count == 0; seed++)
        {
            BasementScenarioSession session = CreateSession(seed);
            if (session.SecretPlans.Count == 0)
            {
                continue;
            }

            for (int tick = 0; tick < 360 && !session.IsComplete; tick++)
            {
                _ = session.AdvanceOneTick();
            }

            foreach (WorldEvent worldEvent in session.Events.Where(worldEvent =>
                worldEvent.Type is EventType.Theft
                    or EventType.SecretMeeting
                    or EventType.NightActivity))
            {
                _ = seen.Add(worldEvent.Type);
            }
        }

        Assert.NotEmpty(seen);
    }

    [Fact]
    public void ASecretIsWorthLeavingYourPostFor()
    {
        // If the shift outranked the secret nobody would ever slip away, and the
        // whole subsystem would be inert while looking wired up.
        Assert.True(HotelSecretStaging.SecretUtility > 38.0f);
    }

    private static RoleId RoleOf(EntityId entity) =>
        CastRoles.First(item => item.Entity == entity).Role;

    private static SessionTruth Generate(ulong seed) => CaseGenerator.Generate(
        seed,
        new CaseGenerationOptions(
            BasementScenario.George,
            Roster,
            shiftTicks: 360,
            pinnedIncidentCulprit: BasementScenario.George));

    private static BasementScenarioSession CreateSession(ulong seed)
    {
        InMemorySuspicionRuleRepository rules = JsonSuspicionRuleParser.Parse(
            File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "SuspicionRules",
                "mvp.json")));
        return new BasementScenario(rules).CreateSession(
            new BasementScenarioOptions(seed, 360, Generate(seed)),
            autoCompleteMovements: true);
    }
}
