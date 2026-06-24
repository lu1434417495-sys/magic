using System.Threading.Tasks;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_battle_loading_overlay_regression : SceneTree
{
    private const string TestConfigPath = "res://data/configs/world_map/test_world_map_config.tres";
    private static readonly PackedScene WorldMapScene = GD.Load<PackedScene>(
        "res://scenes/main/world_map.tscn"
    );

    private readonly TestHarness _test = new();
    private GameSession _gameSession;

    public override async void _Initialize()
    {
        try
        {
            await EnsureGameSession();
            await ResetSession();
            await TestWorldMapLoadingOverlayTracksBattlePanelState();
            await ResetSession();
            await TestWorldMapLoadingOverlayStaysVisibleDuringPendingBattleGeneration();
        }
        catch (System.Exception ex)
        {
            _test.Fail($"未捕获异常：{ex}");
        }
        await Cleanup();
        Quit(_test.Finish("World map battle loading overlay regression"));
    }

    private async Task TestWorldMapLoadingOverlayTracksBattlePanelState()
    {
        Error createError = (Error)_gameSession.StartNewGame(TestConfigPath);
        _test.Eq(createError, Error.Ok, "加载世界地图场景前应能成功创建测试世界。");
        if (createError != Error.Ok)
            return;

        WorldMapSystem worldMap = WorldMapScene.Instantiate<WorldMapSystem>();
        Root.AddChild(worldMap);
        await ProcessFrames(2);

        Control overlay = worldMap.GetNode<Control>("BattleLoadingOverlay");
        Control background = worldMap.GetNode<Control>("Background");
        Label loadingLabel = worldMap.GetNode<Label>("%BattleLoadingLabel");
        ProgressBar loadingProgressBar = worldMap.GetNode<ProgressBar>("%BattleLoadingProgressBar");
        Label loadingPercentLabel = worldMap.GetNode<Label>("%BattleLoadingPercentLabel");
        BattleMapPanel battleMapPanel = worldMap.GetNode<BattleMapPanel>("MapViewport/BattleMapPanel");

        _test.True(overlay != null, "world_map.tscn 应提供根层 battle loading overlay。");
        _test.True(overlay != null && !overlay.Visible, "初始进入世界地图时 battle loading overlay 应保持隐藏。");
        _test.Eq(overlay?.GetGlobalRect(), background?.GetGlobalRect(), "battle loading overlay 应与 Background 保持同尺寸。");
        _test.True(loadingProgressBar != null, "根层 overlay 应提供 battle loading progress bar。");
        _test.True(loadingPercentLabel != null, "根层 overlay 应提供 battle loading 百分比标签。");

        battleMapPanel?.EmitSignal(BattleMapPanel.SignalName.battle_loading_state_changed, true, 48.0f);
        await ProcessFrames(1);

        _test.True(overlay != null && overlay.Visible, "BattleMapPanel 进入 loading 状态时应显示根层黑屏 overlay。");
        _test.Eq(loadingLabel.Text, "LOADING...", "根层 overlay 只应显示 LOADING... 文本。");
        _test.Eq(Mathf.RoundToInt((float)loadingProgressBar.Value), 48, "loading 时 progress bar 应同步 battle panel 的进度值。");
        _test.Eq(loadingPercentLabel.Text, "48%", "loading 时百分比标签应同步 battle panel 的进度值。");

        battleMapPanel?.EmitSignal(BattleMapPanel.SignalName.battle_loading_state_changed, false, 100.0f);
        await ProcessFrames(1);

        _test.True(overlay != null && !overlay.Visible, "BattleMapPanel 结束 loading 状态时应隐藏根层黑屏 overlay。");
        _test.Eq(Mathf.RoundToInt((float)loadingProgressBar.Value), 100, "loading 结束后 progress bar 应保留最终完成进度。");
        _test.Eq(loadingPercentLabel.Text, "100%", "loading 结束后百分比标签应显示 100%。");

        await DisposeNode(worldMap);
    }

    private async Task TestWorldMapLoadingOverlayStaysVisibleDuringPendingBattleGeneration()
    {
        Error createError = (Error)_gameSession.StartNewGame(TestConfigPath);
        _test.Eq(createError, Error.Ok, "pending terrain loading 回归前应能成功创建测试世界。");
        if (createError != Error.Ok)
            return;

        WorldMapSystem worldMap = WorldMapScene.Instantiate<WorldMapSystem>();
        Root.AddChild(worldMap);
        await ProcessFrames(2);

        GameRuntimeFacade runtime = worldMap._runtime;
        Control overlay = worldMap.GetNode<Control>("BattleLoadingOverlay");
        ProgressBar loadingProgressBar = worldMap.GetNode<ProgressBar>("%BattleLoadingProgressBar");
        Label loadingPercentLabel = worldMap.GetNode<Label>("%BattleLoadingPercentLabel");
        BattleMapPanel battleMapPanel = worldMap.GetNode<BattleMapPanel>("MapViewport/BattleMapPanel");
        _test.True(runtime != null, "world_map 场景应初始化 runtime。");
        _test.True(overlay != null, "pending terrain loading 回归需要根层 battle loading overlay。");
        _test.True(loadingProgressBar != null, "pending terrain loading 回归需要 battle loading progress bar。");
        _test.True(loadingPercentLabel != null, "pending terrain loading 回归需要 battle loading 百分比标签。");
        _test.True(battleMapPanel != null, "pending terrain loading 回归需要 BattleMapPanel。");
        if (runtime == null || overlay == null || loadingProgressBar == null || loadingPercentLabel == null || battleMapPanel == null)
        {
            await DisposeNode(worldMap);
            return;
        }

        EncounterAnchorData encounterAnchor = FindEncounterAnchorByKind(_gameSession.GetWorldData(), "single");
        _test.True(encounterAnchor != null, "pending terrain loading 回归需要至少一个单体野怪遭遇。");
        if (encounterAnchor == null)
        {
            await DisposeNode(worldMap);
            return;
        }

        runtime.GetBattleRuntime()._terrain_generator = new PendingBattleTerrainGenerator();
        runtime.StartBattle(encounterAnchor);
        worldMap.RenderFromRuntime(true);

        _test.Eq(runtime.GetActiveModalId(), "battle_loading", "terrain 首次生成失败时应进入 battle_loading modal。");
        _test.True(overlay.Visible, "battle_loading modal 下根层 overlay 应保持可见。");
        _test.Eq(Mathf.RoundToInt((float)loadingProgressBar.Value), 0, "battle_loading modal 初始应显示 0 进度。");
        _test.Eq(loadingPercentLabel.Text, "0%", "battle_loading modal 初始百分比应显示 0%。");

        bool reachedBattleStartConfirm = false;
        for (int frame = 0; frame < 30; frame++)
        {
            await ProcessFrames(1);
            if (runtime.GetActiveModalId() == "battle_start_confirm")
            {
                reachedBattleStartConfirm = true;
                break;
            }
        }
        _test.True(reachedBattleStartConfirm, "terrain 重试成功后应进入 battle_start_confirm modal。");
        if (overlay.Visible)
        {
            for (int frame = 0; frame < 30; frame++)
            {
                await ProcessFrames(1);
                if (!overlay.Visible)
                    break;
            }
        }
        _test.Eq(runtime.GetActiveModalId(), "battle_start_confirm", "terrain 重试成功后应进入 battle_start_confirm modal。");
        _test.Eq(
            overlay.Visible,
            battleMapPanel.IsLoadingBattle(),
            "进入 battle_start_confirm 后根层 overlay 应重新由 BattleMapPanel 的 loading 状态驱动。"
        );

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
        if (node == null || !GodotObject.IsInstanceValid(node))
            return;
        node.QueueFree();
        await ProcessFrames(2);
    }

    private async Task ProcessFrames(int count)
    {
        for (int index = 0; index < count; index++)
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private static EncounterAnchorData FindEncounterAnchorByKind(GDictionary worldData, StringName kind)
    {
        if (worldData == null || !worldData.ContainsKey("encounter_anchors"))
            return null;
        foreach (Variant value in worldData["encounter_anchors"].AsGodotArray())
        {
            EncounterAnchorData anchor = ReadEncounterAnchor(value);
            if (anchor != null && anchor.encounter_kind == kind)
                return anchor;
        }
        return null;
    }

    private static EncounterAnchorData ReadEncounterAnchor(Variant value)
    {
        return value.VariantType == Variant.Type.Dictionary
            ? EncounterAnchorData.FromDictionary(value.AsGodotDictionary())
            : null;
    }
}
