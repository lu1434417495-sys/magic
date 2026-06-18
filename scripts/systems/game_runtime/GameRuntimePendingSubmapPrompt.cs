using Godot;

internal sealed class GameRuntimePendingSubmapPrompt
{
    internal StringName EventId { get; private set; } = "";
    internal string SourceMapId { get; private set; } = "";
    internal Vector2I SourceCoord { get; private set; } = Vector2I.Zero;
    internal StringName TargetSubmapId { get; private set; } = "";
    internal string TargetDisplayName { get; private set; } = "";
    internal string Title { get; private set; } = "";
    internal string Description { get; private set; } = "";

    internal bool IsEmpty => TargetSubmapId == "";

    internal void Set(
        StringName eventId,
        string sourceMapId,
        Vector2I sourceCoord,
        StringName targetSubmapId,
        string targetDisplayName,
        string title,
        string description
    )
    {
        EventId = eventId;
        SourceMapId = sourceMapId ?? "";
        SourceCoord = sourceCoord;
        TargetSubmapId = targetSubmapId;
        TargetDisplayName = targetDisplayName ?? "";
        Title = title ?? "";
        Description = description ?? "";
    }

    internal void Clear()
    {
        EventId = "";
        SourceMapId = "";
        SourceCoord = Vector2I.Zero;
        TargetSubmapId = "";
        TargetDisplayName = "";
        Title = "";
        Description = "";
    }
}
