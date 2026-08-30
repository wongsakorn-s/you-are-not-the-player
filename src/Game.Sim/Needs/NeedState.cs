using Game.Sim.Time;

namespace Game.Sim.Needs;

public sealed class NeedState
{
    private readonly float[] _urgencies = new float[Enum.GetValues<NeedType>().Length];

    public NeedState(float hunger = 0.0f, float fatigue = 0.0f, float social = 0.0f)
    {
        SetUrgency(NeedType.Hunger, hunger);
        SetUrgency(NeedType.Fatigue, fatigue);
        SetUrgency(NeedType.Social, social);
    }

    public float GetUrgency(NeedType type) => _urgencies[GetIndex(type)];

    public void Advance(SimDelta delta, int ticksPerSecond, NeedRates rates)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticksPerSecond);
        ArgumentNullException.ThrowIfNull(rates);
        double elapsedHours = (double)delta.Ticks / ticksPerSecond / 3_600.0;

        foreach (NeedType type in Enum.GetValues<NeedType>())
        {
            float next = (float)(GetUrgency(type) + (rates.GetRate(type) * elapsedHours));
            SetUrgency(type, Math.Clamp(next, 0.0f, 1.0f));
        }
    }

    public void Satisfy(NeedType type, double amount)
    {
        if (!double.IsFinite(amount) || amount < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Satisfaction amount must be a finite non-negative number.");
        }

        float next = (float)(GetUrgency(type) - amount);
        SetUrgency(type, Math.Clamp(next, 0.0f, 1.0f));
    }

    private void SetUrgency(NeedType type, float value)
    {
        if (float.IsNaN(value) || value is < 0.0f or > 1.0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Need urgency must be between 0 and 1 inclusive.");
        }

        _urgencies[GetIndex(type)] = value;
    }

    private static int GetIndex(NeedType type)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown need type.");
        }

        return (int)type - 1;
    }
}
