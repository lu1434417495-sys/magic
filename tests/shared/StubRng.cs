using System;

internal sealed class StubRng
{
    private readonly int[] _rolls;

    public StubRng(int[] rolls)
    {
        _rolls = rolls ?? Array.Empty<int>();
    }

    public int CallCount { get; private set; }

    public int RandiRange(int minValue, int maxValue)
    {
        int lower = Math.Min(minValue, maxValue);
        int upper = Math.Max(minValue, maxValue);
        int roll = CallCount < _rolls.Length ? _rolls[CallCount] : lower;
        CallCount += 1;
        return Math.Clamp(roll, lower, upper);
    }

    public int RemainingCount()
    {
        return Math.Max(_rolls.Length - CallCount, 0);
    }
}
