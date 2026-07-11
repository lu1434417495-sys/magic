using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class AiProfileCapture : IDisposable
{
    public string ScenarioId { get; private set; } = "";
    public string OutputDir { get; private set; } = "res://tests/battle_runtime/benchmarks/profiles/";
    public int TopN { get; private set; } = 20;
    public string SortBy { get; private set; } = "self_usec";
    public string NameFilter { get; private set; } = "";
    public bool DumpTraceJson { get; private set; }
    public string GitCommit { get; private set; } = "unknown";
    public string FilePrefix { get; private set; } = "ai_profile";

    public GDictionary AggregateStats { get; }
    public int MeasuredRuns { get; private set; }
    public int MeasuredAiTurns { get; private set; }
    public bool Balanced { get; private set; } = true;
    public bool Truncated { get; private set; }
    public Dictionary<string, object> LastReport { get; private set; } = new(
        StringComparer.Ordinal
    );

    private readonly NativeLeaseScope _lifetimeScope = new(
        "ai-profile-capture",
        LifetimeDomain.Request
    );
    private GodotProjectionLease<GArray> _traceEventsLease;

    internal AiProfileCapture()
    {
        AggregateStats = _lifetimeScope.Own(
            new GDictionary(),
            "AiProfileCapture.AggregateStats"
        );
    }

    public void Setup(
        string scenarioId,
        string outputDir,
        int topN,
        string sortBy,
        string nameFilter = "",
        bool dumpTraceJson = false,
        string gitCommit = "unknown",
        string filePrefix = "ai_profile"
    )
    {
        ScenarioId = scenarioId ?? "";
        OutputDir = string.IsNullOrEmpty(outputDir)
            ? "res://tests/battle_runtime/benchmarks/profiles/"
            : outputDir;
        if (!OutputDir.EndsWith('/'))
            OutputDir += "/";
        TopN = Mathf.Max(topN, 1);
        SortBy = string.IsNullOrEmpty(sortBy) ? "self_usec" : sortBy;
        NameFilter = nameFilter ?? "";
        DumpTraceJson = dumpTraceJson;
        GitCommit = string.IsNullOrEmpty(gitCommit) ? "unknown" : gitCommit;
        FilePrefix = string.IsNullOrEmpty(filePrefix) ? "ai_profile" : filePrefix;
        AggregateStats.Clear();
        MeasuredRuns = 0;
        MeasuredAiTurns = 0;
        Balanced = true;
        Truncated = false;
        _traceEventsLease?.Dispose();
        _traceEventsLease = null;
        LastReport = new Dictionary<string, object>(StringComparer.Ordinal);
        AiTraceRecorder.SetInstance(null);
    }

    public AiTraceRecorder BeginRun(bool measured = true)
    {
        AiTraceRecorder.SetInstance(null);
        if (!measured)
            return null;
        var recorder = new AiTraceRecorder();
        recorder.SetEventCaptureEnabled(DumpTraceJson);
        AiTraceRecorder.SetInstance(recorder);
        return recorder;
    }

    public Dictionary<string, object> EndRun(AiTraceRecorder recorder, int aiTurns = 0)
    {
        AiTraceRecorder.SetInstance(null);
        if (recorder == null)
            return new Dictionary<string, object>(StringComparer.Ordinal);
        using (GodotProjectionLease<GDictionary> statsLease = recorder.GetFuncStatsLease())
            MergeStats(AggregateStats, statsLease.Value);
        MeasuredRuns++;
        MeasuredAiTurns += Mathf.Max(aiTurns, 0);
        if (!recorder.AssertBalanced())
            Balanced = false;
        if (recorder.IsTruncated())
            Truncated = true;
        if (_traceEventsLease == null && DumpTraceJson)
        {
            GodotProjectionLease<GArray> eventsLease = recorder.GetEventsLease();
            if (eventsLease.Value.Count > 0)
                _traceEventsLease = eventsLease;
            else
                eventsLease.Dispose();
        }
        return BuildSummary();
    }

    public Dictionary<string, object> WriteReports()
    {
        string timestamp = FormatTimestamp();
        string basename = $"{FilePrefix}_{ScenarioId}_{timestamp}";
        string header = FormatHeader();
        string body = FormatBody();
        string hotspotsPath = $"{OutputDir}{basename}.hotspots.txt";
        string csvPath = $"{OutputDir}{basename}.functions.csv";
        bool okText = AiHotspotsFormatter.WriteTextReport(hotspotsPath, header, body);
        bool okCsv = AiHotspotsFormatter.WriteCsv(csvPath, AggregateStats);
        string tracePath = "";
        bool okTrace = false;
        if (DumpTraceJson && _traceEventsLease?.Value.Count > 0)
        {
            tracePath = $"{OutputDir}{basename}.trace.json";
            okTrace = WriteTraceJson(tracePath);
        }

        LastReport = BuildSummary();
        LastReport["header"] = header;
        LastReport["body"] = body;
        LastReport["hotspots_path"] = hotspotsPath;
        LastReport["functions_csv_path"] = csvPath;
        LastReport["trace_path"] = tracePath;
        LastReport["wrote_hotspots"] = okText;
        LastReport["wrote_functions_csv"] = okCsv;
        LastReport["wrote_trace"] = okTrace;
        return LastReport;
    }

    public string FormatHeader() =>
        AiHotspotsFormatter.FormatHeader(
            ScenarioId,
            MeasuredAiTurns,
            TotalSelfUsec(),
            SortBy,
            Engine.GetVersionInfo().GetValueOrDefault("string", "unknown").AsString(),
            GitCommit
        );

    public string FormatBody() =>
        AiHotspotsFormatter.FormatTopN(AggregateStats, SortBy, TopN, NameFilter);

    public long TotalSelfUsec() => AiHotspotsFormatter.TotalSelfUsec(AggregateStats);

    public Dictionary<string, object> BuildSummary() =>
        new(StringComparer.Ordinal)
        {
            ["enabled"] = true,
            ["scenario"] = ScenarioId,
            ["measured_runs"] = MeasuredRuns,
            ["measured_ai_turns"] = MeasuredAiTurns,
            ["total_self_usec"] = TotalSelfUsec(),
            ["sort"] = SortBy,
            ["top_n"] = TopN,
            ["filter"] = NameFilter,
            ["trace_json"] = DumpTraceJson,
            ["balanced"] = Balanced,
            ["truncated"] = Truncated,
            ["git_commit"] = GitCommit,
        };

    public static string ResolveGitCommit()
    {
        using FileAccess head = FileAccess.Open("res://.git/HEAD", FileAccess.ModeFlags.Read);
        if (head == null)
            return "unknown";
        string line = head.GetAsText().StripEdges();
        if (line.StartsWith("ref: "))
        {
            string refPath = "res://.git/" + line[5..].StripEdges();
            using FileAccess refFile = FileAccess.Open(refPath, FileAccess.ModeFlags.Read);
            if (refFile == null)
                return "unknown";
            string sha = refFile.GetAsText().StripEdges();
            return sha.Length >= 7 ? sha[..7] : sha;
        }
        return line.Length >= 7 ? line[..7] : "unknown";
    }

    private void MergeStats(GDictionary target, GDictionary source)
    {
        foreach (Variant nameValue in source?.Keys ?? new Godot.Collections.Array())
        {
            GDictionary src = source[nameValue].AsGodotDictionary();
            GDictionary dst =
                target.ContainsKey(nameValue) && target[nameValue].VariantType == Variant.Type.Dictionary
                    ? target[nameValue].AsGodotDictionary()
                    : _lifetimeScope.Own(
                        new GDictionary
                        {
                            ["ncalls"] = 0,
                            ["self_usec"] = 0L,
                            ["total_usec"] = 0L,
                            ["max_usec"] = 0L,
                        },
                        $"AiProfileCapture.AggregateStats.{nameValue}"
                    );
            dst["ncalls"] = DictInt(dst, "ncalls") + DictInt(src, "ncalls");
            dst["self_usec"] = DictLong(dst, "self_usec") + DictLong(src, "self_usec");
            dst["total_usec"] = DictLong(dst, "total_usec") + DictLong(src, "total_usec");
            dst["max_usec"] = Mathf.Max(DictLong(dst, "max_usec"), DictLong(src, "max_usec"));
            target[nameValue] = dst;
        }
    }

    private bool WriteTraceJson(string path)
    {
        if (_traceEventsLease == null)
            return false;
        using NativeLeaseScope requestScope = new(
            "ai-profile-trace-json",
            LifetimeDomain.Request
        );
        GDictionary metadata = requestScope.Own(
            new GDictionary
            {
                ["scenario"] = ScenarioId,
                ["godot_version"] = Engine.GetVersionInfo().GetValueOrDefault("string", "").AsString(),
                ["git_commit"] = GitCommit,
            },
            "AiProfileCapture.WriteTraceJson.metadata"
        );
        GDictionary traceDoc = requestScope.Own(
            new GDictionary
            {
                ["traceEvents"] = _traceEventsLease.Value,
                ["displayTimeUnit"] = "us",
                ["metadata"] = metadata,
            },
            "AiProfileCapture.WriteTraceJson.document"
        );
        string dirPart = path.GetBaseDir();
        if (!DirAccess.DirExistsAbsolute(dirPart))
            DirAccess.MakeDirRecursiveAbsolute(dirPart);
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
            return false;
        file.StoreString(Json.Stringify(traceDoc));
        return true;
    }

    public void Dispose()
    {
        AiTraceRecorder.SetInstance(null);
        _traceEventsLease?.Dispose();
        _traceEventsLease = null;
        _lifetimeScope.Dispose();
    }

    private static string FormatTimestamp()
    {
        var t = Time.GetDatetimeDictFromSystem();
        return $"{DictInt(t, "year"):0000}{DictInt(t, "month"):00}{DictInt(t, "day"):00}_{DictInt(t, "hour"):00}{DictInt(t, "minute"):00}{DictInt(t, "second"):00}";
    }

    private static int DictInt(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) ? (int)dict[key].AsInt64() : 0;

    private static long DictLong(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) ? dict[key].AsInt64() : 0L;
}
