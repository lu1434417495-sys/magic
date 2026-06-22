using System.Threading.Tasks;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_runtime_log_dock_regression : SceneTree
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
        await TestRuntimeLogDockReusesSameWindowForWorldAndBattle();
        await Cleanup();
        Quit(_test.Finish("World map runtime log dock regression"));
    }

    private async Task TestRuntimeLogDockReusesSameWindowForWorldAndBattle()
    {
        Error createError = (Error)_gameSession.StartNewGame(TestConfigPath);
        _test.Eq(createError, Error.Ok, "runtime log dock 回归前置：应能创建测试世界。");
        if (createError != Error.Ok)
            return;

        WorldMapSystem worldMap = WorldMapScene.Instantiate<WorldMapSystem>();
        Root.AddChild(worldMap);
        await ProcessFrames(2);

        RuntimeLogDock runtimeLogDock = worldMap.GetNode<RuntimeLogDock>("%RuntimeLogDock");
        Control mapViewport = worldMap.GetNode<Control>("MapViewport");
        _test.True(runtimeLogDock != null, "world_map.tscn 应提供共享 RuntimeLogDock。");
        if (runtimeLogDock == null)
        {
            await DisposeNode(worldMap);
            return;
        }

        GDictionary logEntry = _gameSession.LogEvent("info", "world", "world.runtime_log_dock.test", "世界日志窗口回归。");
        logEntry.Dispose();
        worldMap.RenderFromRuntime(false);
        await ProcessFrames(1);

        _test.Eq(runtimeLogDock.title_label.Text, "运行日志", "世界态应使用共享日志窗口显示运行日志。");
        _test.True(
            runtimeLogDock.log_output.GetParsedText().Contains("世界日志窗口回归。"),
            "世界态共享日志窗口应显示最近运行日志。"
        );
        _test.True(runtimeLogDock.meta_label.Text.Contains("最近"), "世界态共享日志窗口元信息应显示运行日志缓冲摘要。");

        if (mapViewport != null)
        {
            Rect2 viewportRect = mapViewport.GetGlobalRect();
            Rect2 dockRect = runtimeLogDock.GetGlobalRect();
            Vector2 designPanelSize = runtimeLogDock.GetDesignPanelSize();
            _test.True(Mathf.IsEqualApprox(viewportRect.Size.X, worldMap.Size.X), "世界态 MapViewport 应为全宽，日志窗口浮在地图之上。");
            _test.True(
                viewportRect.Position.X + viewportRect.Size.X > dockRect.Position.X,
                "世界态共享日志窗口应覆盖在地图之上，而不是把地图挤开。"
            );
            _test.True(
                IsVector2Close(dockRect.Size, designPanelSize, 1.0f),
                $"世界态共享日志窗口默认尺寸应使用锁定宽度与默认高度。 actual={dockRect.Size} expected={designPanelSize}"
            );
            _test.True(
                runtimeLogDock.log_output.GetThemeFontSize("normal_font_size") >= 18,
                "共享日志窗口正文输出字体应放大到更易读的尺寸。"
            );
            _test.True(
                runtimeLogDock.GetThemeStylebox("panel") is StyleBoxFlat,
                "共享日志窗口应使用半透明深色填充面板。"
            );
            var panelStyle = runtimeLogDock.GetThemeStylebox("panel") as StyleBoxFlat;
            _test.True(panelStyle != null && panelStyle.BgColor.A < 1.0f, "共享日志窗口面板背景应为半透明（alpha < 1）。");
            _test.True(panelStyle != null && panelStyle.BorderWidthTop > 0, "共享日志窗口应有可见描边。");

            int defaultFontSize = runtimeLogDock.log_output.GetThemeFontSize("normal_font_size");
            Vector2I originalRootSize = Root.Size;
            Root.Size = new Vector2I(960, 540);
            worldMap.Size = Root.Size;
            await ProcessFrames(1);
            Rect2 resizedDockRect = runtimeLogDock.GetGlobalRect();
            _test.True(Mathf.IsEqualApprox(resizedDockRect.Size.X, dockRect.Size.X), "世界态共享日志窗口宽度应在窗口缩放时保持锁定。");
            _test.True(resizedDockRect.Size.Y < dockRect.Size.Y, "世界态共享日志窗口高度应随窗口高度缩小。");
            _test.Eq(
                runtimeLogDock.log_output.GetThemeFontSize("normal_font_size"),
                defaultFontSize,
                "共享日志窗口宽度锁定后，正文输出字体不应随高度拉伸变化。"
            );
            Root.Size = originalRootSize;
            worldMap.Size = Root.Size;
            await ProcessFrames(1);
        }

        GameRuntimeFacade runtime = worldMap._runtime;
        _test.True(runtime != null, "runtime log dock 回归前置：world_map 场景应初始化 runtime。");
        if (runtime == null)
        {
            await DisposeNode(worldMap);
            return;
        }

        EncounterAnchorData encounterAnchor = FindEncounterAnchorByKind(_gameSession.GetWorldData(), "single");
        _test.True(encounterAnchor != null, "runtime log dock 回归需要至少一个单体野怪遭遇。");
        if (encounterAnchor == null)
        {
            await DisposeNode(worldMap);
            return;
        }

        _gameSession.SetBattleSaveLock(true);
        runtime.StartBattle(encounterAnchor);
        worldMap.RenderFromRuntime(true);
        await ProcessFrames(2);

        _test.Eq(runtimeLogDock.title_label.Text, "战斗日志", "进入战斗后应复用同一个日志窗口切到战斗日志。");
        _test.True(
            runtimeLogDock.log_output.GetParsedText().Contains("战斗开始："),
            "进入战斗后共享日志窗口应切到 battle start 之后的战斗日志。"
        );
        _test.True(runtimeLogDock.meta_label.Text.Contains("上限"), "进入战斗后共享日志窗口元信息应切到 battle log 容量摘要。");
        if (mapViewport != null)
        {
            Rect2 battleViewportRect = mapViewport.GetGlobalRect();
            Rect2 battleDockRect = runtimeLogDock.GetGlobalRect();
            _test.True(
                battleViewportRect.Position.X + battleViewportRect.Size.X > battleDockRect.Position.X,
                "进入战斗后日志窗口应覆盖在战斗地图上，而不是继续把战斗地图挤开。"
            );
            _test.True(
                Mathf.IsEqualApprox(battleViewportRect.Size.X, Root.Size.X),
                "进入战斗后 MapViewport 应恢复为全宽战斗地图。"
            );
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

    private static EncounterAnchorData FindEncounterAnchorByKind(GDictionary worldData, StringName kind)
    {
        if (worldData == null || !worldData.ContainsKey("encounter_anchors"))
            return null;
        foreach (Variant value in worldData["encounter_anchors"].AsGodotArray())
        {
            EncounterAnchorData anchor = value.As<EncounterAnchorData>();
            if (anchor != null && anchor.encounter_kind == kind)
                return anchor;
        }
        return null;
    }

    private static bool IsVector2Close(Vector2 actual, Vector2 expected, float tolerance)
    {
        return Mathf.Abs(actual.X - expected.X) <= tolerance
            && Mathf.Abs(actual.Y - expected.Y) <= tolerance;
    }
}
