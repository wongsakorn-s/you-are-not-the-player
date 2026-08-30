using Game.Sim.Needs;
using Game.Sim.Time;

namespace Game.Sim.Tests.Needs;

public sealed class NeedStateTests
{
    [Fact]
    public void AdvanceAndSatisfy_UpdateUrgencyAndClampToValidRange()
    {
        var needs = new NeedState(hunger: 0.2f, fatigue: 0.9f, social: 0.0f);
        var rates = new NeedRates(
            hungerPerHour: 0.3,
            fatiguePerHour: 0.2,
            socialPerHour: 0.1);

        needs.Advance(new SimDelta(3_600), ticksPerSecond: 1, rates);
        needs.Satisfy(NeedType.Hunger, amount: 2.0);

        Assert.Equal(0.0f, needs.GetUrgency(NeedType.Hunger));
        Assert.Equal(1.0f, needs.GetUrgency(NeedType.Fatigue));
        Assert.Equal(0.1f, needs.GetUrgency(NeedType.Social), precision: 5);
    }
}
