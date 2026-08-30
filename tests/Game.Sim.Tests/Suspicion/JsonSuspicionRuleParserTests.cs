using Game.Sim.Events;
using Game.Sim.Suspicion;

namespace Game.Sim.Tests.Suspicion;

public sealed class JsonSuspicionRuleParserTests
{
    [Fact]
    public void Parse_LoadsAndOrdersProductionRules()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "SuspicionRules",
            "mvp.json");
        string json = File.ReadAllText(path);
        InMemorySuspicionRuleRepository repository = JsonSuspicionRuleParser.Parse(json);
        SuspicionRule rule = Assert.Single(repository.Rules);

        Assert.Equal("restricted_area_entry", rule.Id);
        Assert.Equal(EventType.EnterLocation, rule.EventType);
        Assert.Equal([EventTag.Restricted], rule.RequiredTags);
        Assert.Equal(
            [SuspicionDimension.Secrecy, SuspicionDimension.RoleDeviation],
            rule.Effects.Select(effect => effect.Dimension));
    }

    [Theory]
    [InlineData("{\"id\":\"not-an-array\"}")]
    [InlineData("[{\"id\":\"bad\",\"match\":{\"event\":\"Unknown\"},\"effects\":{\"secrecy\":1}}]")]
    [InlineData("[{\"id\":\"bad\",\"match\":{\"event\":\"EnterLocation\",\"extra\":1},\"effects\":{\"secrecy\":1}}]")]
    [InlineData("[{\"id\":\"bad\",\"match\":{\"event\":\"EnterLocation\"},\"effects\":{\"unknown\":1}}]")]
    [InlineData("[{\"id\":\"bad\",\"match\":{\"event\":\"EnterLocation\"},\"effects\":{\"secrecy\":-1}}]")]
    public void Parse_RejectsInvalidConfiguration(string json)
    {
        Assert.Throws<InvalidDataException>(() => JsonSuspicionRuleParser.Parse(json));
    }

    [Fact]
    public void Parse_RejectsDuplicateRuleIds()
    {
        const string rule =
            "{\"id\":\"duplicate\",\"match\":{\"event\":\"EnterLocation\"}," +
            "\"effects\":{\"secrecy\":1}}";
        string json = $"[{rule},{rule}]";
        Assert.Throws<InvalidDataException>(() => JsonSuspicionRuleParser.Parse(json));
    }
}
