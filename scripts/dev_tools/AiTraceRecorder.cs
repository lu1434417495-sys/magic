using Godot;

/// Lightweight tracer for AI hot-path profiling.
/// Default state: instance == null, so enter() / exit() reduce to a single
/// static-variable read plus a null check. Production code can safely call
/// them on the hot path with negligible overhead.
[GlobalClass]
public partial class AiTraceRecorder : RefCounted
{
    private const string _EVENT_BEGIN = "B";
    private const string _EVENT_END = "E";

    private static AiTraceRecorder _instance;

    private Godot.Collections.Array<Godot.Collections.Dictionary> _events = new();
    private Godot.Collections.Dictionary _funcStats = new();
    private Godot.Collections.Array<Godot.Collections.Dictionary> _callStack = new();
    private ulong _startTsUsec;
    private int _pid = 1;
    private int _tid = 1;
    private int _maxEvents = 200000;
    private bool _truncated;
    private bool _collectEvents = true;
    private int _mismatchCount;

    public AiTraceRecorder()
    {
        _startTsUsec = Time.GetTicksUsec();
    }

    public static void enter(StringName name)
    {
        var i = _instance;
        if (i == null)
            return;
        i._enter_impl(name);
    }

    public static void exit(StringName name)
    {
        var i = _instance;
        if (i == null)
            return;
        i._exit_impl(name);
    }

    public void set_event_capture_enabled(bool enabled)
    {
        _collectEvents = enabled;
        if (!_collectEvents)
        {
            _events.Clear();
            _truncated = false;
        }
    }

    public static bool has_instance()
    {
        return _instance != null;
    }

    public static AiTraceRecorder get_instance()
    {
        return _instance;
    }

    public static void set_instance(AiTraceRecorder next_instance)
    {
        _instance = next_instance;
    }

    private void _enter_impl(StringName name)
    {
        ulong ts = Time.GetTicksUsec();
        if (_collectEvents)
        {
            if (_events.Count < _maxEvents)
            {
                _events.Add(new Godot.Collections.Dictionary
                {
                    { "name", (string)name },
                    { "cat", "ai" },
                    { "ph", _EVENT_BEGIN },
                    { "ts", ts - _startTsUsec },
                    { "pid", _pid },
                    { "tid", _tid },
                });
            }
            else
            {
                _truncated = true;
            }
        }
        _callStack.Add(new Godot.Collections.Dictionary
        {
            { "name", name },
            { "t_enter", ts },
            { "child_usec", (ulong)0 },
        });
    }

    private void _exit_impl(StringName name)
    {
        ulong ts = Time.GetTicksUsec();
        if (_callStack.Count == 0)
        {
            GD.PushWarning($"AiTraceRecorder.exit({name}) called with empty stack");
            return;
        }
        var frame = _callStack[^1];
        var frameName = frame["name"].AsStringName();
        if (frameName != name)
        {
            GD.PushError($"AiTraceRecorder.exit({name}) mismatched stack top={frameName}");
            _mismatchCount += 1;
        }
        _callStack.RemoveAt(_callStack.Count - 1);

        if (_collectEvents)
        {
            if (_events.Count < _maxEvents)
            {
                _events.Add(new Godot.Collections.Dictionary
                {
                    { "name", (string)frameName },
                    { "cat", "ai" },
                    { "ph", _EVENT_END },
                    { "ts", ts - _startTsUsec },
                    { "pid", _pid },
                    { "tid", _tid },
                });
            }
            else
            {
                _truncated = true;
            }
        }

        ulong tEnter = (ulong)(long)frame["t_enter"];
        long ownUsec = (long)(ts - tEnter);
        long childUsec = (long)(ulong)frame["child_usec"];
        long selfUsec = ownUsec - childUsec;
        if (selfUsec < 0)
            selfUsec = 0;

        Godot.Collections.Dictionary stats;
        if (_funcStats.ContainsKey(frameName))
        {
            stats = (Godot.Collections.Dictionary)_funcStats[frameName];
        }
        else
        {
            stats = new Godot.Collections.Dictionary
            {
                { "ncalls", 0 },
                { "self_usec", (long)0 },
                { "total_usec", (long)0 },
                { "max_usec", (long)0 },
            };
        }
        stats["ncalls"] = (long)stats["ncalls"] + 1;
        stats["self_usec"] = (long)stats["self_usec"] + selfUsec;
        stats["total_usec"] = (long)stats["total_usec"] + ownUsec;
        if (ownUsec > (long)stats["max_usec"])
            stats["max_usec"] = ownUsec;
        _funcStats[frameName] = stats;

        if (_callStack.Count > 0)
        {
            var parent = _callStack[^1];
            parent["child_usec"] = (ulong)parent["child_usec"] + (ulong)ownUsec;
            _callStack[^1] = parent;
        }
    }

    public Godot.Collections.Dictionary get_func_stats()
    {
        return _funcStats;
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary> get_events()
    {
        return _events;
    }

    public bool is_truncated()
    {
        return _truncated;
    }

    public bool dump_trace_json(string path, Godot.Collections.Dictionary metadata = null)
    {
        var doc = new Godot.Collections.Dictionary
        {
            { "traceEvents", _events },
            { "displayTimeUnit", "us" },
            { "metadata", metadata ?? new Godot.Collections.Dictionary() },
        };
        var dirPath = path.GetBaseDir();
        if (!DirAccess.DirExistsAbsolute(dirPath))
            DirAccess.MakeDirRecursiveAbsolute(dirPath);
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
            return false;
        file.StoreString(Json.Stringify(doc));
        return true;
    }

    public bool assert_balanced()
    {
        if (_mismatchCount > 0)
            return false;
        if (_callStack.Count > 0)
            return false;
        int begins = 0;
        int ends = 0;
        foreach (var ev in _events)
        {
            string ph = (string)ev["ph"];
            if (ph == _EVENT_BEGIN)
                begins += 1;
            else if (ph == _EVENT_END)
                ends += 1;
        }
        return begins == ends;
    }
}
