using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_titan_colossus_form_regression : LifecycleTestSceneTree
{
    private static readonly StringName TitanColossusForm = "titan_colossus_form";
    private static readonly StringName TitanGiantFormStatus = "titan_giant_form";
    private static readonly StringName TitanColossusChargeKey = "racial_skill_titan_colossus_form";

    private readonly TestHarness _test = new();
    private ContentSnapshot _contentSnapshot;

    public override void _Initialize()
    {
        ProcessFrame += RunOnFirstProcessFrame;
    }

    private void RunOnFirstProcessFrame()
    {
        ProcessFrame -= RunOnFirstProcessFrame;
        _contentSnapshot = GameSessionTestFactory.GetProcessSnapshot();
        Run();
    }

    private void Run()
    {
        TestTitanColossusFormChangesAndRestoresBodySize();
        TestBodySizeRestoreWaitsWhenPreviousFootprintIsBlocked();


        RequestTestExit(_test.Finish("Titan colossus form regression"));
    }

    private void TestTitanColossusFormChangesAndRestoresBodySize()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildState(new Vector2I(5, 5));
        BattleUnitState titan = BuildUnit("titan_user", new Vector2I(1, 1));
        _test.True(
            titan.SetBodySizeCategory(BodySizeContentRules.ToStringName(BodySizeCategoryKind.Large)),
            "测试前置：泰坦升华单位应为 large。"
        );
        titan.known_active_skill_ids = new GStringNameArray { TitanColossusForm };
        titan.known_skill_level_map[TitanColossusForm] = 1;
        titan.per_battle_charges[TitanColossusChargeKey] = 1;
        AddUnit(runtime, state, titan);
        state.ally_unit_ids = new GStringNameArray { titan.unit_id };
        state.active_unit_id = titan.unit_id;
        runtime.SetupStateForTests(state);

        BattleCommand command = new()
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = titan.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(TitanColossusForm),
            skill_id = TitanColossusForm,
            target_unit_id = titan.unit_id,
        };
        command.AddTargetUnitId(titan.unit_id);

        BattlePreview preview = runtime.PreviewCommand(command);
        _test.True(preview != null && preview.allowed, "Titan Colossus Form 应允许自施放。");

        BattleEventBatch batch = runtime.IssueCommand(command);
        _test.True(
            batch != null && batch.changed_unit_ids.Contains(titan.unit_id),
            "Titan Colossus Form 应记录施法者变更。"
        );
        _test.Eq(
            titan.body_size_category,
            BodySizeContentRules.ToStringName(BodySizeCategoryKind.Huge),
            "Titan Colossus Form 应临时改为 huge category。"
        );
        _test.Eq(
            titan.body_size,
            BodySizeContentRules.ToBodySize(BodySizeCategoryKind.Huge),
            "Titan Colossus Form 应同步 huge 的 int body_size。"
        );
        _test.True(
            titan.HasStatusEffect(TitanGiantFormStatus),
            "Titan Colossus Form 应挂 battle-local status。"
        );
        _test.Eq(
            titan.per_battle_charges.Get(TitanColossusChargeKey, -1),
            0,
            "Titan Colossus Form 应消耗身份技能次数。"
        );

        BattleStatusEffectState statusEntry = titan.GetStatusEffect(TitanGiantFormStatus);
        _test.True(statusEntry != null, "Titan giant form status 应可读取。");
        if (statusEntry != null)
        {
            _test.Eq(
                statusEntry.previous_body_size_category,
                BodySizeContentRules.ToStringName(BodySizeCategoryKind.Large),
                "巨神化 status 应记录恢复体型。"
            );
            _test.Eq(
                statusEntry.body_size_category_override,
                BodySizeContentRules.ToStringName(BodySizeCategoryKind.Huge),
                "巨神化 status 应记录覆盖体型。"
            );
        }

        _test.True(
            runtime._advance_unit_status_durations(titan, 80, null),
            "巨神化持续时间耗尽时应产生状态变化。"
        );
        _test.True(
            !titan.HasStatusEffect(TitanGiantFormStatus),
            "巨神化过期后 status 应移除。"
        );
        _test.Eq(
            titan.body_size_category,
            BodySizeContentRules.ToStringName(BodySizeCategoryKind.Large),
            "巨神化过期后应恢复 large category。"
        );
        _test.Eq(
            titan.body_size,
            BodySizeContentRules.ToBodySize(BodySizeCategoryKind.Large),
            "巨神化过期后应恢复 large int body_size。"
        );
        runtime.Dispose();
    }

    private void TestBodySizeRestoreWaitsWhenPreviousFootprintIsBlocked()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildState(new Vector2I(5, 5));
        BattleUnitState shrunken = BuildUnit("blocked_restore_user", new Vector2I(1, 1));
        _test.True(
            shrunken.SetBodySizeCategory(BodySizeContentRules.ToStringName(BodySizeCategoryKind.Medium)),
            "测试前置：单位当前为 medium。"
        );
        BattleUnitState blocker = BuildUnit("blocked_restore_occupant", new Vector2I(2, 1));
        AddUnit(runtime, state, shrunken);
        AddUnit(runtime, state, blocker);
        state.ally_unit_ids = new GStringNameArray { shrunken.unit_id, blocker.unit_id };
        runtime.SetupStateForTests(state);

        BattleStatusEffectState status = new()
        {
            status_id = "blocked_body_restore",
            duration = 1,
            body_size_category_override = BodySizeContentRules.ToStringName(BodySizeCategoryKind.Medium),
            previous_body_size_category = BodySizeContentRules.ToStringName(BodySizeCategoryKind.Large),
        };
        shrunken.SetStatusEffect(status);

        runtime._advance_unit_status_durations(shrunken, 5, null);

        _test.True(
            shrunken.HasStatusEffect("blocked_body_restore"),
            "恢复 footprint 被占用时，体型覆盖 status 应保留以便后续重试。"
        );
        _test.Eq(
            shrunken.body_size_category,
            BodySizeContentRules.ToStringName(BodySizeCategoryKind.Medium),
            "恢复失败时不应切换到会覆盖占位者的 large category。"
        );
        BattleUnitState occupant = runtime._grid_service.GetUnitAtCoord(state, blocker.coord);
        _test.True(occupant == blocker, "恢复失败时不应覆盖目标 footprint 上的其他单位。");
        runtime.Dispose();
    }

    private BattleRuntimeModule BuildRuntime()
    {
        BattleRuntimeModule runtime = new();
        runtime.setup(
            null,
            _contentSnapshot.Skills,
            new Dictionary<StringName, EnemyTemplateDefinition>(),
            new Dictionary<StringName, EnemyAiBrainDefinition>()
        );
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
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                state.SetCell(coord, BuildCell(coord));
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleCellState BuildCell(Vector2I coord)
    {
        BattleCellState cell = new()
        {
            coord = coord,
            base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
            base_height = 4,
            height_offset = 0,
        };
        cell.RecalculateRuntimeValues();
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
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static void AddUnit(BattleRuntimeModule runtime, BattleState state, BattleUnitState unit)
    {
        state.SetUnit(unit);
        runtime._grid_service.PlaceUnit(state, unit, unit.coord, true);
    }

    private static int GetInt(GDictionary source, StringName key, int fallback)
    {
        if (source == null || !source.ContainsKey(key))
            return fallback;
        Variant value = source[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

}
