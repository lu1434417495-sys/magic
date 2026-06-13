using Godot;

public partial class run_battle_board_render_profile_schema_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestRenderProfileReadsFormalSourceSpecs();
        Quit(_test.Finish("Battle board render profile schema regression"));
    }

    private void TestRenderProfileReadsFormalSourceSpecs()
    {
        var profile = new BattleBoardRenderProfile();
        profile.source_specs = new Godot.Collections.Array<Godot.Collections.Dictionary>
        {
            new()
            {
                ["key"] = BattleBoardRenderProfile.SOURCE_LAND(),
                ["files"] = new Godot.Collections.Array<string> { "custom_land.png" },
                ["layer_role"] = BattleBoardRenderProfile.LAYER_ROLE_TOP(),
                ["atlas_region_size"] = new Vector2I(64, 32),
                ["board_tile_size"] = new Vector2I(64, 32),
                ["texture_origin"] = Vector2I.Zero,
                ["visual_origin"] = Vector2I.Zero,
                ["allow_generated_fallback"] = true,
            },
            new()
            {
                ["key"] = BattleBoardRenderProfile.SOURCE_SELECTED(),
                ["files"] = new Godot.Collections.Array<string> { "custom_marker.png" },
                ["layer_role"] = BattleBoardRenderProfile.LAYER_ROLE_MARKER(),
                ["atlas_region_size"] = new Vector2I(64, 32),
                ["board_tile_size"] = new Vector2I(64, 32),
                ["texture_origin"] = Vector2I.Zero,
                ["visual_origin"] = Vector2I.Zero,
                ["allow_generated_fallback"] = true,
            },
        };

        _test.Eq(
            profile.GetPrimaryLandFile(),
            "custom_land.png",
            "BattleBoardRenderProfile 应继续读取 formal string-key source spec 文件。"
        );
        _test.Eq(
            profile.GetSelectedMarkerFile(),
            "custom_marker.png",
            "BattleBoardRenderProfile 应继续读取 formal string-key marker source spec 文件。"
        );
    }
}

