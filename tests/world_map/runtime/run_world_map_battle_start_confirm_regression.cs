using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_battle_start_confirm_regression : LifecycleTestSceneTree
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
        await TestBattleStartConfirmStaysNonCancellableOnWorldMapScene();
        await Cleanup();
        RequestTestExit(_test.Finish("World map battle start confirm regression"));
    }

    private async Task TestBattleStartConfirmStaysNonCancellableOnWorldMapScene()
    {
        Error createError = (Error)_gameSession.StartNewGame(TestConfigPath);
        _test.Eq(createError, Error.Ok, "加载 world_map 场景前应能成功创建测试世界。");
        if (createError != Error.Ok)
            return;

        WorldMapSystem worldMap = WorldMapScene.Instantiate<WorldMapSystem>();
        Root.AddChild(worldMap);
        await ProcessFrames(2);

        GameRuntimeFacade runtime = worldMap._runtime;
        SubmapEntryWindow promptWindow = worldMap.GetNode<SubmapEntryWindow>("SubmapEntryWindow");
        _test.True(runtime != null, "world_map 场景应初始化 runtime。");
        _test.True(promptWindow != null, "world_map 场景应包含 SubmapEntryWindow。");
        if (runtime == null || promptWindow == null)
        {
            await DisposeNode(worldMap);
            return;
        }

        EncounterAnchorData encounterAnchor = FindEncounterAnchorByKind(
            _gameSession.GetWorldData(),
            "single"
        );
        _test.True(encounterAnchor != null, "battle-start confirm 场景回归需要至少一个单体野怪遭遇。");
        if (encounterAnchor == null)
        {
            await DisposeNode(worldMap);
            return;
        }

        _gameSession.SetBattleSaveLock(true);
        runtime.StartBattle(encounterAnchor);
        worldMap.RenderFromRuntime(true);
        await ProcessFrames(1);

        GDictionary startPrompt = runtime.GetPendingBattleStartPrompt();
        BattleState battleState = runtime.GetBattleState();
        _test.Eq(runtime.GetActiveModalId(), "battle_start_confirm", "开战后应进入 battle_start_confirm modal。");
        _test.True(startPrompt.ContainsKey("cancel_visible"), "battle-start confirm prompt 应显式包含 cancel_visible。");
        _test.True(startPrompt.ContainsKey("dismiss_on_shade"), "battle-start confirm prompt 应显式包含 dismiss_on_shade。");
        _test.False(DictBool(startPrompt, "cancel_visible", true), "battle-start confirm prompt 应显式禁用取消按钮。");
        _test.False(DictBool(startPrompt, "dismiss_on_shade", true), "battle-start confirm prompt 应显式禁用遮罩关闭。");
        _test.True(promptWindow.Visible, "battle-start confirm 打开时 SubmapEntryWindow 应可见。");
        _test.False(promptWindow.cancel_button.Visible, "battle-start confirm 模式下不应显示取消按钮。");
        _test.True(promptWindow.cancel_button.Disabled, "battle-start confirm 模式下取消按钮应保持禁用。");
        _test.True(
            battleState != null && battleState.modal_state.ToString() == "start_confirm",
            "battle state 应保持在 start_confirm。"
        );
        if (battleState?.timeline != null)
            _test.True(battleState.timeline.frozen, "battle-start confirm 打开时 timeline 应保持冻结。");

        promptWindow.shade.EmitSignal(
            Control.SignalName.GuiInput,
            MakeMouseButtonEvent(MouseButton.Left)
        );
        await ProcessFrames(1);
        _test.True(promptWindow.Visible, "battle-start confirm 模式下点击遮罩不应关闭窗口。");
        _test.Eq(runtime.GetActiveModalId(), "battle_start_confirm", "点击遮罩后 runtime modal 不应变化。");
        if (battleState?.timeline != null)
            _test.True(battleState.timeline.frozen, "点击遮罩后 timeline 应继续冻结。");

        promptWindow.cancel_button.EmitSignal(BaseButton.SignalName.Pressed);
        await ProcessFrames(1);
        _test.True(promptWindow.Visible, "battle-start confirm 模式下触发取消按钮回调不应关闭窗口。");
        _test.Eq(runtime.GetActiveModalId(), "battle_start_confirm", "触发取消按钮回调后 runtime modal 不应变化。");
        if (battleState?.timeline != null)
            _test.True(battleState.timeline.frozen, "触发取消按钮回调后 timeline 应继续冻结。");

        promptWindow.HideWindow();
        promptWindow.EmitSignal(SubmapEntryWindow.SignalName.cancelled);
        await ProcessFrames(1);
        _test.True(promptWindow.Visible, "battle-start confirm 模式下 stray cancel 信号后场景应重新显示确认窗。");
        _test.False(promptWindow.cancel_button.Visible, "stray cancel 信号后取消按钮仍应保持隐藏。");
        _test.Eq(runtime.GetActiveModalId(), "battle_start_confirm", "stray cancel 信号不应改写 runtime modal。");
        _test.False(
            DictBool(runtime.GetPendingBattleStartPrompt(), "cancel_visible", true),
            "stray cancel 信号后 runtime prompt 仍应保持不可取消契约。"
        );
        if (battleState?.timeline != null)
            _test.True(battleState.timeline.frozen, "stray cancel 信号后 timeline 应继续冻结。");

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

    private static InputEventMouseButton MakeMouseButtonEvent(MouseButton buttonIndex)
    {
        return new InputEventMouseButton { ButtonIndex = buttonIndex, Pressed = true };
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key) ? dictionary[key].AsBool() : fallback;
    }
}
