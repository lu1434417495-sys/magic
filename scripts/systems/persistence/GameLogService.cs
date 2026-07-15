using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal class GameLogService
{
    private const string LogDirectory = "user://logs";
    private const int DefaultBufferLimit = 400;
    private const int DefaultTailLimit = 50;
    private const bool DefaultFileOutputEnabled = false;

    private readonly object _sync = new();
    private readonly List<StoredGameLogEntry> _entries = new();
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
        lock (_sync)
        {
            _entries.Clear();
            _nextSeq = 1;
            Initialize(maxEntries, fileOutputEnabled);
        }
    }

    internal void AppendEntry(GameLogRecord record)
    {
        lock (_sync)
        {
            long timestampMs = record.TimestampUnixMs > 0
                ? record.TimestampUnixMs
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            StoredGameLogEntry entry = new(
                _nextSeq,
                timestampMs,
                FormatUnixTimeMs(timestampMs),
                record.LevelText,
                record.Domain,
                record.EventId,
                record.Message,
                record.Context
            );
            _nextSeq += 1;
            _entries.Add(entry);
            if (_entries.Count > _maxEntries)
                _entries.RemoveAt(0);
            AppendToFile(entry);
        }
    }

    internal IReadOnlyList<IReadOnlyDictionary<string, object>> GetRecentEntriesPlain(
        int limit = DefaultTailLimit
    )
    {
        lock (_sync)
            return BuildRecentEntriesPlainLocked(limit);
    }

    private IReadOnlyList<IReadOnlyDictionary<string, object>> BuildRecentEntriesPlainLocked(
        int limit
    )
    {
        int resolvedLimit = Math.Max(limit, 0);
        int startIndex = Math.Max(_entries.Count - resolvedLimit, 0);
        var result = new List<IReadOnlyDictionary<string, object>>(
            _entries.Count - startIndex
        );
        for (int index = startIndex; index < _entries.Count; index++)
        {
            result.Add(_entries[index].BuildFactsPlain());
        }
        return result.AsReadOnly();
    }

    internal IReadOnlyDictionary<string, object> BuildSnapshotPlain(
        int limit = DefaultTailLimit
    )
    {
        lock (_sync)
        {
            return new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["file_path"] = BuildLogPathLocked(),
                    ["virtual_path"] = _sessionLogVirtualPath,
                    ["file_output_enabled"] = _fileOutputEnabled,
                    ["file_write_active"] = _writeEnabled,
                    ["entry_count"] = _entries.Count,
                    ["buffer_limit"] = _maxEntries,
                    ["entries"] = BuildRecentEntriesPlainLocked(limit),
                }
            );
        }
    }

    internal void StartNewSession()
    {
        lock (_sync)
        {
            _entries.Clear();
            _nextSeq = 1;
            StartFileSessionIfEnabled();
        }
    }

    internal void ClearEntries()
    {
        lock (_sync)
            _entries.Clear();
    }

    internal string GetLogPath()
    {
        lock (_sync)
            return BuildLogPathLocked();
    }

    internal string GetVirtualLogPath()
    {
        lock (_sync)
            return _sessionLogVirtualPath;
    }

    internal void SetFileOutputEnabled(bool enabled)
    {
        lock (_sync)
        {
            if (_fileOutputEnabled == enabled)
                return;
            _fileOutputEnabled = enabled;
            StartFileSessionIfEnabled();
        }
    }

    internal bool IsFileOutputEnabled()
    {
        lock (_sync)
            return _fileOutputEnabled;
    }

    private string BuildLogPathLocked() =>
        string.IsNullOrEmpty(_sessionLogVirtualPath)
            ? ""
            : ProjectSettings.GlobalizePath(_sessionLogVirtualPath);

    private void Initialize(int maxEntries, bool fileOutputEnabled)
    {
        _maxEntries = Math.Max(maxEntries, 1);
        _fileOutputEnabled = fileOutputEnabled;
        StartFileSessionIfEnabled();
    }

    private static string BuildSessionLogVirtualPath()
    {
        long timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int suffix = TrueRandomSeedService.RandiRange(0, 999999);
        return $"{LogDirectory}/session_{timestampMs}_{suffix:D6}.jsonl";
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
        FileAccess file = FileAccess.Open(_sessionLogVirtualPath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            DisableFileWrite(
                $"Failed to initialize log file {_sessionLogVirtualPath}. Error: {(int)FileAccess.GetOpenError()}"
            );
            return;
        }
        try
        {
            _writeEnabled = true;
        }
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(file);
        }
    }

    private void AppendToFile(StoredGameLogEntry entry)
    {
        if (!_writeEnabled || string.IsNullOrEmpty(_sessionLogVirtualPath))
        {
            return;
        }
        FileAccess file = FileAccess.Open(_sessionLogVirtualPath, FileAccess.ModeFlags.ReadWrite);
        if (file == null)
        {
            DisableFileWrite(
                $"Failed to append log file {_sessionLogVirtualPath}. Error: {(int)FileAccess.GetOpenError()}"
            );
            return;
        }
        try
        {
            file.SeekEnd();
            using GodotProjectionLease<GDictionary> entryLease =
                RuntimePlainPayload.ProjectDictionaryLease(
                    entry.BuildFactsPlain(),
                    "game-log-file-entry",
                    LifetimeDomain.Request,
                    "GameLogService.AppendToFile"
                );
            file.StoreLine(Json.Stringify(entryLease.Value));
        }
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(file);
        }
    }

    private void DisableFileWrite(string message)
    {
        _writeEnabled = false;
        GameLog.Warning(message, "log.file.disabled", "log");
    }

    private static string FormatUnixTimeMs(long unixTimeMs)
    {
        if (unixTimeMs <= 0)
            return "";
        return DateTimeOffset
            .FromUnixTimeMilliseconds(unixTimeMs)
            .UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    private sealed class StoredGameLogEntry
    {
        public StoredGameLogEntry(
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

        internal IReadOnlyDictionary<string, object> BuildFactsPlain()
        {
            return new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["seq"] = Seq,
                    ["time_unix_ms"] = TimeUnixMs,
                    ["time_text"] = TimeText,
                    ["level"] = Level,
                    ["domain"] = Domain,
                    ["event_id"] = EventId,
                    ["message"] = Message,
                    ["context"] = Context,
                }
            );
        }
    }
}
