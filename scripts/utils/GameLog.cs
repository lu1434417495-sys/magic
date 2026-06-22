using System;
using System.Collections.Generic;

public enum GameLogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Fatal,
}

public readonly struct GameLogRecord
{
    public GameLogRecord(
        GameLogLevel level,
        string eventId,
        string domain,
        string message,
        string context
    )
    {
        Level = level;
        EventId = eventId ?? "";
        Domain = string.IsNullOrEmpty(domain) ? "runtime" : domain;
        Message = message ?? "";
        Context = context ?? "";
    }

    public GameLogLevel Level { get; }
    public string EventId { get; }
    public string Domain { get; }
    public string Message { get; }
    public string Context { get; }

    public string RuntimeLevelName => Level switch
    {
        GameLogLevel.Fatal => "fatal",
        GameLogLevel.Error => "error",
        GameLogLevel.Warning => "warn",
        GameLogLevel.Debug => "debug",
        _ => "info",
    };

    public string FormatDiagnosticLine() => $"[{Level}][{Domain}] {Message}";
}

/// <summary>
/// 全局静态日志门面。C# runtime 诊断统一投递为 GameLogRecord，再由注册的 target 决定输出位置。
/// </summary>
public static class GameLog
{
    public static bool IsDebugEnabled { get; set; }
    public static bool IsConsoleOutputEnabled { get; set; } = true;

    private static readonly IGameLogTarget _consoleTarget = new ConsoleGameLogTarget(
        () => IsConsoleOutputEnabled
    );
    private static readonly List<IGameLogTarget> _targets = new() { _consoleTarget };
    private static readonly object _lock = new();

    public static void AddTarget(IGameLogTarget target)
    {
        if (target == null) return;
        lock (_lock)
        {
            if (!_targets.Contains(target))
                _targets.Add(target);
        }
    }

    public static void RemoveTarget(IGameLogTarget target)
    {
        if (target == null || ReferenceEquals(target, _consoleTarget)) return;
        lock (_lock)
        {
            _targets.Remove(target);
        }
    }

    public static void ClearTargets()
    {
        lock (_lock)
        {
            _targets.Clear();
            _targets.Add(_consoleTarget);
        }
    }

    public static void Fatal(string message, string eventId = null, string domain = null)
    {
        Write(GameLogLevel.Fatal, eventId, domain, message, null);
        throw new InvalidOperationException(message);
    }

    public static void Error(
        string message,
        string eventId = null,
        string domain = null,
        string context = null
    )
    {
        Write(GameLogLevel.Error, eventId, domain, message, context);
    }

    public static void Warning(
        string message,
        string eventId = null,
        string domain = null,
        string context = null
    )
    {
        Write(GameLogLevel.Warning, eventId, domain, message, context);
    }

    public static void Info(
        string message,
        string eventId = null,
        string domain = null,
        string context = null
    )
    {
        Write(GameLogLevel.Info, eventId, domain, message, context);
    }

    public static void Debug(
        string message,
        string eventId = null,
        string domain = null,
        string context = null
    )
    {
        if (!IsDebugEnabled)
            return;
        Write(GameLogLevel.Debug, eventId, domain, message, context);
    }

    private static void Write(
        GameLogLevel level,
        string eventId,
        string domain,
        string message,
        string context
    )
    {
        var record = new GameLogRecord(level, eventId, domain, message, context);

        IGameLogTarget[] snapshot;
        lock (_lock)
        {
            snapshot = _targets.ToArray();
        }

        foreach (var target in snapshot)
        {
            try
            {
                target.Write(record);
            }
            catch
            {
                // 日志 target 自身出错不应中断 runtime 流程。
            }
        }
    }
}
