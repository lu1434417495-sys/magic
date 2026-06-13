using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_magic_backlash_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestSpellControlMetadataUsesTypedStateAndProjection();
        TestGroundEffectSpellControlHelperUsesTypedSurface();
        TestMagicBacklashUsesTypedFumbleProtectionState();
        TestFireballNormalCastHitsFriendAtFullDamageRoute();
        TestFireballBurnAppliesToEveryTeamInArea();
        TestFireballCriticalRefundsMpWithoutBlockingFriendlyFire();
        TestFireballProtectedFumbleConsumesExtraMpAndSkipsBlast();
        TestFireballUnprotectedFumbleDriftsGroundAnchor();
        TestCastingTimePendingCastStartsThenCompletes();
        TestCastingTimeSpellControlDcOrdinaryFailureConsumesApOnly();
        TestCastingTimeCriticalFailureConsumesHalfCurrentApAndStartsCooldown();
        TestCastingTimeCancelRefundsHalfResourcesWithoutCooldown();
        TestCastingTimeHpMaintenanceFailureInterruptsWithCooldown();

        return _test.Finish("Magic backlash regression");
    }

    private void TestSpellControlMetadataUsesTypedStateAndProjection()
    {
        Type metadataType = typeof(BattleSpellControlMetadata);
        _test.True(
            typeof(BattleSpellControlResult).GetProperty("SpellControl")?.PropertyType
                == typeof(BattleSpellControlMetadata),
            "BattleSpellControlResult 应持有 typed BattleSpellControlMetadata，而不是 Godot Dictionary。"
        );

        var metadata = new BattleSpellControlMetadata
        {
            AttackResolution = "critical_hit",
            SpellControlResolution = "critical_success",
            AttackSuccess = true,
            CriticalHit = true,
            HitRoll = 20,
            EffectiveHitRoll = 20,
        };
        BattleSpellControlResult result =
            BattleSpellControlResult.None(metadata) with { MpRefund = 5 };
        GDictionary payload = result.ToDictionary();
        GDictionary spellControl = payload["spell_control"].AsGodotDictionary();

        _test.Eq(payload["mp_refund"].AsInt32(), 5, "spell-control result 应投影 MP 返还。");
        _test.Eq(
            spellControl["spell_control_resolution"].AsString(),
            "critical_success",
            "spell-control metadata 只在 ToDictionary 边界投影。"
        );
    }

    private void TestGroundEffectSpellControlHelperUsesTypedSurface()
    {
        _test.True(
            typeof(BattleMagicBacklashResolver).GetMethod("should_resolve_spell_control") == null
                && typeof(BattleMagicBacklashResolver).GetMethod("apply_spell_control_after_cost") == null
                && typeof(BattleMagicBacklashResolver).GetMethod("apply_spell_control_after_cost_result", new[] { typeof(BattleUnitState), typeof(SkillDef), typeof(int), typeof(int), typeof(GDictionary), typeof(BattleEventBatch) }) == null
                && typeof(BattleMagicBacklashResolver).GetMethod("build_ground_backlash_target_coords") == null
                && typeof(BattleMagicBacklashResolver).GetMethod("build_ground_backlash_target_coords_result") == null
                && typeof(BattleMagicBacklashResolver).GetMethod("append_ground_backlash_log") == null,
            "BattleMagicBacklashResolver 不应继续保留 snake_case / Dictionary wrapper surface。"
        );
        _test.True(
            typeof(BattleGroundEffectService).GetMethod("_resolve_ground_spell_control_after_cost") == null
                && typeof(BattleGroundEffectService).GetMethod("_resolve_unit_spell_control_after_cost") == null,
            "BattleGroundEffectService 不应继续保留 spell-control 的 Dictionary helper wrapper。"
        );
        _test.True(
            typeof(BattleGroundEffectService).GetMethod(
                "ResolveGroundSpellControlAfterCostResult",
                BindingFlags.Public | BindingFlags.Instance
            ) == null
                && typeof(BattleGroundEffectService).GetMethod(
                    "ResolveGroundSpellControlAfterCostResult",
                    BindingFlags.NonPublic | BindingFlags.Instance
                ) != null
                && typeof(BattleGroundEffectService).GetMethod(
                    "ResolveUnitSpellControlAfterCostResult",
                    BindingFlags.Public | BindingFlags.Instance
                ) == null
                && typeof(BattleGroundEffectService).GetMethod(
                    "ResolveUnitSpellControlAfterCostResult",
                    BindingFlags.NonPublic | BindingFlags.Instance
                ) != null,
            "BattleGroundEffectService 应继续提供 nonpublic typed spell-control helper。"
        );
    }

    private void TestMagicBacklashUsesTypedFumbleProtectionState()
    {
        BattleUnitState unit = BuildUnit("fumble_state_unit", "player", Vector2I.Zero, 0, 0, -1);
        _test.True(
            unit.GetFumbleProtectionUsedTyped("mage_fireball") == 0,
            "typed fumble protection getter 默认应返回 0。"
        );
        unit.SetFumbleProtectionUsedTyped("mage_fireball", 2);
        _test.Eq(
            unit.GetFumbleProtectionUsedTyped("mage_fireball"),
            2,
            "typed fumble protection helper 应保留写入值。"
        );
    }

    private void TestFireballNormalCastHitsFriendAtFullDamageRoute()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithSpellControlRoll(10);
        BattleState state = BuildState(new Vector2I(3, 1));
        BattleUnitState caster = BuildUnit("normal_caster", "player", new Vector2I(0, 0), 1, 200, 0);
        BattleUnitState friend = BuildUnit("normal_friend", "player", new Vector2I(1, 0), 0, 0, 0);
        BattleUnitState enemy = BuildUnit("normal_enemy", "enemy", new Vector2I(2, 0), 0, 0, 0);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, friend, false);
        AddUnit(runtime, state, enemy, true);
        Activate(runtime, state, caster);

        BattleCommand command = BuildFireballCommand(caster.unit_id, friend.coord);
        BattlePreview preview = runtime.PreviewCommand(command);
        int beforeFriendHp = friend.current_hp;
        BattleEventBatch batch = runtime.IssueCommand(command);

        _test.True(preview.allowed, "火球术瞄准友军地格应通过地面目标预览。");
        _test.True(
            preview.target_unit_ids.Contains(friend.unit_id),
            "火球术普通预览应把范围内友军列为受影响单位。"
        );
        _test.True(
            batch.changed_unit_ids.Contains(friend.unit_id),
            "普通施法应标记被火球波及的友军。"
        );
        _test.True(friend.current_hp < beforeFriendHp, "普通施法时范围内友军应受到火球伤害。");
        _test.Eq(caster.current_mp, 100, "普通施法只应扣除火球本身 100 法力。");
    }

    private void TestFireballBurnAppliesToEveryTeamInArea()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithSpellControlRoll(10);
        BattleState state = BuildState(new Vector2I(3, 1));
        BattleUnitState caster = BuildUnit("burn_caster", "player", new Vector2I(0, 0), 1, 200, 3);
        BattleUnitState friend = BuildUnit("burn_friend", "player", new Vector2I(1, 0), 0, 0, 0);
        BattleUnitState enemy = BuildUnit("burn_enemy", "enemy", new Vector2I(2, 0), 0, 0, 0);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, friend, false);
        AddUnit(runtime, state, enemy, true);
        Activate(runtime, state, caster);

        BattleEventBatch batch = runtime.IssueCommand(BuildFireballCommand(caster.unit_id, friend.coord));
        BattleStatusEffectState friendBurning = friend.GetStatusEffect("burning");
        int beforeBurnTickHp = friend.current_hp;

        _test.True(caster.HasStatusEffect("burning"), "火球术灼烧不应保护范围内施法者。");
        _test.True(friendBurning != null, "火球术灼烧应和伤害一样波及友军。");
        _test.True(enemy.HasStatusEffect("burning"), "火球术灼烧仍应作用于范围内敌人。");
        _test.Eq(
            friendBurning != null ? friendBurning.tick_interval_tu : -1,
            10,
            "火球术友军灼烧应保留正式 timeline tick。"
        );
        _test.True(batch.changed_unit_ids.Contains(friend.unit_id), "友军被灼烧时应标记单位变化。");
        AdvanceTimelineTu(runtime, state, 10);
        _test.True(friend.current_hp < beforeBurnTickHp, "火球术友军灼烧应按 timeline tick 造成伤害。");
    }

    private void TestFireballCriticalRefundsMpWithoutBlockingFriendlyFire()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithSpellControlRoll(20);
        BattleState state = BuildState(new Vector2I(3, 1));
        BattleUnitState caster = BuildUnit("crit_caster", "player", new Vector2I(0, 0), 1, 200, 0);
        BattleUnitState friend = BuildUnit("crit_friend", "player", new Vector2I(1, 0), 0, 0, 0);
        BattleUnitState enemy = BuildUnit("crit_enemy", "enemy", new Vector2I(2, 0), 0, 0, 0);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, friend, false);
        AddUnit(runtime, state, enemy, true);
        Activate(runtime, state, caster);

        int beforeFriendHp = friend.current_hp;
        BattleEventBatch batch = runtime.IssueCommand(BuildFireballCommand(caster.unit_id, friend.coord));

        _test.True(friend.current_hp < beforeFriendHp, "法术控制大成功不应取消范围内友军伤害。");
        _test.Eq(caster.current_mp, 150, "火球术大成功应返还本次实际法力消耗的 50%。");
        _test.True(LogsContain(batch.log_lines, "返还 50 点法力"), "火球术大成功应写入 MP 返还日志。");
    }

    private void TestFireballProtectedFumbleConsumesExtraMpAndSkipsBlast()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithSpellControlRoll(1);
        BattleState state = BuildState(new Vector2I(3, 1));
        BattleUnitState caster = BuildUnit("protected_caster", "player", new Vector2I(0, 0), 1, 250, 3);
        BattleUnitState friend = BuildUnit(
            "protected_friend",
            "player",
            new Vector2I(1, 0),
            0,
            0,
            0
        );
        BattleUnitState enemy = BuildUnit("protected_enemy", "enemy", new Vector2I(2, 0), 0, 0, 0);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, friend, false);
        AddUnit(runtime, state, enemy, true);
        Activate(runtime, state, caster);

        int beforeFriendHp = friend.current_hp;
        BattleEventBatch batch = runtime.IssueCommand(BuildFireballCommand(caster.unit_id, friend.coord));

        _test.Eq(friend.current_hp, beforeFriendHp, "受精通保护的大失败不应释放火球爆炸。");
        _test.Eq(caster.current_mp, 50, "受保护大失败应在原 100 法力外额外吞噬 100 法力。");
        _test.Eq(
            caster.GetFumbleProtectionUsedTyped("mage_fireball"),
            1,
            "受保护大失败应消耗一次火球术保护次数。"
        );
        _test.True(!batch.changed_unit_ids.Contains(friend.unit_id), "受保护大失败不应标记目标友军变化。");
    }

    private void TestFireballUnprotectedFumbleDriftsGroundAnchor()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithSpellControlRoll(1);
        BattleState state = BuildState(new Vector2I(2, 1));
        BattleUnitState caster = BuildUnit("drift_caster", "player", new Vector2I(0, 0), 1, 200, 0);
        BattleUnitState friend = BuildUnit("drift_friend", "player", new Vector2I(1, 0), 0, 0, 0);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, friend, false);
        Activate(runtime, state, caster);

        int beforeCasterHp = caster.current_hp;
        int beforeFriendHp = friend.current_hp;
        BattleEventBatch batch = runtime.IssueCommand(BuildFireballCommand(caster.unit_id, caster.coord));

        _test.Eq(caster.current_hp, beforeCasterHp, "无保护大失败偏移后不应继续结算原落点。");
        _test.True(friend.current_hp < beforeFriendHp, "无保护大失败应把火球偏移到唯一候选地格并伤到友军。");
        _test.True(LogsContain(batch.log_lines, "偏移到 (1, 0)"), "无保护大失败应写入明确的落点偏移日志。");
    }

    private void TestCastingTimePendingCastStartsThenCompletes()
    {
        SkillDef skillDef = BuildCastingTimeSkill(maintenanceDc: 0);
        BattleRuntimeModule runtime = BuildRuntimeWithCastingSkill(skillDef);
        BattleState state = BuildState(new Vector2I(4, 1));
        BattleUnitState caster = BuildUnit("casting_caster", "player", new Vector2I(0, 0), 1, 50, -1);
        BattleUnitState target = BuildUnit("casting_target", "enemy", new Vector2I(1, 0), 0, 0, -1);
        LearnSkill(caster, skillDef.skill_id);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, target, true);
        Activate(runtime, state, caster);

        int beforeTargetHp = target.current_hp;
        BattlePreview preview = runtime.PreviewCommand(BuildCastingTimeCommand(caster.unit_id, target.unit_id));
        BattleEventBatch startBatch = runtime.IssueCommand(BuildCastingTimeCommand(caster.unit_id, target.unit_id));

        _test.True(preview.allowed, "读条技能启动前预览应允许。");
        _test.True(caster.HasPendingCast(), "使用读条技能后应创建 pending cast。");
        _test.Eq(caster.pending_cast?.RemainingCastProgress ?? -1, 1000, "pending cast 初始剩余进度应按 TU*100 存储。");
        _test.Eq(caster.pending_cast?.TargetUnitIds.Count ?? -1, 1, "pending cast 应保留单位目标。");
        _test.Eq(caster.current_mp, 40, "启动读条应立即扣除资源成本。");
        _test.Eq(caster.current_ap, 0, "启动读条应消耗本次行动。");
        _test.Eq(caster.GetCooldownTyped(skillDef.skill_id), 0, "启动读条不应立刻进入冷却。");
        _test.Eq(target.current_hp, beforeTargetHp, "启动读条不应立刻结算伤害。");
        _test.True(startBatch.changed_unit_ids.Contains(caster.unit_id), "启动读条应标记施法者变化。");

        AdvanceTimelineTu(runtime, state, 10);

        _test.True(!caster.HasPendingCast(), "读条完成后应清除 pending cast。");
        _test.True(target.current_hp < beforeTargetHp, "读条完成后应结算技能效果。");
        _test.Eq(caster.GetCooldownTyped(skillDef.skill_id), 20, "读条完成后应启动技能冷却。");
        _test.Eq(caster.action_progress, 0, "读条完成的同一 tick 不应额外获得行动进度。");
    }

    private void TestCastingTimeSpellControlDcOrdinaryFailureConsumesApOnly()
    {
        SkillDef skillDef = BuildCastingTimeSkill(maintenanceDc: 0, castingSpellControlDc: 999);
        BattleRuntimeModule runtime = BuildRuntimeWithCastingSkill(skillDef, spellControlRoll: 10);
        BattleState state = BuildState(new Vector2I(3, 1));
        BattleUnitState caster = BuildUnit("casting_dc_fail_caster", "player", new Vector2I(0, 0), 1, 50, -1);
        BattleUnitState target = BuildUnit("casting_dc_fail_target", "enemy", new Vector2I(1, 0), 0, 0, -1);
        LearnSkill(caster, skillDef.skill_id);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, target, true);
        Activate(runtime, state, caster);

        int beforeTargetHp = target.current_hp;
        runtime.IssueCommand(BuildCastingTimeCommand(caster.unit_id, target.unit_id));

        _test.True(!caster.HasPendingCast(), "读条法术控制普通失败不应创建 pending cast。");
        _test.Eq(caster.current_ap, 0, "法术控制普通失败应按技能 AP 成本消耗行动。");
        _test.Eq(caster.current_mp, 50, "法术控制普通失败发生在持久资源扣除前。");
        _test.Eq(caster.GetCooldownTyped(skillDef.skill_id), 0, "法术控制普通失败不应启动冷却。");
        _test.Eq(target.current_hp, beforeTargetHp, "法术控制普通失败不应结算技能效果。");
    }

    private void TestCastingTimeCriticalFailureConsumesHalfCurrentApAndStartsCooldown()
    {
        SkillDef skillDef = BuildCastingTimeSkill(maintenanceDc: 0, castingSpellControlDc: 1);
        BattleRuntimeModule runtime = BuildRuntimeWithCastingSkill(skillDef, spellControlRoll: 1);
        BattleState state = BuildState(new Vector2I(3, 1));
        BattleUnitState caster = BuildUnit("casting_critical_fail_caster", "player", new Vector2I(0, 0), 5, 50, -1);
        BattleUnitState target = BuildUnit("casting_critical_fail_target", "enemy", new Vector2I(1, 0), 0, 0, -1);
        LearnSkill(caster, skillDef.skill_id);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, target, true);
        Activate(runtime, state, caster);

        int beforeTargetHp = target.current_hp;
        runtime.IssueCommand(BuildCastingTimeCommand(caster.unit_id, target.unit_id));

        _test.True(!caster.HasPendingCast(), "读条法术控制大失败不应创建 pending cast。");
        _test.Eq(caster.current_ap, 2, "读条法术控制大失败应扣除当前 AP 的 50% 且向上取整。");
        _test.Eq(caster.current_mp, 50, "读条法术控制大失败发生在持久资源扣除前。");
        _test.Eq(caster.GetCooldownTyped(skillDef.skill_id), 20, "读条法术控制大失败应启动技能冷却。");
        _test.Eq(target.current_hp, beforeTargetHp, "读条法术控制大失败不应结算技能效果。");
    }

    private void TestCastingTimeCancelRefundsHalfResourcesWithoutCooldown()
    {
        SkillDef skillDef = BuildCastingTimeSkill(maintenanceDc: 0);
        BattleRuntimeModule runtime = BuildRuntimeWithCastingSkill(skillDef);
        BattleState state = BuildState(new Vector2I(3, 1));
        BattleUnitState caster = BuildUnit("casting_cancel_caster", "player", new Vector2I(0, 0), 1, 50, -1);
        BattleUnitState target = BuildUnit("casting_cancel_target", "enemy", new Vector2I(1, 0), 0, 0, -1);
        LearnSkill(caster, skillDef.skill_id);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, target, true);
        Activate(runtime, state, caster);

        runtime.IssueCommand(BuildCastingTimeCommand(caster.unit_id, target.unit_id));
        BattlePreview cancelPreview = runtime.PreviewCommand(BuildCancelCastCommand(caster.unit_id));
        BattleEventBatch cancelBatch = runtime.IssueCommand(BuildCancelCastCommand(caster.unit_id));

        _test.True(cancelPreview.allowed, "玩家可控己方单位应可在 timeline 阶段取消读条。");
        _test.True(!caster.HasPendingCast(), "取消读条后应清除 pending cast。");
        _test.Eq(caster.current_mp, 45, "取消读条应返还已付资源成本的一半。");
        _test.Eq(caster.GetCooldownTyped(skillDef.skill_id), 0, "手动取消读条不应启动冷却。");
        _test.True(cancelBatch.changed_unit_ids.Contains(caster.unit_id), "取消读条应标记施法者变化。");
        _test.True(
            ReportContainsRefundPolicy(cancelBatch.report_entries, "half_persistent_costs"),
            "取消读条战报应暴露 refund_policy。"
        );
    }

    private void TestCastingTimeHpMaintenanceFailureInterruptsWithCooldown()
    {
        SkillDef skillDef = BuildCastingTimeSkill(maintenanceDc: 999);
        BattleRuntimeModule runtime = BuildRuntimeWithCastingSkill(skillDef);
        BattleState state = BuildState(new Vector2I(3, 1));
        BattleUnitState caster = BuildUnit("casting_interrupt_caster", "player", new Vector2I(0, 0), 1, 50, -1);
        BattleUnitState target = BuildUnit("casting_interrupt_target", "enemy", new Vector2I(1, 0), 0, 0, -1);
        LearnSkill(caster, skillDef.skill_id);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, target, true);
        Activate(runtime, state, caster);

        int beforeTargetHp = target.current_hp;
        runtime.IssueCommand(BuildCastingTimeCommand(caster.unit_id, target.unit_id));
        caster.current_hp -= 20;
        runtime.advance(1);

        _test.True(!caster.HasPendingCast(), "维持检定失败后应中断读条。");
        _test.Eq(caster.GetCooldownTyped(skillDef.skill_id), 20, "维持失败中断应启动技能冷却。");
        _test.Eq(target.current_hp, beforeTargetHp, "维持失败中断不应结算技能效果。");
    }

    private static BattleRuntimeModule BuildRuntimeWithSpellControlRoll(int roll)
    {
        SkillDef skillDef = ResourceLoader.Load<SkillDef>("res://data/configs/skills/mage_fireball.tres");
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            new Dictionary<StringName, SkillDef> { [skillDef.skill_id] = skillDef }
        );
        runtime.ConfigureDamageResolverForTests(
            new FixedFailedSaveDamageResolver(new GArray(), new GArray { roll })
        );
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(roll));
        return runtime;
    }

    private static BattleRuntimeModule BuildRuntimeWithCastingSkill(
        SkillDef skillDef,
        int spellControlRoll = 20
    )
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup(null, new Dictionary<StringName, SkillDef> { [skillDef.skill_id] = skillDef });
        runtime.ConfigureDamageResolverForTests(
            new FixedRollDamageResolver(new GArray { 4 }, new GArray { 20 })
        );
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(spellControlRoll));
        return runtime;
    }

    private static SkillDef BuildCastingTimeSkill(
        int maintenanceDc,
        int castingSpellControlDc = 0
    )
    {
        StringName skillId = "test_slow_bolt";
        var damageEffect = new CombatEffectDef
        {
            effect_type = "damage",
            power = 10,
            damage_tag = "physical_blunt",
            effect_target_team_filter = "enemy",
            @params = new GDictionary(),
        };
        var combatProfile = new CombatSkillDef
        {
            skill_id = skillId,
            target_mode = "unit",
            target_team_filter = "enemy",
            target_selection_mode = "single_unit",
            range_value = 3,
            ap_cost = 1,
            mp_cost = 10,
            cooldown_tu = 20,
            casting_time_tu = 10,
            casting_spell_control_dc = castingSpellControlDc,
            casting_maintenance_dc = maintenanceDc,
            pending_cast_binding_mode = "soft_anchor",
            effect_defs = new Godot.Collections.Array<CombatEffectDef> { damageEffect },
        };
        return new SkillDef
        {
            skill_id = skillId,
            display_name = "Test Slow Bolt",
            tags = new GStringNameArray { "test", "magic" },
            combat_profile = combatProfile,
        };
    }

    private static void LearnSkill(BattleUnitState unit, StringName skillId)
    {
        unit.known_active_skill_ids.Add(skillId);
        unit.known_skill_level_map[skillId] = 1;
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "magic_backlash_regression",
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        state.cells = new GDictionary();
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                state.cells[coord] = BuildCell(coord);
            }
        }
        state.cell_columns = BattleCellState.BuildColumnsFromSurfaceCells(state.cells);
        return state;
    }

    private static BattleCellState BuildCell(Vector2I coord)
    {
        var cell = new BattleCellState
        {
            coord = coord,
            base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
            base_height = 4,
        };
        cell.RecalculateRuntimeValues();
        return cell;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int currentAp,
        int currentMp,
        int fireballLevel
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            source_member_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            current_ap = currentAp,
            current_move_points = BattleUnitState.DefaultMovePointsPerTurn,
            current_hp = 100,
            current_mp = currentMp,
            current_stamina = 60,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 100);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), Math.Max(currentMp, 200));
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.SpellProficiencyBonus), 2);
        unit.attribute_snapshot.SetValue("agility", 10);
        unit.attribute_snapshot.SetValue("intelligence", 16);
        unit.attribute_snapshot.SetValue("hidden_luck_at_birth", 0);
        unit.attribute_snapshot.SetValue("faith_luck_bonus", 0);
        unit.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.ArmorClass),
            AttributeService.BASE_ARMOR_CLASS
        );
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));
        if (fireballLevel >= 0)
        {
            unit.known_active_skill_ids.Add("mage_fireball");
            unit.known_skill_level_map[new StringName("mage_fireball")] = fireballLevel;
        }
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private void AddUnit(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit,
        bool isEnemy
    )
    {
        state.units[unit.unit_id] = unit;
        if (isEnemy)
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        else
        {
            state.ally_unit_ids.Add(unit.unit_id);
        }
        _test.True(
            runtime._grid_service.PlaceUnit(state, unit, unit.coord, true),
            $"{unit.unit_id} 应能放入战场。"
        );
    }

    private static void Activate(BattleRuntimeModule runtime, BattleState state, BattleUnitState caster)
    {
        state.active_unit_id = caster.unit_id;
        runtime._state = state;
    }

    private static BattleCommand BuildFireballCommand(StringName unitId, Vector2I targetCoord)
    {
        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = unitId,
            skill_id = "mage_fireball",
            target_coord = targetCoord,
        };
        command.AddTargetCoord(targetCoord);
        return command;
    }

    private static BattleCommand BuildCastingTimeCommand(StringName unitId, StringName targetUnitId)
    {
        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = unitId,
            skill_id = "test_slow_bolt",
            target_unit_id = targetUnitId,
        };
        command.AddTargetUnitId(targetUnitId);
        return command;
    }

    private static BattleCommand BuildCancelCastCommand(StringName unitId)
    {
        return new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.CancelCast),
            unit_id = unitId,
        };
    }

    private static bool LogsContain(GStringArray logLines, string needle)
    {
        foreach (string line in logLines)
        {
            if (line.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool ReportContainsRefundPolicy(GArray reportEntries, string refundPolicy)
    {
        foreach (GDictionary reportEntry in reportEntries)
        {
            if (
                reportEntry.ContainsKey("refund_policy")
                && reportEntry["refund_policy"].AsString() == refundPolicy
            )
            {
                return true;
            }
        }
        return false;
    }

    private static void AdvanceTimelineTu(BattleRuntimeModule runtime, BattleState state, int totalTu)
    {
        if (runtime == null || state == null || totalTu <= 0)
        {
            return;
        }
        state.phase = "timeline_running";
        state.active_unit_id = "";
        state.timeline.ready_unit_ids.Clear();
        state.timeline.tu_per_tick = 5;
        foreach (BattleUnitState unitState in state.GetUnitsTyped())
        {
            if (unitState != null)
            {
                unitState.action_threshold = 1_000_000;
            }
        }
        runtime.advance(totalTu / 5);
    }

}
