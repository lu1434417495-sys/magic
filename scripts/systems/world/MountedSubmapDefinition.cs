using System;
using Godot;

public sealed class MountedSubmapDefinition
{
    public MountedSubmapDefinition(
        StringName submapId,
        string displayName,
        string generationConfigPath,
        string returnHintText,
        WorldGenerationDefinition generation
    )
    {
        SubmapId = submapId;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        GenerationConfigPath = generationConfigPath
            ?? throw new ArgumentNullException(nameof(generationConfigPath));
        ReturnHintText = returnHintText
            ?? throw new ArgumentNullException(nameof(returnHintText));
        Generation = generation ?? throw new ArgumentNullException(nameof(generation));
    }

    public StringName SubmapId { get; }
    public string DisplayName { get; }
    public string GenerationConfigPath { get; }
    public string ReturnHintText { get; }
    public WorldGenerationDefinition Generation { get; }

    internal static MountedSubmapDefinition FromResource(
        MountedSubmapConfig source,
        string generationConfigPath,
        WorldGenerationDefinition generation,
        string path
    )
    {
        if (source == null)
            throw WorldDefinitionProjection.Invalid(path, "resource is null");
        return new MountedSubmapDefinition(
            source.submap_id,
            WorldDefinitionProjection.RequireString(
                source.display_name,
                path + ".display_name"
            ),
            generationConfigPath,
            WorldDefinitionProjection.RequireString(
                source.return_hint_text,
                path + ".return_hint_text"
            ),
            generation
        );
    }
}
