using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_game_log_service_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestGameLogServiceKeepsRingBufferWithoutDefaultFileOutput();
        TestGameLogServiceCanAppendOptInFile();
        TestGameLogServiceAcceptsConcurrentAppends();
        TestGameLogDispatcherFiltersAndIsolatesSinks();

        RequestTestExit(_test.Finish("Game log module regression"));
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

        IReadOnlyList<IReadOnlyDictionary<string, object>> recentEntries =
            logService.GetRecentEntriesPlain(10);
        _test.Eq(recentEntries.Count, 3, "ring buffer 应只保留最近 3 条内存日志。");
        if (recentEntries.Count == 3)
        {
            IReadOnlyDictionary<string, object> firstEntry = recentEntries[0];
            IReadOnlyDictionary<string, object> lastEntry = recentEntries[2];
            _test.Eq(PlainInt(firstEntry, "seq", 0), 2, "ring buffer 应丢弃最早一条日志。");
            _test.Eq(PlainString(lastEntry, "message", ""), "fourth", "最后一条内存日志应保留最新消息。");
        }

        IReadOnlyDictionary<string, object> snapshot = logService.BuildSnapshotPlain(10);
        _test.Eq(PlainString(snapshot, "virtual_path", ""), virtualPath, "日志快照应返回当前虚拟路径。");
        _test.Eq(PlainString(snapshot, "file_path", ""), "", "默认日志快照不应暴露文件路径。");
        _test.False(PlainBool(snapshot, "file_output_enabled", true), "日志文件输出默认应关闭。");
        _test.False(PlainBool(snapshot, "file_write_active", true), "日志文件写入默认不应处于 active 状态。");
        _test.Eq(PlainInt(snapshot, "entry_count", 0), 3, "日志快照 entry_count 应匹配当前内存缓冲。");
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

        IReadOnlyDictionary<string, object> snapshot = logService.BuildSnapshotPlain(10);
        _test.True(PlainBool(snapshot, "file_output_enabled", false), "显式开启时日志快照应标记文件输出启用。");
        _test.True(PlainBool(snapshot, "file_write_active", false), "显式开启时日志文件写入应处于 active 状态。");

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
        logService.AppendEntry(
            new GameLogRecord(
                GameLogLevel.Info,
                "session.test.first",
                "session",
                "first",
                "{\"step\":1}"
            )
        );
        logService.AppendEntry(
            new GameLogRecord(
                GameLogLevel.Info,
                "world.test.second",
                "world",
                "second",
                "{\"step\":2}"
            )
        );
        logService.AppendEntry(
            new GameLogRecord(
                GameLogLevel.Warning,
                "world.test.third",
                "world",
                "third",
                "{\"step\":3}"
            )
        );
        logService.AppendEntry(
            new GameLogRecord(
                GameLogLevel.Error,
                "battle.test.fourth",
                "battle",
                "fourth",
                "{\"step\":4}"
            )
        );
    }

    private void TestGameLogServiceAcceptsConcurrentAppends()
    {
        const int EntryCount = 512;
        GameLogService logService = new();
        logService.Setup(EntryCount, false);

        Parallel.For(
            0,
            EntryCount,
            index =>
                logService.AppendEntry(
                    new GameLogRecord(
                        GameLogLevel.Info,
                        $"concurrent.{index}",
                        "test",
                        $"entry-{index}"
                    )
                )
        );

        IReadOnlyList<IReadOnlyDictionary<string, object>> entries =
            logService.GetRecentEntriesPlain(EntryCount);
        _test.Eq(entries.Count, EntryCount, "并发日志写入不应丢失内存条目。");
        if (entries.Count == EntryCount)
        {
            _test.Eq(PlainInt(entries[0], "seq", 0), 1, "并发日志序号应从 1 开始。");
            _test.Eq(
                PlainInt(entries[^1], "seq", 0),
                EntryCount,
                "并发日志序号应保持连续。"
            );
        }
    }

    private void TestGameLogDispatcherFiltersAndIsolatesSinks()
    {
        int baselineSinkCount = GameLog.SinkCount;
        GameLogLevel previousMinimumLevel = GameLog.MinimumLevel;
        bool previousConsoleOutputEnabled = GameLog.IsConsoleOutputEnabled;
        var throwingSink = new ThrowingLogSink();
        var recordingSink = new RecordingLogSink();

        try
        {
            GameLog.MinimumLevel = GameLogLevel.Info;
            GameLog.IsConsoleOutputEnabled = false;
            GameLog.AddSink(throwingSink);
            GameLog.AddSink(recordingSink);
            GameLog.AddSink(recordingSink);

            _test.Eq(
                GameLog.SinkCount,
                baselineSinkCount + 2,
                "重复注册同一个日志 sink 不应产生重复订阅。"
            );

            GameLog.Debug("filtered", "log.dispatch.debug", "test");
            GameLog.Warning("dispatch", "log.dispatch.test", "test", "{\"step\":1}");

            _test.Eq(recordingSink.Records.Count, 1, "关闭 debug 时只应派发 warning 记录。");
            if (recordingSink.Records.Count == 1)
            {
                GameLogRecord record = recordingSink.Records[0];
                _test.Eq(record.Level, GameLogLevel.Warning, "sink 应收到 typed level。");
                _test.Eq(record.EventId, "log.dispatch.test", "sink 应收到稳定 event_id。");
                _test.Eq(record.Domain, "test", "sink 应收到 domain。");
                _test.Eq(record.Context, "{\"step\":1}", "sink 应收到结构化 context 文本。");
                _test.True(
                    GameLog
                        .FormatForConsole(record)
                        .EndsWith(
                            "[WARN] [test] [log.dispatch.test] dispatch | context={\"step\":1}",
                            StringComparison.Ordinal
                        ),
                    "C# console 输出应包含 UTC 时间、level、domain、event_id 与 context。"
                );
            }
        }
        finally
        {
            GameLog.RemoveSink(recordingSink);
            GameLog.RemoveSink(throwingSink);
            GameLog.MinimumLevel = previousMinimumLevel;
            GameLog.IsConsoleOutputEnabled = previousConsoleOutputEnabled;
        }

        _test.Eq(GameLog.SinkCount, baselineSinkCount, "日志回归结束后应恢复 sink 基线。");
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

    private static string PlainString(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        string fallback
    )
    {
        return dictionary != null
            && dictionary.TryGetValue(key, out object value)
            && value is string text
                ? text
                : fallback;
    }

    private static int PlainInt(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        int fallback
    )
    {
        return dictionary != null
            && dictionary.TryGetValue(key, out object value)
            && value is int number
                ? number
                : fallback;
    }

    private static bool PlainBool(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        bool fallback
    )
    {
        return dictionary != null
            && dictionary.TryGetValue(key, out object value)
            && value is bool flag
                ? flag
                : fallback;
    }

    private sealed class RecordingLogSink : IGameLogSink
    {
        internal List<GameLogRecord> Records { get; } = new();

        public void Write(GameLogRecord record)
        {
            Records.Add(record);
        }
    }

    private sealed class ThrowingLogSink : IGameLogSink
    {
        public void Write(GameLogRecord record)
        {
            throw new InvalidOperationException("expected sink failure");
        }
    }
}
