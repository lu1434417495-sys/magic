using System;
using System.Globalization;
using System.Threading;

public enum GameLogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Fatal,
}

/// <summary>
/// A normalized structured log record shared by the dispatcher and every sink.
/// </summary>
public readonly record struct GameLogRecord
{
    public GameLogRecord(
        GameLogLevel level,
        string eventId,
        string domain,
        string message,
        string context = null
    )
        : this(
            level,
            eventId,
            domain,
            message,
            context,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        ) { }

    internal GameLogRecord(
        GameLogLevel level,
        string eventId,
        string domain,
        string message,
        string context,
        long timestampUnixMs
    )
    {
        Level = level;
        EventId = eventId?.Trim() ?? "";
        Domain = string.IsNullOrWhiteSpace(domain) ? "runtime" : domain.Trim();
        Message = message ?? "";
        Context = context ?? "";
        TimestampUnixMs = timestampUnixMs > 0
            ? timestampUnixMs
            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public GameLogLevel Level { get; }
    public string EventId { get; }
    public string Domain { get; }
    public string Message { get; }
    public string Context { get; }
    public long TimestampUnixMs { get; }

    internal string LevelText => GameLogLevelNames.ToStorageValue(Level);
}

internal static class GameLogLevelNames
{
    internal static string ToStorageValue(GameLogLevel level) =>
        level switch
        {
            GameLogLevel.Fatal => "fatal",
            GameLogLevel.Error => "error",
            GameLogLevel.Warning => "warn",
            GameLogLevel.Debug => "debug",
            _ => "info",
        };

    internal static string ToDisplayValue(GameLogLevel level) =>
        level switch
        {
            GameLogLevel.Fatal => "FATAL",
            GameLogLevel.Error => "ERROR",
            GameLogLevel.Warning => "WARN",
            GameLogLevel.Debug => "DEBUG",
            _ => "INFO",
        };
}

/// <summary>
/// Process-wide C# structured diagnostic dispatcher. Gameplay/session feed events use
/// the owning GameSession directly; diagnostics published here are also copied to every
/// active GameSession through GameSessionLogSink.
/// </summary>
public static class GameLog
{
    private static readonly object _lock = new();
    private static IGameLogSink[] _sinks = Array.Empty<IGameLogSink>();

    [ThreadStatic]
    private static bool _isDispatching;

    public static GameLogLevel MinimumLevel { get; set; } = ResolveMinimumLevel();
    public static bool IsConsoleOutputEnabled { get; set; } = true;

    internal static int SinkCount => Volatile.Read(ref _sinks).Length;

    public static bool IsEnabled(GameLogLevel level) => level >= MinimumLevel;

    public static void AddSink(IGameLogSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        lock (_lock)
        {
            IGameLogSink[] current = _sinks;
            if (Array.IndexOf(current, sink) >= 0)
                return;

            var next = new IGameLogSink[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[^1] = sink;
            Volatile.Write(ref _sinks, next);
        }
    }

    public static void RemoveSink(IGameLogSink sink)
    {
        if (sink == null)
            return;

        lock (_lock)
        {
            IGameLogSink[] current = _sinks;
            int index = Array.IndexOf(current, sink);
            if (index < 0)
                return;

            var next = new IGameLogSink[current.Length - 1];
            if (index > 0)
                Array.Copy(current, 0, next, 0, index);
            if (index < current.Length - 1)
                Array.Copy(current, index + 1, next, index, current.Length - index - 1);
            Volatile.Write(ref _sinks, next);
        }
    }

    public static void Fatal(string message, string eventId = null, string domain = null)
    {
        string resolvedMessage = message ?? "";
        Write(GameLogLevel.Fatal, resolvedMessage, eventId, domain);
        throw new InvalidOperationException(resolvedMessage);
    }

    public static void Error(
        string message,
        string eventId = null,
        string domain = null,
        string context = null
    ) => Write(GameLogLevel.Error, message, eventId, domain, context);

    public static void Warning(
        string message,
        string eventId = null,
        string domain = null,
        string context = null
    ) => Write(GameLogLevel.Warning, message, eventId, domain, context);

    public static void Info(
        string message,
        string eventId = null,
        string domain = null,
        string context = null
    ) => Write(GameLogLevel.Info, message, eventId, domain, context);

    public static void Debug(
        string message,
        string eventId = null,
        string domain = null,
        string context = null
    ) => Write(GameLogLevel.Debug, message, eventId, domain, context);

    public static void Write(
        GameLogLevel level,
        string message,
        string eventId = null,
        string domain = null,
        string context = null
    )
    {
        if (!IsEnabled(level))
            return;

        var record = new GameLogRecord(level, eventId, domain, message, context);
        if (_isDispatching)
        {
            WriteConsole(record);
            return;
        }

        _isDispatching = true;
        try
        {
            WriteConsole(record);
            foreach (IGameLogSink sink in Volatile.Read(ref _sinks))
            {
                try
                {
                    sink.Write(record);
                }
                catch (Exception exception)
                {
                    ReportSinkFailure(sink, exception);
                }
            }
        }
        finally
        {
            _isDispatching = false;
        }
    }

    internal static string FormatForConsole(GameLogRecord record)
    {
        string timestamp = DateTimeOffset
            .FromUnixTimeMilliseconds(record.TimestampUnixMs)
            .UtcDateTime.ToString(
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture
            );
        string eventText = string.IsNullOrEmpty(record.EventId) ? "-" : record.EventId;
        string line =
            $"{timestamp} [{GameLogLevelNames.ToDisplayValue(record.Level)}] [{record.Domain}] [{eventText}] {ToSingleLine(record.Message)}";
        return string.IsNullOrEmpty(record.Context)
            ? line
            : $"{line} | context={ToSingleLine(record.Context)}";
    }

    private static string ToSingleLine(string value) =>
        (value ?? "").Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static void WriteConsole(GameLogRecord record)
    {
        if (!IsConsoleOutputEnabled)
            return;

        try
        {
            ConsoleLogSink.Instance.Write(record);
        }
        catch
        {
            // Logging must never become an application failure path.
        }
    }

    private static void ReportSinkFailure(IGameLogSink sink, Exception exception)
    {
        if (!IsConsoleOutputEnabled)
            return;

        var failure = new GameLogRecord(
            GameLogLevel.Error,
            "log.sink.failed",
            "log",
            $"Log sink {sink?.GetType().FullName ?? "<unknown>"} failed: {exception.GetType().Name}: {exception.Message}"
        );
        try
        {
            ConsoleLogSink.Instance.Write(failure);
        }
        catch
        {
            // There is no safe fallback after the console boundary also fails.
        }
    }

    private static GameLogLevel ResolveMinimumLevel()
    {
        string configured = Environment.GetEnvironmentVariable("MAGIC_LOG_LEVEL")?.Trim();
        return configured?.ToLowerInvariant() switch
        {
            "debug" => GameLogLevel.Debug,
            "warning" or "warn" => GameLogLevel.Warning,
            "error" => GameLogLevel.Error,
            "fatal" => GameLogLevel.Fatal,
            _ => GameLogLevel.Info,
        };
    }
}
