using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

/// <summary>
/// GameLog 输出目标接口。实现类注册到 GameLog.AddSink 即可接收日志。
/// </summary>
public interface IGameLogSink
{
    void Write(
        GameLogLevel level,
        string eventId,
        string domain,
        string message,
        string context
    );
}

/// <summary>
/// 输出到 Console.Error / Console.Out。已内建于 GameLog，无需手动注册。
/// </summary>
public class ConsoleLogSink : IGameLogSink
{
    public void Write(
        GameLogLevel level,
        string eventId,
        string domain,
        string message,
        string context
    )
    {
        // GameLog.Write 已负责 Console 输出，本 Sink 空置以避免重复打印
    }
}

/// <summary>
/// 输出到 Godot 编辑器日志（调试用）。可选注册。
/// </summary>
public class GodotEditorLogSink : IGameLogSink
{
    public void Write(
        GameLogLevel level,
        string eventId,
        string domain,
        string message,
        string context
    )
    {
        switch (level)
        {
            case GameLogLevel.Fatal:
            case GameLogLevel.Error:
                GD.PushError($"[{domain}] {message}");
                break;
            case GameLogLevel.Warning:
                GD.PushWarning($"[{domain}] {message}");
                break;
            case GameLogLevel.Debug:
            case GameLogLevel.Info:
                GD.Print($"[{domain}] {message}");
                break;
        }
    }
}

/// <summary>
/// 输出到 GameSession 的 log_event，接入 GameLogService + RuntimeLogDock 链路。
/// </summary>
public class GameSessionLogSink : IGameLogSink
{
    private WeakReference<GameSession> _sessionRef;

    public GameSessionLogSink(GameSession session)
    {
        _sessionRef = session != null ? new WeakReference<GameSession>(session) : null;
    }

    public void Write(
        GameLogLevel level,
        string eventId,
        string domain,
        string message,
        string context
    )
    {
        if (_sessionRef == null || !_sessionRef.TryGetTarget(out var session))
            return;
        if (session == null || !GodotObject.IsInstanceValid(session))
            return;

        string levelStr = level switch
        {
            GameLogLevel.Fatal => "fatal",
            GameLogLevel.Error => "error",
            GameLogLevel.Warning => "warn",
            GameLogLevel.Debug => "debug",
            _ => "info",
        };

        session.log_event(levelStr, domain, eventId, message, context);
    }
}
