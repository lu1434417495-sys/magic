using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class GameRuntimePendingSubmapPrompt
{
    public StringName EventId { get; private set; } = "";
    public string SourceMapId { get; private set; } = "";
    public Vector2I SourceCoord { get; private set; } = Vector2I.Zero;
    public StringName TargetSubmapId { get; private set; } = "";
    public string TargetDisplayName { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";

    public bool IsEmpty => TargetSubmapId == "";

    public void Set(
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

    public void Clear()
    {
        EventId = "";
        SourceMapId = "";
        SourceCoord = Vector2I.Zero;
        TargetSubmapId = "";
        TargetDisplayName = "";
        Title = "";
        Description = "";
    }

    public GDictionary ToDictionary()
    {
        if (IsEmpty)
        {
            return new GDictionary();
        }
        return new GDictionary
        {
            ["event_id"] = EventId.ToString(),
            ["source_map_id"] = SourceMapId,
            ["source_coord"] = SourceCoord,
            ["target_submap_id"] = TargetSubmapId.ToString(),
            ["target_display_name"] = TargetDisplayName,
            ["title"] = Title,
            ["description"] = Description,
        };
    }
}
