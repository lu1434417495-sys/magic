using System;
using Godot;

/// <summary>
/// GameLog 输出目标接口。实现类注册到 GameLog.AddTarget 即可接收日志记录。
/// </summary>
public interface IGameLogTarget
{
    void Write(GameLogRecord record);
}

/// <summary>
/// 输出到当前进程的标准输出/错误流，供 headless 与 CI 捕获。
/// </summary>
public sealed class ConsoleGameLogTarget : IGameLogTarget
{
    private readonly Func<bool> _isEnabled;

    public ConsoleGameLogTarget(Func<bool> isEnabled = null)
    {
        _isEnabled = isEnabled;
    }

    public void Write(GameLogRecord record)
    {
        if (_isEnabled != null && !_isEnabled())
            return;

        string line = record.FormatDiagnosticLine();
        if (record.Level == GameLogLevel.Error || record.Level == GameLogLevel.Fatal)
        {
            Console.Error.WriteLine(line);
        }
        else
        {
            Console.Out.WriteLine(line);
        }
    }
}

/// <summary>
/// 输出到 GameSession 的 log_event，接入 GameLogService + RuntimeLogDock 链路。
/// </summary>
public sealed class GameSessionLogTarget : IGameLogTarget
{
    private WeakReference<GameSession> _sessionRef;

    public GameSessionLogTarget(GameSession session)
    {
        _sessionRef = session != null ? new WeakReference<GameSession>(session) : null;
    }

    public void Write(GameLogRecord record)
    {
        if (_sessionRef == null || !_sessionRef.TryGetTarget(out var session))
            return;
        if (session == null || !GodotObject.IsInstanceValid(session))
            return;

        Godot.Collections.Dictionary entry = session.LogEvent(
            record.RuntimeLevelName,
            record.Domain,
            record.EventId,
            record.Message,
            record.Context
        );
        entry?.Dispose();
    }
}
