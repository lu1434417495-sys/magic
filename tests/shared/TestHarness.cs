using System.Collections.Generic;
using Godot;

internal sealed class TestHarness
{
    public readonly List<string> Failures = new();
    private readonly object _sync = new();
    private TestResult _result;

    public void True(bool condition, string message)
    {
        if (!condition)
            Fail(message);
    }

    public void False(bool condition, string message)
    {
        if (condition)
            Fail(message);
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
            Fail($"{message} | actual={actual} expected={expected}");
    }

    public void Eq(object actual, object expected, string message)
    {
        if (actual is StringName || expected is StringName)
        {
            if (actual?.ToString() != expected?.ToString())
                Fail($"{message} | actual={actual} expected={expected}");
            return;
        }

        if (!Equals(actual, expected))
            Fail($"{message} | actual={actual} expected={expected}");
    }

    public void Ne<T>(T actual, T unexpected, string message)
    {
        if (Equals(actual, unexpected))
            Fail($"{message} | unexpected={unexpected}");
    }

    public void Fail(string message)
    {
        lock (_sync)
            Failures.Add(message);
    }

    public TestResult Finish(string label, int exitCode = 0)
    {
        lock (_sync)
        {
            if (_result != null)
                return _result;

            IReadOnlyList<string> failures = new List<string>(Failures).AsReadOnly();
            bool passed = failures.Count == 0 && exitCode == 0;
            _result = new TestResult(label, passed, passed ? 0 : 1, failures);
            return _result;
        }
    }
}
