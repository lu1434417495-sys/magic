using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_warrior_double_strike_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "warrior_double_strike";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestContentContract(skill);
            TestFixedRepeatSchema();
            TestFormalCommandAppliesDoubleStrikeState(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Warrior double strike regression"));
    }

    private void TestFixedRepeatSchema()
    {
        using SkillContentRegistry registry = new(loadDefaultContent: false);
        using CombatEffectDef invalidCount = new()
        {
            effect_type = "fixed_repeat_attack",
            fixed_attack_count = 1,
        };
        GStringArray errors = new();
        registry.AppendEffectValidationErrors(
            errors,
            "invalid_fixed_repeat",
            invalidCount,
            "test_effect"
        );
        _test.True(
            string.Join(" | ", errors).Contains("fixed_attack_count >= 2"),
            "fixed_repeat_attack 必须拒绝不足两段的配置。"
        );

        using CombatEffectDef misplacedCount = new()
        {
            effect_type = "damage",
            fixed_attack_count = 2,
            dice_count = 1,
            dice_sides = 4,
        };
        errors.Clear();
        registry.AppendEffectValidationErrors(
            errors,
            "misplaced_fixed_count",
            misplacedCount,
            "test_effect"
        );
        _test.True(
            string.Join(" | ", errors).Contains(
                "fixed_attack_count is only supported on fixed_repeat_attack"
            ),
            "其他效果类型不得误用 fixed_attack_count。"
        );
    }

    private void TestContentContract(SkillDefinition skill)
    {
        _test.True(skill?.CombatProfile != null, "应能加载双重打击正式技能资源。");
        if (skill?.CombatProfile == null)
            return;

        CombatSkillDefinition combat = skill.CombatProfile;
        _test.Eq(combat.ApCost, 1, "双重打击应消耗 1 AP。");
        _test.Eq(combat.StaminaCost, 25, "双重打击基础体力消耗应为 25。");
        _test.True(Contains(skill.Tags, "melee"), "双重打击应通过 melee 标签绑定近战武器。");

        CombatEffectDefinition repeatEffect = FindFixedRepeatEffect(skill);
        _test.True(repeatEffect != null, "双重打击应声明 fixed_repeat_attack。");
        _test.Eq(repeatEffect?.FixedAttackCount ?? 0, 2, "双重打击应固定结算两段。");
    }

    private void TestFormalCommandAppliesDoubleStrikeState(SkillDefinition skill)
    {
        using BattleRuntimeModule runtime = BuildRuntime(skill);
        runtime.ConfigureDamageResolverForTests(
            new FixedRollDamageResolver(
                new GArray { 4, 3, 5, 2 },
                new GArray { 10, 10 }
            )
        );
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
        (BattleUnitState caster, BattleUnitState target) = SetupDuel(runtime, targetHp: 100);

        int hpBefore = target.GetCurrentHp();
        BattleEventBatch batch = runtime.IssueCommand(BuildCommand(caster, target));

        _test.True(batch != null, "双重打击应通过正式技能命令完成结算。");
        _test.True(target.GetCurrentHp() < hpBefore, "两段命中应实际降低目标 HP。");
        _test.Eq(caster.GetCurrentStamina(), 75, "双重打击正式命令应消耗 25 体力。");
        _test.Eq(
            caster.GetStatusEffect("melee_combo_stack")?.stacks ?? 0,
            2,
            "每段成功命中都应授予一层 melee_combo_stack。"
        );
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            "warrior_double_strike",
            "warrior_double_strike_regression"
        );

    private static BattleRuntimeModule BuildRuntime(SkillDefinition skill)
    {
        BattleRuntimeModule runtime = new();
        runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [skill.SkillId] = skill }
        );
        return runtime;
    }

    private static (BattleUnitState caster, BattleUnitState target) SetupDuel(
        BattleRuntimeModule runtime,
        int targetHp
    )
    {
        BattleState state = BuildState();
        BattleUnitState caster = BuildUnit("double_user", "player", new Vector2I(1, 1));
        BattleUnitState target = BuildUnit("double_target", "enemy", new Vector2I(2, 1));
        target.SetCurrentHp(targetHp);
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, targetHp);
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, 0, preserveZero: true);
        ApplyWeapon(caster, "sword", "melee", 1);
        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, target);
        state.active_unit_id = caster.unit_id;
        runtime.SetupStateForTests(state);
        return (caster, target);
    }

    private static BattleState BuildState()
    {
        BattleState state = new()
        {
            battle_id = "warrior_double_strike_regression",
            phase = "unit_acting",
            map_size = new Vector2I(4, 4),
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < 4; y++)
        for (int x = 0; x < 4; x++)
        {
            Vector2I coord = new(x, y);
            BattleCellState cell = new()
            {
                coord = coord,
                base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                base_height = 4,
            };
            cell.RecalculateRuntimeValues();
            state.SetCell(coord, cell);
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState BuildUnit(StringName id, StringName faction, Vector2I coord)
    {
        BattleUnitState unit = new BattleUnitState()
        {
            unit_id = id,
            display_name = id.ToString(),
            faction_id = faction,
        }.WithCombatResourcesForTest(
            hp: 100,
            stamina: 100,
            ap: 2,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 1);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 20);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 20);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static void ApplyWeapon(BattleUnitState unit, StringName family, StringName rangeType, int range)
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "double_strike_test_weapon",
                weapon_profile_type_id = "test_blade",
                weapon_range_type = rangeType,
                weapon_family = family,
                weapon_current_grip = "one_handed",
                weapon_attack_range = range,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 6,
                    flat_bonus = 2,
                },
                weapon_two_handed_dice = new WeaponDice(),
                weapon_is_versatile = false,
                weapon_uses_two_hands = false,
                weapon_physical_damage_tag = "physical_slash",
            }
        );
    }

    private static void AddUnit(BattleRuntimeModule runtime, BattleState state, BattleUnitState unit)
    {
        state.SetUnit(unit);
        runtime._grid_service.PlaceUnit(state, unit, unit.GetAnchorCoord(), true);
    }

    private static BattleCommand BuildCommand(BattleUnitState caster, BattleUnitState target) =>
        new()
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_unit_id = target.unit_id,
            target_coord = target.GetAnchorCoord(),
        };

    private static CombatEffectDefinition FindFixedRepeatEffect(SkillDefinition skill)
    {
        foreach (CombatEffectDefinition effect in skill?.CombatProfile?.EffectDefinitions
            ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effect?.EffectKind == BattleEffectKind.FixedRepeatAttack)
                return effect;
        }
        return null;
    }

    private static bool Contains(IReadOnlyList<StringName> values, StringName expected)
    {
        foreach (StringName value in values)
            if (value == expected)
                return true;
        return false;
    }

}
