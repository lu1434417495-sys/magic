using System;
using Godot;

public sealed class MountedSubmapDefinition
{
    public MountedSubmapDefinition(
        StringName submapId,
        string displayName,
        StringName worldGenerationId,
        string returnHintText,
        WorldGenerationDefinition generation
    )
    {
        SubmapId = submapId;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        if (worldGenerationId == "")
            throw new ArgumentException("Mounted submap world generation ID is required.", nameof(worldGenerationId));
        WorldGenerationId = worldGenerationId;
        ReturnHintText = returnHintText
            ?? throw new ArgumentNullException(nameof(returnHintText));
        Generation = generation ?? throw new ArgumentNullException(nameof(generation));
    }

    public StringName SubmapId { get; }
    public string DisplayName { get; }
    public StringName WorldGenerationId { get; }
    public string ReturnHintText { get; }
    public WorldGenerationDefinition Generation { get; }
}
