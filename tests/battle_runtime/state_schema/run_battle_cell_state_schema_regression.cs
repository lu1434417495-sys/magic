using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_cell_state_schema_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
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

        if (_failures.Count == 0)
        {
            GD.Print("Battle cell state schema regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle cell state schema regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestValidRoundTripWithEdgeWallAndTimedEffect()
    {
        BattleCellState source = BuildValidCell();
        GDictionary payload = source.to_dict();
        BattleCellState restored = BattleCellState.from_dict(payload);
        AssertTrue(restored != null, "合法 BattleCellState payload 应能恢复。");
        if (restored == null)
        {
            return;
        }

        AssertEq(restored.coord, source.coord, "roundtrip 应保留 coord。");
        AssertEq(restored.base_terrain, source.base_terrain, "roundtrip 应保留 base_terrain。");
        AssertEq(restored.base_height, source.base_height, "roundtrip 应保留 base_height。");
        AssertEq(restored.height_offset, source.height_offset, "roundtrip 应保留 height_offset。");
        AssertEq(restored.current_height, source.current_height, "roundtrip 应保留 current_height。");
        AssertEq(restored.stack_layer, source.stack_layer, "roundtrip 应保留 stack_layer。");
        AssertEq(restored.move_cost, source.move_cost, "roundtrip 应保留 move_cost。");
        AssertEq(restored.flow_direction, source.flow_direction, "roundtrip 应保留 flow_direction。");
        AssertStringNameArrayEq(restored.prop_ids, new[] { "stone_pillar", "torch" }, "roundtrip 应保留 prop_ids。");
        AssertStringNameArrayEq(
            restored.terrain_effect_ids,
            new[] { "rapid_current" },
            "roundtrip 应保留 terrain_effect_ids。"
        );
        AssertEq(restored.timed_terrain_effects.Count, 1, "roundtrip 应恢复 timed terrain effect。");
        if (restored.timed_terrain_effects.Count > 0)
        {
            AssertEq(
                restored.timed_terrain_effects[0].field_instance_id,
                new StringName("field_001"),
                "roundtrip 应保留 terrain effect 字段。"
            );
        }
        AssertEq(
            restored.edge_feature_east.feature_kind,
            BattleEdgeFeatureState.FEATURE_WALL(),
            "roundtrip 应恢复 east wall。"
        );
        AssertEq(
            restored.edge_feature_south.feature_kind,
            BattleEdgeFeatureState.FEATURE_NONE(),
            "roundtrip 应恢复 south none edge。"
        );

        BattleCellState duplicate = restored.duplicate_cell();
        AssertTrue(duplicate != null, "duplicate_cell 应继续可用。");
        if (duplicate != null)
        {
            AssertEq(
                duplicate.edge_feature_east.feature_kind,
                BattleEdgeFeatureState.FEATURE_WALL(),
                "duplicate_cell 应复制 edge feature。"
            );
        }

        GDictionary columns = BattleCellState.build_columns_from_surface_cells(
            new GDictionary { [restored.coord] = restored }
        );
        AssertTrue(columns.ContainsKey(restored.coord), "build_columns_from_surface_cells 应继续为合法 cell 生成列。");
        if (columns.ContainsKey(restored.coord))
        {
            AssertTrue(
                columns[restored.coord].AsGodotArray().Count > 0,
                "build_columns_from_surface_cells 生成的列不应为空。"
            );
        }
    }

    private void TestRejectsMissingField()
    {
        GDictionary payload = ValidPayload();
        payload.Remove("move_cost");
        AssertNull(BattleCellState.from_dict(payload), "from_dict 应拒绝缺字段 payload。");
    }

    private void TestRejectsExtraField()
    {
        GDictionary payload = ValidPayload();
        payload["legacy_height"] = 2;
        AssertNull(BattleCellState.from_dict(payload), "from_dict 应拒绝额外旧字段 payload。");
    }

    private void TestRejectsWrongType()
    {
        GDictionary payload = ValidPayload();
        payload["coord"] = new Vector2(1.0f, 2.0f);
        AssertNull(BattleCellState.from_dict(payload), "from_dict 应拒绝 coord 非 Vector2i。");

        payload = ValidPayload();
        payload["passable"] = "true";
        AssertNull(BattleCellState.from_dict(payload), "from_dict 应拒绝 passable 非 bool。");

        payload = ValidPayload();
        payload["base_terrain"] = "";
        AssertNull(BattleCellState.from_dict(payload), "from_dict 应拒绝空 base_terrain。");

        payload = ValidPayload();
        payload["occupant_unit_id"] = 12;
        AssertNull(BattleCellState.from_dict(payload), "from_dict 应拒绝 occupant_unit_id 非 String/StringName。");

        payload = ValidPayload();
        payload["flow_direction"] = new Vector2(1.0f, 0.0f);
        AssertNull(BattleCellState.from_dict(payload), "from_dict 应拒绝 flow_direction 非 Vector2i。");

        payload = ValidPayload();
        payload["move_cost"] = 0;
        AssertNull(BattleCellState.from_dict(payload), "from_dict 应拒绝非正 move_cost。");
    }

    private void TestRejectsStringNumericValues()
    {
        foreach (string fieldName in new[] { "stack_layer", "base_height", "height_offset", "current_height", "move_cost" })
        {
            GDictionary payload = ValidPayload();
            payload[fieldName] = "1";
            AssertNull(BattleCellState.from_dict(payload), $"from_dict 应拒绝字符串数值字段 {fieldName}。");
        }
    }

    private void TestRejectsNonArrayIds()
    {
        GDictionary payload = ValidPayload();
        payload["prop_ids"] = "rock";
        AssertNull(BattleCellState.from_dict(payload), "from_dict 应拒绝非 Array prop_ids。");

        payload = ValidPayload();
        payload["terrain_effect_ids"] = "mud";
        AssertNull(BattleCellState.from_dict(payload), "from_dict 应拒绝非 Array terrain_effect_ids。");
    }

    private void TestRejectsEmptyIdEntry()
    {
        GDictionary payload = ValidPayload();
        payload["prop_ids"] = new GArray { "rock", "" };
        AssertNull(BattleCellState.from_dict(payload), "from_dict 应拒绝空 prop id entry。");

        payload = ValidPayload();
        payload["terrain_effect_ids"] = new GArray { new StringName("mud"), 7 };
        AssertNull(BattleCellState.from_dict(payload), "from_dict 应拒绝非 String/StringName terrain effect id entry。");
    }

    private void TestRejectsBadTimedTerrainEffectEntry()
    {
        GDictionary payload = ValidPayload();
        payload["timed_terrain_effects"] = new GArray { BuildTimedEffect().to_dict(), "bad_effect_entry" };
        AssertNull(BattleCellState.from_dict(payload), "from_dict 应拒绝 timed_terrain_effects 坏 entry。");
    }

    private void TestRejectsBadEdgeFeatureEntry()
    {
        GDictionary payload = ValidPayload();
        payload["edge_feature_east"] = "bad_edge_entry";
        AssertNull(BattleCellState.from_dict(payload), "from_dict 应拒绝 edge feature 坏 entry。");
    }

    private void TestNullEdgeFeatureSerializesAsCurrentNonePayload()
    {
        BattleCellState cell = BuildValidCell();
        cell.edge_feature_east = null;
        GDictionary payload = cell.to_dict();
        Variant edgePayload = payload["edge_feature_east"];
        AssertTrue(
            edgePayload.VariantType == Variant.Type.Dictionary,
            "null edge feature 的 to_dict 仍应输出当前 none edge Dictionary。"
        );
        if (edgePayload.VariantType == Variant.Type.Dictionary)
        {
            AssertTrue(
                edgePayload.AsGodotDictionary().ContainsKey("feature_kind"),
                "none edge payload 应包含正式字段。"
            );
        }

        BattleCellState restored = BattleCellState.from_dict(payload);
        AssertTrue(restored != null, "null edge feature 的 canonical to_dict payload 应能被 strict from_dict 恢复。");
        if (restored != null)
        {
            AssertEq(
                restored.edge_feature_east.feature_kind,
                BattleEdgeFeatureState.FEATURE_NONE(),
                "null edge feature 应恢复为 none。"
            );
        }
    }

    private static BattleCellState BuildValidCell()
    {
        BattleCellState cell = new()
        {
            coord = new Vector2I(2, 3),
            base_terrain = BattleCellState.TERRAIN_FLOWING_WATER(),
            base_height = 1,
            height_offset = 1,
        };
        cell.recalculate_runtime_values();
        cell.occupant_unit_id = "unit_001";
        cell.prop_ids = new GStringNameArray { new StringName("stone_pillar"), new StringName("torch") };
        cell.terrain_effect_ids = new GStringNameArray { new StringName("rapid_current") };
        cell.timed_terrain_effects = new Godot.Collections.Array<BattleTerrainEffectState>
        {
            BuildTimedEffect(),
        };
        cell.flow_direction = Vector2I.Right;
        cell.edge_feature_east = BattleEdgeFeatureState.make_wall();
        cell.edge_feature_south = BattleEdgeFeatureState.make_none();
        return cell;
    }

    private static BattleTerrainEffectState BuildTimedEffect()
    {
        return new BattleTerrainEffectState
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
            @params = new GDictionary { ["damage_tag"] = "fire" },
        };
    }

    private static GDictionary ValidPayload()
    {
        return (GDictionary)BuildValidCell().to_dict().Duplicate(true);
    }

    private void AssertNull(object value, string message)
    {
        if (value != null)
        {
            _failures.Add($"{message} actual={value}");
        }
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            _failures.Add($"{message} actual={actual} expected={expected}");
        }
    }

    private void AssertStringNameArrayEq(GStringNameArray actual, IReadOnlyList<string> expected, string message)
    {
        if (actual == null || actual.Count != expected.Count)
        {
            _failures.Add($"{message} actual={FormatStringNameArray(actual)} expected=[{string.Join(", ", expected)}]");
            return;
        }
        for (int index = 0; index < expected.Count; index++)
        {
            if (actual[index].ToString() != expected[index])
            {
                _failures.Add($"{message} actual={FormatStringNameArray(actual)} expected=[{string.Join(", ", expected)}]");
                return;
            }
        }
    }

    private static string FormatStringNameArray(GStringNameArray values)
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
