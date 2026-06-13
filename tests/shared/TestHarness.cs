using System;
using System.Collections.Generic;
using Godot;

internal sealed class TestHarness
{
    public readonly List<string> Failures = new();

    public void True(bool condition, string message)
    {
        if (!condition)
            Failures.Add(message);
    }

    public void False(bool condition, string message)
    {
        if (condition)
            Failures.Add(message);
    }

    public void Eq<T>(T actual, T expected, string message)
    {
        bool equal;
        if (typeof(T) == typeof(StringName))
        {
            equal = actual?.ToString() == expected?.ToString();
        }
        else
        {
            equal = EqualityComparer<T>.Default.Equals(actual, expected);
        }

        if (!equal)
            Failures.Add($"{message} | actual={actual} expected={expected}");
    }

    public void Eq(object actual, object expected, string message)
    {
        if (actual is StringName || expected is StringName)
        {
            if (actual?.ToString() != expected?.ToString())
                Failures.Add($"{message} | actual={actual} expected={expected}");
            return;
        }

        if (!Equals(actual, expected))
            Failures.Add($"{message} | actual={actual} expected={expected}");
    }

    public void Ne<T>(T actual, T unexpected, string message)
    {
        if (Equals(actual, unexpected))
            Failures.Add($"{message} | unexpected={unexpected}");
    }

    public void Fail(string message) => Failures.Add(message);

    public int Finish(string label)
    {
        if (Failures.Count == 0)
        {
            GD.Print($"{label}: PASS");
            return 0;
        }

        foreach (string failure in Failures)
            GD.PushError(failure);
        GD.Print($"{label}: FAIL ({Failures.Count})");
        return 1;
    }
}

