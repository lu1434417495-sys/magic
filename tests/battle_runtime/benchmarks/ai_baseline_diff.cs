using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class AiBaselineDiff
{
    public const int SchemaVersion = 1;
    public const double DefaultTolerancePct = 20.0;
    public const int DefaultAbsoluteToleranceUsec = 0;
    public const int DefaultMinMetricCallCount = 0;
    public const int DefaultMinPercentileCallCount = 0;
    public const int NoiseFloorUsec = 30;

    public static GDictionary SummarizeStats(GDictionary stats)
    {
        int callCount = DictInt(stats, "call_count");
        long totalUsec = DictLong(stats, "total_usec");
        long maxUsec = DictLong(stats, "max_usec");
        long[] samples = DictSamples(stats, "samples");
        var summary = new GDictionary
        {
            ["call_count"] = callCount,
            ["total_usec"] = totalUsec,
            ["max_usec"] = maxUsec,
            ["avg_usec"] = 0,
            ["p50_usec"] = 0,
            ["p95_usec"] = 0,
        };
        if (callCount <= 0 || samples.Length == 0)
            return summary;

        var sorted = new List<long>();
        foreach (long sample in samples)
            sorted.Add(sample);
        sorted.Sort();
        summary["avg_usec"] = (int)(totalUsec / Math.Max(callCount, 1));
        summary["p50_usec"] = sorted[Math.Clamp((int)(sorted.Count * 0.50), 0, sorted.Count - 1)];
        summary["p95_usec"] = sorted[Math.Clamp((int)(sorted.Count * 0.95), 0, sorted.Count - 1)];
        return summary;
    }

    public static GDictionary MergeRuns(GArray perRunStats)
    {
        var merged = new GDictionary
        {
            ["call_count"] = 0,
            ["total_usec"] = 0L,
            ["max_usec"] = 0L,
            ["samples"] = System.Array.Empty<long>(),
        };
        if (perRunStats == null)
            return merged;

        var samples = new List<long>();
        foreach (Variant runValue in perRunStats)
        {
            if (runValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary runStats = runValue.AsGodotDictionary();
            merged["call_count"] = DictInt(merged, "call_count") + DictInt(runStats, "call_count");
            merged["total_usec"] = DictLong(merged, "total_usec") + DictLong(runStats, "total_usec");
            merged["max_usec"] = Math.Max(DictLong(merged, "max_usec"), DictLong(runStats, "max_usec"));
            long[] runSamples = DictSamples(runStats, "samples");
            foreach (long sample in runSamples)
                samples.Add(sample);
        }
        merged["samples"] = samples.ToArray();
        return merged;
    }

    public static GDictionary BuildBaselineDoc(GDictionary scenarios, string gitCommit) =>
        new()
        {
            ["schema_version"] = SchemaVersion,
            ["godot_version"] = Engine.GetVersionInfo().GetValueOrDefault("string", "unknown"),
            ["generated_at_unix"] = (long)Time.GetUnixTimeFromSystem(),
            ["git_commit"] = gitCommit ?? "unknown",
            ["scenarios"] = scenarios ?? new GDictionary(),
        };

    public static bool WriteBaseline(string path, GDictionary doc)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        string dirPath = path.GetBaseDir();
        if (!DirAccess.DirExistsAbsolute(dirPath))
            DirAccess.MakeDirRecursiveAbsolute(dirPath);
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
            return false;
        file.StoreString(Json.Stringify(doc ?? new GDictionary(), "\t"));
        return true;
    }

    public static GDictionary ReadBaseline(string path)
    {
        if (string.IsNullOrEmpty(path) || !FileAccess.FileExists(path))
            return new GDictionary();
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
            return new GDictionary();
        Variant parsed = Json.ParseString(file.GetAsText());
        return parsed.VariantType == Variant.Type.Dictionary
            ? parsed.AsGodotDictionary()
            : new GDictionary();
    }

    public static GArray Compare(
        GDictionary baseline,
        GDictionary current,
        double tolerancePct,
        IReadOnlyList<string> metrics = null,
        int absoluteToleranceUsec = DefaultAbsoluteToleranceUsec,
        int minMetricCallCount = DefaultMinMetricCallCount,
        int minPercentileCallCount = DefaultMinPercentileCallCount
    )
    {
        string[] metricList =
            metrics != null && metrics.Count > 0
                ? CopyMetrics(metrics)
                : new[] { "avg_usec", "p50_usec", "p95_usec" };
        var diffs = new GArray();
        GDictionary currentScenarios = DictDict(current, "scenarios");
        GDictionary baselineScenarios = DictDict(baseline, "scenarios");
        foreach (Variant scenarioId in currentScenarios.Keys)
        {
            GDictionary currentScenario = DictDict(currentScenarios, scenarioId);
            if (!baselineScenarios.ContainsKey(scenarioId))
            {
                diffs.Add(new GDictionary
                {
                    ["scenario_id"] = scenarioId,
                    ["layer"] = "(scenario)",
                    ["metric"] = "(any)",
                    ["status"] = "missing_in_baseline",
                });
                continue;
            }

            GDictionary baselineScenario = DictDict(baselineScenarios, scenarioId);
            GDictionary currentLayers = DictDict(currentScenario, "layers");
            GDictionary baselineLayers = DictDict(baselineScenario, "layers");
            foreach (Variant layerName in currentLayers.Keys)
            {
                if (!baselineLayers.ContainsKey(layerName))
                {
                    diffs.Add(new GDictionary
                    {
                        ["scenario_id"] = scenarioId,
                        ["layer"] = layerName,
                        ["metric"] = "(any)",
                        ["status"] = "missing_in_baseline",
                    });
                    continue;
                }

                GDictionary currentLayer = DictDict(currentLayers, layerName);
                GDictionary baselineLayer = DictDict(baselineLayers, layerName);
                int baselineCallCount = DictInt(baselineLayer, "call_count");
                int currentCallCount = DictInt(currentLayer, "call_count");
                foreach (string metric in metricList)
                {
                    int baselineValue = DictInt(baselineLayer, metric);
                    int currentValue = DictInt(currentLayer, metric);
                    int absoluteDeltaUsec = currentValue - baselineValue;
                    double deltaPct = baselineValue > 0
                        ? (currentValue - baselineValue) * 100.0 / baselineValue
                        : 0.0;
                    string status = "ok";
                    if (baselineValue <= 0)
                        status = "skipped_zero_baseline";
                    else if (
                        minMetricCallCount > 0
                        && Math.Min(baselineCallCount, currentCallCount) < minMetricCallCount
                    )
                    {
                        status = "low_sample_count";
                    }
                    else if (baselineValue < NoiseFloorUsec)
                        status = "noise_floor";
                    else if (
                        minPercentileCallCount > 0
                        && IsPercentileMetric(metric)
                        && Math.Min(baselineCallCount, currentCallCount) < minPercentileCallCount
                    )
                    {
                        status = "low_sample_count";
                    }
                    else if (absoluteDeltaUsec > 0 && absoluteDeltaUsec <= absoluteToleranceUsec)
                        status = "absolute_tolerance";
                    else if (deltaPct > tolerancePct)
                        status = "regression";

                    diffs.Add(new GDictionary
                    {
                        ["scenario_id"] = scenarioId,
                        ["layer"] = layerName,
                        ["metric"] = metric,
                        ["baseline_usec"] = baselineValue,
                        ["current_usec"] = currentValue,
                        ["baseline_call_count"] = baselineCallCount,
                        ["current_call_count"] = currentCallCount,
                        ["absolute_delta_usec"] = absoluteDeltaUsec,
                        ["delta_pct"] = deltaPct,
                        ["status"] = status,
                    });
                }
            }
        }
        return diffs;
    }

    public static string FormatDiffReport(
        GArray diffs,
        double tolerancePct,
        int absoluteToleranceUsec = DefaultAbsoluteToleranceUsec,
        int minMetricCallCount = DefaultMinMetricCallCount,
        int minPercentileCallCount = DefaultMinPercentileCallCount
    )
    {
        string absoluteToleranceNote =
            absoluteToleranceUsec > 0 ? $" absolute_tolerance={absoluteToleranceUsec}us" : "";
        string metricSampleNote =
            minMetricCallCount > 0 ? $" min_metric_call_count={minMetricCallCount}" : "";
        string percentileSampleNote =
            minPercentileCallCount > 0
                ? $" min_percentile_call_count={minPercentileCallCount}"
                : "";
        var lines = new List<string>
        {
            $"[BASELINE_DIFF] tolerance=±{tolerancePct:F1}%{absoluteToleranceNote}{metricSampleNote}{percentileSampleNote}",
        };
        foreach (Variant diffValue in diffs ?? new GArray())
        {
            if (diffValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary diff = diffValue.AsGodotDictionary();
            lines.Add(
                $"  [{DictString(diff, "status")}] {DictString(diff, "scenario_id")}/{DictString(diff, "layer")}/{DictString(diff, "metric")} "
                    + $"baseline={DictInt(diff, "baseline_usec")}us current={DictInt(diff, "current_usec")}us "
                    + $"calls={DictInt(diff, "baseline_call_count")}/{DictInt(diff, "current_call_count")} "
                    + $"abs_delta={DictInt(diff, "absolute_delta_usec")}us delta={DictDouble(diff, "delta_pct"):+0.0;-0.0;0.0}%"
            );
        }
        return string.Join("\n", lines);
    }

    public static int CountRegressions(GArray diffs)
    {
        int count = 0;
        foreach (Variant diffValue in diffs ?? new GArray())
        {
            if (
                diffValue.VariantType == Variant.Type.Dictionary
                && DictString(diffValue.AsGodotDictionary(), "status") == "regression"
            )
            {
                count++;
            }
        }
        return count;
    }

    private static GDictionary DictDict(GDictionary dict, Variant key)
    {
        if (dict != null && dict.ContainsKey(key) && dict[key].VariantType == Variant.Type.Dictionary)
            return dict[key].AsGodotDictionary();
        return new GDictionary();
    }

    private static int DictInt(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) ? (int)dict[key].AsInt64() : 0;

    private static long DictLong(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) ? dict[key].AsInt64() : 0L;

    private static double DictDouble(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) ? dict[key].AsDouble() : 0.0;

    private static string DictString(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) ? dict[key].AsString() : "";

    private static string[] CopyMetrics(IReadOnlyList<string> metrics)
    {
        var result = new List<string>();
        foreach (string metric in metrics)
        {
            string trimmed = metric?.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                result.Add(trimmed);
        }
        return result.Count > 0 ? result.ToArray() : new[] { "avg_usec", "p50_usec", "p95_usec" };
    }

    private static bool IsPercentileMetric(string metric) =>
        !string.IsNullOrEmpty(metric) && metric.StartsWith("p", StringComparison.OrdinalIgnoreCase);

    private static long[] DictSamples(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) && dict[key].VariantType == Variant.Type.PackedInt64Array
            ? dict[key].AsInt64Array()
            : System.Array.Empty<long>();
}
