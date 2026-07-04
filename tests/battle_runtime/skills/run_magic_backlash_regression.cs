using System;
using System.Collections.Generic;
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
        TestMagicBacklashUsesTypedFumbleProtectionState();
        TestMagicBacklashProjectionPreservesBoundaryPayload();
        TestFireballNormalCastHitsFriendAtFullDamageRoute();
        TestFireballBurnAppliesToEveryTeamInArea();
        TestFireballCriticalRefundsMpWithoutBlockingFriendlyFire();
        TestFireballProtectedFumbleConsumesExtraMpAndSkipsBlast();
        TestFireballUnprotectedFumbleDriftsGroundAnchor();
        TestCastingTimePendingCastStartsThenCompletes();
        TestGroundCastingTimePendingCastStartsThenCompletes();
        TestCastingTimeSpellControlDcOrdinaryFailureConsumesApOnly();
        TestCastingTimeCriticalFailureConsumesHalfCurrentApAndStartsCooldown();
        TestCastingTimeCancelRefundsHalfResourcesWithoutCooldown();
        TestCastingTimeHpMaintenanceFailureInterruptsWithCooldown();

        return _test.Finish("Magic backlash regression");
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

    private void TestMagicBacklashProjectionPreservesBoundaryPayload()
    {
        BattleSpellControlMetadata metadata = new()
        {
            AttackResolution = "critical_fail",
            SpellControlResolution = "critical_fail",
            AttackSuccess = false,
            CriticalHit = false,
            CriticalFail = true,
            OrdinaryMiss = false,
            IsDisadvantage = true,
            HiddenLuckAtBirth = -1,
            FaithLuckBonus = 2,
            EffectiveLuck = 1,
            CritLocked = true,
            CritGateDie = 20,
            CritGateRoll = 1,
            HitRoll = 1,
            FumbleLowEnd = 2,
            CritThreshold = 19,
            LockedSkillHitBonus = 3,
            EffectiveHitRoll = 4,
            ReverseFateDowngraded = true,
        };
        BattleSpellControlResult controlResult = BattleSpellControlResult.None(metadata) with
        {
            SkipEffects = true,
            BacklashTriggered = true,
            FumbleProtected = false,
            MpRefund = 5,
            ExtraMpDrained = 7,
        };
        BattleGroundBacklashTargetResult driftResult = new(
            new[] { new Vector2I(2, 3) },
            true,
            new Vector2I(1, 3),
            new Vector2I(2, 3),
            Vector2I.Right,
            false
        );

        GDictionary metadataPayload = BattleMagicBacklashProjection.Project(metadata);
        GDictionary controlPayload = BattleMagicBacklashProjection.Project(controlResult);
        GDictionary driftPayload = BattleMagicBacklashProjection.Project(driftResult);

        _test.Eq(
            metadataPayload["spell_control_resolution"].AsStringName(),
            new StringName("critical_fail"),
            "spell control metadata projection 应保留控制结果。"
        );
        _test.True(
            metadataPayload["reverse_fate_downgraded"].AsBool(),
            "spell control metadata projection 应保留逆命降级标记。"
        );
        _test.True(
            metadataPayload["trait_trigger_results"].AsGodotArray().Count == 0,
            "spell control metadata projection 应保留旧 payload 的 trait trigger 数组字段。"
        );
        _test.True(controlPayload["skip_effects"].AsBool(), "spell control result projection 应保留跳过效果。");
        _test.Eq(
            ((GDictionary)controlPayload["spell_control"])["crit_gate_roll"].AsInt32(),
            1,
            "spell control result projection 应嵌套 metadata projection。"
        );
        _test.Eq(
            driftPayload["target_coords"].AsGodotArray<Vector2I>()[0],
            new Vector2I(2, 3),
            "ground backlash projection 应保留偏移后的目标坐标。"
        );
        _test.Eq(
            driftPayload["offset_delta"].AsVector2I(),
            Vector2I.Right,
            "ground backlash projection 应保留偏移 delta。"
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
        SkillDefinition skillDef = BuildCastingTimeSkill(maintenanceDc: 0);
        BattleRuntimeModule runtime = BuildRuntimeWithCastingSkill(skillDef);
        BattleState state = BuildState(new Vector2I(4, 1));
        BattleUnitState caster = BuildUnit("casting_caster", "player", new Vector2I(0, 0), 1, 50, -1);
        BattleUnitState target = BuildUnit("casting_target", "enemy", new Vector2I(1, 0), 0, 0, -1);
        LearnSkill(caster, skillDef.SkillId);
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
        _test.Eq(caster.GetCooldownTyped(skillDef.SkillId), 0, "启动读条不应立刻进入冷却。");
        _test.Eq(target.current_hp, beforeTargetHp, "启动读条不应立刻结算伤害。");
        _test.True(startBatch.changed_unit_ids.Contains(caster.unit_id), "启动读条应标记施法者变化。");

        AdvanceTimelineTu(runtime, state, 10);

        _test.True(!caster.HasPendingCast(), "读条完成后应清除 pending cast。");
        _test.True(target.current_hp < beforeTargetHp, "读条完成后应结算技能效果。");
        _test.Eq(caster.GetCooldownTyped(skillDef.SkillId), 20, "读条完成后应启动技能冷却。");
        _test.Eq(caster.action_progress, 0, "读条完成的同一 tick 不应额外获得行动进度。");
    }

    private void TestGroundCastingTimePendingCastStartsThenCompletes()
    {
        SkillDefinition skillDef = BuildGroundCastingTimeSkill(maintenanceDc: 0);
        BattleRuntimeModule runtime = BuildRuntimeWithCastingSkill(skillDef);
        BattleState state = BuildState(new Vector2I(4, 1));
        BattleUnitState caster = BuildUnit("ground_casting_caster", "player", new Vector2I(0, 0), 1, 50, -1);
        BattleUnitState target = BuildUnit("ground_casting_target", "enemy", new Vector2I(1, 0), 0, 0, -1);
        LearnSkill(caster, skillDef.SkillId);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, target, true);
        Activate(runtime, state, caster);

        int beforeTargetHp = target.current_hp;
        BattleCommand command = BuildGroundCastingTimeCommand(caster.unit_id, target.coord);
        BattlePreview preview = runtime.PreviewCommand(command);
        BattleEventBatch startBatch = runtime.IssueCommand(command);

        _test.True(preview.allowed, "地面读条技能启动前预览应允许。");
        _test.True(caster.HasPendingCast(), "地面读条技能使用后应创建 pending cast。");
        _test.Eq(caster.pending_cast?.TargetMode ?? BattleTargetMode.Unknown, BattleTargetMode.Ground, "地面读条 pending cast 应保留 ground target mode。");
        _test.Eq(caster.pending_cast?.TargetCoords.Count ?? -1, 1, "地面读条 pending cast 应保留目标地格。");
        _test.Eq(caster.current_mp, 40, "地面读条启动应立即扣除资源成本。");
        _test.Eq(caster.current_ap, 0, "地面读条启动应消耗本次行动。");
        _test.Eq(target.current_hp, beforeTargetHp, "地面读条启动不应立刻结算伤害。");
        _test.True(startBatch.changed_unit_ids.Contains(caster.unit_id), "地面读条启动应标记施法者变化。");

        AdvanceTimelineTu(runtime, state, 10);

        _test.True(!caster.HasPendingCast(), "地面读条完成后应清除 pending cast。");
        _test.True(target.current_hp < beforeTargetHp, "地面读条完成后应结算目标地格上的单位效果。");
        _test.Eq(caster.GetCooldownTyped(skillDef.SkillId), 20, "地面读条完成后应启动技能冷却。");
    }

    private void TestCastingTimeSpellControlDcOrdinaryFailureConsumesApOnly()
    {
        SkillDefinition skillDef = BuildCastingTimeSkill(maintenanceDc: 0, castingSpellControlDc: 999);
        BattleRuntimeModule runtime = BuildRuntimeWithCastingSkill(skillDef, spellControlRoll: 10);
        BattleState state = BuildState(new Vector2I(3, 1));
        BattleUnitState caster = BuildUnit("casting_dc_fail_caster", "player", new Vector2I(0, 0), 1, 50, -1);
        BattleUnitState target = BuildUnit("casting_dc_fail_target", "enemy", new Vector2I(1, 0), 0, 0, -1);
        LearnSkill(caster, skillDef.SkillId);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, target, true);
        Activate(runtime, state, caster);

        int beforeTargetHp = target.current_hp;
        runtime.IssueCommand(BuildCastingTimeCommand(caster.unit_id, target.unit_id));

        _test.True(!caster.HasPendingCast(), "读条法术控制普通失败不应创建 pending cast。");
        _test.Eq(caster.current_ap, 0, "法术控制普通失败应按技能 AP 成本消耗行动。");
        _test.Eq(caster.current_mp, 50, "法术控制普通失败发生在持久资源扣除前。");
        _test.Eq(caster.GetCooldownTyped(skillDef.SkillId), 0, "法术控制普通失败不应启动冷却。");
        _test.Eq(target.current_hp, beforeTargetHp, "法术控制普通失败不应结算技能效果。");
    }

    private void TestCastingTimeCriticalFailureConsumesHalfCurrentApAndStartsCooldown()
    {
        SkillDefinition skillDef = BuildCastingTimeSkill(maintenanceDc: 0, castingSpellControlDc: 1);
        BattleRuntimeModule runtime = BuildRuntimeWithCastingSkill(skillDef, spellControlRoll: 1);
        BattleState state = BuildState(new Vector2I(3, 1));
        BattleUnitState caster = BuildUnit("casting_critical_fail_caster", "player", new Vector2I(0, 0), 5, 50, -1);
        BattleUnitState target = BuildUnit("casting_critical_fail_target", "enemy", new Vector2I(1, 0), 0, 0, -1);
        LearnSkill(caster, skillDef.SkillId);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, target, true);
        Activate(runtime, state, caster);

        int beforeTargetHp = target.current_hp;
        runtime.IssueCommand(BuildCastingTimeCommand(caster.unit_id, target.unit_id));

        _test.True(!caster.HasPendingCast(), "读条法术控制大失败不应创建 pending cast。");
        _test.Eq(caster.current_ap, 2, "读条法术控制大失败应扣除当前 AP 的 50% 且向上取整。");
        _test.Eq(caster.current_mp, 50, "读条法术控制大失败发生在持久资源扣除前。");
        _test.Eq(caster.GetCooldownTyped(skillDef.SkillId), 20, "读条法术控制大失败应启动技能冷却。");
        _test.Eq(target.current_hp, beforeTargetHp, "读条法术控制大失败不应结算技能效果。");
    }

    private void TestCastingTimeCancelRefundsHalfResourcesWithoutCooldown()
    {
        SkillDefinition skillDef = BuildCastingTimeSkill(maintenanceDc: 0);
        BattleRuntimeModule runtime = BuildRuntimeWithCastingSkill(skillDef);
        BattleState state = BuildState(new Vector2I(3, 1));
        BattleUnitState caster = BuildUnit("casting_cancel_caster", "player", new Vector2I(0, 0), 1, 50, -1);
        BattleUnitState target = BuildUnit("casting_cancel_target", "enemy", new Vector2I(1, 0), 0, 0, -1);
        LearnSkill(caster, skillDef.SkillId);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, target, true);
        Activate(runtime, state, caster);

        runtime.IssueCommand(BuildCastingTimeCommand(caster.unit_id, target.unit_id));
        BattlePreview cancelPreview = runtime.PreviewCommand(BuildCancelCastCommand(caster.unit_id));
        BattleEventBatch cancelBatch = runtime.IssueCommand(BuildCancelCastCommand(caster.unit_id));

        _test.True(cancelPreview.allowed, "玩家可控己方单位应可在 timeline 阶段取消读条。");
        _test.True(!caster.HasPendingCast(), "取消读条后应清除 pending cast。");
        _test.Eq(caster.current_mp, 45, "取消读条应返还已付资源成本的一半。");
        _test.Eq(caster.GetCooldownTyped(skillDef.SkillId), 0, "手动取消读条不应启动冷却。");
        _test.True(cancelBatch.changed_unit_ids.Contains(caster.unit_id), "取消读条应标记施法者变化。");
        _test.True(
            ReportContainsRefundPolicy(cancelBatch.report_entries, "half_persistent_costs"),
            "取消读条战报应暴露 refund_policy。"
        );
    }

    private void TestCastingTimeHpMaintenanceFailureInterruptsWithCooldown()
    {
        SkillDefinition skillDef = BuildCastingTimeSkill(maintenanceDc: 999);
        BattleRuntimeModule runtime = BuildRuntimeWithCastingSkill(skillDef);
        BattleState state = BuildState(new Vector2I(3, 1));
        BattleUnitState caster = BuildUnit("casting_interrupt_caster", "player", new Vector2I(0, 0), 1, 50, -1);
        BattleUnitState target = BuildUnit("casting_interrupt_target", "enemy", new Vector2I(1, 0), 0, 0, -1);
        LearnSkill(caster, skillDef.SkillId);
        AddUnit(runtime, state, caster, false);
        AddUnit(runtime, state, target, true);
        Activate(runtime, state, caster);

        int beforeTargetHp = target.current_hp;
        runtime.IssueCommand(BuildCastingTimeCommand(caster.unit_id, target.unit_id));
        caster.current_hp -= 20;
        runtime.advance(1);

        _test.True(!caster.HasPendingCast(), "维持检定失败后应中断读条。");
        _test.Eq(caster.GetCooldownTyped(skillDef.SkillId), 20, "维持失败中断应启动技能冷却。");
        _test.Eq(target.current_hp, beforeTargetHp, "维持失败中断不应结算技能效果。");
    }

    private static BattleRuntimeModule BuildRuntimeWithSpellControlRoll(int roll)
    {
        SkillDefinition skillDefinition = TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/mage_fireball.tres",
            "magic_backlash:mage_fireball"
        );
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition>
            {
                [skillDefinition.SkillId] = skillDefinition,
            }
        );
        runtime.ConfigureDamageResolverForTests(
            new FixedFailedSaveDamageResolver(new GArray(), new GArray { roll })
        );
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(roll));
        return runtime;
    }

    private static BattleRuntimeModule BuildRuntimeWithCastingSkill(
        SkillDefinition skillDef,
        int spellControlRoll = 20
    )
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup(null, new Dictionary<StringName, SkillDefinition> { [skillDef.SkillId] = skillDef });
        runtime.ConfigureDamageResolverForTests(
            new FixedRollDamageResolver(new GArray { 4 }, new GArray { 20 })
        );
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(spellControlRoll));
        return runtime;
    }

    private static SkillDefinition BuildCastingTimeSkill(
        int maintenanceDc,
        int castingSpellControlDc = 0
    )
    {
        StringName skillId = "test_slow_bolt";
        CombatEffectDefinition damageEffect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            power: 10,
            damageTag: "physical_blunt"
        );
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: "Test Slow Bolt",
            tags: new[] { new StringName("test"), new StringName("magic") },
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[] { damageEffect },
                targetMode: "unit",
                targetTeamFilter: "enemy",
                targetSelectionMode: "single_unit",
                rangeValue: 3,
                apCost: 1,
                mpCost: 10,
                cooldownTu: 20,
                castingTimeTu: 10,
                castingSpellControlDc: castingSpellControlDc,
                castingMaintenanceDc: maintenanceDc,
                pendingCastBindingMode: "soft_anchor"
            )
        );
    }

    private static SkillDefinition BuildGroundCastingTimeSkill(int maintenanceDc)
    {
        StringName skillId = "test_slow_ground_burst";
        CombatEffectDefinition damageEffect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            power: 10,
            damageTag: "physical_blunt"
        );
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: "Test Slow Ground Burst",
            tags: new[] { new StringName("test"), new StringName("magic") },
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[] { damageEffect },
                targetMode: "ground",
                targetTeamFilter: "enemy",
                targetSelectionMode: "single_cell",
                areaPattern: "single",
                rangeValue: 3,
                apCost: 1,
                mpCost: 10,
                cooldownTu: 20,
                castingTimeTu: 10,
                castingMaintenanceDc: maintenanceDc,
                pendingCastBindingMode: "ground_bind"
            )
        );
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
        state.ClearCells();
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
        state.SetUnit(unit);
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
        runtime.SetupStateForTests(state);
    }

    private static BattleCommand BuildFireballCommand(StringName unitId, Vector2I targetCoord)
    {
        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = unitId,
            skill_entry_id = BattleSkillEntryIds.KnownSkill("mage_fireball"),
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
            skill_entry_id = BattleSkillEntryIds.KnownSkill("test_slow_bolt"),
            skill_id = "test_slow_bolt",
            target_unit_id = targetUnitId,
        };
        command.AddTargetUnitId(targetUnitId);
        return command;
    }

    private static BattleCommand BuildGroundCastingTimeCommand(StringName unitId, Vector2I targetCoord)
    {
        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = unitId,
            skill_entry_id = BattleSkillEntryIds.KnownSkill("test_slow_ground_burst"),
            skill_id = "test_slow_ground_burst",
            target_coord = targetCoord,
        };
        command.AddTargetCoord(targetCoord);
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
