using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class run_battle_unit_hover_details_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private WorldMapSystem _world;
    private E2eWait _wait;
    private E2eInputDriver _input;
    private BattleState _state;
    private BattleUnitState _ally;
    private BattleUnitState _enemy;
    private BattleMapPanel Panel => _world.battle_map_panel;
    private BattleBoard2D Board => Panel._battle_board;
    private BattleHoverPreviewOverlay Overlay => Panel.hover_overlay;

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private async void Run()
    {
        try
        {
            _wait = new E2eWait(this);
            _input = new E2eInputDriver(this, _wait);
            var session = Root.GetNode<GameSession>("GameSession");
            _test.Eq((Error)session.StartNewGame("test"), Error.Ok, "创建隔离世界。");
            _world = EngineAssetAccess.ResolveCodeAssetBorrowed<PackedScene>(
                "res://scenes/main/world_map.tscn").Instantiate<WorldMapSystem>();
            Root.AddChild(_world);
            await _wait.FramesAsync(2);
            var runtime = _world._runtime;
            runtime.StartBattle(runtime.GetActiveWorldRuntimeData().EncounterAnchors.First(a => a.encounter_kind == "single"));
            await _wait.UntilAsync(() => runtime.GetBattleState()?.IsEmpty() == false, 600, "生成战斗");
            _world.SetProcess(false);
            _state = runtime.GetBattleState();
            runtime.CommandConfirmBattleStartTyped();
            _ally = _state.GetUnit(_state.GetAllyUnitIdsTyped()[0]);
            _enemy = _state.GetUnit(_state.GetEnemyUnitIdsTyped()[0]);
            foreach (BattleCellState cell in _state.Cells())
            {
                cell.SetBaseHeight(4); cell.SetHeightOffset(0); cell.SetTerrain("land"); cell.prop_ids.Clear();
            }
            _state.RebuildCellColumns();
            _state.MarkRuntimeEdgesDirty();
            Vector2I center = _state.map_size / 2;
            _test.True(runtime.GetBattleRuntime()._grid_service.PlaceUnit(_state, _ally, center, true), "友方就位。");
            _test.True(runtime.GetBattleRuntime()._grid_service.PlaceUnit(_state, _enemy, center + Vector2I.Right * 2, true), "敌方就位。");
            _ally.display_name = "悬停法师";
            _ally.battle_sprite_asset_id = "battle.unit.player.mage";
            _enemy.display_name = "悬停敌兵";
            _enemy.battle_sprite_asset_id = "battle.unit.player.warrior";
            _state.active_unit_id = _ally.unit_id;
            _state.PhaseKind = BattlePhaseKind.UnitActing;
            _ally.control_mode = "manual";
            _ally.attribute_snapshot.SetValue("hp_max", 60);
            _ally.attribute_snapshot.SetValue("mp_max", 80);
            _ally.attribute_snapshot.SetValue("stamina_max", 50);
            _ally.attribute_snapshot.SetValue("intelligence", 18);
            _ally.UnlockCombatResource("mp");
            _ally.SetCurrentMp(37); _ally.SetCurrentStamina(29);
            _enemy.SetStatusEffect(new() { status_id = "attack_up", display_label = "攻击提升", stacks = 1, duration = -1 });
            ((IGameRuntimeBattleSessionPort)runtime).SetBattleSelectedCoord(center);

            foreach (Vector2I size in new[] { new Vector2I(1280, 720), new(3840, 2160) })
            {
                new DisplaySettingsService().ApplySettings(new(size, false), Root);
                _ally.SetCurrentHp(45);
                _ally.SetStatusEffect(new() { status_id = "poisoned", stacks = 2, duration = 30 });
                _world.RenderFromRuntime(false);
                await _wait.UntilAsync(() => Panel.IsBattleRenderContentReady() && !Panel.IsLoadingBattle(), 600, "战场绘制");
                await _wait.FramesAsync(8);
                await MoveOutside();
                Vector2I selection = runtime.GetBattleSelectedCoord();
                await HoverBody(_ally);
                _test.Eq(runtime.GetBattleSelectedCoord(), selection, "悬停不改变当前选择。");
                _test.Eq(runtime.GetSelectedBattleSkillId(), new StringName(""), "查看人物不要求选技能。");
                AssertText("45/60", "37/80", "29/50", "智力 18", "中毒", "30 TU");
                AssertBounds();
                await Capture($"ally_details_{size.X}x{size.Y}");

                ulong generation = Board._controller.TerrainArtGeneration;
                _ally.SetCurrentHp(21);
                _ally.SetStatusEffect(new() { status_id = "poisoned", stacks = 3, duration = 19 });
                var batch = new BattleEventBatch(); batch.AddChangedUnitId(_ally.unit_id);
                _world.RenderFromRuntime(false, BattlePresentationDeltaFactory.Create(batch));
                await _wait.FramesAsync(5);
                AssertText("21/60", "×3", "19 TU");
                _test.Eq(Board._controller.TerrainArtGeneration, generation, "人物状态刷新不重建地形。");

                await MoveOutside();
                await HoverBody(_enemy);
                AssertText("悬停敌兵", "敌方", "攻击提升", "持续生效");
                _test.False(AllText(Overlay).Contains("中毒"), "切换敌人不残留友方状态。");
                AssertBounds();
                await Capture($"enemy_details_{size.X}x{size.Y}");
                await MoveOutside();
                await HoverBody(_ally);
                for (int i = 0; i < 9; i++)
                    _ally.SetStatusEffect(new() { status_id = new StringName($"hover_fixture_{i}"), display_label = $"测试增益 {i + 1}", stacks = 1, duration = 40 + i });
                batch = new BattleEventBatch(); batch.AddChangedUnitId(_ally.unit_id);
                _world.RenderFromRuntime(false, BattlePresentationDeltaFactory.Create(batch));
                await _wait.FramesAsync(6);
                var scroll = Overlay.GetNode<ScrollContainer>("HoverLayout/StatusScroll");
                await MovePointer(scroll, scroll.Size * 0.5f);
                await _wait.FramesAsync(30);
                _test.True(Overlay.Visible, "鼠标进入详情面板后应保留，允许滚动。");
                await _input.ClickAsync(scroll, MouseButton.WheelDown);
                await _input.ClickAsync(scroll, MouseButton.WheelDown);
                _test.True(scroll.ScrollVertical > 0, "多状态详情使用真实滚轮查看，不能挤出屏幕。");
                AssertBounds();
                await Capture($"many_statuses_{size.X}x{size.Y}");
                for (int i = 0; i < 9; i++) _ally.EraseStatusEffect(new StringName($"hover_fixture_{i}"));
                await MoveOutside();
            }
            Panel.HideBattle();
            _test.False(Overlay.Visible, "离开战斗关闭人物详情。");
        }
        catch (Exception exception) { _test.Fail($"Unit hover details: {exception}"); }
        finally
        {
            if (_input != null) await _input.ReleaseAllAsync();
            _world?.QueueFree();
            if (_wait != null) await _wait.FramesAsync(2);
            RequestTestExit(_test.Finish("Battle unit hover details regression"));
        }
    }

    private async Task HoverBody(BattleUnitState unit)
    {
        Sprite2D sprite = Board.unit_layer.GetNode<Node2D>(unit.unit_id.ToString()).GetNode<Sprite2D>("UnitSprite");
        using Image image = sprite.Texture.GetImage();
        if (image.IsCompressed()) image.Decompress();
        Rect2I bounds = image.GetUsedRect();
        Vector2I pixel = new(bounds.Position.X + bounds.Size.X / 2, bounds.Position.Y + bounds.Size.Y / 3);
        bool found = false;
        for (int d = 0; d < bounds.Size.X / 2 && !found; d++)
        foreach (int x in new[] { pixel.X + d, pixel.X - d })
        {
            if (x < 0 || x >= image.GetWidth() || image.GetPixel(x, pixel.Y).A < 0.9f) continue;
            pixel.X = x; found = true; break;
        }
        _test.True(found, "选取人物上半身的真实不透明像素。");
        Vector2 margin = sprite.Texture is AtlasTexture atlas ? atlas.Margin.Position : Vector2.Zero;
        Vector2 bodyPoint = sprite.ToGlobal((Vector2)pixel + margin - sprite.Texture.GetSize() * 0.5f);
        _test.Eq(Board.ResolveHoverCoord(bodyPoint), unit.GetAnchorCoord(), "人物上半身应命中人物，不能误识别背后地面。");
        await MovePointer(Panel.map_viewport_container, bodyPoint);
        await _wait.UntilAsync(() => Overlay.Visible && Overlay.DisplayedUnitId == unit.unit_id, 120, "悬停人物详情");
        await _wait.FramesAsync(6);
    }

    private async Task MovePointer(Control control, Vector2 local)
    {
        Vector2 position = control.GetGlobalTransform() * local;
        using var motion = new InputEventMouseMotion { Position = position, GlobalPosition = position };
        control.GetViewport().PushInput(motion, true);
        await _wait.FramesAsync(3);
    }

    private async Task MoveOutside()
    {
        await MovePointer(Panel.resolve_button, new Vector2(5, 5));
        await _wait.UntilAsync(() => !Overlay.Visible, 120, "移开人物与详情后关闭浮层");
    }

    private void AssertBounds()
    {
        Rect2 rect = Overlay.GetGlobalRect();
        _test.True(Panel.GetGlobalRect().Encloses(rect), $"人物详情必须处于屏幕内：{rect}");
        _test.True(Overlay.Size.X <= 420 && Overlay.Size.Y <= 580, $"人物详情需限制尺寸：{Overlay.Size}");
    }

    private void AssertText(params string[] values)
    {
        string text = AllText(Overlay);
        foreach (string value in values) _test.True(text.Contains(value), $"详情应包含 {value}，实际 {text}");
    }

    private static string AllText(Node node) =>
        (node is Label label && label.IsVisibleInTree() ? label.Text : "")
        + string.Join("\n", node.GetChildren().Select(AllText));

    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        string dir = System.Environment.GetEnvironmentVariable("MAGIC_BATTLE_HUD_CAPTURE_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        System.IO.Directory.CreateDirectory(dir);
        using Image image = Root.GetTexture().GetImage();
        _test.Eq(image.SavePng(System.IO.Path.Combine(dir, name + ".png")), Error.Ok, "保存原生悬停详情。");
    }
}
