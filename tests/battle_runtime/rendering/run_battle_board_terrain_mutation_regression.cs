using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class run_battle_board_terrain_mutation_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private readonly BattleBoardSnapshotBuilder _snapshots = new();
    private readonly Vector2I _target = new(6, 6);
    private BattleRuntimeModule _runtime;
    private BattleState _state;
    private BattleUnitState _caster;
    private BattleBoard2D _board;
    private int _renderOwners;

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private async void Run()
    {
        try
        {
            BuildFixture();
            _board = EngineAssetAccess.ResolveCodeAssetBorrowed<PackedScene>(
                "res://scenes/ui/battle_board_2d.tscn").Instantiate<BattleBoard2D>();
            Root.AddChild(_board);
            await ToSignal(this, SignalName.ProcessFrame);
            Root.Size = new Vector2I(3840, 2160);
            _board.SetViewportSize(Root.Size);
            await RefreshAndAssert("initial_height_2");

            await CastTerrainSkill("mage_rampart_raise", "pillar", 5, "raised_height_5");
            for (int height = 4; height >= -5; height--)
                await CastTerrainSkill("mage_fossil_to_mud", "lower_single_1", height, $"lowered_height_{height}");

            // Returning to the same surface must remove every old face, tree and grid command.
            for (int height = -2; height <= 7; height += 3)
                await CastTerrainSkill("mage_rampart_raise", "pillar", height, $"raised_height_{height}");
            await CastTerrainSkill("mage_rampart_raise", "pillar", 8, "raised_max_height_8");

            Vector2I forest = new(4, 5);
            _test.True(_board.GetNodeOrNull<Sprite2D>("PaintedOak_4_5") != null,
                "森林 fixture 应先显示树木。");
            Vector2 treeBefore = _board.GetNode<Sprite2D>("PaintedOak_4_5").Position;
            var raise = TestSkillDefinitionProjection.LoadSkillDefinition("mage_rampart_raise")
                .CombatProfile.EffectDefinitions.Single(e => e.MinSkillLevel == 7);
            var forestBatch = new BattleEventBatch();
            _runtime.ApplyGroundTerrainEffectsResultTyped(_caster, null, new[] { raise }, new[] { forest }, forestBatch);
            AssertFullRefresh(forestBatch, forest);
            await RefreshAndAssert("forest_raised");
            _test.True(_board.GetNode<Sprite2D>("PaintedOak_4_5").Position.IsEqualApprox(
                treeBefore - new Vector2(0, 3 * _board._render_profile.visual_height_step)),
                "升高森林格时树木应随地面升高，不能悬空或留在旧高度。");
            var replace = TestSkillDefinitionProjection.LoadSkillDefinition("mage_fossil_to_mud")
                .CombatProfile.CastVariants.Single(v => v.VariantId == "mud_single").EffectDefinitions[0];
            var replaceBatch = new BattleEventBatch();
            _runtime.ApplyGroundTerrainEffectsResultTyped(_caster, null, new[] { replace }, new[] { forest }, replaceBatch);
            AssertFullRefresh(replaceBatch, forest);
            await RefreshAndAssert("forest_removed");
            _test.True(_board.GetNodeOrNull<Sprite2D>("PaintedOak_4_5") == null,
                "森林被地形效果替换后，旧树木必须消失。");
            _test.True(_board.GetNodeOrNull<BattleTerrainPaintLayer>("TreeShadow_4_5") == null,
                "森林替换后旧树影必须消失。");

            Root.Size = new Vector2I(1280, 720);
            _board.SetViewportSize(Root.Size);
            await RefreshAndAssert("mutation_720p");
        }
        catch (Exception exception)
        {
            _test.Fail($"Terrain mutation rendering: {exception}");
        }
        finally
        {
            if (_board != null)
            {
                _board.Free();
                _board = null;
            }
            BattleTestFixture.DisposeBattleFixture(_runtime, _state);
            RequestTestExit(_test.Finish("Battle board terrain mutation regression"));
        }
    }

    private void BuildFixture()
    {
        var skills = new Dictionary<StringName, SkillDefinition>();
        foreach (string id in new[] { "mage_rampart_raise", "mage_fossil_to_mud" })
            skills.Add(id, TestSkillDefinitionProjection.LoadSkillDefinition(id));
        _runtime = new BattleRuntimeModule();
        _runtime.setup(skill_definitions: skills);
        BattleTestFixture.ConfigureHitResolverForTests(_runtime, new FixedHitResolver(10));
        BattleTestFixture.ConfigureDamageResolverForTests(_runtime, new DeterministicBattleDamageResolver());
        _state = new BattleState { battle_id = "terrain_mutation_board", map_size = new(7, 7),
            terrain_profile_id = "canyon", phase = "unit_acting" };
        for (int y = 0; y < 7; y++)
        for (int x = 0; x < 7; x++)
        {
            var cell = new BattleCellState { coord = new(x, y) };
            cell.SetBaseHeight(2);
            if (x == 4 && y == 5) cell.SetTerrain("forest");
            if (x == 1 && y >= 4) cell.SetTerrain("shallow_water");
            _state.SetCell(cell);
        }
        _state.RebuildCellColumns();
        _caster = BuildUnit("terrain_caster", _target, "player");
        foreach (StringName id in skills.Keys)
        {
            _caster.AddKnownActiveSkill(id);
            _caster.SetKnownSkillLevelTyped(id, 7);
        }
        _state.SetUnit(_caster);
        _state.ally_unit_ids.Add(_caster.unit_id);
        BattleUnitState enemy = BuildUnit("terrain_enemy", Vector2I.Zero, "enemy");
        _state.SetUnit(enemy);
        _state.enemy_unit_ids.Add(enemy.unit_id);
        foreach (BattleUnitState unit in _state.Units())
            _test.True(_runtime._grid_service.PlaceUnit(_state, unit, unit.GetAnchorCoord(), true), "fixture 单位落位。");
        _state.active_unit_id = _caster.unit_id;
        _runtime.SetupStateForTests(_state);
    }

    private static BattleUnitState BuildUnit(StringName id, Vector2I coord, StringName faction)
    {
        var unit = new BattleUnitState { unit_id = id, display_name = id.ToString(),
            faction_id = faction, control_mode = "manual" }.WithCombatResourcesForTest(
                hp: 10000, mp: 1000, stamina: 1000, ap: 20, isAlive: true);
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue("hp_max", 10000);
        unit.attribute_snapshot.SetValue("mp_max", 1000);
        unit.attribute_snapshot.SetValue("stamina_max", 1000);
        unit.attribute_snapshot.SetValue("action_points", 20);
        unit.attribute_snapshot.SetValue("intelligence", 100);
        unit.attribute_snapshot.SetValue("willpower", 100);
        unit.attribute_snapshot.SetValue("armor_class", 10);
        unit.UnlockCombatResource("mp");
        return unit;
    }

    private async Task CastTerrainSkill(StringName skill, StringName variant, int expectedHeight, string label)
    {
        // Each case starts an available fixture turn; effect execution and payment are production code.
        _state.phase = "unit_acting";
        _state.active_unit_id = _caster.unit_id;
        _caster.SetCurrentAp(20);
        _caster.SetCurrentMp(1000);
        _caster.SetCooldownTyped(skill, 0);
        var command = new BattleCommand { command_type = "skill", unit_id = _caster.unit_id,
            skill_id = skill, skill_entry_id = BattleSkillEntryIds.KnownSkill(skill),
            skill_variant_id = variant, target_coord = _target };
        command.SetTargetCoords(new[] { _target });
        BattlePreview preview = _runtime.PreviewCommand(command);
        _test.True(preview.allowed, $"{label}: 正式技能预览应允许：{string.Join(" | ", preview.log_lines)}");
        BattleEventBatch batch = _runtime.IssueCommand(command);
        _test.Eq(_state.GetCell(_target).current_height, expectedHeight,
            $"{label}: 正式技能执行应改变 runtime 高度。{string.Join(" | ", batch.LogLinesTyped)}");
        _test.True(_caster.GetCurrentMp() < 1000, $"{label}: 正式施法应支付法力。");
        AssertFullRefresh(batch, _target);
        await RefreshAndAssert(label);
    }

    private void AssertFullRefresh(BattleEventBatch batch, Vector2I coord)
    {
        BattlePresentationDelta delta = BattlePresentationDeltaFactory.Create(batch);
        _test.True(delta.RequiresFullBoardRefresh, "地形技能必须触发完整棋盘刷新，不能只刷新单位。");
        _test.True(batch.ChangedCoordsTyped.Contains(coord), "地形变动必须报告实际改变的坐标。");
    }

    private async Task RefreshAndAssert(string label)
    {
        var oldPaintNodes = _board.GetChildren().OfType<BattleTerrainPaintLayer>().ToArray();
        _board.Configure(_snapshots.Build(_state), _target);
        await ToSignal(this, SignalName.ProcessFrame);
        foreach (Node node in oldPaintNodes)
            _test.False(GodotObject.IsInstanceValid(node), $"{label}: 旧地形绘制节点必须释放。");
        _test.True(_board.IsRenderContentReady(), $"{label}: 更新后棋盘应完成重建。");
        _test.Eq(_board._controller.PaintedSurfaceCount, _state.CellCount, $"{label}: 表面不能缺失或累加。");
        if (_renderOwners == 0) _renderOwners = _board._controller.RenderOwnerCount;
        _test.Eq(_board._controller.RenderOwnerCount, _renderOwners,
            $"{label}: 连续升降重建不能累积 native resource owner。");
        int height = _state.GetCell(_target).current_height;
        Vector2 expected = _board.input_layer.MapToLocal(_target)
            - new Vector2(0, height * _board._render_profile.visual_height_step);
        _test.True(_board._get_coord_anchor(_target).IsEqualApprox(expected),
            $"{label}: 拾取锚点应使用真实有符号高度 {height}。");
        var grid = _board.GetNodeOrNull<BattleTerrainPaintLayer>($"TacticalGridH{height}");
        _test.True(grid != null, $"{label}: 格线必须落在真实高度层。");
        if (grid != null)
            _test.True(grid.Patches.Any(p => Average(p.Points).IsEqualApprox(expected)),
                $"{label}: 格线几何应跟随新高度。");
        var top = _board.GetNodeOrNull<BattleTerrainPaintLayer>($"PaintTopH{height}");
        _test.True(top != null && top.Patches.Any(p => Average(p.Points).IsEqualApprox(expected)),
            $"{label}: 手绘表面必须跟随新高度。");
        var marker = _board.GetNodeOrNull<TileMapLayer>($"MarkerH{height}");
        _test.True(marker != null && marker.GetCellSourceId(_target) >= 0,
            $"{label}: 选中标记必须跟随新高度。");
        if (marker != null)
            _test.True((marker.MapToLocal(_target) + marker.Position).IsEqualApprox(expected),
                $"{label}: 标记绘制和拾取必须对齐。");
        Node2D token = _board.unit_layer.GetNode<Node2D>(_caster.unit_id.ToString());
        _test.True(token.Position.IsEqualApprox(expected + _board._render_profile.unit_anchor_bias),
            $"{label}: 单位应跟随升降后的地面。");
        if (height < 0)
        {
            _test.True(token.ZIndex < 0 && grid?.ZIndex < 0 && marker?.ZIndex < 0,
                $"{label}: 负高度对象应按真实层级排序。");
            _test.True(_board.GetChildren().OfType<BattleTerrainPaintLayer>()
                .Any(layer => layer.Name.ToString().StartsWith("PaintFace", StringComparison.Ordinal)
                    && layer.ZIndex < 0 && layer.Patches.Count > 0),
                $"{label}: 零层以下的落差必须有实际岩壁绘制。");
        }

        // Click the exposed front portion, since a deep cell's rear can be occluded by its bank.
        Vector2I clicked = new(-1, -1);
        void OnClick(Vector2I coord) => clicked = coord;
        _board.battle_cell_clicked += OnClick;
        Vector2 click = _board.ToGlobal(expected + new Vector2(0, _board._render_profile.tile_half_size.Y * 0.65f));
        _board.HandleViewportMouseButton(click, (int)MouseButton.Left);
        _board.battle_cell_clicked -= OnClick;
        _test.Eq(clicked, _target, $"{label}: 实际显示的前沿必须命中同一个格子。");
    }

    private static Vector2 Average(Vector2[] points)
    {
        Vector2 sum = Vector2.Zero;
        foreach (Vector2 point in points) sum += point;
        return sum / points.Length;
    }
}
