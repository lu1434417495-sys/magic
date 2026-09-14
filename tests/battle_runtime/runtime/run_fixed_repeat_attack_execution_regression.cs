using System;
using System.Collections.Generic;
using Godot;

public partial class run_fixed_repeat_attack_execution_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "fixed_repeat_execution_probe";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            SkillDefinition skill = BuildSkill();
            TestPreviewCommandReportsConfiguredStageCount(skill);
            TestFixedRepeatChargesCommandCostOnce(skill);
            TestFixedRepeatContinuesAfterMiss(skill);
            TestFixedRepeatStopsAfterTargetDown(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Fixed repeat attack execution regression"));
    }

    private void TestPreviewCommandReportsConfiguredStageCount(SkillDefinition skill)
    {
        using BattleRuntimeModule runtime = BuildRuntime(skill);
        (BattleUnitState caster, BattleUnitState target) = SetupDuel(runtime, targetHp: 100);
        BattleCommand command = BuildCommand(caster, target);
        BattlePreview preview = null;
        try
        {
            preview = runtime.PreviewCommand(command);
            _test.True(preview?.allowed == true, "fixed-repeat preview should be allowed through PreviewCommand.");
            _test.True(preview?.hit_preview != null, "fixed-repeat PreviewCommand should expose typed hit preview data.");
            _test.Eq(
                preview?.hit_preview?.StageCount ?? 0,
                3,
                "fixed-repeat PreviewCommand should expose every configured attack stage."
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattlePreview(preview);
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestFixedRepeatChargesCommandCostOnce(SkillDefinition skill)
    {
        using BattleRuntimeModule runtime = BuildRuntime(skill);
        StageOutcomeDamageResolver resolver = BuildStageResolver(
            new[] { true, true, true },
            new[] { 3, 3, 3 }
        );
        runtime.ConfigureDamageResolverForTests(resolver);
        (BattleUnitState caster, BattleUnitState target) = SetupDuel(runtime, targetHp: 100);

        BattleCommand command = BuildCommand(caster, target);
        try
        {
            using BattleEventBatch batch = runtime.IssueCommand(command);
            _test.True(batch != null, "fixed-repeat skill should execute through the formal command path.");
            _test.Eq(resolver.call_count, 3, "fixed-repeat execution should resolve every configured stage.");
            _test.Eq(target.GetCurrentHp(), 91, "three successful stages should mutate target HP three times.");
            _test.Eq(caster.GetCurrentAp(), 1, "fixed-repeat execution should charge the command AP cost once.");
            _test.Eq(
                caster.GetCurrentStamina(),
                87,
                "fixed-repeat execution should charge the command stamina cost once instead of per stage."
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestFixedRepeatContinuesAfterMiss(SkillDefinition skill)
    {
        using BattleRuntimeModule runtime = BuildRuntime(skill);
        StageOutcomeDamageResolver resolver = BuildStageResolver(
            new[] { false, true, true },
            new[] { 0, 4, 5 }
        );
        runtime.ConfigureDamageResolverForTests(resolver);
        (BattleUnitState caster, BattleUnitState target) = SetupDuel(runtime, targetHp: 100);

        BattleCommand command = BuildCommand(caster, target);
        try
        {
            using BattleEventBatch batch = runtime.IssueCommand(command);
            _test.Eq(
                resolver.call_count,
                3,
                "a fixed-repeat miss should not cancel the remaining configured stages."
            );
            _test.Eq(
                target.GetCurrentHp(),
                91,
                "stages after a miss should still resolve against the live target."
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private void TestFixedRepeatStopsAfterTargetDown(SkillDefinition skill)
    {
        using BattleRuntimeModule runtime = BuildRuntime(skill);
        StageOutcomeDamageResolver resolver = BuildStageResolver(
            new[] { true, true, true },
            new[] { 10, 10, 10 }
        );
        runtime.ConfigureDamageResolverForTests(resolver);
        (BattleUnitState caster, BattleUnitState target) = SetupDuel(runtime, targetHp: 1);

        BattleCommand command = BuildCommand(caster, target);
        try
        {
            using BattleEventBatch batch = runtime.IssueCommand(command);
            _test.False(target.IsAlive(), "the first lethal stage should down the target.");
            _test.Eq(
                resolver.call_count,
                1,
                "fixed-repeat execution should not resolve later stages against a downed target."
            );
            _test.Eq(
                resolver.dead_target_ids_seen.Count,
                0,
                "the damage resolver should never receive an already-dead target."
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleCommand(command);
        }
    }

    private static SkillDefinition BuildSkill()
    {
        using CombatEffectDef damageResource = new()
        {
            effect_type = "damage",
            requires_weapon = true,
            add_weapon_dice = true,
            use_weapon_physical_damage_tag = true,
            dice_count = 1,
            dice_sides = 4,
        };
        using CombatEffectDef repeatResource = new()
        {
            effect_type = "fixed_repeat_attack",
            stop_on_miss = false,
            fixed_attack_count = 3,
        };
        CombatEffectDefinition damage = CombatEffectDefinition.FromDiagnosticFixture(
            damageResource,
            "fixed_repeat_execution.damage"
        );
        CombatEffectDefinition repeat = CombatEffectDefinition.FromDiagnosticFixture(
            repeatResource,
            "fixed_repeat_execution.repeat"
        );
        CombatSkillDefinition combat = TestSkillDefinitionProjection.BuildCombatProfile(
            SkillId,
            effects: new[] { damage, repeat },
            rangeValue: 1,
            apCost: 1,
            staminaCost: 13
        );
        return TestSkillDefinitionProjection.BuildSkill(
            SkillId,
            displayName: "Fixed Repeat Execution Probe",
            combatProfile: combat,
            tags: new[] { new StringName("melee") }
        );
    }

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
        BattleUnitState caster = BuildUnit("fixed_repeat_caster", "player", new Vector2I(1, 1));
        BattleUnitState target = BuildUnit("fixed_repeat_target", "enemy", new Vector2I(2, 1));
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, targetHp);
        target.SetCurrentHp(targetHp);
        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, 0, preserveZero: true);
        ApplyMeleeWeapon(caster);
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
            battle_id = "fixed_repeat_attack_execution_regression",
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
        BattleUnitState unit = new BattleUnitState
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
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static void ApplyMeleeWeapon(BattleUnitState unit)
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "fixed_repeat_probe_weapon",
                weapon_profile_type_id = "test_blade",
                weapon_range_type = "melee",
                weapon_family = "sword",
                weapon_current_grip = "one_handed",
                weapon_attack_range = 1,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 6,
                },
                weapon_two_handed_dice = new WeaponDice(),
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

    private static StageOutcomeDamageResolver BuildStageResolver(
        IReadOnlyList<bool> stageSuccesses,
        IReadOnlyList<int> stageDamage
    )
    {
        StageOutcomeDamageResolver resolver = new();
        foreach (bool success in stageSuccesses)
            resolver.stage_successes.Add(success);
        foreach (int damage in stageDamage)
            resolver.stage_damage.Add(damage);
        return resolver;
    }
}
