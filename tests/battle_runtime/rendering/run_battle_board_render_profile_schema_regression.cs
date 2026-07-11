using Godot;

public partial class run_battle_board_render_profile_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestRenderProfileReadsFormalSourceSpecs();
        RequestTestExit(_test.Finish("Battle board render profile schema regression"));
    }

    private void TestRenderProfileReadsFormalSourceSpecs()
    {
        var profile = new BattleBoardRenderProfile();
        profile.SetSourceSpecs(
            new[]
            {
                new BattleBoardTileSourceSpec(
                    BattleBoardRenderProfile.SOURCE_LAND(),
                    new[] { "custom_land.png" },
                    BattleBoardRenderProfile.LAYER_ROLE_TOP(),
                    new Vector2I(64, 32),
                    new Vector2I(64, 32),
                    Vector2I.Zero,
                    Vector2I.Zero,
                    allowGeneratedFallback: true
                ),
                new BattleBoardTileSourceSpec(
                    BattleBoardRenderProfile.SOURCE_SELECTED(),
                    new[] { "custom_marker.png" },
                    BattleBoardRenderProfile.LAYER_ROLE_MARKER(),
                    new Vector2I(64, 32),
                    new Vector2I(64, 32),
                    Vector2I.Zero,
                    Vector2I.Zero,
                    allowGeneratedFallback: true
                ),
            }
        );

        _test.Eq(
            profile.GetPrimaryLandFile(),
            "custom_land.png",
            "BattleBoardRenderProfile 应读取 formal typed source spec 文件。"
        );
        _test.Eq(
            profile.GetSelectedMarkerFile(),
            "custom_marker.png",
            "BattleBoardRenderProfile 应读取 formal typed marker source spec 文件。"
        );
    }
}
