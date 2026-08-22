using System;
using Godot;

internal sealed class WorldPresetDefinition
{
    internal WorldPresetDefinition(
        StringName presetId,
        string displayName,
        string sizeLabel,
        StringName generationId
    )
    {
        if (presetId == "")
            throw new ArgumentException("World preset ID is required.", nameof(presetId));
        if (generationId == "")
            throw new ArgumentException("World generation ID is required.", nameof(generationId));
        PresetId = presetId;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        SizeLabel = sizeLabel ?? throw new ArgumentNullException(nameof(sizeLabel));
        GenerationId = generationId;
    }

    internal StringName PresetId { get; }
    internal string DisplayName { get; }
    internal string SizeLabel { get; }
    internal StringName GenerationId { get; }
}
