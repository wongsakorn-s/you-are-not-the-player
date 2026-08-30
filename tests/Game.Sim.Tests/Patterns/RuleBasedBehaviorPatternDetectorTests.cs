using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Patterns;
using Game.Sim.Time;

namespace Game.Sim.Tests.Patterns;

public sealed class RuleBasedBehaviorPatternDetectorTests
{
    private static readonly EntityId Actor = new("actor");
    private static readonly LocationId Room = new("room");

    [Fact]
    public void Process_DetectsLootSweepOnceForContinuousEpisode()
    {
        var detector = new RuleBasedBehaviorPatternDetector(ticksPerSecond: 1);
        var matches = new List<BehaviorPatternMatch>();

        for (int index = 1; index <= 11; index++)
        {
            matches.AddRange(detector.Process(CreateEvent(
                index,
                index,
                EventType.Interaction,
                new InteractionPayload(InteractionKind.LootContainer, $"container-{index}"))));
        }

        BehaviorPatternMatch match = Assert.Single(matches);
        Assert.Equal(BehaviorPatternKind.LootSweep, match.Pattern);
        Assert.Equal(10, match.EvidenceEvents.Count);
        Assert.Equal(new EventId(10), match.EvidenceEvents[^1]);
    }

    [Fact]
    public void Process_DoesNotCombineLootInteractionsOutsideWindow()
    {
        var detector = new RuleBasedBehaviorPatternDetector(ticksPerSecond: 1);
        var matches = new List<BehaviorPatternMatch>();

        for (int index = 1; index <= 9; index++)
        {
            matches.AddRange(detector.Process(CreateEvent(
                index,
                index,
                EventType.Interaction,
                new InteractionPayload(InteractionKind.LootContainer, $"container-{index}"))));
        }

        matches.AddRange(detector.Process(CreateEvent(
            id: 10,
            tick: 200,
            EventType.Interaction,
            new InteractionPayload(InteractionKind.LootContainer, "container-10"))));

        Assert.Empty(matches);
    }

    [Fact]
    public void Process_DetectsRepeatInteraction()
    {
        var detector = new RuleBasedBehaviorPatternDetector(ticksPerSecond: 1);
        var matches = new List<BehaviorPatternMatch>();

        for (int index = 1; index <= 6; index++)
        {
            matches.AddRange(detector.Process(CreateEvent(
                index,
                index,
                EventType.Interaction,
                new InteractionPayload(InteractionKind.Generic, "same-terminal"))));
        }

        BehaviorPatternMatch match = Assert.Single(matches);
        Assert.Equal(BehaviorPatternKind.RepeatInteraction, match.Pattern);
        Assert.Equal(5, match.EvidenceEvents.Count);
    }

    [Fact]
    public void Process_DetectsRoleNeglect()
    {
        var detector = new RuleBasedBehaviorPatternDetector(ticksPerSecond: 1);
        var matches = new List<BehaviorPatternMatch>();

        for (int index = 1; index <= 3; index++)
        {
            matches.AddRange(detector.Process(CreateEvent(
                index,
                index * 600L,
                EventType.RoleDutyMissed,
                new RoleDutyPayload($"shift-{index}"))));
        }

        BehaviorPatternMatch match = Assert.Single(matches);
        Assert.Equal(BehaviorPatternKind.RoleNeglect, match.Pattern);
        Assert.Equal(3, match.EvidenceEvents.Count);
    }

    [Fact]
    public void Process_DetectsBoundaryTestingFromDistinctProbes()
    {
        var detector = new RuleBasedBehaviorPatternDetector(ticksPerSecond: 1);
        var matches = new List<BehaviorPatternMatch>();

        for (int index = 1; index <= 4; index++)
        {
            matches.AddRange(detector.Process(CreateEvent(
                index,
                index,
                EventType.BoundaryProbe,
                new BoundaryProbePayload($"boundary-{index}"))));
        }

        BehaviorPatternMatch match = Assert.Single(matches);
        Assert.Equal(BehaviorPatternKind.BoundaryTesting, match.Pattern);
        Assert.Equal(4, match.EvidenceEvents.Count);
    }

    [Fact]
    public void Process_RejectsOutOfOrderEventStream()
    {
        var detector = new RuleBasedBehaviorPatternDetector(ticksPerSecond: 1);
        _ = detector.Process(CreateEvent(
            id: 1,
            tick: 10,
            EventType.Interaction,
            new InteractionPayload(InteractionKind.Generic, "terminal")));

        Assert.Throws<ArgumentException>(() => detector.Process(CreateEvent(
            id: 2,
            tick: 9,
            EventType.Interaction,
            new InteractionPayload(InteractionKind.Generic, "terminal"))));
    }

    private static WorldEvent CreateEvent(
        long id,
        long tick,
        EventType type,
        EventPayload payload) =>
        new(
            new EventId(id),
            new SimTime(tick),
            Actor,
            type,
            Room,
            payload: payload);
}
