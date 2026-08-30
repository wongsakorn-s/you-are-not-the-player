using Game.Sim.Entities;
using Game.Sim.Events;
using Game.Sim.Locations;
using Game.Sim.Perception;
using Game.Sim.Time;

namespace Game.Sim.Tests.Perception;

public sealed class ObservationTests
{
    [Theory]
    [InlineData(-0.1f, 0.5f)]
    [InlineData(1.1f, 0.5f)]
    [InlineData(0.5f, -0.1f)]
    [InlineData(0.5f, 1.1f)]
    public void Constructor_RejectsValuesOutsideUnitInterval(float confidence, float salience)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Observation(
            new ObservationId(1),
            new EventId(1),
            new EntityId("anna"),
            new EntityId("george"),
            EventType.EnterLocation,
            new LocationId("basement"),
            perceivedTags: [],
            SimTime.Zero,
            confidence,
            salience,
            PerceptionChannel.Visual));
    }

    [Fact]
    public void SequentialGenerator_ProducesMonotonicIds()
    {
        var ids = new SequentialObservationIdGenerator(firstValue: 4);

        ObservationId[] actual = [ids.NextId(), ids.NextId(), ids.NextId()];

        Assert.Equal(
            [new ObservationId(4), new ObservationId(5), new ObservationId(6)],
            actual);
    }
}
