using Godot;

[GlobalClass]
public partial class EngineAssetCatalogDef : Resource
{
    [Export]
    public Godot.Collections.Array<EngineTextureAssetEntryDef> texture_assets { get; set; } = new();

    [Export]
    public Godot.Collections.Array<EngineSceneAssetEntryDef> scene_assets { get; set; } = new();

    [Export]
    public Godot.Collections.Array<EngineAudioAssetEntryDef> audio_assets { get; set; } = new();

    [Export]
    public Godot.Collections.Array<EngineShaderAssetEntryDef> shader_assets { get; set; } = new();
}
