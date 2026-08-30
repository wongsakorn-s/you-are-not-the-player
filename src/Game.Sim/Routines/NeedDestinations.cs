using Game.Sim.Locations;

namespace Game.Sim.Routines;

public sealed record NeedDestinations
{
    public NeedDestinations(
        LocationId mealLocation,
        LocationId restLocation,
        LocationId socialLocation)
    {
        Validate(mealLocation, nameof(mealLocation));
        Validate(restLocation, nameof(restLocation));
        Validate(socialLocation, nameof(socialLocation));
        MealLocation = mealLocation;
        RestLocation = restLocation;
        SocialLocation = socialLocation;
    }

    public LocationId MealLocation { get; }

    public LocationId RestLocation { get; }

    public LocationId SocialLocation { get; }

    private static void Validate(LocationId location, string parameterName)
    {
        if (location.IsEmpty)
        {
            throw new ArgumentException("Need destination cannot be empty.", parameterName);
        }
    }
}
