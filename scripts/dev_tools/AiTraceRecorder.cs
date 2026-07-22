using System;
using System.Collections.Generic;
using Godot;

/// Lightweight tracer for AI hot-path profiling.
/// Default state: instance == null, so enter() / exit() reduce to a single
/// static-variable read plus a null check. Production code can safely call
/// them on the hot path with negligible overhead.
public class AiTraceRecorder
{
    private sealed class InstanceScope : IDisposable
    {
        private readonly AiTraceRecorder _previousInstance;
        private bool _disposed;

        internal InstanceScope(AiTraceRecorder nextInstance)
        {
            _previousInstance = GetInstance();
            SetInstance(nextInstance);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            SetInstance(_previousInstance);
        }
    }

    private sealed class TraceEventData
    {
        public string Name { get; init; } = "";
        public string Category { get; init; } = "ai";
        public string Phase { get; init; } = "";
        public ulong TimestampUsec { get; init; }
        public int Pid { get; init; }
        public int Tid { get; init; }

        public Godot.Collections.Dictionary ToDictionary() =>
            new()
            {
                { "name", Name },
                { "cat", Category },
                { "ph", Phase },
                { "ts", TimestampUsec },
                { "pid", Pid },
                { "tid", Tid },
            };
    }

    private sealed class TraceFrameData
    {
        public StringName Name { get; init; } = "";
        public ulong EnteredAtUsec { get; init; }
        public ulong ChildUsec { get; set; }
    }

    private sealed class FuncStatsData
    {
        public long NCalls { get; set; }
        public long SelfUsec { get; set; }
        public long TotalUsec { get; set; }
        public long MaxUsec { get; set; }

        public Godot.Collections.Dictionary ToDictionary(List<long> samples = null)
        {
            var result = new Godot.Collections.Dictionary
            {
                { "ncalls", NCalls },
                { "self_usec", SelfUsec },
                { "total_usec", TotalUsec },
                { "max_usec", MaxUsec },
            };
            if (samples != null)
                result["samples"] = samples.ToArray();
            return result;
        }
    }

    private const string _EVENT_BEGIN = "B";

    private const string _EVENT_END = "E";

    private static AiTraceRecorder _instance;

    private List<TraceEventData> _events = new();

    private Dictionary<StringName, FuncStatsData> _funcStats = new();

    private Dictionary<StringName, List<long>> _funcSamples = new();

    private List<TraceFrameData> _callStack = new();

    private ulong _startTsUsec;

    private int _pid = 1;

    private int _tid = 1;

    private int _maxEvents = 200000;

    private bool _truncated;

    private bool _collectEvents = true;

    private bool _collectSamples;

    private int _mismatchCount;

    public AiTraceRecorder()
    {
        _startTsUsec = Time.GetTicksUsec();
    }

    public static void Enter(StringName name)
    {
        var i = _instance;

        if (i == null)
            return;

        i._enter_impl(name);
    }

    public static void Exit(StringName name)
    {
        var i = _instance;

        if (i == null)
            return;

        i._exit_impl(name);
    }

    public void SetEventCaptureEnabled(bool enabled)
    {
        _collectEvents = enabled;

        if (!_collectEvents)
        {
            _events.Clear();

            _truncated = false;
        }
    }

    public void SetSampleCaptureEnabled(bool enabled)
    {
        _collectSamples = enabled;

        if (!_collectSamples)
        {
            _funcSamples.Clear();
        }
    }

    public static bool HasInstance()
    {
        return _instance != null;
    }

    public static AiTraceRecorder GetInstance()
    {
        return _instance;
    }

    public static void SetInstance(AiTraceRecorder next_instance)
    {
        _instance = next_instance;
    }

    internal static IDisposable PushInstance(AiTraceRecorder nextInstance)
    {
        return new InstanceScope(nextInstance);
    }

    private void _enter_impl(StringName name)
    {
        ulong ts = Time.GetTicksUsec();

        if (_collectEvents)
        {
            if (_events.Count < _maxEvents)
            {
                _events.Add(
                    new TraceEventData
                    {
                        Name = name.ToString(),
                        Phase = _EVENT_BEGIN,
                        TimestampUsec = ts - _startTsUsec,
                        Pid = _pid,
                        Tid = _tid,
                    }
                );
            }
            else
            {
                _truncated = true;
            }
        }

        _callStack.Add(
            new TraceFrameData
            {
                Name = name,
                EnteredAtUsec = ts,
                ChildUsec = 0,
            }
        );
    }

    private void _exit_impl(StringName name)
    {
        ulong ts = Time.GetTicksUsec();

        if (_callStack.Count == 0)
        {
            GameLog.Warning($"AiTraceRecorder.Exit({name}) called with empty stack", "trace.empty_stack", "dev");

            return;
        }

        TraceFrameData frame = _callStack[^1];

        StringName frameName = frame.Name;

        if (frameName != name)
        {
            GameLog.Error($"AiTraceRecorder.Exit({name}) mismatched stack top={frameName}.", "trace.mismatch", "dev");

            _mismatchCount += 1;
        }

        _callStack.RemoveAt(_callStack.Count - 1);

        if (_collectEvents)
        {
            if (_events.Count < _maxEvents)
            {
                _events.Add(
                    new TraceEventData
                    {
                        Name = frameName.ToString(),
                        Phase = _EVENT_END,
                        TimestampUsec = ts - _startTsUsec,
                        Pid = _pid,
                        Tid = _tid,
                    }
                );
            }
            else
            {
                _truncated = true;
            }
        }

        ulong tEnter = frame.EnteredAtUsec;

        long ownUsec = (long)(ts - tEnter);

        long childUsec = (long)frame.ChildUsec;

        long selfUsec = ownUsec - childUsec;

        if (selfUsec < 0)
            selfUsec = 0;

        if (!_funcStats.TryGetValue(frameName, out FuncStatsData stats))
        {
            stats = new FuncStatsData();
            _funcStats[frameName] = stats;
        }

        stats.NCalls += 1;

        stats.SelfUsec += selfUsec;

        stats.TotalUsec += ownUsec;

        if (ownUsec > stats.MaxUsec)
            stats.MaxUsec = ownUsec;
        if (_collectSamples)
        {
            if (!_funcSamples.TryGetValue(frameName, out List<long> samples))
            {
                samples = new List<long>();
                _funcSamples[frameName] = samples;
            }
            samples.Add(ownUsec);
        }

        if (_callStack.Count > 0)
        {
            _callStack[^1].ChildUsec += (ulong)ownUsec;
        }
    }

    internal GodotProjectionLease<Godot.Collections.Dictionary> GetFuncStatsLease()
    {
        var root = new Godot.Collections.Dictionary();
        GodotProjectionLease<Godot.Collections.Dictionary> lease =
            GodotProjectionLease<Godot.Collections.Dictionary>.CreateOwnedRoot(
                root,
                "ai-trace-func-stats",
                LifetimeDomain.Request,
                "AiTraceRecorder.GetFuncStatsLease"
            );
        try
        {
            foreach (KeyValuePair<StringName, FuncStatsData> entry in _funcStats)
            {
                _funcSamples.TryGetValue(entry.Key, out List<long> samples);
                root[entry.Key] = lease.Own(
                    entry.Value.ToDictionary(_collectSamples ? samples : null),
                    $"AiTraceRecorder.GetFuncStatsLease.{entry.Key}"
                );
            }
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal GodotProjectionLease<Godot.Collections.Array> GetEventsLease()
    {
        var root = new Godot.Collections.Array();
        GodotProjectionLease<Godot.Collections.Array> lease =
            GodotProjectionLease<Godot.Collections.Array>.CreateOwnedRoot(
                root,
                "ai-trace-events",
                LifetimeDomain.Request,
                "AiTraceRecorder.GetEventsLease"
            );
        try
        {
            int index = 0;
            foreach (TraceEventData entry in _events)
            {
                root.Add(
                    lease.Own(
                        entry.ToDictionary(),
                        $"AiTraceRecorder.GetEventsLease[{index}]"
                    )
                );
                index++;
            }
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    public bool IsTruncated()
    {
        return _truncated;
    }

    public bool DumpTraceJson(string path, Godot.Collections.Dictionary metadata = null)
    {
        using GodotProjectionLease<Godot.Collections.Array> eventsLease = GetEventsLease();
        using NativeLeaseScope requestScope = new(
            "ai-trace-json",
            LifetimeDomain.Request
        );
        Godot.Collections.Dictionary doc = requestScope.Own(
            new Godot.Collections.Dictionary
            {
                { "traceEvents", eventsLease.Value },
                { "displayTimeUnit", "us" },
                {
                    "metadata",
                    metadata
                        ?? requestScope.Own(
                            new Godot.Collections.Dictionary(),
                            "AiTraceRecorder.DumpTraceJson.metadata"
                        )
                },
            },
            "AiTraceRecorder.DumpTraceJson.document"
        );

        var dirPath = path.GetBaseDir();

        if (!DirAccess.DirExistsAbsolute(dirPath))
            DirAccess.MakeDirRecursiveAbsolute(dirPath);

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);

        if (file == null)
            return false;

        file.StoreString(Json.Stringify(doc));

        return true;
    }

    public bool AssertBalanced()
    {
        if (_mismatchCount > 0)
            return false;

        if (_callStack.Count > 0)
            return false;

        int begins = 0;

        int ends = 0;

        foreach (TraceEventData ev in _events)
        {
            string ph = ev.Phase;

            if (ph == _EVENT_BEGIN)
                begins += 1;
            else if (ph == _EVENT_END)
                ends += 1;
        }

        return begins == ends;
    }
}
