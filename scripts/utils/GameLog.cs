using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public enum GameLogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Fatal,
}

/// <summary>
/// 全局静态日志门面。C# 代码统一走这里，不再直接调用 GD.Print/GD.PushError/Console.WriteLine。
/// </summary>
public static class GameLog
{
    private static readonly List<IGameLogSink> _sinks = new();
    private static readonly object _lock = new();

    public static bool IsDebugEnabled { get; set; }
    public static bool IsConsoleOutputEnabled { get; set; } = true;

    public static void AddSink(IGameLogSink sink)
    {
        if (sink == null) return;
        lock (_lock)
        {
            if (!_sinks.Contains(sink))
                _sinks.Add(sink);
        }
    }

    public static void RemoveSink(IGameLogSink sink)
    {
        if (sink == null) return;
        lock (_lock)
        {
            _sinks.Remove(sink);
        }
    }

    public static void ClearSinks()
    {
        lock (_lock)
        {
            _sinks.Clear();
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
        GDictionary context = null
    )
    {
        Write(GameLogLevel.Error, eventId, domain, message, context);
    }

    public static void Warning(
        string message,
        string eventId = null,
        string domain = null,
        GDictionary context = null
    )
    {
        Write(GameLogLevel.Warning, eventId, domain, message, context);
    }

    public static void Info(
        string message,
        string eventId = null,
        string domain = null,
        GDictionary context = null
    )
    {
        Write(GameLogLevel.Info, eventId, domain, message, context);
    }

    public static void Debug(
        string message,
        string eventId = null,
        string domain = null,
        GDictionary context = null
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
        GDictionary context
    )
    {
        string resolvedDomain = string.IsNullOrEmpty(domain) ? "runtime" : domain;
        string resolvedEventId = eventId ?? "";

        IGameLogSink[] snapshot;
        lock (_lock)
        {
            snapshot = _sinks.ToArray();
        }

        foreach (var sink in snapshot)
        {
            try
            {
                sink.Write(level, resolvedEventId, resolvedDomain, message, context);
            }
            catch
            {
                // Sink 自身出错不应中断日志流程
            }
        }

        if (!IsConsoleOutputEnabled)
            return;

        string line = $"[{level}][{resolvedDomain}] {message}";
        if (level == GameLogLevel.Error || level == GameLogLevel.Fatal)
        {
            Console.Error.WriteLine(line);
        }
        else
        {
            Console.WriteLine(line);
        }
    }
}
