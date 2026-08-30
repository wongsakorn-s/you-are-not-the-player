namespace Game.Sim.Needs;

public sealed record NeedRates
{
    public NeedRates(double hungerPerHour, double fatiguePerHour, double socialPerHour)
    {
        ValidateRate(hungerPerHour, nameof(hungerPerHour));
        ValidateRate(fatiguePerHour, nameof(fatiguePerHour));
        ValidateRate(socialPerHour, nameof(socialPerHour));
        HungerPerHour = hungerPerHour;
        FatiguePerHour = fatiguePerHour;
        SocialPerHour = socialPerHour;
    }

    public double HungerPerHour { get; }

    public double FatiguePerHour { get; }

    public double SocialPerHour { get; }

    public double GetRate(NeedType type) => type switch
    {
        NeedType.Hunger => HungerPerHour,
        NeedType.Fatigue => FatiguePerHour,
        NeedType.Social => SocialPerHour,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown need type."),
    };

    private static void ValidateRate(double rate, string parameterName)
    {
        if (!double.IsFinite(rate) || rate < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                rate,
                "Need rate must be a finite non-negative number.");
        }
    }
}
