using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_ai_trace_recorder_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestRecorderCapturesBalancedNestedSpans();
            TestDisablingEventCaptureClearsEvents();
            TestInstanceScopeRestoresPreviousRecorderAfterException();
        }
        finally
        {
            AiTraceRecorder.SetInstance(null);
        }

        RequestTestExit(_test.Finish("AI trace recorder regression"));
    }

    private void TestRecorderCapturesBalancedNestedSpans()
    {
        AiTraceRecorder.SetInstance(null);
        AiTraceRecorder.Enter("no_instance_span");
        AiTraceRecorder.Exit("no_instance_span");

        var recorder = new AiTraceRecorder();
        AiTraceRecorder.SetInstance(recorder);

        AiTraceRecorder.Enter("outer");
        AiTraceRecorder.Enter("inner");
        AiTraceRecorder.Exit("inner");
        AiTraceRecorder.Exit("outer");

        _test.True(recorder.AssertBalanced(), "nested trace spans should be balanced.");
        using GodotProjectionLease<Godot.Collections.Array> eventsLease =
            recorder.GetEventsLease();
        Godot.Collections.Array events = eventsLease.Value;
        _test.Eq(events.Count, 4, "nested trace spans should emit begin/end events.");
        _test.Eq(
            events[0].AsGodotDictionary()["ph"].AsString(),
            "B",
            "first trace event should be a begin event."
        );
        _test.Eq(
            events[3].AsGodotDictionary()["ph"].AsString(),
            "E",
            "last trace event should be an end event."
        );

        using GodotProjectionLease<GDictionary> statsLease = recorder.GetFuncStatsLease();
        GDictionary stats = statsLease.Value;
        _test.True(stats.ContainsKey(new StringName("outer")), "stats should include outer span.");
        _test.True(stats.ContainsKey(new StringName("inner")), "stats should include inner span.");

        GDictionary outerStats = stats[new StringName("outer")].AsGodotDictionary();
        GDictionary innerStats = stats[new StringName("inner")].AsGodotDictionary();
        _test.Eq(outerStats["ncalls"].AsInt64(), 1L, "outer span call count should be tracked.");
        _test.Eq(innerStats["ncalls"].AsInt64(), 1L, "inner span call count should be tracked.");
        _test.True(
            outerStats["total_usec"].AsInt64() >= outerStats["self_usec"].AsInt64(),
            "outer total time should include self time."
        );
    }

    private void TestDisablingEventCaptureClearsEvents()
    {
        var recorder = new AiTraceRecorder();
        AiTraceRecorder.SetInstance(recorder);

        AiTraceRecorder.Enter("captured");
        AiTraceRecorder.Exit("captured");
        using (GodotProjectionLease<Godot.Collections.Array> eventsLease =
            recorder.GetEventsLease())
        {
            _test.True(eventsLease.Value.Count > 0, "enabled event capture should record events.");
        }

        recorder.SetEventCaptureEnabled(false);
        using GodotProjectionLease<Godot.Collections.Array> disabledEventsLease =
            recorder.GetEventsLease();
        _test.Eq(
            disabledEventsLease.Value.Count,
            0,
            "disabling event capture should clear events."
        );
        _test.True(!recorder.IsTruncated(), "disabling event capture should reset truncation flag.");
    }

    private void TestInstanceScopeRestoresPreviousRecorderAfterException()
    {
        var previousRecorder = new AiTraceRecorder();
        var scopedRecorder = new AiTraceRecorder();
        var expectedFailure = new System.InvalidOperationException("expected scoped trace failure");
        System.Exception observedFailure = null;
        AiTraceRecorder.SetInstance(previousRecorder);

        try
        {
            using (AiTraceRecorder.PushInstance(scopedRecorder))
            {
                _test.True(
                    ReferenceEquals(AiTraceRecorder.GetInstance(), scopedRecorder),
                    "trace instance scope should install its recorder."
                );
                throw expectedFailure;
            }
        }
        catch (System.Exception exception)
        {
            observedFailure = exception;
        }

        _test.True(
            ReferenceEquals(observedFailure, expectedFailure),
            "trace instance scope should not replace the operation failure."
        );
        _test.True(
            ReferenceEquals(AiTraceRecorder.GetInstance(), previousRecorder),
            "trace instance scope should restore the previous recorder after an exception."
        );

        AiTraceRecorder.Enter("restored_scope_probe");
        AiTraceRecorder.Exit("restored_scope_probe");
        using GodotProjectionLease<GDictionary> statsLease =
            previousRecorder.GetFuncStatsLease();
        _test.True(
            statsLease.Value.ContainsKey(new StringName("restored_scope_probe")),
            "subsequent trace events should reach the restored recorder."
        );
    }
}
