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
        SuspicionRule rule = repository.Rules[6];

        Assert.Equal(11, repository.Rules.Count);
        Assert.Equal(
            [
                "detected_boundary_testing",
                "detected_loot_sweep",
                "detected_repeat_interaction",
                "detected_role_neglect",
                "detected_save_reload_anomaly",
                "detected_the_blink_anomaly",
                "restricted_area_entry",
                "witnessed_night_activity",
                "witnessed_secret_meeting",
                "witnessed_suspicious_tampering",
                "witnessed_theft",
            ],
            repository.Rules.Select(item => item.Id));
        Assert.Equal("restricted_area_entry", rule.Id);
        Assert.Equal(EventType.EnterLocation, rule.EventType);
        Assert.Equal([EventTag.Restricted], rule.RequiredTags);
        Assert.Equal(
            [SuspicionDimension.Secrecy, SuspicionDimension.RoleDeviation],
            rule.Effects.Select(effect => effect.Dimension));
        SuspicionRule lootSweep = Assert.Single(
            repository.Rules,
            item => item.Id == "detected_loot_sweep");
        Assert.Equal(EventType.BehaviorPattern, lootSweep.EventType);
        Assert.Equal(BehaviorPatternKind.LootSweep, lootSweep.BehaviorPattern);
    }

    [Theory]
    [InlineData("{\"id\":\"not-an-array\"}")]
    [InlineData("[{\"id\":\"bad\",\"match\":{\"event\":\"Unknown\"},\"effects\":{\"secrecy\":1}}]")]
    [InlineData("[{\"id\":\"bad\",\"match\":{\"event\":\"EnterLocation\",\"extra\":1},\"effects\":{\"secrecy\":1}}]")]
    [InlineData("[{\"id\":\"bad\",\"match\":{\"event\":\"BehaviorPattern\",\"pattern\":\"Unknown\"},\"effects\":{\"secrecy\":1}}]")]
    [InlineData("[{\"id\":\"bad\",\"match\":{\"event\":\"EnterLocation\",\"pattern\":\"LootSweep\"},\"effects\":{\"secrecy\":1}}]")]
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
