using System;

/// <summary>
/// Receives normalized structured diagnostic records published by GameLog.
/// </summary>
public interface IGameLogSink
{
    void Write(GameLogRecord record);
}

/// <summary>
/// Default C# process output. Error and fatal records use stderr; other levels use stdout.
/// </summary>
internal sealed class ConsoleLogSink : IGameLogSink
{
    internal static readonly ConsoleLogSink Instance = new();

    private ConsoleLogSink() { }

    public void Write(GameLogRecord record)
    {
        string line = GameLog.FormatForConsole(record);
        if (record.Level >= GameLogLevel.Error)
            ConsoleProcessOutput.WriteStandardError(line);
        else
            ConsoleProcessOutput.WriteStandard(line);
    }
}

/// <summary>
/// Raw C# process-output boundary. Structured application diagnostics must use GameLog;
/// lifecycle/test protocols use these methods when their exact output is machine-read.
/// </summary>
internal static class ConsoleProcessOutput
{
    internal static void WriteStandard(params object[] values) =>
        Console.Out.WriteLine(JoinValues(values));

    internal static void WriteStandardError(params object[] values) =>
        Console.Error.WriteLine(JoinValues(values));

    internal static void WriteFailure(params object[] values) =>
        Console.Error.WriteLine($"ERROR: {JoinValues(values)}");

    private static string JoinValues(object[] values)
    {
        if (values == null || values.Length == 0)
            return "";

        var result = new System.Text.StringBuilder();
        foreach (object value in values)
            result.Append(value?.ToString() ?? "<null>");
        return result.ToString();
    }
}

/// <summary>
/// Copies process-wide diagnostics into the owning GameSession log buffer/file/UI feed.
/// </summary>
internal sealed class GameSessionLogSink : IGameLogSink
{
    private readonly WeakReference<GameSession> _sessionRef;

    internal GameSessionLogSink(GameSession session)
    {
        _sessionRef = session != null ? new WeakReference<GameSession>(session) : null;
    }

    public void Write(GameLogRecord record)
    {
        if (_sessionRef == null || !_sessionRef.TryGetTarget(out GameSession session))
            return;
        if (session == null)
            return;

        session.RecordLogEvent(record);
    }
}
