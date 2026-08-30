namespace Game.Sim.Patterns;

public sealed record BehaviorPatternPolicy
{
    public BehaviorPatternPolicy(
        int lootSweepDistinctInteractions = 10,
        int lootSweepWindowSeconds = 90,
        int repeatInteractionCount = 5,
        int repeatInteractionWindowSeconds = 60,
        int roleNeglectCount = 3,
        int roleNeglectWindowSeconds = 3_600,
        int boundaryTestingDistinctProbes = 4,
        int boundaryTestingWindowSeconds = 60)
    {
        ValidateCount(lootSweepDistinctInteractions, nameof(lootSweepDistinctInteractions));
        ValidateSeconds(lootSweepWindowSeconds, nameof(lootSweepWindowSeconds));
        ValidateCount(repeatInteractionCount, nameof(repeatInteractionCount));
        ValidateSeconds(repeatInteractionWindowSeconds, nameof(repeatInteractionWindowSeconds));
        ValidateCount(roleNeglectCount, nameof(roleNeglectCount));
        ValidateSeconds(roleNeglectWindowSeconds, nameof(roleNeglectWindowSeconds));
        ValidateCount(boundaryTestingDistinctProbes, nameof(boundaryTestingDistinctProbes));
        ValidateSeconds(boundaryTestingWindowSeconds, nameof(boundaryTestingWindowSeconds));

        LootSweepDistinctInteractions = lootSweepDistinctInteractions;
        LootSweepWindowSeconds = lootSweepWindowSeconds;
        RepeatInteractionCount = repeatInteractionCount;
        RepeatInteractionWindowSeconds = repeatInteractionWindowSeconds;
        RoleNeglectCount = roleNeglectCount;
        RoleNeglectWindowSeconds = roleNeglectWindowSeconds;
        BoundaryTestingDistinctProbes = boundaryTestingDistinctProbes;
        BoundaryTestingWindowSeconds = boundaryTestingWindowSeconds;
    }

    public int LootSweepDistinctInteractions { get; }

    public int LootSweepWindowSeconds { get; }

    public int RepeatInteractionCount { get; }

    public int RepeatInteractionWindowSeconds { get; }

    public int RoleNeglectCount { get; }

    public int RoleNeglectWindowSeconds { get; }

    public int BoundaryTestingDistinctProbes { get; }

    public int BoundaryTestingWindowSeconds { get; }

    internal long GetMaximumWindowTicks(int ticksPerSecond) => checked(
        (long)Math.Max(
            Math.Max(LootSweepWindowSeconds, RepeatInteractionWindowSeconds),
            Math.Max(RoleNeglectWindowSeconds, BoundaryTestingWindowSeconds)) *
        ticksPerSecond);

    private static void ValidateCount(int value, string parameterName)
    {
        if (value < 2)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Pattern threshold must be at least two events.");
        }
    }

    private static void ValidateSeconds(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Pattern window must be a positive number of seconds.");
        }
    }
}
