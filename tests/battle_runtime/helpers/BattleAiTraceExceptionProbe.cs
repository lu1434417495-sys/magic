using System;

internal static class BattleAiTraceExceptionProbe
{
    internal static void AssertPreservedAndBalanced(
        TestHarness test,
        string label,
        Action<InvalidOperationException> invoke,
        params string[] completedSpanNames
    )
    {
        var recorder = new AiTraceRecorder();
        var expectedFailure = new InvalidOperationException($"{label} trace probe");
        Exception observedFailure = null;

        try
        {
            using (AiTraceRecorder.PushInstance(recorder))
                invoke(expectedFailure);
        }
        catch (Exception exception)
        {
            observedFailure = exception;
        }

        test.True(
            ReferenceEquals(observedFailure, expectedFailure),
            $"{label}: evaluator should preserve the dependency failure."
        );
        test.True(
            recorder.AssertBalanced(),
            $"{label}: evaluator should close every AI trace span after the failure."
        );
        using GodotProjectionLease<Godot.Collections.Dictionary> statsLease =
            recorder.GetFuncStatsLease();
        foreach (string spanName in completedSpanNames)
        {
            test.True(
                statsLease.Value.ContainsKey(new Godot.StringName(spanName)),
                $"{label}: evaluator should complete trace span '{spanName}'."
            );
        }
    }
}
