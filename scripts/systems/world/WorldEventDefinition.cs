using System;
using Godot;

internal enum WorldEventTypeKind
{
    Unknown = 0,
    EnterSubmap,
}

public sealed class WorldEventDefinition
{
    private static readonly StringName EventTypeEnterSubmap = "enter_submap";

    public WorldEventDefinition(
        StringName eventId,
        string displayName,
        Vector2I worldCoord,
        StringName eventType,
        StringName targetSubmapId,
        StringName discoveryConditionId,
        string promptTitle,
        string promptText
    )
    {
        EventId = eventId;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        WorldCoord = worldCoord;
        EventType = eventType;
        TargetSubmapId = targetSubmapId;
        DiscoveryConditionId = discoveryConditionId;
        PromptTitle = promptTitle ?? throw new ArgumentNullException(nameof(promptTitle));
        PromptText = promptText ?? throw new ArgumentNullException(nameof(promptText));
    }

    public StringName EventId { get; }
    public string DisplayName { get; }
    public Vector2I WorldCoord { get; }
    public StringName EventType { get; }
    public StringName TargetSubmapId { get; }
    public StringName DiscoveryConditionId { get; }
    public string PromptTitle { get; }
    public string PromptText { get; }

    internal static StringName ToStringName(WorldEventTypeKind kind) =>
        kind == WorldEventTypeKind.EnterSubmap ? EventTypeEnterSubmap : "";

    internal static WorldEventTypeKind ToEventTypeKind(StringName eventType) =>
        eventType == EventTypeEnterSubmap
            ? WorldEventTypeKind.EnterSubmap
            : WorldEventTypeKind.Unknown;

    internal static bool IsEnterSubmapEventType(StringName eventType) =>
        ToEventTypeKind(eventType) == WorldEventTypeKind.EnterSubmap;

    internal static WorldEventDefinition FromResource(WorldEventConfig source, string path)
    {
        if (source == null)
            throw WorldDefinitionProjection.Invalid(path, "resource is null");
        return new WorldEventDefinition(
            source.event_id,
            WorldDefinitionProjection.RequireString(
                source.display_name,
                path + ".display_name"
            ),
            source.world_coord,
            source.event_type,
            source.target_submap_id,
            source.discovery_condition_id,
            WorldDefinitionProjection.RequireString(
                source.prompt_title,
                path + ".prompt_title"
            ),
            WorldDefinitionProjection.RequireString(
                source.prompt_text,
                path + ".prompt_text"
            )
        );
    }
}
