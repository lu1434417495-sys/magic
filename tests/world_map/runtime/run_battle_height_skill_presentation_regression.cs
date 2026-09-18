using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

// Scene integration with controlled battle setup, followed by real HUD input.
// Unlike a board-only test, casts must reach the screen through the runtime proxy.
public partial class run_battle_height_skill_presentation_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private WorldMapSystem _world;
    private E2eWait _wait;
    private E2eInputDriver _input;
    private BattleState _state;
    private BattleUnitState _caster;
    private Vector2I _target;
    private GameRuntimeFacade Runtime => _world._runtime;
    private BattleMapPanel Panel => _world.battle_map_panel;
    private BattleBoard2D Board => Panel._battle_board;

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private async void Run()
    {
        try
        {
            _wait = new E2eWait(this);
            _input = new E2eInputDriver(this, _wait);
            GameSession session = Root.GetNode<GameSession>("GameSession");
            _test.Eq((Error)session.StartNewGame("test"), Error.Ok, "创建隔离测试世界。");
            _world = EngineAssetAccess.ResolveCodeAssetBorrowed<PackedScene>(
                "res://scenes/main/world_map.tscn").Instantiate<WorldMapSystem>();
            Root.AddChild(_world);
            await _wait.FramesAsync(2);
            EncounterAnchorData encounter = Runtime.GetActiveWorldRuntimeData().EncounterAnchors
                .First(anchor => anchor.encounter_kind == "single");
            Runtime.StartBattle(encounter);
            await _wait.UntilAsync(() => Runtime.GetBattleState()?.IsEmpty() == false, 600, "战斗生成");
            _world.SetProcess(false);
            _state = Runtime.GetBattleState();
            _caster = _state.GetUnit(_state.GetAllyUnitIdsTyped()[0]);
            // The front corner remains visible when lowered below its neighbors;
            // an enclosed center pit can correctly be occluded by foreground land.
            _target = _state.map_size - Vector2I.One;
            ((IGameRuntimeBattleSessionPort)Runtime).SetBattleSelectedCoord(_target);
            Runtime.CommandConfirmBattleStartTyped();
            _test.Eq(_state.ModalStateKind, BattleModalStateKind.None, "正式确认战斗后应解除开始提示锁。");
            _test.True(_state.Cells().All(cell => cell.current_height >= 4 && cell.current_height <= 8),
                "正式生成地图必须保持修改前的初始高度范围 4～8，不能归零或相对最低格重新编号。");
            foreach (Vector2I size in new[] { new Vector2I(1280, 720), new(3840, 2160) })
            {
                new DisplaySettingsService().ApplySettings(new(size, false), Root);
                await RenderBoard();
                AssertBoundaryDatum(0);
                await SaveCapture($"generated_base_{size.X}x{size.Y}");
            }
            PrepareBattle();

            foreach (Vector2I size in new[] { new Vector2I(1280, 720), new(3840, 2160) })
            {
                new DisplaySettingsService().ApplySettings(new(size, false), Root);
                FlattenTerrain(4);
                await RenderBoard();
                AssertBoundaryDatum(0);
                await SaveCapture($"minimum_4_base_{size.X}x{size.Y}");
                await CastThroughHud("mage_rampart_raise", "pillar", 7);
                await SaveCapture($"raised_7_{size.X}x{size.Y}");
                for (int height = 6; height >= -1; height--)
                {
                    await CastThroughHud("mage_fossil_to_mud", "lower_single_1", height);
                    if (height == 3) await SaveCapture($"lowered_3_{size.X}x{size.Y}");
                }
                await SaveCapture($"lowered_minus1_{size.X}x{size.Y}");
                FlattenTerrain(0);
                await RenderBoard();
                AssertBoundaryDatum(-1);
                await SaveCapture($"zero_base_{size.X}x{size.Y}");
            }
        }
        catch (Exception exception) { _test.Fail($"Height skill presentation: {exception}"); }
        finally
        {
            if (_input != null) await _input.ReleaseAllAsync();
            _world?.QueueFree();
            if (_wait != null) await _wait.FramesAsync(2);
            RequestTestExit(_test.Finish("Battle height skill presentation regression"));
        }
    }

    private void PrepareBattle()
    {
        _state.timeline.frozen = false;
        _state.phase = "unit_acting";
        _state.active_unit_id = _caster.unit_id;
        _caster.control_mode = "manual";
        _caster.battle_sprite_asset_id = "battle.unit.player.mage";
        _caster.SetKnownActiveSkillIds(new StringName[] { "mage_rampart_raise", "mage_fossil_to_mud" });
        foreach (StringName skill in new StringName[] { "mage_rampart_raise", "mage_fossil_to_mud" })
            _caster.SetKnownSkillLevelTyped(skill, 7);
        _caster.attribute_snapshot.SetValue("mp_max", 10000);
        _caster.attribute_snapshot.SetValue("action_points", 100);
        _caster.attribute_snapshot.SetValue("intelligence", 100);
        _caster.attribute_snapshot.SetValue("willpower", 100);
        _caster.UnlockCombatResource("mp");
        _caster.SetCurrentMp(10000);
        _caster.SetCurrentAp(100);
        FlattenTerrain(4);
        _test.True(Runtime.GetBattleRuntime()._grid_service.PlaceUnit(_state, _caster, _target + Vector2I.Left, true),
            "测试施法者站在目标旁边。");
        ((IGameRuntimeBattleSessionPort)Runtime).SetBattleSelectedCoord(_target);
        Runtime.RefreshBattleRuntimeState();
    }

    private void FlattenTerrain(int height)
    {
        foreach (BattleCellState cell in _state.Cells())
        {
            cell.SetBaseHeight(height);
            cell.SetHeightOffset(0);
            cell.SetTerrain("land");
            cell.prop_ids.Clear();
        }
        _state.RebuildCellColumns();
        _state.MarkRuntimeEdgesDirty();
    }

    private async Task RenderBoard()
    {
        _world.RenderFromRuntime(false);
        await _wait.UntilAsync(() => Panel.IsBattleRenderContentReady() && !Panel.IsLoadingBattle(),
            600, "战斗界面绘制");
        await _wait.FramesAsync(8);
    }

    private void AssertBoundaryDatum(int bottom)
    {
        // The front corner exposes both outer faces. Check actual quads, including
        // every layer down to the old fixed zero datum, not merely any cliff node.
        Vector2I corner = _state.map_size - Vector2I.One;
        int surface = _state.GetCell(corner).current_height;
        Vector2 origin = Board.input_layer.MapToLocal(Vector2I.Zero);
        Vector2 axisX = Board.input_layer.MapToLocal(Vector2I.Right) - origin;
        Vector2 axisY = Board.input_layer.MapToLocal(Vector2I.Down) - origin;
        float step = Board._render_profile.visual_height_step;
        foreach (bool right in new[] { true, false })
        for (int height = surface; height > bottom; height--)
        {
            Vector2 anchor = Board.input_layer.MapToLocal(corner) - new Vector2(0, height * step);
            Vector2 a = anchor + (right ? (axisX - axisY) : (axisX + axisY)) * 0.5f;
            Vector2 b = anchor + (right ? (axisX + axisY) : (axisY - axisX)) * 0.5f;
            var face = Board.GetNodeOrNull<BattleTerrainPaintLayer>(
                $"PaintFaceR{corner.X + corner.Y}H{height}_False_{right}");
            _test.True(face != null && face.Patches.Any(patch =>
                    patch.Points[0].IsEqualApprox(a) && patch.Points[1].IsEqualApprox(b)
                    && Mathf.IsEqualApprox(patch.Points[2].Y, b.Y + step + 0.5f)),
                $"外围高度 {surface} 必须完整画到基底 {bottom}：缺失 {height}→{height - 1} 层岩壁，方向 {right}。");
        }
        _test.True(Board.GetNodeOrNull<BattleTerrainPaintLayer>(
            $"PaintFaceR{corner.X + corner.Y}H{bottom}_False_True") == null,
            $"外围岩壁应止于基底 {bottom}，不能越界堆叠。");
    }

    private async Task CastThroughHud(StringName skillId, StringName variantId, int expectedHeight)
    {
        // Start another available fixture turn; all selection, payment, effects and
        // presentation after this point must be driven by the real controls.
        _state.PhaseKind = BattlePhaseKind.UnitActing;
        _state.active_unit_id = _caster.unit_id;
        _caster.SetCurrentAp(100);
        _caster.SetCurrentMp(10000);
        _caster.SetCooldownTyped(skillId, 0);
        _world.RenderFromRuntime(false);
        await _wait.FramesAsync(3);
        using var hud = new BattleHudAdapter();
        hud.SetupRuntimeContext(Runtime);
        BattleHudSnapshot snapshot = hud.BuildSnapshot(_state, _target, "", "", "",
            Array.Empty<Vector2I>(), 0, Array.Empty<StringName>(), "", "", null);
        var slots = snapshot.SkillSlots.Where(slot => !slot.IsEmpty).ToArray();
        int index = Array.FindIndex(slots, slot => slot.SkillId == skillId.ToString());
        _test.True(index >= 0, $"技能栏必须提供 {skillId}。");
        if (index < 0) throw new InvalidOperationException("Missing terrain skill slot.");
        Button button = Panel.skill_grid.GetChild(index).GetChildren().OfType<BattleSkillSlotButton>().Single();
        if (Runtime.GetSelectedBattleSkillId() != skillId)
            await _input.ClickAsync(button);
        _test.Eq(Runtime.GetSelectedBattleSkillId(), skillId, "真实技能图标点击应选中技能。");
        for (int attempt = 0; Runtime.GetSelectedBattleSkillVariantId() != variantId && attempt < 16; attempt++)
            await _input.ClickAsync(Panel.next_variant_button);
        _test.Eq(Runtime.GetSelectedBattleSkillVariantId(), variantId, "真实变体按钮选择指定升降幅度。");

        int before = _state.GetCell(_target).current_height;
        ulong generation = Board._controller.TerrainArtGeneration;
        Vector2 position = Board.CoordToViewportPosition(_target)
            + new Vector2(0, Board._render_profile.tile_half_size.Y * 0.65f * Board.GetCameraZoom());
        await _input.ClickAtAsync(Panel.map_viewport_container, position);
        // Single-target skills may queue or execute on click according to their actual selection mode.
        if (_state.GetCell(_target).current_height == before)
            await _input.ClickAsync(Panel.resolve_button);
        await _wait.FramesAsync(8);
        int actual = _state.GetCell(_target).current_height;
        _test.Eq(actual, expectedHeight, $"{skillId}/{variantId}: 从界面施法应改变真实地形高度。");
        _test.True(Board._controller.TerrainArtGeneration > generation, "正式输入后的 delta 必须重建地形画面。");
        Vector2 anchor = Board.input_layer.MapToLocal(_target)
            - new Vector2(0, expectedHeight * Board._render_profile.visual_height_step);
        _test.True(Board._get_coord_anchor(_target).IsEqualApprox(anchor),
            "技能提交后不手动刷新，棋盘拾取锚点应已更新到新高度。");
        var top = Board.GetNodeOrNull<BattleTerrainPaintLayer>($"PaintTopH{expectedHeight}R{_target.X + _target.Y}");
        _test.True(top != null && top.Patches.Any(patch =>
            (patch.Points.Aggregate(Vector2.Zero, (sum, point) => sum + point) / patch.Points.Length)
                .IsEqualApprox(anchor)), "正式输入后的实际地面必须处于新高度。");
        AssertBoundaryDatum(expectedHeight <= 0 ? expectedHeight - 1 : 0);
        ConsoleProcessOutput.WriteStandard($"HUD cast {skillId}/{variantId}: {before} -> {actual}, generation {generation} -> {Board._controller.TerrainArtGeneration}");
        if (actual != expectedHeight) throw new InvalidOperationException("Terrain skill did not execute.");
    }

    private async Task SaveCapture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string directory = System.Environment.GetEnvironmentVariable("MAGIC_BATTLE_HUD_CAPTURE_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        System.IO.Directory.CreateDirectory(directory);
        using Image image = Root.GetTexture().GetImage();
        _test.Eq(image.SavePng(System.IO.Path.Combine(directory, name + ".png")), Error.Ok, "保存原生升降画面。");
    }
}
