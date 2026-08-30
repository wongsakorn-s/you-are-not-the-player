namespace Game.Sim.Random;

public interface ISimRandom
{
    int NextInt(int minInclusive, int maxExclusive);

    float NextFloat();

    bool Chance(float probability);
}
