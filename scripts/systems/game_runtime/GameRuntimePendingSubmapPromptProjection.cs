using GDictionary = Godot.Collections.Dictionary;

internal static class GameRuntimePendingSubmapPromptProjection
{
    internal static GDictionary Project(GameRuntimePendingSubmapPrompt prompt)
    {
        if (prompt == null || prompt.IsEmpty)
            return new GDictionary();
        return new GDictionary
        {
            ["event_id"] = prompt.EventId.ToString(),
            ["source_map_id"] = prompt.SourceMapId,
            ["source_coord"] = prompt.SourceCoord,
            ["target_submap_id"] = prompt.TargetSubmapId.ToString(),
            ["target_display_name"] = prompt.TargetDisplayName,
            ["title"] = prompt.Title,
            ["description"] = prompt.Description,
        };
    }
}
