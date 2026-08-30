namespace Game.Sim.Needs;

public sealed record NeedProfile
{
    public NeedProfile(
        NeedRates growthRates,
        double eatingRecoveryPerHour,
        double sleepingRecoveryPerHour,
        double socialRecoveryPerHour)
    {
        ArgumentNullException.ThrowIfNull(growthRates);
        ValidateRecovery(eatingRecoveryPerHour, nameof(eatingRecoveryPerHour));
        ValidateRecovery(sleepingRecoveryPerHour, nameof(sleepingRecoveryPerHour));
        ValidateRecovery(socialRecoveryPerHour, nameof(socialRecoveryPerHour));
        GrowthRates = growthRates;
        EatingRecoveryPerHour = eatingRecoveryPerHour;
        SleepingRecoveryPerHour = sleepingRecoveryPerHour;
        SocialRecoveryPerHour = socialRecoveryPerHour;
    }

    public NeedRates GrowthRates { get; }

    public double EatingRecoveryPerHour { get; }

    public double SleepingRecoveryPerHour { get; }

    public double SocialRecoveryPerHour { get; }

    private static void ValidateRecovery(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Recovery rate must be a finite non-negative number.");
        }
    }
}
