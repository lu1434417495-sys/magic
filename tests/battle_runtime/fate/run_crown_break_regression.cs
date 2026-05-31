using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GIntArray = Godot.Collections.Array<int>;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public partial class run_crown_break_regression : SceneTree
{
    private static readonly StringName CROWN_BREAK_SKILL_ID = "crown_break";
    private static readonly StringName SAINT_BLADE_COMBO_SKILL_ID = "saint_blade_combo";
    private static readonly StringName WARRIOR_HEAVY_STRIKE_SKILL_ID = "warrior_heavy_strike";
    private static readonly StringName OPTION_BROKEN_FANG = "broken_fang";
    private static readonly StringName OPTION_BROKEN_HAND = "broken_hand";
    private static readonly StringName OPTION_BLINDED_EYE = "blinded_eye";
    private static readonly StringName STATUS_BLACK_STAR_BRAND_NORMAL = "black_star_brand_normal";
    private static readonly StringName STATUS_BLACK_STAR_BRAND_ELITE = "black_star_brand_elite";
    private static readonly StringName STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW = "black_star_brand_elite_guard_window";
    private static readonly StringName STATUS_CROWN_BREAK_BROKEN_FANG = "crown_break_broken_fang";
    private static readonly StringName STATUS_CROWN_BREAK_BROKEN_HAND = "crown_break_broken_hand";
    private static readonly StringName STATUS_CROWN_BREAK_BLINDED_EYE = "crown_break_blinded_eye";
    private static readonly StringName FORTUNE_MARK_TARGET_STAT_ID = "fortune_mark_target";

    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestCrownBreakBrokenFangBlocksCrit();
        TestCrownBreakBrokenHandBlocksCounterattackAndFollowUp();
        TestCrownBreakBlindedEyeBlocksEvasion();
        TestCrownBreakRejectsIllegalTargetsInSelectionPreviewAndIssue();

        if (_failures.Count == 0)
        {
            GD.Print("Crown break regression: PASS");
            return 0;
        }
        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Crown break regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestCrownBreakBrokenFangBlocksCrit()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildSkillTestState("crown_break_broken_fang", new Vector2I(6, 3));
        BattleUnitState caster = BuildUnit("crown_break_fang_caster", "施法者", "player", new Vector2I(1, 1), 3, "hero");
        caster.known_active_skill_ids = new GStringNameArray { CROWN_BREAK_SKILL_ID };
        caster.known_skill_level_map[CROWN_BREAK_SKILL_ID] = 1;
        BattleUnitState elite = BuildUnit("crown_break_fang_target", "精英敌人", "enemy", new Vector2I(2, 1), 2, "", true);
        elite.known_active_skill_ids = new GStringNameArray { WARRIOR_HEAVY_STRIKE_SKILL_ID };
        elite.known_skill_level_map[WARRIOR_HEAVY_STRIKE_SKILL_ID] = 1;
        BattleUnitState allyTarget = BuildUnit("crown_break_fang_ally", "被打击者", "player", new Vector2I(3, 1), 2);

        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, elite);
        AddUnit(runtime, state, allyTarget);
        state.ally_unit_ids = new GStringNameArray { caster.unit_id, allyTarget.unit_id };
        state.enemy_unit_ids = new GStringNameArray { elite.unit_id };
        state.active_unit_id = caster.unit_id;
        runtime._state = state;
        BeginRuntimeBattle(runtime);
        ApplyEliteBrand(elite, caster.unit_id);
        runtime.calamity_by_member_id["hero"] = 2;

        BattleCommand command = BuildGroundSkillCommand(caster.unit_id, OPTION_BROKEN_FANG, elite.coord);
        BattlePreview preview = runtime.preview_command(command);
        AssertTrue(preview != null && preview.allowed, "断牙分支前置：已被烙印的 elite 应允许预览折冠。");
        runtime.issue_command(command);
        AssertEq(runtime.get_member_calamity("hero"), 0, "折冠成功施放后应固定扣除 2 点 calamity。");
        AssertTrue(elite.has_status_effect(STATUS_CROWN_BREAK_BROKEN_FANG), "断牙分支应写入 broken_fang 状态。");
        AssertTrue(!elite.has_status_effect(STATUS_CROWN_BREAK_BROKEN_HAND), "断牙分支不应混入折手状态。");
        AssertTrue(!elite.has_status_effect(STATUS_CROWN_BREAK_BLINDED_EYE), "断牙分支不应混入遮目状态。");
    }

    private void TestCrownBreakBrokenHandBlocksCounterattackAndFollowUp()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        SkillDef skillDef = GetSkill(runtime.get_skill_defs(), SAINT_BLADE_COMBO_SKILL_ID);
        AssertTrue(skillDef != null && skillDef.combat_profile != null, "折手回归前置：saint_blade_combo 定义应存在。");
        if (skillDef == null || skillDef.combat_profile == null)
            return;
        CombatEffectDef repeatEffect = GetEffectDef(skillDef.combat_profile.effect_defs, "repeat_attack_until_fail");
        AssertTrue(repeatEffect != null, "折手回归前置：saint_blade_combo 应声明 repeat_attack_until_fail。");
        if (repeatEffect == null)
            return;

        BattleState state = BuildSkillTestState("crown_break_broken_hand", new Vector2I(6, 3));
        BattleUnitState caster = BuildUnit("crown_break_hand_caster", "施法者", "player", new Vector2I(1, 1), 3, "hero");
        caster.known_active_skill_ids = new GStringNameArray { CROWN_BREAK_SKILL_ID };
        caster.known_skill_level_map[CROWN_BREAK_SKILL_ID] = 1;
        BattleUnitState elite = BuildUnit("crown_break_hand_target", "精英敌人", "enemy", new Vector2I(2, 1), 2, "", true);
        elite.current_aura = 4;
        elite.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 100);
        elite.known_active_skill_ids = new GStringNameArray { SAINT_BLADE_COMBO_SKILL_ID };
        elite.known_skill_level_map[SAINT_BLADE_COMBO_SKILL_ID] = 1;
        BattleUnitState allyTarget = BuildUnit("crown_break_hand_ally", "被追击者", "player", new Vector2I(3, 1), 2);
        allyTarget.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), -10);

        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, elite);
        AddUnit(runtime, state, allyTarget);
        state.ally_unit_ids = new GStringNameArray { caster.unit_id, allyTarget.unit_id };
        state.enemy_unit_ids = new GStringNameArray { elite.unit_id };
        state.active_unit_id = caster.unit_id;
        runtime._state = state;
        BeginRuntimeBattle(runtime);
        ApplyEliteBrand(elite, caster.unit_id);
        runtime.calamity_by_member_id["hero"] = 2;

        BattleCommand sealCommand = BuildGroundSkillCommand(caster.unit_id, OPTION_BROKEN_HAND, elite.coord);
        runtime.issue_command(sealCommand);
        AssertTrue(elite.has_status_effect(STATUS_CROWN_BREAK_BROKEN_HAND), "折手分支应写入 broken_hand 状态。");
        AssertTrue(runtime.is_unit_counterattack_locked(elite), "折手分支应封锁反击读面。");

        state.active_unit_id = elite.unit_id;
        state.phase = "unit_acting";
        elite.current_ap = 2;
        int followUpSeed = FindRepeatAttackSeedForStageOutcomes(
            runtime, state, elite, allyTarget, skillDef, repeatEffect, new[] { true, true }
        );
        AssertTrue(followUpSeed >= 0, "折手回归应能找到原本可连续命中的圣剑连斩 battle seed。");
        if (followUpSeed < 0)
            return;
        state.seed = followUpSeed;
        state.attack_roll_nonce = 0;

        BattleCommand followUpCommand = BuildUnitSkillCommand(elite.unit_id, SAINT_BLADE_COMBO_SKILL_ID, allyTarget);
        BattlePreview followUpPreview = runtime.preview_command(followUpCommand);
        GStringArray stagePreviewTexts = followUpPreview.hit_preview?.StagePreviewTexts ?? new GStringArray();
        AssertEq(stagePreviewTexts.Count, 1, "折手分支应把追击预览压成 1 段。");
        state.attack_roll_nonce = 0;

        int auraBefore = elite.current_aura;
        int hpBefore = allyTarget.current_hp;
        BattleEventBatch batch = runtime.issue_command(followUpCommand);
        AssertEq(
            elite.current_aura,
            auraBefore - (int)skillDef.combat_profile.aura_cost,
            "折手分支下的连斩应只结算首段 Aura 成本。"
        );
        AssertTrue(
            batch != null && LogContains(batch.log_lines, "无法继续追击"),
            $"折手分支应写出追击被封锁的 battle log。 log={batch?.log_lines}"
        );
        AssertTrue(
            !(batch != null && LogContains(batch.log_lines, "第 2 段")),
            $"折手分支不应继续进入第二段追击日志。 log={batch?.log_lines}"
        );
        AssertTrue(
            allyTarget.current_hp < hpBefore && allyTarget.current_hp >= hpBefore - 18,
            $"折手分支应保留首段命中，但不应继续叠第二段伤害。 before={hpBefore} after={allyTarget.current_hp}"
        );
    }

    private void TestCrownBreakBlindedEyeBlocksEvasion()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildSkillTestState("crown_break_blinded_eye", new Vector2I(6, 3));
        BattleUnitState caster = BuildUnit("crown_break_eye_caster", "施法者", "player", new Vector2I(1, 1), 3, "hero");
        caster.known_active_skill_ids = new GStringNameArray { CROWN_BREAK_SKILL_ID, WARRIOR_HEAVY_STRIKE_SKILL_ID };
        caster.known_skill_level_map[CROWN_BREAK_SKILL_ID] = 1;
        caster.known_skill_level_map[WARRIOR_HEAVY_STRIKE_SKILL_ID] = 1;
        BattleUnitState elite = BuildUnit("crown_break_eye_target", "精英敌人", "enemy", new Vector2I(2, 1), 2, "", true);
        elite.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 25);

        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, elite);
        state.ally_unit_ids = new GStringNameArray { caster.unit_id };
        state.enemy_unit_ids = new GStringNameArray { elite.unit_id };
        state.active_unit_id = caster.unit_id;
        runtime._state = state;
        BeginRuntimeBattle(runtime);
        ApplyEliteBrand(elite, caster.unit_id);
        runtime.calamity_by_member_id["hero"] = 2;

        BattleCommand sealCommand = BuildGroundSkillCommand(caster.unit_id, OPTION_BLINDED_EYE, elite.coord);
        runtime.issue_command(sealCommand);
        AssertTrue(elite.has_status_effect(STATUS_CROWN_BREAK_BLINDED_EYE), "遮目分支应写入 blinded_eye 状态。");
        AssertTrue(!elite.has_status_effect(STATUS_CROWN_BREAK_BROKEN_FANG), "遮目分支不应混入断牙状态。");
        AssertTrue(!elite.has_status_effect(STATUS_CROWN_BREAK_BROKEN_HAND), "遮目分支不应混入折手状态。");
    }

    private void TestCrownBreakRejectsIllegalTargetsInSelectionPreviewAndIssue()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildSkillTestState("crown_break_illegal_target", new Vector2I(7, 3));
        BattleUnitState caster = BuildUnit("crown_break_illegal_caster", "施法者", "player", new Vector2I(1, 1), 3, "hero");
        caster.control_mode = "manual";
        caster.known_active_skill_ids = new GStringNameArray { CROWN_BREAK_SKILL_ID };
        caster.known_skill_level_map[CROWN_BREAK_SKILL_ID] = 1;
        BattleUnitState brandedElite = BuildUnit("crown_break_valid_target", "已烙印精英", "enemy", new Vector2I(2, 1), 2, "", true);
        BattleUnitState brandedNormal = BuildUnit("crown_break_normal_brand", "已烙印普通敌人", "enemy", new Vector2I(3, 1), 2);
        BattleUnitState unbrandedElite = BuildUnit("crown_break_unbranded_elite", "未烙印精英", "enemy", new Vector2I(4, 1), 2, "", true);

        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, brandedElite);
        AddUnit(runtime, state, brandedNormal);
        AddUnit(runtime, state, unbrandedElite);
        state.ally_unit_ids = new GStringNameArray { caster.unit_id };
        state.enemy_unit_ids = new GStringNameArray { brandedElite.unit_id, brandedNormal.unit_id, unbrandedElite.unit_id };
        state.active_unit_id = caster.unit_id;
        runtime._state = state;
        BeginRuntimeBattle(runtime);
        ApplyEliteBrand(brandedElite, caster.unit_id);
        SetStatus(brandedNormal, STATUS_BLACK_STAR_BRAND_NORMAL, 60, caster.unit_id);
        runtime.calamity_by_member_id["hero"] = 4;

        BattleCommand illegalCommand = BuildGroundSkillCommand(caster.unit_id, OPTION_BROKEN_HAND, unbrandedElite.coord);
        BattlePreview illegalPreview = runtime.preview_command(illegalCommand);
        AssertTrue(illegalPreview != null && !illegalPreview.allowed, "未烙印的 elite 不应通过折冠 preview。");
        AssertTrue(
            illegalPreview != null && LogContains(illegalPreview.log_lines, "黑星烙印"),
            $"非法目标预览应明确指出需要黑星烙印。 log={illegalPreview?.log_lines}"
        );

        int apBeforeIssue = caster.current_ap;
        int calamityBeforeIssue = runtime.get_member_calamity("hero");
        BattleEventBatch illegalBatch = runtime.issue_command(illegalCommand);
        AssertEq(caster.current_ap, apBeforeIssue, "非法目标被 issue 拒绝时不应扣除 AP。");
        AssertEq(runtime.get_member_calamity("hero"), calamityBeforeIssue, "非法目标被 issue 拒绝时不应扣除 calamity。");
        AssertTrue(
            !unbrandedElite.has_status_effect(STATUS_CROWN_BREAK_BROKEN_HAND),
            "非法目标被 issue 拒绝后不应获得折手状态。"
        );
        AssertTrue(
            illegalBatch != null && LogContains(illegalBatch.log_lines, "黑星烙印"),
            $"非法目标被 issue 拒绝时应回传 preview 阻断原因。 log={illegalBatch?.log_lines}"
        );
    }

    private BattleRuntimeModule BuildRuntime()
    {
        var registry = new ProgressionContentRegistry();
        var runtime = new BattleRuntimeModule();
        runtime.setup(null, registry.get_skill_defs(), new GDictionary(), new GDictionary());
        runtime.configure_hit_resolver_for_tests(new FixedHitResolver(10));
        var damageResolver = new DeterministicBattleDamageResolver();
        runtime.configure_damage_resolver_for_tests(damageResolver);
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
        unit.control_mode = "manual";
        unit.current_ap = currentAp;
        unit.current_hp = 60;
        unit.current_mp = 4;
        unit.current_stamina = 60;
        unit.current_aura = 0;
        unit.is_alive = true;
        unit.set_anchor_coord(coord);
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 60);
        unit.attribute_snapshot.set_value(AttributeService.MP_MAX_ID(), 4);
        unit.attribute_snapshot.set_value(AttributeService.STAMINA_MAX_ID(), 60);
        unit.attribute_snapshot.set_value(AttributeService.AURA_MAX_ID(), 6);
        unit.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_MP());
        unit.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_AURA());
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
        ApplyTestEquippedWeapon(unit);
        return unit;
    }

    private static void ApplyTestEquippedWeapon(BattleUnitState unit, int attackRange = 1)
    {
        if (unit == null)
            return;
        unit.apply_weapon_projection(new GDictionary
        {
            ["weapon_profile_kind"] = "equipped",
            ["weapon_item_id"] = "crown_break_test_blade",
            ["weapon_profile_type_id"] = "test_blade",
            ["weapon_current_grip"] = "one_handed",
            ["weapon_attack_range"] = attackRange,
            ["weapon_one_handed_dice"] = new GDictionary { ["dice_count"] = 1, ["dice_sides"] = 6, ["flat_bonus"] = 0 },
            ["weapon_uses_two_hands"] = false,
            ["weapon_physical_damage_tag"] = "physical_slash",
        });
    }

    private void AddUnit(BattleRuntimeModule runtime, BattleState state, BattleUnitState unit)
    {
        state.units[unit.unit_id] = unit;
        runtime._grid_service.place_unit(state, unit, unit.coord, true);
    }

    private void ApplyEliteBrand(BattleUnitState targetUnit, StringName sourceUnitId = default)
    {
        SetStatus(targetUnit, STATUS_BLACK_STAR_BRAND_ELITE, 60, sourceUnitId);
        SetStatus(targetUnit, STATUS_BLACK_STAR_BRAND_ELITE_GUARD_WINDOW, 60, sourceUnitId);
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

    private BattleCommand BuildGroundSkillCommand(StringName unitId, StringName variantId, Vector2I targetCoord)
    {
        var command = new BattleCommand();
        command.command_type = BattleCommand.TYPE_SKILL();
        command.unit_id = unitId;
        command.skill_id = CROWN_BREAK_SKILL_ID;
        command.skill_variant_id = variantId;
        command.target_coord = targetCoord;
        command.target_coords = new GVector2IArray { targetCoord };
        return command;
    }

    private BattleCommand BuildUnitSkillCommand(StringName unitId, StringName skillId, BattleUnitState targetUnit)
    {
        var command = new BattleCommand();
        command.command_type = BattleCommand.TYPE_SKILL();
        command.unit_id = unitId;
        command.skill_id = skillId;
        command.target_unit_id = targetUnit?.unit_id ?? default;
        command.target_coord = targetUnit?.coord ?? new Vector2I(-1, -1);
        return command;
    }

    private static CombatEffectDef GetEffectDef(Godot.Collections.Array<CombatEffectDef> effectDefs, StringName effectType)
    {
        if (effectDefs == null)
            return null;
        foreach (CombatEffectDef typedEffect in effectDefs)
        {
            if (typedEffect != null && typedEffect.effect_type == effectType)
                return typedEffect;
        }
        return null;
    }

    private int FindRepeatAttackSeedForStageOutcomes(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        CombatEffectDef repeatEffect,
        bool[] expectedStageOutcomes
    )
    {
        if (runtime == null || state == null || activeUnit == null || targetUnit == null || skillDef == null || repeatEffect == null)
            return -1;
        for (int candidateSeed = 0; candidateSeed < 4096; candidateSeed++)
        {
            state.seed = candidateSeed;
            state.attack_roll_nonce = 0;
            bool matched = true;
            for (int stageIndex = 0; stageIndex < expectedStageOutcomes.Length; stageIndex++)
            {
                AttackRollResult rollResult = runtime.get_hit_resolver().resolve_repeat_attack_stage_hit(
                    state,
                    activeUnit,
                    targetUnit,
                    skillDef,
                    repeatEffect,
                    stageIndex
                );
                if (rollResult.Success != expectedStageOutcomes[stageIndex])
                {
                    matched = false;
                    break;
                }
            }
            if (matched)
            {
                state.attack_roll_nonce = 0;
                return candidateSeed;
            }
        }
        state.attack_roll_nonce = 0;
        return -1;
    }

    private GVector2IArray ExtractCoordPairs(GVector2IArray coords)
    {
        var pairs = new GVector2IArray();
        foreach (Vector2I coord in coords)
            pairs.Add(new Vector2I(coord.X, coord.Y));
        return pairs;
    }

    private static bool LogContains(GArray lines, string needle)
    {
        if (lines == null)
            return false;
        foreach (Variant line in lines)
        {
            if (line.AsString().Contains(needle))
                return true;
        }
        return false;
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

    private sealed class SelectionRuntimeProxy
    {
        public BattleRuntimeModule runtime;
        public GDictionary skill_defs = new();
        public StringName selected_skill_id = "";
        public StringName selected_skill_variant_id = "";
        public StringName last_manual_unit_id = "";
        public GVector2IArray target_coords_state = new();
        public GStringNameArray target_unit_ids_state = new();
        public Vector2I selected_coord = new(-1, -1);
        public string status_text = "";

        public SelectionRuntimeProxy(BattleRuntimeModule battleRuntime, GDictionary battleSkillDefs)
        {
            runtime = battleRuntime;
            skill_defs = battleSkillDefs ?? new GDictionary();
        }

        public BattleUnitState get_manual_battle_unit()
        {
            BattleUnitState activeUnit = get_runtime_battle_active_unit();
            return activeUnit != null && activeUnit.control_mode == "manual" ? activeUnit : null;
        }

        public BattleUnitState get_runtime_battle_active_unit()
        {
            BattleState state = get_battle_state();
            if (state == null)
                return null;
            return state.units.GetValueOrDefault(state.active_unit_id, new Variant()).AsGodotObject() as BattleUnitState;
        }

        public BattleUnitState get_runtime_battle_unit_at_coord(Vector2I coord)
        {
            if (runtime == null)
                return null;
            return runtime.get_grid_service().get_unit_at_coord(runtime.get_state(), coord);
        }

        public BattleUnitState get_runtime_battle_unit_by_id(StringName unitId)
        {
            BattleState state = get_battle_state();
            if (state == null)
                return null;
            return state.units.GetValueOrDefault(unitId, new Variant()).AsGodotObject() as BattleUnitState;
        }

        public BattleState get_battle_state()
        {
            return runtime?.get_state();
        }

        public BattleGridService get_battle_grid_service()
        {
            return runtime?.get_grid_service();
        }

        public BattlePreview preview_battle_command(BattleCommand command)
        {
            return runtime?.preview_command(command);
        }

        public StringName issue_battle_command(BattleCommand command)
        {
            runtime?.issue_command(command);
            return "full";
        }

        public void refresh_battle_selection_state() { }

        public void update_status(string message)
        {
            status_text = message;
        }

        public string format_coord(Vector2I coord)
        {
            return $"({coord.X},{coord.Y})";
        }

        public bool is_battle_active()
        {
            return runtime != null && runtime.is_battle_active();
        }

        public StringName get_selected_battle_skill_id()
        {
            return selected_skill_id;
        }

        public void set_battle_selection_skill_id(StringName skillId)
        {
            selected_skill_id = skillId;
        }

        public StringName get_selected_battle_skill_variant_id()
        {
            return selected_skill_variant_id;
        }

        public void set_battle_selection_skill_variant_id(StringName variantId)
        {
            selected_skill_variant_id = variantId;
        }

        public StringName get_battle_selection_last_manual_unit_id()
        {
            return last_manual_unit_id;
        }

        public void set_battle_selection_last_manual_unit_id(StringName unitId)
        {
            last_manual_unit_id = unitId;
        }

        public GVector2IArray get_battle_selection_target_coords_state()
        {
            return (GVector2IArray)target_coords_state.Duplicate();
        }

        public void set_battle_selection_target_coords_state(GVector2IArray targetCoords)
        {
            target_coords_state = targetCoords != null ? (GVector2IArray)targetCoords.Duplicate() : new GVector2IArray();
        }

        public GStringNameArray get_battle_selection_target_unit_ids_state()
        {
            return (GStringNameArray)target_unit_ids_state.Duplicate();
        }

        public void set_battle_selection_target_unit_ids_state(GStringNameArray targetUnitIds)
        {
            target_unit_ids_state = targetUnitIds != null ? (GStringNameArray)targetUnitIds.Duplicate() : new GStringNameArray();
        }

        public void set_runtime_battle_selected_coord(Vector2I coord)
        {
            selected_coord = coord;
        }

        public GDictionary get_skill_defs()
        {
            return skill_defs;
        }
    }
}
