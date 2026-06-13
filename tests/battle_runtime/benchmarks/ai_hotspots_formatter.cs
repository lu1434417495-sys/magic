using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class AiHotspotsFormatter
{
    private readonly record struct Entry(
        string Name,
        int NCalls,
        long SelfUsec,
        long TotalUsec,
        long MaxUsec
    );

    public static string FormatTopN(
        GDictionary funcStats,
        string sortBy = "self_usec",
        int topN = 20,
        string nameFilter = ""
    )
    {
        var entries = new List<Entry>();
        foreach (Variant nameValue in funcStats?.Keys ?? new GArray())
        {
            string name = nameValue.AsString();
            if (!string.IsNullOrEmpty(nameFilter) && !name.Contains(nameFilter, StringComparison.Ordinal))
                continue;
            GDictionary stats = DictDict(funcStats, nameValue);
            entries.Add(
                new Entry(
                    name,
                    DictInt(stats, "ncalls"),
                    DictLong(stats, "self_usec"),
                    DictLong(stats, "total_usec"),
                    DictLong(stats, "max_usec")
                )
            );
        }

        entries.Sort((left, right) => SortMetric(right, sortBy).CompareTo(SortMetric(left, sortBy)));

        var lines = new List<string>
        {
            "ncalls       tottime(ms)   percall(us)   cumtime(ms)   percall(us)   maxcall(us)   function",
        };
        int displayed = 0;
        foreach (Entry entry in entries)
        {
            if (displayed >= Math.Max(topN, 1))
                break;
            displayed++;
            double selfPerCall = entry.NCalls > 0 ? entry.SelfUsec / (double)entry.NCalls : 0.0;
            double totalPerCall = entry.NCalls > 0 ? entry.TotalUsec / (double)entry.NCalls : 0.0;
            lines.Add(
                $"{entry.NCalls,8}   {entry.SelfUsec / 1000.0,11:F3}   {selfPerCall,11:F1}   {entry.TotalUsec / 1000.0,11:F3}   {totalPerCall,11:F1}   {entry.MaxUsec,11}   {entry.Name}"
            );
        }
        if (entries.Count > displayed)
            lines.Add($"... ({entries.Count - displayed} more)");
        return string.Join("\n", lines);
    }

    public static string FormatHeader(
        string scenarioId,
        int aiTurns,
        long totalSelfUsec,
        string sortBy,
        string godotVersion,
        string gitCommit
    ) =>
        $"=== AI Profile · {scenarioId} · godot={godotVersion} · commit={gitCommit} ===\n"
        + $"ai_turns={aiTurns}  total_self_usec={totalSelfUsec / 1000.0:F3} ms  sort={sortBy}\n";

    public static bool WriteCsv(string path, GDictionary funcStats)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        string dirPath = path.GetBaseDir();
        if (!DirAccess.DirExistsAbsolute(dirPath))
            DirAccess.MakeDirRecursiveAbsolute(dirPath);
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
            return false;

        file.StoreLine("function,ncalls,self_usec,total_usec,max_usec,self_per_call_usec,total_per_call_usec");
        var names = new List<Variant>();
        foreach (Variant name in funcStats?.Keys ?? new GArray())
            names.Add(name);
        names.Sort(
            (left, right) =>
                DictLong(DictDict(funcStats, right), "self_usec")
                    .CompareTo(DictLong(DictDict(funcStats, left), "self_usec"))
        );

        foreach (Variant name in names)
        {
            GDictionary stats = DictDict(funcStats, name);
            int nCalls = DictInt(stats, "ncalls");
            long selfUsec = DictLong(stats, "self_usec");
            long totalUsec = DictLong(stats, "total_usec");
            long maxUsec = DictLong(stats, "max_usec");
            double selfPerCall = nCalls > 0 ? selfUsec / (double)nCalls : 0.0;
            double totalPerCall = nCalls > 0 ? totalUsec / (double)nCalls : 0.0;
            file.StoreLine(
                $"{Csv(name.AsString())},{nCalls},{selfUsec},{totalUsec},{maxUsec},{selfPerCall:F2},{totalPerCall:F2}"
            );
        }
        return true;
    }

    public static bool WriteTextReport(string path, string header, string body)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        string dirPath = path.GetBaseDir();
        if (!DirAccess.DirExistsAbsolute(dirPath))
            DirAccess.MakeDirRecursiveAbsolute(dirPath);
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
            return false;
        file.StoreString(header ?? "");
        file.StoreString(body ?? "");
        return true;
    }

    public static long TotalSelfUsec(GDictionary funcStats)
    {
        long sum = 0;
        foreach (Variant statsValue in funcStats?.Values ?? new GArray())
        {
            if (statsValue.VariantType == Variant.Type.Dictionary)
                sum += DictLong(statsValue.AsGodotDictionary(), "self_usec");
        }
        return sum;
    }

    private static long SortMetric(Entry entry, string sortBy) =>
        sortBy switch
        {
            "total_usec" => entry.TotalUsec,
            "max_usec" => entry.MaxUsec,
            "ncalls" => entry.NCalls,
            _ => entry.SelfUsec,
        };

    private static string Csv(string value)
    {
        value ??= "";
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static GDictionary DictDict(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) && dict[key].VariantType == Variant.Type.Dictionary
            ? dict[key].AsGodotDictionary()
            : new GDictionary();

    private static int DictInt(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) ? (int)dict[key].AsInt64() : 0;

    private static long DictLong(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) ? dict[key].AsInt64() : 0L;
}
