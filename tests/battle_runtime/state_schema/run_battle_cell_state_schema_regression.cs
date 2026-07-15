using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_cell_state_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestValidRoundTripWithEdgeWallAndTimedEffect();
        TestRejectsMissingField();
        TestRejectsExtraField();
        TestRejectsWrongType();
        TestRejectsStringNumericValues();
        TestRejectsNonArrayIds();
        TestRejectsEmptyIdEntry();
        TestRejectsBadTimedTerrainEffectEntry();
        TestRejectsBadEdgeFeatureEntry();
        TestNullEdgeFeatureSerializesAsCurrentNonePayload();
        TestAllowsEmptyOccupantUnitId();
        TestOwnerMutationApiNormalizesCellFields();

        RequestTestExit(_test.Finish("Battle cell state schema regression"));
    }

    private void TestValidRoundTripWithEdgeWallAndTimedEffect()
    {
        BattleCellState source = BuildValidCell();
        using GodotProjectionLease<GDictionary> payloadLease = source.ToDictionaryLease(
            LifetimeDomain.Request,
            "run_battle_cell_state_schema_regression.valid_round_trip"
        );
        GDictionary payload = payloadLease.Value;
        BattleCellState restored = BattleCellState.FromDictionary(payload);
        _test.True(restored != null, "合法 BattleCellState payload 应能恢复。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(restored.coord, source.coord, "roundtrip 应保留 coord。");
        _test.Eq(restored.base_terrain, source.base_terrain, "roundtrip 应保留 base_terrain。");
        _test.Eq(restored.base_height, source.base_height, "roundtrip 应保留 base_height。");
        _test.Eq(restored.height_offset, source.height_offset, "roundtrip 应保留 height_offset。");
        _test.Eq(restored.current_height, source.current_height, "roundtrip 应保留 current_height。");
        _test.Eq(restored.stack_layer, source.stack_layer, "roundtrip 应保留 stack_layer。");
        _test.Eq(restored.move_cost, source.move_cost, "roundtrip 应保留 move_cost。");
        _test.Eq(restored.flow_direction, source.flow_direction, "roundtrip 应保留 flow_direction。");
        AssertStringNameArrayEq(restored.prop_ids, new[] { "stone_pillar", "torch" }, "roundtrip 应保留 prop_ids。");
        AssertStringNameArrayEq(
            restored.terrain_effect_ids,
            new[] { "rapid_current" },
            "roundtrip 应保留 terrain_effect_ids。"
        );
        _test.Eq(restored.timed_terrain_effects.Count, 1, "roundtrip 应恢复 timed terrain effect。");
        if (restored.timed_terrain_effects.Count > 0)
        {
            _test.Eq(
                restored.timed_terrain_effects[0].field_instance_id,
                new StringName("field_001"),
                "roundtrip 应保留 terrain effect 字段。"
            );
        }
        _test.Eq(
            restored.edge_feature_east.feature_kind,
            BattleEdgeFeatureState.ToStringName(BattleEdgeFeatureKind.Wall),
            "roundtrip 应恢复 east wall。"
        );
        _test.Eq(
            restored.edge_feature_south.feature_kind,
            BattleEdgeFeatureState.ToStringName(BattleEdgeFeatureKind.None),
            "roundtrip 应恢复 south none edge。"
        );

        BattleCellState duplicate = restored.DuplicateCell();
        _test.True(duplicate != null, "duplicate_cell 应继续可用。");
        if (duplicate != null)
        {
            _test.Eq(
                duplicate.edge_feature_east.feature_kind,
                BattleEdgeFeatureState.ToStringName(BattleEdgeFeatureKind.Wall),
                "duplicate_cell 应复制 edge feature。"
            );
        }

        Dictionary<Vector2I, List<BattleCellState>> columns =
            BattleCellState.BuildColumnsFromSurfaceCells(
                new Dictionary<Vector2I, BattleCellState> { [restored.coord] = restored }
            );
        _test.True(columns.ContainsKey(restored.coord), "build_columns_from_surface_cells 应继续为合法 cell 生成列。");
        if (columns.ContainsKey(restored.coord))
        {
            _test.True(
                columns[restored.coord].Count > 0,
                "build_columns_from_surface_cells 生成的列不应为空。"
            );
        }
    }

    private void TestRejectsMissingField()
    {
        using GodotProjectionLease<GDictionary> payloadLease = ValidPayloadLease();
        GDictionary payload = payloadLease.Value;
        payload.Remove("move_cost");
        _test.True(BattleCellState.FromDictionary(payload) == null, "from_dict 应拒绝缺字段 payload。");
    }

    private void TestRejectsExtraField()
    {
        using GodotProjectionLease<GDictionary> payloadLease = ValidPayloadLease();
        GDictionary payload = payloadLease.Value;
        payload["legacy_height"] = 2;
        _test.True(BattleCellState.FromDictionary(payload) == null, "from_dict 应拒绝额外旧字段 payload。");
    }

    private void TestRejectsWrongType()
    {
        using GodotProjectionLease<GDictionary> coordLease = ValidPayloadLease();
        GDictionary payload = coordLease.Value;
        payload["coord"] = new Vector2(1.0f, 2.0f);
        _test.True(BattleCellState.FromDictionary(payload) == null, "from_dict 应拒绝 coord 非 Vector2i。");

        using GodotProjectionLease<GDictionary> passableLease = ValidPayloadLease();
        payload = passableLease.Value;
        payload["passable"] = "true";
        _test.True(BattleCellState.FromDictionary(payload) == null, "from_dict 应拒绝 passable 非 bool。");

        using GodotProjectionLease<GDictionary> terrainLease = ValidPayloadLease();
        payload = terrainLease.Value;
        payload["base_terrain"] = "";
        _test.True(BattleCellState.FromDictionary(payload) == null, "from_dict 应拒绝空 base_terrain。");

        using GodotProjectionLease<GDictionary> occupantLease = ValidPayloadLease();
        payload = occupantLease.Value;
        payload["occupant_unit_id"] = 12;
        _test.True(BattleCellState.FromDictionary(payload) == null, "from_dict 应拒绝 occupant_unit_id 非 String/StringName。");

        using GodotProjectionLease<GDictionary> flowLease = ValidPayloadLease();
        payload = flowLease.Value;
        payload["flow_direction"] = new Vector2(1.0f, 0.0f);
        _test.True(BattleCellState.FromDictionary(payload) == null, "from_dict 应拒绝 flow_direction 非 Vector2i。");

        using GodotProjectionLease<GDictionary> moveCostLease = ValidPayloadLease();
        payload = moveCostLease.Value;
        payload["move_cost"] = 0;
        _test.True(BattleCellState.FromDictionary(payload) == null, "from_dict 应拒绝非正 move_cost。");
    }

    private void TestRejectsStringNumericValues()
    {
        foreach (string fieldName in new[] { "stack_layer", "base_height", "height_offset", "current_height", "move_cost" })
        {
            using GodotProjectionLease<GDictionary> payloadLease = ValidPayloadLease();
            GDictionary payload = payloadLease.Value;
            payload[fieldName] = "1";
            _test.True(BattleCellState.FromDictionary(payload) == null, $"from_dict 应拒绝字符串数值字段 {fieldName}。");
        }
    }

    private void TestRejectsNonArrayIds()
    {
        using GodotProjectionLease<GDictionary> propLease = ValidPayloadLease();
        GDictionary payload = propLease.Value;
        payload["prop_ids"] = "rock";
        _test.True(BattleCellState.FromDictionary(payload) == null, "from_dict 应拒绝非 Array prop_ids。");

        using GodotProjectionLease<GDictionary> terrainEffectLease = ValidPayloadLease();
        payload = terrainEffectLease.Value;
        payload["terrain_effect_ids"] = "mud";
        _test.True(BattleCellState.FromDictionary(payload) == null, "from_dict 应拒绝非 Array terrain_effect_ids。");
    }

    private void TestRejectsEmptyIdEntry()
    {
        using GodotProjectionLease<GDictionary> propLease = ValidPayloadLease();
        GDictionary payload = propLease.Value;
        payload["prop_ids"] = propLease.Own(
            new GArray { "rock", "" },
            "run_battle_cell_state_schema_regression.bad_prop_ids"
        );
        _test.True(BattleCellState.FromDictionary(payload) == null, "from_dict 应拒绝空 prop id entry。");

        using GodotProjectionLease<GDictionary> terrainEffectLease = ValidPayloadLease();
        payload = terrainEffectLease.Value;
        payload["terrain_effect_ids"] = terrainEffectLease.Own(
            new GArray { new StringName("mud"), 7 },
            "run_battle_cell_state_schema_regression.bad_terrain_effect_ids"
        );
        _test.True(BattleCellState.FromDictionary(payload) == null, "from_dict 应拒绝非 String/StringName terrain effect id entry。");
    }

    private void TestRejectsBadTimedTerrainEffectEntry()
    {
        using GodotProjectionLease<GDictionary> payloadLease = ValidPayloadLease();
        using GodotProjectionLease<GDictionary> effectLease = BuildTimedEffect().ToDictionaryLease();
        GDictionary payload = payloadLease.Value;
        payload["timed_terrain_effects"] = payloadLease.Own(
            new GArray { effectLease.Value, "bad_effect_entry" },
            "run_battle_cell_state_schema_regression.bad_timed_effects"
        );
        _test.True(BattleCellState.FromDictionary(payload) == null, "from_dict 应拒绝 timed_terrain_effects 坏 entry。");
    }

    private void TestRejectsBadEdgeFeatureEntry()
    {
        using GodotProjectionLease<GDictionary> payloadLease = ValidPayloadLease();
        GDictionary payload = payloadLease.Value;
        payload["edge_feature_east"] = "bad_edge_entry";
        _test.True(BattleCellState.FromDictionary(payload) == null, "from_dict 应拒绝 edge feature 坏 entry。");
    }

    private void TestNullEdgeFeatureSerializesAsCurrentNonePayload()
    {
        BattleCellState cell = BuildValidCell();
        cell.edge_feature_east = null;
        using GodotProjectionLease<GDictionary> payloadLease = cell.ToDictionaryLease(
            LifetimeDomain.Request,
            "run_battle_cell_state_schema_regression.null_edge"
        );
        GDictionary payload = payloadLease.Value;
        Variant edgePayload = payload["edge_feature_east"];
        _test.True(
            edgePayload.VariantType == Variant.Type.Dictionary,
            "null edge feature 的 to_dict 仍应输出当前 none edge Dictionary。"
        );
        if (edgePayload.VariantType == Variant.Type.Dictionary)
        {
            _test.True(
                edgePayload.AsGodotDictionary().ContainsKey("feature_kind"),
                "none edge payload 应包含正式字段。"
            );
        }

        BattleCellState restored = BattleCellState.FromDictionary(payload);
        _test.True(restored != null, "null edge feature 的 canonical to_dict payload 应能被 strict from_dict 恢复。");
        if (restored != null)
        {
            _test.Eq(
                restored.edge_feature_east.feature_kind,
                BattleEdgeFeatureState.ToStringName(BattleEdgeFeatureKind.None),
                "null edge feature 应恢复为 none。"
            );
        }
    }

    private void TestAllowsEmptyOccupantUnitId()
    {
        using GodotProjectionLease<GDictionary> payloadLease = ValidPayloadLease();
        GDictionary payload = payloadLease.Value;
        payload["occupant_unit_id"] = "";

        BattleCellState restored = BattleCellState.FromDictionary(payload);
        _test.True(restored != null, "from_dict 应接受空 occupant_unit_id 表示未占用格子。");
        if (restored != null)
        {
            _test.Eq(
                restored.occupant_unit_id,
                new StringName(""),
                "空 occupant_unit_id roundtrip 后应保持为空。"
            );
        }
    }

    private void TestOwnerMutationApiNormalizesCellFields()
    {
        BattleCellState cell = new();

        cell.SetCoord(new Vector2I(4, 5));
        _test.Eq(cell.coord, new Vector2I(4, 5), "SetCoord 应写入 cell 坐标。");

        cell.SetTerrain(BattleTerrainRules.ToStringName(BattleTerrainKind.FlowingWater));
        _test.Eq(
            cell.base_terrain,
            BattleTerrainRules.ToStringName(BattleTerrainKind.FlowingWater),
            "SetTerrain 应规范化 terrain id。"
        );

        cell.SetBaseHeight(3);
        cell.SetHeightOffset(2);
        _test.Eq(cell.current_height, 5, "SetBaseHeight/SetHeightOffset 应刷新 current_height。");
        _test.Eq(cell.stack_layer, 5, "SetBaseHeight/SetHeightOffset 应同步 stack_layer。");

        cell.SetMoveCost(-10);
        _test.Eq(cell.move_cost, 1, "SetMoveCost 应保持正数 move cost。");

        cell.SetOccupant("unit_001");
        _test.Eq(cell.occupant_unit_id, new StringName("unit_001"), "SetOccupant 应规范化 occupant id。");
        cell.ClearOccupant();
        _test.Eq(cell.occupant_unit_id, new StringName(""), "ClearOccupant 应清空 occupant id。");
    }

    private static BattleCellState BuildValidCell()
    {
        BattleCellState cell = new()
        {
            coord = new Vector2I(2, 3),
            base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.FlowingWater),
            base_height = 1,
            height_offset = 1,
        };
        cell.RecalculateRuntimeValues();
        cell.occupant_unit_id = "unit_001";
        cell.prop_ids = new List<StringName> { new("stone_pillar"), new("torch") };
        cell.terrain_effect_ids = new List<StringName> { new("rapid_current") };
        cell.timed_terrain_effects = new List<BattleTerrainEffectState>
        {
            BuildTimedEffect(),
        };
        cell.flow_direction = Vector2I.Right;
        cell.edge_feature_east = BattleEdgeFeatureState.MakeWall();
        cell.edge_feature_south = BattleEdgeFeatureState.MakeNone();
        return cell;
    }

    private static BattleTerrainEffectState BuildTimedEffect()
    {
        var effect = new BattleTerrainEffectState
        {
            field_instance_id = "field_001",
            effect_id = "burning_ground",
            effect_type = "damage",
            source_unit_id = "caster_001",
            source_skill_id = "flame_patch",
            target_team_filter = "enemy",
            power = 3,
            damage_tag = "fire",
            remaining_tu = 20,
            tick_interval_tu = 10,
            next_tick_at_tu = 10,
            stack_behavior = "refresh",
        };
        using GDictionary parameters = new() { ["damage_tag"] = "fire" };
        effect.@params = parameters;
        return effect;
    }

    private static GodotProjectionLease<GDictionary> ValidPayloadLease() =>
        BuildValidCell()
            .ToDictionaryLease(
                LifetimeDomain.Request,
                "run_battle_cell_state_schema_regression.valid_payload"
            );

    private void AssertStringNameArrayEq(IReadOnlyList<StringName> actual, IReadOnlyList<string> expected, string message)
    {
        if (actual == null || actual.Count != expected.Count)
        {
            _test.Fail($"{message} actual={FormatStringNameArray(actual)} expected=[{string.Join(", ", expected)}]");
            return;
        }
        for (int index = 0; index < expected.Count; index++)
        {
            if (actual[index].ToString() != expected[index])
            {
                _test.Fail($"{message} actual={FormatStringNameArray(actual)} expected=[{string.Join(", ", expected)}]");
                return;
            }
        }
    }

    private static string FormatStringNameArray(IEnumerable<StringName> values)
    {
        if (values == null)
        {
            return "<null>";
        }
        List<string> parts = new();
        foreach (StringName value in values)
        {
            parts.Add(value.ToString());
        }
        return $"[{string.Join(", ", parts)}]";
    }
}
