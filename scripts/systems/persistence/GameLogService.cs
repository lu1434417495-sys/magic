using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
internal partial class GameLogService : RefCounted
{
    [Signal]
    public delegate void EntryAddedEventHandler(GDictionary entry);

    private const string LogDirectory = "user://logs";
    private const int DefaultBufferLimit = 400;
    private const int DefaultTailLimit = 50;
    private const bool DefaultFileOutputEnabled = false;

    private readonly List<GameLogEntry> _entries = new();
    private int _maxEntries = DefaultBufferLimit;
    private int _nextSeq = 1;
    private string _sessionLogVirtualPath = "";
    private bool _fileOutputEnabled = DefaultFileOutputEnabled;
    private bool _writeEnabled;

    internal GameLogService()
    {
        Initialize(DefaultBufferLimit, DefaultFileOutputEnabled);
    }

    internal void Setup(int maxEntries, bool fileOutputEnabled)
    {
        _entries.Clear();
        _nextSeq = 1;
        Initialize(maxEntries, fileOutputEnabled);
    }

    internal GDictionary AppendEntry(
        string level,
        string domain,
        string event_id,
        string message,
        string context = ""
    )
    {
        long timestampMs = (long)(Time.GetUnixTimeFromSystem() * 1000.0);
        GameLogEntry entry = new(
            _nextSeq,
            timestampMs,
            FormatUnixTimeMs(timestampMs),
            string.IsNullOrEmpty(level) ? "info" : level,
            string.IsNullOrEmpty(domain) ? "runtime" : domain,
            event_id ?? "",
            message ?? "",
            context ?? ""
        );
        _nextSeq += 1;
        _entries.Add(entry);
        if (_entries.Count > _maxEntries)
        {
            _entries.RemoveAt(0);
        }
        AppendToFile(entry);
        GDictionary emittedEntry = entry.ToDictionary();
        EmitSignal(SignalName.EntryAdded, emittedEntry);
        return DuplicateDictionary(entry);
    }

    internal GArray GetRecentEntries(int limit = DefaultTailLimit)
    {
        int resolvedLimit = Math.Max(limit, 0);
        int startIndex = Math.Max(_entries.Count - resolvedLimit, 0);
        var result = new GArray();
        for (int index = startIndex; index < _entries.Count; index++)
        {
            result.Add(_entries[index].ToDictionary());
        }
        return result;
    }

    internal GDictionary BuildSnapshot(int limit = DefaultTailLimit)
    {
        return new GDictionary
        {
            ["file_path"] = GetLogPath(),
            ["virtual_path"] = _sessionLogVirtualPath,
            ["file_output_enabled"] = _fileOutputEnabled,
            ["file_write_active"] = _writeEnabled,
            ["entry_count"] = _entries.Count,
            ["buffer_limit"] = _maxEntries,
            ["entries"] = GetRecentEntries(limit),
        };
    }

    internal void StartNewSession()
    {
        ClearEntries();
        _nextSeq = 1;
        StartFileSessionIfEnabled();
    }

    internal void ClearEntries()
    {
        _entries.Clear();
    }

    internal string GetLogPath()
    {
        return string.IsNullOrEmpty(_sessionLogVirtualPath)
            ? ""
            : ProjectSettings.GlobalizePath(_sessionLogVirtualPath);
    }

    internal string GetVirtualLogPath()
    {
        return _sessionLogVirtualPath;
    }

    internal void SetFileOutputEnabled(bool enabled)
    {
        if (_fileOutputEnabled == enabled)
        {
            return;
        }
        _fileOutputEnabled = enabled;
        StartFileSessionIfEnabled();
    }

    internal bool IsFileOutputEnabled()
    {
        return _fileOutputEnabled;
    }

    private void Initialize(int maxEntries, bool fileOutputEnabled)
    {
        _maxEntries = Math.Max(maxEntries, 1);
        _fileOutputEnabled = fileOutputEnabled;
        StartFileSessionIfEnabled();
    }

    private static string BuildSessionLogVirtualPath()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        long timestampMs = (long)(Time.GetUnixTimeFromSystem() * 1000.0);
        return $"{LogDirectory}/session_{timestampMs}_{rng.RandiRange(0, 999999):D6}.jsonl";
    }

    private void StartFileSessionIfEnabled()
    {
        _writeEnabled = false;
        _sessionLogVirtualPath = "";
        if (!_fileOutputEnabled)
        {
            return;
        }
        _sessionLogVirtualPath = BuildSessionLogVirtualPath();
        InitializeLogFile();
    }

    private void InitializeLogFile()
    {
        if (!_fileOutputEnabled || string.IsNullOrEmpty(_sessionLogVirtualPath))
        {
            return;
        }
        Error ensureDirError = DirAccess.MakeDirRecursiveAbsolute(
            ProjectSettings.GlobalizePath(LogDirectory)
        );
        if (ensureDirError != Error.Ok)
        {
            DisableFileWrite(
                $"Failed to create log directory {LogDirectory}. Error: {(int)ensureDirError}"
            );
            return;
        }
        using var file = FileAccess.Open(_sessionLogVirtualPath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            DisableFileWrite(
                $"Failed to initialize log file {_sessionLogVirtualPath}. Error: {(int)FileAccess.GetOpenError()}"
            );
            return;
        }
        _writeEnabled = true;
    }

    private void AppendToFile(GameLogEntry entry)
    {
        if (!_writeEnabled || string.IsNullOrEmpty(_sessionLogVirtualPath))
        {
            return;
        }
        using var file = FileAccess.Open(_sessionLogVirtualPath, FileAccess.ModeFlags.ReadWrite);
        if (file == null)
        {
            DisableFileWrite(
                $"Failed to append log file {_sessionLogVirtualPath}. Error: {(int)FileAccess.GetOpenError()}"
            );
            return;
        }
        file.SeekEnd();
        file.StoreLine(Json.Stringify(entry.ToDictionary()));
    }

    private void DisableFileWrite(string message)
    {
        _writeEnabled = false;
        GameLog.Warning(message, "log.file.disabled", "log");
    }

    private static string FormatUnixTimeMs(long unixTimeMs)
    {
        if (unixTimeMs <= 0)
        {
            return "";
        }
        long unixTimeSeconds = unixTimeMs / 1000;
        GDictionary datetime = Time.GetDatetimeDictFromUnixTime(unixTimeSeconds);
        return string.Format(
            "{0:D4}-{1:D2}-{2:D2} {3:D2}:{4:D2}:{5:D2}.{6:D3}",
            GetInt(datetime, "year", 1970),
            GetInt(datetime, "month", 1),
            GetInt(datetime, "day", 1),
            GetInt(datetime, "hour", 0),
            GetInt(datetime, "minute", 0),
            GetInt(datetime, "second", 0),
            MathMod(unixTimeMs, 1000)
        );
    }

    private static GDictionary DuplicateDictionary(GameLogEntry value)
    {
        return value?.ToDictionary() ?? new GDictionary();
    }

    private static GDictionary DuplicateDictionary(GDictionary value)
    {
        return value?.Duplicate(true) ?? new GDictionary();
    }

    private static int GetInt(GDictionary source, string key, int fallback)
    {
        return source.ContainsKey(key) ? source[key].AsInt32() : fallback;
    }

    private static long MathMod(long value, long modulus)
    {
        long result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private sealed class GameLogEntry
    {
        public GameLogEntry(
            int seq,
            long timeUnixMs,
            string timeText,
            string level,
            string domain,
            string eventId,
            string message,
            string context
        )
        {
            Seq = seq;
            TimeUnixMs = timeUnixMs;
            TimeText = timeText ?? "";
            Level = level ?? "";
            Domain = domain ?? "";
            EventId = eventId ?? "";
            Message = message ?? "";
            Context = context ?? "";
        }

        public int Seq { get; }
        public long TimeUnixMs { get; }
        public string TimeText { get; }
        public string Level { get; }
        public string Domain { get; }
        public string EventId { get; }
        public string Message { get; }
        public string Context { get; }

        internal GDictionary ToDictionary()
        {
            return new GDictionary
            {
                ["seq"] = Seq,
                ["time_unix_ms"] = TimeUnixMs,
                ["time_text"] = TimeText,
                ["level"] = Level,
                ["domain"] = Domain,
                ["event_id"] = EventId,
                ["message"] = Message,
                ["context"] = Context,
            };
        }
    }
}
