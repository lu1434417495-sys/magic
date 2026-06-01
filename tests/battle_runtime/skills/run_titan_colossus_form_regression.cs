using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_titan_colossus_form_regression : SceneTree
{
    private static readonly StringName TitanColossusForm = "titan_colossus_form";
    private static readonly StringName TitanGiantFormStatus = "titan_giant_form";
    private static readonly StringName TitanColossusChargeKey = "racial_skill_titan_colossus_form";

    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestTitanColossusFormChangesAndRestoresBodySize();
        TestBodySizeRestoreWaitsWhenPreviousFootprintIsBlocked();

        GodotSharpCleanup.collect_pending_finalizers();

        if (_failures.Count == 0)
        {
            GD.Print("Titan colossus form regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Titan colossus form regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestTitanColossusFormChangesAndRestoresBodySize()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildState(new Vector2I(5, 5));
        BattleUnitState titan = BuildUnit("titan_user", new Vector2I(1, 1));
        AssertTrue(
            titan.set_body_size_category(BodySizeContentRules.BODY_SIZE_CATEGORY_LARGE),
            "测试前置：泰坦升华单位应为 large。"
        );
        titan.known_active_skill_ids = new GStringNameArray { TitanColossusForm };
        titan.known_skill_level_map[TitanColossusForm] = 1;
        titan.per_battle_charges[TitanColossusChargeKey] = 1;
        AddUnit(runtime, state, titan);
        state.ally_unit_ids = new GStringNameArray { titan.unit_id };
        state.active_unit_id = titan.unit_id;
        runtime._state = state;

        BattleCommand command = new()
        {
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = titan.unit_id,
            skill_id = TitanColossusForm,
            target_unit_id = titan.unit_id,
        };

        BattlePreview preview = runtime.preview_command(command);
        AssertTrue(preview != null && preview.allowed, "Titan Colossus Form 应允许自施放。");

        BattleEventBatch batch = runtime.issue_command(command);
        AssertTrue(
            batch != null && batch.changed_unit_ids.Contains(titan.unit_id),
            "Titan Colossus Form 应记录施法者变更。"
        );
        AssertEq(
            titan.body_size_category,
            BodySizeContentRules.BODY_SIZE_CATEGORY_HUGE,
            "Titan Colossus Form 应临时改为 huge category。"
        );
        AssertEq(
            titan.body_size,
            BodySizeContentRules.BODY_SIZE_HUGE,
            "Titan Colossus Form 应同步 huge 的 int body_size。"
        );
        AssertTrue(
            titan.has_status_effect(TitanGiantFormStatus),
            "Titan Colossus Form 应挂 battle-local status。"
        );
        AssertEq(
            GetInt(titan.per_battle_charges, TitanColossusChargeKey, -1),
            0,
            "Titan Colossus Form 应消耗身份技能次数。"
        );

        BattleStatusEffectState statusEntry = titan.get_status_effect(TitanGiantFormStatus);
        AssertTrue(statusEntry != null, "Titan giant form status 应可读取。");
        if (statusEntry != null)
        {
            AssertEq(
                ReadString(statusEntry.@params, "previous_body_size_category"),
                "large",
                "巨神化 status 应记录恢复体型。"
            );
            AssertEq(
                ReadString(statusEntry.@params, "body_size_category_override"),
                "huge",
                "巨神化 status 应记录覆盖体型。"
            );
        }

        AssertTrue(
            runtime._advance_unit_status_durations(titan, 80, null),
            "巨神化持续时间耗尽时应产生状态变化。"
        );
        AssertTrue(
            !titan.has_status_effect(TitanGiantFormStatus),
            "巨神化过期后 status 应移除。"
        );
        AssertEq(
            titan.body_size_category,
            BodySizeContentRules.BODY_SIZE_CATEGORY_LARGE,
            "巨神化过期后应恢复 large category。"
        );
        AssertEq(
            titan.body_size,
            BodySizeContentRules.BODY_SIZE_LARGE,
            "巨神化过期后应恢复 large int body_size。"
        );
        runtime.Dispose();
    }

    private void TestBodySizeRestoreWaitsWhenPreviousFootprintIsBlocked()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildState(new Vector2I(5, 5));
        BattleUnitState shrunken = BuildUnit("blocked_restore_user", new Vector2I(1, 1));
        AssertTrue(
            shrunken.set_body_size_category(BodySizeContentRules.BODY_SIZE_CATEGORY_MEDIUM),
            "测试前置：单位当前为 medium。"
        );
        BattleUnitState blocker = BuildUnit("blocked_restore_occupant", new Vector2I(2, 1));
        AddUnit(runtime, state, shrunken);
        AddUnit(runtime, state, blocker);
        state.ally_unit_ids = new GStringNameArray { shrunken.unit_id, blocker.unit_id };
        runtime._state = state;

        BattleStatusEffectState status = new()
        {
            status_id = "blocked_body_restore",
            duration = 1,
            @params = new GDictionary
            {
                ["body_size_category_override"] =
                    BodySizeContentRules.BODY_SIZE_CATEGORY_MEDIUM.ToString(),
                ["previous_body_size_category"] =
                    BodySizeContentRules.BODY_SIZE_CATEGORY_LARGE.ToString(),
            },
        };
        shrunken.set_status_effect(status);

        runtime._advance_unit_status_durations(shrunken, 5, null);

        AssertTrue(
            shrunken.has_status_effect("blocked_body_restore"),
            "恢复 footprint 被占用时，体型覆盖 status 应保留以便后续重试。"
        );
        AssertEq(
            shrunken.body_size_category,
            BodySizeContentRules.BODY_SIZE_CATEGORY_MEDIUM,
            "恢复失败时不应切换到会覆盖占位者的 large category。"
        );
        BattleUnitState occupant = runtime._grid_service.get_unit_at_coord(state, blocker.coord);
        AssertTrue(occupant == blocker, "恢复失败时不应覆盖目标 footprint 上的其他单位。");
        runtime.Dispose();
    }

    private static BattleRuntimeModule BuildRuntime()
    {
        ProgressionContentRegistry registry = new();
        BattleRuntimeModule runtime = new();
        runtime.setup(null, registry.get_skill_defs(), new GDictionary(), new GDictionary());
        registry.Dispose();
        return runtime;
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        BattleState state = new()
        {
            battle_id = "titan_colossus_form",
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
            cells = new GDictionary(),
            units = new GDictionary(),
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                state.cells[coord] = BuildCell(coord);
            }
        }
        state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
        return state;
    }

    private static BattleCellState BuildCell(Vector2I coord)
    {
        BattleCellState cell = new()
        {
            coord = coord,
            base_terrain = BattleCellState.TERRAIN_LAND(),
            base_height = 4,
            height_offset = 0,
        };
        cell.recalculate_runtime_values();
        return cell;
    }

    private static BattleUnitState BuildUnit(StringName unitId, Vector2I coord)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
            current_ap = 2,
            current_hp = 30,
            current_mp = 0,
            current_stamina = 30,
            current_aura = 0,
            is_alive = true,
        };
        unit.set_anchor_coord(coord);
        return unit;
    }

    private static void AddUnit(BattleRuntimeModule runtime, BattleState state, BattleUnitState unit)
    {
        state.units[unit.unit_id] = unit;
        runtime._grid_service.place_unit(state, unit, unit.coord, true);
    }

    private static int GetInt(GDictionary source, StringName key, int fallback)
    {
        if (source == null || !source.ContainsKey(key))
            return fallback;
        Variant value = source[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static string ReadString(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
            return "";
        return source[key].ToString();
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} Expected {expected}, got {actual}.");
    }
}
