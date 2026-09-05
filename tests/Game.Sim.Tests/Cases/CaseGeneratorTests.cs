using Game.Sim.Cases;
using Game.Sim.Entities;
using Game.Sim.PlayerAi;
using Game.Sim.Secrets;

namespace Game.Sim.Tests.Cases;

public sealed class CaseGeneratorTests
{
    private const long ShiftTicks = 360;

    private static readonly EntityId George = new("george");
    private static readonly EntityId[] Roster =
    [
        new("anna"),
        new("bob"),
        George,
        new("charlie"),
        new("dana"),
        new("evelyn"),
    ];

    [Fact]
    public void Generate_IsDeterministicForTheSameSeed()
    {
        SessionTruth first = CaseGenerator.Generate(481516UL, CreateOptions());
        SessionTruth second = CaseGenerator.Generate(481516UL, CreateOptions());

        Assert.Equal(first.Fingerprint(), second.Fingerprint());
        Assert.Equal(first.HiddenPlayer, second.HiddenPlayer);
        Assert.Equal(first.IncidentCulprit, second.IncidentCulprit);
        Assert.Equal(first.HiddenPlayerArchetype, second.HiddenPlayerArchetype);
    }

    [Fact]
    public void Generate_DoesNotDependOnRosterEnumerationOrder()
    {
        SessionTruth ordered = CaseGenerator.Generate(2026UL, CreateOptions());
        SessionTruth shuffled = CaseGenerator.Generate(
            2026UL,
            CreateOptions(roster: Roster.Reverse()));

        Assert.Equal(ordered.Fingerprint(), shuffled.Fingerprint());
    }

    [Fact]
    public void Generate_VariesTheCaseAcrossSeeds()
    {
        // The gap this milestone exists to close: ten seeds used to produce one
        // fingerprint, so replaying was always the same night with the same answer.
        SessionTruth[] truths = Enumerable.Range(0, 10)
            .Select(index => CaseGenerator.Generate((ulong)(1000 + index), CreateOptions()))
            .ToArray();

        Assert.True(
            truths.Select(truth => truth.Fingerprint()).Distinct(StringComparer.Ordinal).Count() >= 8,
            "Ten seeds should produce at least eight distinct cases.");
        Assert.True(
            truths.Select(truth => truth.HiddenPlayer).Distinct().Count() >= 3,
            "Ten seeds should accuse at least three different hidden players.");
    }

    [Fact]
    public void Generate_KeepsTheHostOffTheHiddenPlayerPoolByDefault()
    {
        for (ulong seed = 0; seed < 200; seed++)
        {
            SessionTruth truth = CaseGenerator.Generate(seed, CreateOptions());
            Assert.NotEqual(George, truth.HiddenPlayer);
            Assert.False(truth.HostIsHiddenPlayer);
        }
    }

    [Fact]
    public void Generate_CanPutTheHostBehindThePlayerWhenContentAllowsIt()
    {
        bool sawHost = false;
        for (ulong seed = 0; seed < 200 && !sawHost; seed++)
        {
            SessionTruth truth = CaseGenerator.Generate(
                seed,
                CreateOptions(allowHostAsHiddenPlayer: true));
            sawHost = truth.HostIsHiddenPlayer;
        }

        Assert.True(sawHost, "The host should be reachable once content opts in.");
    }

    [Fact]
    public void Generate_HonoursContentPins()
    {
        var pinned = new EntityId("dana");
        SessionTruth truth = CaseGenerator.Generate(
            99UL,
            CreateOptions(
                pinnedHiddenPlayer: pinned,
                pinnedIncidentCulprit: George,
                pinnedArchetype: PlayerAiArchetype.Roleplayer));

        Assert.Equal(pinned, truth.HiddenPlayer);
        Assert.Equal(George, truth.IncidentCulprit);
        Assert.Equal(PlayerAiArchetype.Roleplayer, truth.HiddenPlayerArchetype);
        Assert.False(truth.HiddenPlayerIsCulprit);
    }

    [Fact]
    public void Generate_SometimesBlamesSomeoneOtherThanThePlayer()
    {
        SessionTruth[] truths = Enumerable.Range(0, 60)
            .Select(index => CaseGenerator.Generate((ulong)index, CreateOptions()))
            .ToArray();

        Assert.Contains(truths, truth => truth.HiddenPlayerIsCulprit);
        Assert.Contains(truths, truth => !truth.HiddenPlayerIsCulprit);
    }

    [Fact]
    public void Generate_NeverGivesTheHiddenPlayerOrHostAnNpcSecret()
    {
        for (ulong seed = 0; seed < 200; seed++)
        {
            SessionTruth truth = CaseGenerator.Generate(seed, CreateOptions());
            Assert.DoesNotContain(truth.Secrets, secret => secret.Owner == truth.HiddenPlayer);
            Assert.DoesNotContain(truth.Secrets, secret => secret.Owner == truth.HumanHost);
            Assert.All(
                truth.Secrets.Where(secret => secret.Behavior == SecretBehaviorKind.SecretMeeting),
                secret =>
                {
                    Assert.NotNull(secret.Accomplice);
                    Assert.NotEqual(secret.Owner, secret.Accomplice!.Value);
                });
        }
    }

    [Fact]
    public void Generate_SchedulesAnomaliesInsideTheShiftAndSpreadsThem()
    {
        SessionTruth truth = CaseGenerator.Generate(481516UL, CreateOptions());

        Assert.Equal(CaseGenerationOptions.DefaultAnomalyCount, truth.AnomalySchedule.Count);
        Assert.All(truth.AnomalySchedule, beat => Assert.InRange(beat.Tick, 0, ShiftTicks - 1));
        Assert.Equal(
            truth.AnomalySchedule.OrderBy(beat => beat.Tick).Select(beat => beat.Tick),
            truth.AnomalySchedule.Select(beat => beat.Tick));
    }

    [Fact]
    public void Options_RejectAHostThatIsNotOnTheRoster() =>
        Assert.Throws<ArgumentException>(() => new CaseGenerationOptions(
            new EntityId("nobody"),
            Roster,
            ShiftTicks));

    [Fact]
    public void Options_RejectPinningTheHostAsHiddenPlayerWithoutOptIn() =>
        Assert.Throws<ArgumentException>(() => new CaseGenerationOptions(
            George,
            Roster,
            ShiftTicks,
            pinnedHiddenPlayer: George));

    private static CaseGenerationOptions CreateOptions(
        IEnumerable<EntityId>? roster = null,
        EntityId? pinnedHiddenPlayer = null,
        EntityId? pinnedIncidentCulprit = null,
        PlayerAiArchetype? pinnedArchetype = null,
        bool allowHostAsHiddenPlayer = false) =>
        new(
            George,
            roster ?? Roster,
            ShiftTicks,
            pinnedHiddenPlayer,
            pinnedIncidentCulprit,
            pinnedArchetype,
            allowHostAsHiddenPlayer);
}
