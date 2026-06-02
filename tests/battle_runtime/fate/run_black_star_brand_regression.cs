using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_black_star_brand_regression : SceneTree
{
    private static readonly StringName BLACK_STAR_BRAND_SKILL_ID = "black_star_brand";
    private static readonly StringName WARRIOR_GUARD_SKILL_ID = "warrior_guard";
    private static readonly StringName WARRIOR_HEAVY_STRIKE_SKILL_ID = "warrior_heavy_strike";
    private static readonly StringName STATUS_GUARDING = "guarding";
    private static readonly StringName STATUS_BLACK_STAR_BRAND_NORMAL = "black_star_brand_normal";
    private static readonly StringName STATUS_BLACK_STAR_BRAND_ELITE = "black_star_brand_elite";
    private static readonly StringName STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW =
        "black_star_brand_elite_guard_window";
    private static readonly StringName FORTUNE_MARK_TARGET_STAT_ID = "fortune_mark_target";

    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestBlackStarBrandFirstCastFreeThenCostsCalamity();
        TestBlackStarBrandNormalTargetBlocksGuardAndCounterattack();
        TestBlackStarBrandEliteTargetUsesEliteOnlyDebuffs();

        GodotSharpCleanup.collect_pending_finalizers();
        if (_failures.Count == 0)
        {
            GD.Print("Black star brand regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Black star brand regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestBlackStarBrandFirstCastFreeThenCostsCalamity()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        SkillDef blackStarBrand = GetSkill(runtime.get_skill_defs(), BLACK_STAR_BRAND_SKILL_ID);
        AssertTrue(blackStarBrand != null, "black_star_brand SkillDef 应能从内容注册表加载。");
        if (blackStarBrand == null)
        {
            runtime.dispose();
            return;
        }

        BattleState state = BuildSkillTestState("black_star_brand_costs", new Vector2I(5, 3));
        BattleUnitState caster = BuildUnit("brand_cost_caster", "施法者", "player", new Vector2I(1, 1), 3, "hero");
        caster.known_active_skill_ids = new GStringNameArray { BLACK_STAR_BRAND_SKILL_ID };
        caster.known_skill_level_map[BLACK_STAR_BRAND_SKILL_ID] = 1;
        BattleUnitState enemy = BuildUnit("brand_cost_enemy", "普通敌人", "enemy", new Vector2I(2, 1), 2);

        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, enemy);
        state.ally_unit_ids = new GStringNameArray { caster.unit_id };
        state.enemy_unit_ids = new GStringNameArray { enemy.unit_id };
        state.active_unit_id = caster.unit_id;
        runtime._state = state;
        BeginRuntimeBattle(runtime);

        AssertEq(runtime.get_black_star_brand_cast_cost("hero"), 0, "黑星烙印首次施放应为免费。");
        BattleCommand firstCommand = BuildSkillCommand(caster.unit_id, BLACK_STAR_BRAND_SKILL_ID, enemy);
        BattlePreview firstPreview = runtime.preview_command(firstCommand);
        AssertTrue(firstPreview != null && firstPreview.allowed, "首次免费施放时预览应允许黑星烙印。");
        BattleEventBatch firstBatch = runtime.issue_command(firstCommand);
        AssertTrue(enemy.has_status_effect(STATUS_BLACK_STAR_BRAND_NORMAL), "首次施放后普通敌人应获得普通黑星烙印。");
        AssertEq(runtime.get_member_calamity("hero"), 0, "首次免费施放后不应扣除 calamity。");
        AssertEq(runtime.get_black_star_brand_cast_cost("hero"), 1, "首次施放后应切换为每次 1 点 calamity。");
        AssertTrue(
            firstBatch != null && LogContains(firstBatch.log_lines, "无法反击"),
            $"首次施放成功后日志应说明普通黑星烙印效果。 log={firstBatch?.log_lines}"
        );

        caster.current_ap = 3;
        int apBeforeBlocked = caster.current_ap;
        BattlePreview blockedPreview = runtime.preview_command(firstCommand);
        AssertTrue(
            blockedPreview != null && !blockedPreview.allowed && LastLogContains(blockedPreview.log_lines, "calamity 不足"),
            $"后续施放在 calamity 为 0 时应被正式拦截。 log={blockedPreview?.log_lines}"
        );
        BattleEventBatch blockedBatch = runtime.issue_command(firstCommand);
        AssertEq(runtime.get_member_calamity("hero"), 0, "因 calamity 不足而失败时不应扣减资源。");
        AssertEq(caster.current_ap, apBeforeBlocked, "因 calamity 不足而失败时不应继续扣除行动点。");
        AssertTrue(
            blockedBatch != null && !blockedBatch.changed_unit_ids.Contains(caster.unit_id),
            "因 calamity 不足而失败时不应把施法者记录为已执行变更。"
        );

        runtime.calamity_by_member_id["hero"] = 2;
        caster.current_ap = 3;
        BattlePreview spendPreview = runtime.preview_command(firstCommand);
        AssertTrue(spendPreview != null && spendPreview.allowed, "有足够 calamity 时后续施放应允许。");
        runtime.issue_command(firstCommand);
        AssertEq(runtime.get_member_calamity("hero"), 1, "后续成功施放黑星烙印后应只扣除 1 点 calamity。");
        runtime.dispose();
    }

    private void TestBlackStarBrandNormalTargetBlocksGuardAndCounterattack()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildSkillTestState("black_star_brand_normal", new Vector2I(5, 3));
        BattleUnitState caster = BuildUnit("brand_normal_caster", "施法者", "player", new Vector2I(1, 1), 3, "hero");
        caster.known_active_skill_ids = new GStringNameArray { BLACK_STAR_BRAND_SKILL_ID };
        caster.known_skill_level_map[BLACK_STAR_BRAND_SKILL_ID] = 1;
        BattleUnitState enemy = BuildUnit("brand_normal_enemy", "普通敌人", "enemy", new Vector2I(2, 1), 2);
        enemy.current_stamina = 60;
        enemy.attribute_snapshot.set_value(AttributeService.STAMINA_MAX_ID(), 60);
        enemy.known_active_skill_ids = new GStringNameArray { WARRIOR_GUARD_SKILL_ID };
        enemy.known_skill_level_map[WARRIOR_GUARD_SKILL_ID] = 1;
        SetStatus(enemy, STATUS_GUARDING, 60);

        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, enemy);
        state.ally_unit_ids = new GStringNameArray { caster.unit_id };
        state.enemy_unit_ids = new GStringNameArray { enemy.unit_id };
        state.active_unit_id = caster.unit_id;
        runtime._state = state;
        BeginRuntimeBattle(runtime);

        BattleCommand brandCommand = BuildSkillCommand(caster.unit_id, BLACK_STAR_BRAND_SKILL_ID, enemy);
        runtime.issue_command(brandCommand);

        AssertTrue(enemy.has_status_effect(STATUS_BLACK_STAR_BRAND_NORMAL), "普通敌人应获得普通黑星烙印状态。");
        AssertTrue(!enemy.has_status_effect(STATUS_GUARDING), "普通黑星烙印应立即打断已有 guarding。");
        AssertTrue(runtime.is_unit_guard_locked(enemy), "普通黑星烙印应封锁后续格挡。");
        AssertTrue(runtime.is_unit_counterattack_locked(enemy), "普通黑星烙印应标记为无法反击。");

        state.active_unit_id = enemy.unit_id;
        state.phase = "unit_acting";
        enemy.current_ap = 2;
        enemy.current_stamina = 60;
        BattleCommand guardCommand = BuildSkillCommand(enemy.unit_id, WARRIOR_GUARD_SKILL_ID, enemy);
        BattlePreview guardPreview = runtime.preview_command(guardCommand);
        AssertTrue(
            guardPreview != null && !guardPreview.allowed && LastLogContains(guardPreview.log_lines, "封锁了格挡"),
            $"普通黑星烙印下预览 warrior_guard 应被阻断。 log={guardPreview?.log_lines}"
        );
        int apBeforeIssue = enemy.current_ap;
        runtime.issue_command(guardCommand);
        AssertEq(enemy.current_ap, apBeforeIssue, "被普通黑星烙印封锁时不应继续扣除格挡技能的行动点。");
        AssertTrue(!enemy.has_status_effect(STATUS_GUARDING), "被普通黑星烙印封锁时不应重新获得 guarding。");
        runtime.dispose();
    }

    private void TestBlackStarBrandEliteTargetUsesEliteOnlyDebuffs()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        SkillDef heavyStrike = GetSkill(runtime.get_skill_defs(), WARRIOR_HEAVY_STRIKE_SKILL_ID);
        AssertTrue(heavyStrike != null, "elite case 前置：warrior_heavy_strike 定义应存在。");
        if (heavyStrike == null)
        {
            runtime.dispose();
            return;
        }

        BattleState state = BuildSkillTestState("black_star_brand_elite", new Vector2I(6, 3));
        BattleUnitState caster = BuildUnit("brand_elite_caster", "施法者", "player", new Vector2I(1, 1), 3, "hero");
        caster.known_active_skill_ids = new GStringNameArray { BLACK_STAR_BRAND_SKILL_ID };
        caster.known_skill_level_map[BLACK_STAR_BRAND_SKILL_ID] = 1;
        BattleUnitState elite = BuildUnit("brand_elite_target", "精英敌人", "enemy", new Vector2I(2, 1), 2, "", true);
        elite.current_stamina = 60;
        elite.attribute_snapshot.set_value(AttributeService.STAMINA_MAX_ID(), 60);
        elite.known_active_skill_ids = new GStringNameArray
        {
            WARRIOR_GUARD_SKILL_ID,
            WARRIOR_HEAVY_STRIKE_SKILL_ID,
        };
        elite.known_skill_level_map[WARRIOR_GUARD_SKILL_ID] = 1;
        elite.known_skill_level_map[WARRIOR_HEAVY_STRIKE_SKILL_ID] = 1;
        BattleUnitState allyTarget = BuildUnit("brand_elite_ally_target", "被打击者", "player", new Vector2I(3, 1), 2);

        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, elite);
        AddUnit(runtime, state, allyTarget);
        state.ally_unit_ids = new GStringNameArray { caster.unit_id, allyTarget.unit_id };
        state.enemy_unit_ids = new GStringNameArray { elite.unit_id };
        state.active_unit_id = caster.unit_id;
        runtime._state = state;
        BeginRuntimeBattle(runtime);

        BattleCommand brandCommand = BuildSkillCommand(caster.unit_id, BLACK_STAR_BRAND_SKILL_ID, elite);
        runtime.issue_command(brandCommand);
        AssertTrue(elite.has_status_effect(STATUS_BLACK_STAR_BRAND_ELITE), "elite 目标应获得专属黑星烙印状态。");
        AssertTrue(
            elite.has_status_effect(STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW),
            "elite 目标应持有首次受击穿透 guard 的窗口状态。"
        );
        AssertTrue(!runtime.is_unit_guard_locked(elite), "elite 黑星烙印不应沿用普通目标的格挡封锁。");
        AssertTrue(!runtime.is_unit_counterattack_locked(elite), "elite 黑星烙印不应沿用普通目标的反击封锁。");

        state.active_unit_id = elite.unit_id;
        state.phase = "unit_acting";
        elite.current_ap = 2;
        elite.current_stamina = 60;
        BattleCommand guardCommand = BuildSkillCommand(elite.unit_id, WARRIOR_GUARD_SKILL_ID, elite);
        BattlePreview guardPreview = runtime.preview_command(guardCommand);
        AssertTrue(guardPreview != null && guardPreview.allowed, "elite 黑星烙印下 warrior_guard 仍应允许施放。");
        runtime.issue_command(guardCommand);
        AssertTrue(elite.has_status_effect(STATUS_GUARDING), "elite 黑星烙印不应阻止目标进入 guarding。");

        GDictionary firstHitResult = runtime
            .get_damage_resolver()
            .resolve_effects(caster, elite, new GArray { BuildDamageEffect() });
        GDictionary firstEvent = ExtractFirstDamageEvent(firstHitResult);
        GDictionary secondHitResult = runtime
            .get_damage_resolver()
            .resolve_effects(caster, elite, new GArray { BuildDamageEffect() });
        AssertTrue(
            ReadInt(firstHitResult, "damage") > ReadInt(secondHitResult, "damage"),
            $"elite 黑星烙印的第一次受击应比后续同条件受击承受更高伤害。 first={firstHitResult} second={secondHitResult}"
        );
        AssertTrue(
            ReadInt(firstEvent, "guard_ignore_applied") > 0,
            $"elite 黑星烙印的首次受击应记录 guard_ignore_applied。 event={firstEvent}"
        );
        AssertTrue(
            !elite.has_status_effect(STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW),
            "elite 黑星烙印的首次受击窗口应在第一下结算后被消耗。"
        );
        runtime.dispose();
    }

    private BattleRuntimeModule BuildRuntime()
    {
        var registry = new ProgressionContentRegistry();
        var runtime = new BattleRuntimeModule();
        runtime.setup(null, registry.get_skill_defs(), new GDictionary(), new GDictionary());
        runtime.configure_damage_resolver_for_tests(new DeterministicBattleDamageResolver());
        runtime.configure_hit_resolver_for_tests(new FixedHitResolver(10));
        return runtime;
    }

    private void BeginRuntimeBattle(BattleRuntimeModule runtime)
    {
        if (runtime == null)
            return;
        runtime.calamity_by_member_id.Clear();
        runtime.get_fate_runtime().begin_battle(runtime.calamity_by_member_id);
    }

    private BattleState BuildSkillTestState(StringName battleId, Vector2I mapSize)
    {
        var state = new BattleState();
        state.battle_id = battleId;
        state.phase = "unit_acting";
        state.map_size = mapSize;
        state.timeline = new BattleTimelineState();
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

    private BattleCellState BuildCell(Vector2I coord)
    {
        var cell = new BattleCellState();
        cell.coord = coord;
        cell.base_terrain = BattleCellState.TERRAIN_LAND();
        cell.base_height = 4;
        cell.height_offset = 0;
        cell.recalculate_runtime_values();
        return cell;
    }

    private BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord,
        int currentAp,
        StringName sourceMemberId = default,
        bool isEliteOrBoss = false
    )
    {
        var unit = new BattleUnitState();
        unit.unit_id = unitId;
        unit.source_member_id = sourceMemberId;
        unit.display_name = displayName;
        unit.faction_id = factionId;
        unit.current_ap = currentAp;
        unit.current_hp = 60;
        unit.current_mp = 4;
        unit.current_stamina = 4;
        unit.current_aura = 0;
        unit.is_alive = true;
        unit.set_anchor_coord(coord);
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 60);
        unit.attribute_snapshot.set_value(AttributeService.MP_MAX_ID(), 4);
        unit.attribute_snapshot.set_value(AttributeService.STAMINA_MAX_ID(), 4);
        unit.attribute_snapshot.set_value(AttributeService.AURA_MAX_ID(), 2);
        unit.attribute_snapshot.set_value("action_points", Mathf.Max(currentAp, 1));
        unit.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 12);
        unit.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 4);
        unit.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 6);
        unit.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 4);
        unit.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 60);
        unit.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 10);
        unit.attribute_snapshot.set_value("hidden_luck_at_birth", 0);
        unit.attribute_snapshot.set_value("faith_luck_bonus", 0);
        unit.attribute_snapshot.set_value(FORTUNE_MARK_TARGET_STAT_ID, isEliteOrBoss ? 1 : 0);
        return unit;
    }

    private BattleCommand BuildSkillCommand(StringName unitId, StringName skillId, BattleUnitState targetUnit)
    {
        var command = new BattleCommand();
        command.command_type = BattleCommand.TYPE_SKILL();
        command.unit_id = unitId;
        command.skill_id = skillId;
        command.target_unit_id = targetUnit?.unit_id ?? default;
        command.target_coord = targetUnit?.coord ?? new Vector2I(-1, -1);
        return command;
    }

    private CombatEffectDef BuildDamageEffect()
    {
        var effect = new CombatEffectDef();
        effect.effect_type = "damage";
        effect.damage_tag = "physical_slash";
        effect.power = 12;
        return effect;
    }

    private void SetStatus(
        BattleUnitState unitState,
        StringName statusId,
        int durationTu,
        StringName sourceUnitId = default,
        int power = 1
    )
    {
        if (unitState == null || statusId == default)
            return;
        var statusEntry = new BattleStatusEffectState();
        statusEntry.status_id = statusId;
        statusEntry.source_unit_id = sourceUnitId;
        statusEntry.power = Mathf.Max(power, 1);
        statusEntry.stacks = 1;
        statusEntry.duration = durationTu;
        unitState.set_status_effect(statusEntry);
    }

    private void AddUnit(BattleRuntimeModule runtime, BattleState state, BattleUnitState unit)
    {
        state.units[unit.unit_id] = unit;
        runtime._grid_service.place_unit(state, unit, unit.coord, true);
    }

    private static GDictionary ExtractFirstDamageEvent(GDictionary result)
    {
        if (result == null || !result.ContainsKey("damage_events"))
            return new GDictionary();
        Variant rawEvents = result["damage_events"];
        if (rawEvents.VariantType != Variant.Type.Array)
            return new GDictionary();
        GArray damageEvents = rawEvents.AsGodotArray();
        if (damageEvents.Count <= 0)
            return new GDictionary();
        Variant firstEvent = damageEvents[0];
        return firstEvent.VariantType == Variant.Type.Dictionary
            ? firstEvent.AsGodotDictionary()
            : new GDictionary();
    }

    private static bool LastLogContains(GArray lines, string needle)
    {
        if (lines == null || lines.Count <= 0)
            return false;
        return lines[lines.Count - 1].AsString().Contains(needle);
    }

    private static bool LogContains(GStringArray lines, string needle)
    {
        if (lines == null)
            return false;
        foreach (string line in lines)
        {
            if (line.Contains(needle))
                return true;
        }
        return false;
    }

    private static int ReadInt(GDictionary data, string key, int fallback = 0)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
            return fallback;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static SkillDef GetSkill(GDictionary skillDefs, StringName skillId)
    {
        if (skillDefs == null || !skillDefs.ContainsKey(skillId))
            return null;
        return skillDefs[skillId].AsGodotObject() as SkillDef;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
            _failures.Add($"{message} actual={actual} expected={expected}");
    }
}
