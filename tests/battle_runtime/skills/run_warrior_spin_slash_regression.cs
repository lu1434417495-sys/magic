using System;
using System.Collections.Generic;
using Godot;

public partial class run_warrior_spin_slash_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "warrior_spin_slash";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            SkillDefinition skill = LoadSkill();
            TestContentContract(skill);
            TestMeleeWeaponGate(skill);
            TestRuntimeDamageAndArea(skill);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Warrior spin slash regression"));
    }

    private void TestContentContract(SkillDefinition skill)
    {
        _test.True(skill != null, "应能加载正式旋斩技能资源。");
        if (skill?.CombatProfile == null)
        {
            _test.Fail("旋斩应声明 combat_profile。");
            return;
        }

        CombatSkillDefinition combat = skill.CombatProfile;
        _test.Eq(combat.TargetMode, new StringName("ground"), "旋斩应走地面范围技能路径。");
        _test.Eq(combat.RangeValue, 0, "旋斩配置射程应为 0。");
        _test.Eq(
            combat.WeaponRangePolicy,
            new StringName("configured"),
            "旋斩必须保留配置射程，不能被当前武器射程替换。"
        );
        _test.Eq(combat.AreaPattern, new StringName("radius"), "旋斩应使用半径范围。");
        _test.Eq(combat.AreaValue, 1, "旋斩应只覆盖自身周围一格。");
        _test.Eq(combat.ApCost, 1, "旋斩应消耗 1 AP。");
        _test.Eq(combat.StaminaCost, 50, "旋斩应消耗 50 体力。");
        _test.Eq(combat.ExcludedWeaponFamilies.Count, 0, "旋斩不应维护近战武器家族黑名单。");
        _test.True(Contains(skill.Tags, "melee"), "旋斩应通过 melee 标签绑定近战武器。");
        _test.Eq(combat.EffectDefinitions.Count, 1, "旋斩应有且只有一个武器伤害效果。");

        if (combat.EffectDefinitions.Count == 1)
        {
            CombatEffectDefinition effect = combat.EffectDefinitions[0];
            _test.Eq(effect.EffectType, new StringName("damage"), "旋斩效果应为伤害。");
            _test.True(effect.AddWeaponDice, "旋斩伤害应加入当前武器骰。");
            _test.True(effect.RequiresWeapon, "旋斩伤害应要求装备武器。");
            _test.True(effect.UseWeaponPhysicalDamageTag, "旋斩应使用当前武器物理伤害类型。");
            _test.True(effect.ResolveAsWeaponAttack, "旋斩的每个目标应作为武器攻击结算。");
        }
    }

    private void TestMeleeWeaponGate(SkillDefinition skill)
    {
        if (skill?.CombatProfile == null)
        {
            return;
        }

        using BattleRuntimeModule runtime = BuildRuntime(skill);
        BattleUnitState caster = BuildUnit("spin_gate_user", "player", new Vector2I(1, 1));
        caster.SetCurrentStamina(100);

        ApplyWeapon(caster, "spear", "melee", 2);
        _test.Eq(
            runtime.GetSkillCastBlockReason(caster, skill),
            BattleSkillCastBlockReasonKind.None,
            "长矛是近战武器，应允许使用旋斩。"
        );
        _test.Eq(
            BattleRangeService.ResolveBaseSkillRange(caster, skill),
            0,
            "长矛射程不得把旋斩从原地技能变成投射范围技能。"
        );

        ApplyWeapon(caster, "dagger", "melee", 1);
        _test.Eq(
            runtime.GetSkillCastBlockReason(caster, skill),
            BattleSkillCastBlockReasonKind.None,
            "匕首是近战武器，应允许使用旋斩。"
        );

        ApplyWeapon(caster, "bow", "ranged", 4);
        _test.Eq(
            runtime.GetSkillCastBlockReason(caster, skill),
            BattleSkillCastBlockReasonKind.MeleeWeaponRequired,
            "弓属于远程武器，应被 melee 门禁拒绝。"
        );

        ClearWeapon(caster);
        _test.Eq(
            runtime.GetSkillCastBlockReason(caster, skill),
            BattleSkillCastBlockReasonKind.MeleeWeaponRequired,
            "未装备武器时应被旋斩的近战武器门禁拒绝。"
        );
    }

    private void TestRuntimeDamageAndArea(SkillDefinition skill)
    {
        if (skill?.CombatProfile == null)
        {
            return;
        }

        using BattleRuntimeModule runtime = BuildRuntime(skill);
        runtime.ConfigureDamageResolverForTests(new FixedRollDamageResolver());
        BattleState state = BuildState(new Vector2I(5, 5));
        BattleUnitState caster = BuildUnit("spin_user", "player", new Vector2I(2, 2));
        BattleUnitState adjacentEnemy = BuildUnit(
            "spin_adjacent_enemy",
            "enemy",
            new Vector2I(2, 3)
        );
        BattleUnitState outsideEnemy = BuildUnit(
            "spin_outside_enemy",
            "enemy",
            new Vector2I(2, 4)
        );
        BattleUnitState adjacentAlly = BuildUnit(
            "spin_adjacent_ally",
            "player",
            new Vector2I(3, 2)
        );

        caster.AddKnownActiveSkill(SkillId);
        caster.SetKnownSkillLevelTyped(SkillId, 1);
        caster.SetCurrentAp(2);
        caster.SetCurrentStamina(100);
        caster.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 100);
        caster.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 100);
        ApplyWeapon(caster, "spear", "melee", 2);

        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, adjacentEnemy);
        AddUnit(runtime, state, outsideEnemy);
        AddUnit(runtime, state, adjacentAlly);
        state.active_unit_id = caster.unit_id;
        runtime.SetupStateForTests(state);

        BattleCommand displacedCommand = BuildCommand(caster, new Vector2I(2, 3));
        BattlePreview displacedPreview = runtime.PreviewCommand(displacedCommand);
        _test.True(
            displacedPreview != null && !displacedPreview.allowed,
            "旋斩不能把范围中心投到相邻地格。"
        );

        int adjacentHpBefore = adjacentEnemy.GetCurrentHp();
        int outsideHpBefore = outsideEnemy.GetCurrentHp();
        int allyHpBefore = adjacentAlly.GetCurrentHp();
        BattleCommand command = BuildCommand(caster, caster.GetAnchorCoord());
        BattlePreview preview = runtime.PreviewCommand(command);
        _test.True(preview != null && preview.allowed, "以自身格为目标时旋斩应允许施放。");

        BattleEventBatch batch = runtime.IssueCommand(command);
        _test.True(batch != null, "旋斩应通过正式技能命令完成结算。");
        _test.True(
            adjacentEnemy.GetCurrentHp() < adjacentHpBefore,
            "相邻敌人应受到旋斩的真实 HP 伤害。"
        );
        _test.Eq(outsideEnemy.GetCurrentHp(), outsideHpBefore, "距离 2 的敌人不应受到旋斩伤害。");
        _test.Eq(adjacentAlly.GetCurrentHp(), allyHpBefore, "相邻友军不应受到旋斩伤害。");
        _test.Eq(caster.GetCurrentAp(), 1, "旋斩成功施放后应消耗 1 AP。");
        _test.Eq(caster.GetCurrentStamina(), 50, "旋斩成功施放后应消耗 50 体力。");
        _test.True(
            batch.changed_unit_ids.Contains(adjacentEnemy.unit_id),
            "旋斩伤害应把受伤目标写入战斗变更数据。"
        );

        BattleUnitState eventSource = BuildUnit(
            "spin_damage_event_source",
            "player",
            Vector2I.Zero
        );
        BattleUnitState eventTarget = BuildUnit(
            "spin_damage_event_target",
            "enemy",
            Vector2I.One
        );
        ApplyWeapon(eventSource, "dagger", "melee", 1);
        eventSource.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 100);
        eventSource.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 100);
        int eventHpBefore = eventTarget.GetCurrentHp();
        AttackEffectResolutionResult damageResult = runtime
            .GetDamageResolver()
            .ResolveEffects(
                eventSource,
                eventTarget,
                skill.CombatProfile.EffectDefinitions,
                DamageResolutionContext.Empty()
            );
        _test.True(damageResult.Damage > 0, "旋斩武器效果应产生正数伤害数据。");
        _test.True(
            damageResult.DamageEvents.Length > 0,
            "旋斩武器效果应产生正式 DamageEvent 数据。"
        );
        _test.True(
            eventTarget.GetCurrentHp() < eventHpBefore,
            "DamageEvent 对应的伤害应实际写入目标 HP。"
        );
    }

    private static SkillDefinition LoadSkill() =>
        TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/warrior_spin_slash.tres",
            "warrior_spin_slash_regression"
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

    private static BattleState BuildState(Vector2I mapSize)
    {
        BattleState state = new()
        {
            battle_id = "warrior_spin_slash_regression",
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
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
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord
    )
    {
        BattleUnitState unit = new BattleUnitState()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
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

    private static void ApplyWeapon(
        BattleUnitState unit,
        StringName family,
        StringName rangeType,
        int range
    )
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "spin_slash_test_weapon",
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

    private static void ClearWeapon(BattleUnitState unit)
    {
        unit.ClearWeaponProjection();
    }

    private static void AddUnit(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit
    )
    {
        state.SetUnit(unit);
        runtime._grid_service.PlaceUnit(state, unit, unit.GetAnchorCoord(), true);
    }

    private static BattleCommand BuildCommand(BattleUnitState caster, Vector2I targetCoord) =>
        new()
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = caster.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            skill_id = SkillId,
            target_coord = targetCoord,
        };

    private static bool Contains(IReadOnlyList<StringName> values, StringName expected)
    {
        foreach (StringName value in values)
        {
            if (value == expected)
            {
                return true;
            }
        }
        return false;
    }
}
