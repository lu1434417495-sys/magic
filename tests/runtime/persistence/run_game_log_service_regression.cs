using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_game_log_service_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestGameLogServiceKeepsRingBufferWithoutDefaultFileOutput();
        TestGameLogServiceCanAppendOptInFile();

        Quit(_test.Finish("Game log service regression"));
    }

    private void TestGameLogServiceKeepsRingBufferWithoutDefaultFileOutput()
    {
        GameLogService logService = new();
        logService.Setup(3, false);
        string virtualPath = logService.GetVirtualLogPath();
        string absolutePath = logService.GetLogPath();

        _test.True(string.IsNullOrEmpty(virtualPath), "日志服务默认不应初始化虚拟日志路径。");
        _test.True(string.IsNullOrEmpty(absolutePath), "日志服务默认不应初始化绝对日志路径。");

        AppendFourEntries(logService);

        GArray recentEntries = logService.GetRecentEntries(10);
        _test.Eq(recentEntries.Count, 3, "ring buffer 应只保留最近 3 条内存日志。");
        if (recentEntries.Count == 3)
        {
            GDictionary firstEntry = recentEntries[0].AsGodotDictionary();
            GDictionary lastEntry = recentEntries[2].AsGodotDictionary();
            _test.Eq(DictInt(firstEntry, "seq", 0), 2, "ring buffer 应丢弃最早一条日志。");
            _test.Eq(DictString(lastEntry, "message", ""), "fourth", "最后一条内存日志应保留最新消息。");
        }

        GDictionary snapshot = logService.BuildSnapshot(10);
        _test.Eq(DictString(snapshot, "virtual_path", ""), virtualPath, "日志快照应返回当前虚拟路径。");
        _test.Eq(DictString(snapshot, "file_path", ""), "", "默认日志快照不应暴露文件路径。");
        _test.False(DictBool(snapshot, "file_output_enabled", true), "日志文件输出默认应关闭。");
        _test.False(DictBool(snapshot, "file_write_active", true), "日志文件写入默认不应处于 active 状态。");
        _test.Eq(DictInt(snapshot, "entry_count", 0), 3, "日志快照 entry_count 应匹配当前内存缓冲。");
    }

    private void TestGameLogServiceCanAppendOptInFile()
    {
        GameLogService logService = new();
        logService.Setup(3, true);
        string virtualPath = logService.GetVirtualLogPath();
        string absolutePath = logService.GetLogPath();

        _test.False(string.IsNullOrEmpty(virtualPath), "显式开启文件输出时，日志服务应初始化虚拟日志路径。");
        _test.False(string.IsNullOrEmpty(absolutePath), "显式开启文件输出时，日志服务应初始化绝对日志路径。");

        AppendFourEntries(logService);

        GDictionary snapshot = logService.BuildSnapshot(10);
        _test.True(DictBool(snapshot, "file_output_enabled", false), "显式开启时日志快照应标记文件输出启用。");
        _test.True(DictBool(snapshot, "file_write_active", false), "显式开启时日志文件写入应处于 active 状态。");

        List<string> lines = ReadNonEmptyLines(virtualPath);
        _test.Eq(lines.Count, 4, "jsonl 文件应追加所有写入日志，而不仅是 ring buffer。");
        if (lines.Count == 4)
        {
            GDictionary lastEntry = ParseDictionaryLine(lines[3], "日志文件中的每一行都应是合法 JSON。");
            _test.Eq(DictString(lastEntry, "event_id", ""), "battle.test.fourth", "日志文件应按顺序追加最新事件。");
            _test.Eq(DictString(lastEntry, "level", ""), "error", "日志文件应保留日志级别。");
            _test.False(string.IsNullOrEmpty(DictString(lastEntry, "time_text", "")), "日志文件应额外保留可读时间文本。");
        }

        CleanupLogFile(absolutePath);
    }

    private static void AppendFourEntries(GameLogService logService)
    {
        logService.AppendEntry("info", "session", "session.test.first", "first", "{\"step\":1}");
        logService.AppendEntry("info", "world", "world.test.second", "second", "{\"step\":2}");
        logService.AppendEntry("warn", "world", "world.test.third", "third", "{\"step\":3}");
        logService.AppendEntry("error", "battle", "battle.test.fourth", "fourth", "{\"step\":4}");
    }

    private List<string> ReadNonEmptyLines(string virtualPath)
    {
        List<string> result = new();
        using FileAccess file = FileAccess.Open(virtualPath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            return result;
        }

        string contents = file.GetAsText();
        foreach (string line in contents.Split('\n'))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }
            result.Add(trimmed);
        }
        return result;
    }

    private GDictionary ParseDictionaryLine(string line, string message)
    {
        var json = new Json();
        Error error = json.Parse(line);
        if (error != Error.Ok)
        {
            _test.Fail($"{message} | parse_error={error}");
            return new GDictionary();
        }

        Variant data = json.Data;
        if (data.VariantType != Variant.Type.Dictionary)
        {
            _test.Fail($"{message} | parsed_type={data.VariantType}");
            return new GDictionary();
        }
        return data.AsGodotDictionary();
    }

    private static void CleanupLogFile(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
        {
            return;
        }
        if (FileAccess.FileExists(absolutePath))
        {
            DirAccess.RemoveAbsolute(absolutePath);
        }
    }

    private static string DictString(GDictionary dictionary, string key, string fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsString()
            : fallback;
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsInt32()
            : fallback;
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsBool()
            : fallback;
    }
}
