using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class run_world_map_view_color_config_regression : LifecycleTestSceneTree
{
    private const string TestConfigPath = "res://data/configs/world_map/test_world_map_config.tres";
    private static readonly PackedScene WorldMapScene = GD.Load<PackedScene>(
        "res://scenes/main/world_map.tscn"
    );

    private readonly TestHarness _test = new();
    private GameSession _gameSession;

    public override async void _Initialize()
    {
        await EnsureGameSession();
        await ResetSession();
        await TestWorldMapSceneExposesDefaultViewPalette();
        await Cleanup();
        RequestTestExit(_test.Finish("World map view color config regression"));
    }

    private async Task TestWorldMapSceneExposesDefaultViewPalette()
    {
        Error createError = (Error)_gameSession.StartNewGame(TestConfigPath);
        _test.Eq(createError, Error.Ok, "world_map view color 回归前置：应能成功创建测试世界。");
        if (createError != Error.Ok)
            return;

        WorldMapSystem worldMap = WorldMapScene.Instantiate<WorldMapSystem>();
        Root.AddChild(worldMap);
        await ProcessFrames(2);

        WorldMapView worldMapView = worldMap.GetNode<WorldMapView>("MapViewport/WorldMapView");
        _test.True(worldMapView != null, "world_map.tscn 应继续提供 WorldMapView 节点。");
        _test.True(worldMapView != null && worldMapView.Visible, "世界态初始化后 WorldMapView 应保持可见。");
        _test.True(
            worldMapView != null && worldMapView.Size.X > 0.0f && worldMapView.Size.Y > 0.0f,
            "WorldMapView 应在场景中获得有效尺寸。"
        );

        if (worldMapView != null)
        {
            worldMap.RenderFromRuntime(true);
            await ProcessFrames(1);

            var expectedColors = new Dictionary<string, Color>
            {
                ["selection_outline_color"] = new(0.98f, 0.9f, 0.42f, 0.95f),
                ["world_event_marker_fill_color"] = new(0.95f, 0.78f, 0.28f, 0.96f),
                ["world_event_marker_outline_color"] = new(0.25f, 0.11f, 0.02f, 1.0f),
                ["world_event_marker_center_color"] = new(0.32f, 0.06f, 0.02f, 1.0f),
                ["encounter_marker_outer_color"] = new(0.87f, 0.28f, 0.23f, 0.95f),
                ["encounter_marker_inner_color"] = new(0.15f, 0.02f, 0.02f, 0.95f),
                ["npc_marker_body_color"] = new(0.42f, 0.77f, 0.87f, 0.95f),
                ["npc_marker_head_color"] = new(0.88f, 0.94f, 0.98f, 1.0f),
                ["village_tier_color"] = new(0.57f, 0.75f, 0.43f, 1.0f),
                ["town_tier_color"] = new(0.51f, 0.7f, 0.84f, 1.0f),
                ["city_tier_color"] = new(0.78f, 0.63f, 0.42f, 1.0f),
                ["capital_tier_color"] = new(0.74f, 0.48f, 0.76f, 1.0f),
                ["world_stronghold_tier_color"] = new(0.9f, 0.43f, 0.31f, 1.0f),
                ["metropolis_tier_color"] = new(0.95f, 0.82f, 0.45f, 1.0f),
                ["fallback_tier_color"] = new(0.5f, 0.5f, 0.5f, 1.0f),
            };

            foreach ((string propertyName, Color expectedColor) in expectedColors)
            {
                _test.True(
                    PropertyListHasName(worldMapView, propertyName),
                    $"WorldMapView 应把 {propertyName} 暴露为可配置导出字段。"
                );
                _test.Eq(
                    worldMapView.Get(propertyName).AsColor(),
                    expectedColor,
                    $"WorldMapView 的 {propertyName} 默认值应继续贴近当前主线视觉。"
                );
            }

            _test.True(
                PropertyListHasName(worldMapView, "village_settlement_texture"),
                "WorldMapView 应把 village_settlement_texture 暴露为可配置导出字段。"
            );
            Texture2D villageTexture = worldMapView.Get("village_settlement_texture").As<Texture2D>();
            _test.True(villageTexture != null, "world_map.tscn 应绑定村级据点贴图。");
            if (villageTexture != null)
            {
                _test.Eq(
                    villageTexture.ResourcePath,
                    "res://assets/main/basic_map/village_dark.png",
                    "world_map.tscn 的村级据点贴图应指向暗黑风的 village_dark.png。"
                );
            }

            var tierToProperty = new Dictionary<int, string>
            {
                [0] = "village_tier_color",
                [1] = "town_tier_color",
                [2] = "city_tier_color",
                [3] = "capital_tier_color",
                [4] = "world_stronghold_tier_color",
                [5] = "metropolis_tier_color",
            };
            foreach ((int tier, string propertyName) in tierToProperty)
            {
                _test.Eq(
                    worldMapView._get_settlement_color(tier),
                    worldMapView.Get(propertyName).AsColor(),
                    $"tier {tier} 应通过导出字段提供颜色，而不是回退到 draw 内硬编码。"
                );
            }
        }

        await DisposeNode(worldMap);
    }

    private async Task EnsureGameSession()
    {
        _gameSession = Root.GetNodeOrNull<GameSession>("GameSession");
        if (_gameSession != null)
            return;
        _gameSession = new GameSession { Name = "GameSession" };
        Root.AddChild(_gameSession);
        await ProcessFrames(1);
    }

    private async Task ResetSession()
    {
        _gameSession?.ClearPersistedGame();
        await ProcessFrames(1);
    }

    private async Task Cleanup()
    {
        _gameSession?.ClearPersistedGame();
        await ProcessFrames(1);
    }

    private async Task DisposeNode(Node node)
    {
        node.QueueFree();
        await ProcessFrames(1);
    }

    private async Task ProcessFrames(int count)
    {
        for (int index = 0; index < count; index++)
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private static bool PropertyListHasName(GodotObject instance, string propertyName)
    {
        if (instance == null)
            return false;
        foreach (Godot.Collections.Dictionary propertyInfo in instance.GetPropertyList())
        {
            if (propertyInfo.ContainsKey("name") && propertyInfo["name"].AsString() == propertyName)
                return true;
        }
        return false;
    }
}
