using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_game_log_service_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestGameLogServiceKeepsRingBufferWithoutDefaultFileOutput();
        TestGameLogServiceCanAppendOptInFile();

        if (_failures.Count == 0)
        {
            GD.Print("Game log service regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Game log service regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestGameLogServiceKeepsRingBufferWithoutDefaultFileOutput()
    {
        GameLogService logService = new();
        logService.setup(3, false);
        string virtualPath = logService.get_virtual_log_path();
        string absolutePath = logService.get_log_path();

        AssertTrue(string.IsNullOrEmpty(virtualPath), "日志服务默认不应初始化虚拟日志路径。");
        AssertTrue(string.IsNullOrEmpty(absolutePath), "日志服务默认不应初始化绝对日志路径。");

        AppendFourEntries(logService);

        GArray recentEntries = logService.get_recent_entries(10);
        AssertEq(recentEntries.Count, 3, "ring buffer 应只保留最近 3 条内存日志。");
        if (recentEntries.Count == 3)
        {
            GDictionary firstEntry = recentEntries[0].AsGodotDictionary();
            GDictionary lastEntry = recentEntries[2].AsGodotDictionary();
            AssertEq(DictInt(firstEntry, "seq", 0), 2, "ring buffer 应丢弃最早一条日志。");
            AssertEq(DictString(lastEntry, "message", ""), "fourth", "最后一条内存日志应保留最新消息。");
        }

        GDictionary snapshot = logService.build_snapshot(10);
        AssertEq(DictString(snapshot, "virtual_path", ""), virtualPath, "日志快照应返回当前虚拟路径。");
        AssertEq(DictString(snapshot, "file_path", ""), "", "默认日志快照不应暴露文件路径。");
        AssertFalse(DictBool(snapshot, "file_output_enabled", true), "日志文件输出默认应关闭。");
        AssertFalse(DictBool(snapshot, "file_write_active", true), "日志文件写入默认不应处于 active 状态。");
        AssertEq(DictInt(snapshot, "entry_count", 0), 3, "日志快照 entry_count 应匹配当前内存缓冲。");
    }

    private void TestGameLogServiceCanAppendOptInFile()
    {
        GameLogService logService = new();
        logService.setup(3, true);
        string virtualPath = logService.get_virtual_log_path();
        string absolutePath = logService.get_log_path();

        AssertFalse(string.IsNullOrEmpty(virtualPath), "显式开启文件输出时，日志服务应初始化虚拟日志路径。");
        AssertFalse(string.IsNullOrEmpty(absolutePath), "显式开启文件输出时，日志服务应初始化绝对日志路径。");

        AppendFourEntries(logService);

        GDictionary snapshot = logService.build_snapshot(10);
        AssertTrue(DictBool(snapshot, "file_output_enabled", false), "显式开启时日志快照应标记文件输出启用。");
        AssertTrue(DictBool(snapshot, "file_write_active", false), "显式开启时日志文件写入应处于 active 状态。");

        List<string> lines = ReadNonEmptyLines(virtualPath);
        AssertEq(lines.Count, 4, "jsonl 文件应追加所有写入日志，而不仅是 ring buffer。");
        if (lines.Count == 4)
        {
            GDictionary lastEntry = ParseDictionaryLine(lines[3], "日志文件中的每一行都应是合法 JSON。");
            AssertEq(DictString(lastEntry, "event_id", ""), "battle.test.fourth", "日志文件应按顺序追加最新事件。");
            AssertEq(DictString(lastEntry, "level", ""), "error", "日志文件应保留日志级别。");
            AssertFalse(string.IsNullOrEmpty(DictString(lastEntry, "time_text", "")), "日志文件应额外保留可读时间文本。");
        }

        CleanupLogFile(absolutePath);
    }

    private static void AppendFourEntries(GameLogService logService)
    {
        logService.append_entry("info", "session", "session.test.first", "first", "{\"step\":1}");
        logService.append_entry("info", "world", "world.test.second", "second", "{\"step\":2}");
        logService.append_entry("warn", "world", "world.test.third", "third", "{\"step\":3}");
        logService.append_entry("error", "battle", "battle.test.fourth", "fourth", "{\"step\":4}");
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
            _failures.Add($"{message} | parse_error={error}");
            return new GDictionary();
        }

        Variant data = json.Data;
        if (data.VariantType != Variant.Type.Dictionary)
        {
            _failures.Add($"{message} | parsed_type={data.VariantType}");
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

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}
