using System;
using System.Collections.Generic;
using System.Threading;
using Godot;

internal sealed class TestHarness
{
    public readonly List<string> Failures = new();
    private int _finished;
    private int _exitCode;

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

    public int Finish(string label, int exitCode = 0)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
            return _exitCode;

        bool passed = Failures.Count == 0 && exitCode == 0;
        _exitCode = passed ? 0 : 1;

        if (passed)
        {
            GD.Print($"{label}: PASS");
        }
        else
        {
            foreach (string failure in Failures)
                GD.PushError(failure);
            GD.Print($"{label}: FAIL ({Failures.Count})");
        }

        try
        {
            TestResourceOwnership.Drain();
            GodotSharpCleanup.CollectPendingFinalizers();
        }
        catch (Exception exception)
        {
            GD.PushError(
                $"TestHarness finalizer drain failed before Quit. label={label}, error={exception}"
            );
            _exitCode = 1;
        }

        return _exitCode;
    }
}

internal static class TestResourceOwnership
{
    private static readonly GodotTransientResourceScope Scope =
        new("TestResourceOwnership", quarantineOnDrain: true);

    internal static T Own<T>(T resource, string reason)
        where T : Resource
    {
        return Scope.Own(resource, reason);
    }

    internal static T OwnWrapper<T>(T wrapper, string reason)
        where T : class
    {
        return Scope.OwnWrapper(wrapper, reason);
    }

    internal static void Drain()
    {
        Scope.Drain();
    }
}
