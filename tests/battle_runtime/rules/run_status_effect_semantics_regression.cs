using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_status_effect_semantics_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestStatusSemanticTableIsPlainStaticTypedCSharp();
        TestStaggeredRefreshesWithoutStackingAndExpiresOnTuProgress();
        TestMeteorConcussedSharesStaggeredApPenaltyGroup();
        TestMeteorConcussedConsumesWithoutZeroApLog();
        TestBurningStacksAndTicksOnTimelineInterval();
        TestShortBurningCanExpireBeforeFirstTick();
        TestSlowIncreasesMoveCostAndExpiresOnTuProgress();
        TestRefreshTimelineStatusesKeepSingleStackAndMaxDuration();
        TestTauntedUsesTimelineDecayWithoutTurnEndDecay();
        TestStatusDurationIsNotBackfilledFromSemanticDefaults();
        TestStatusParamsDurationIsNotUsedAsRuntimeDuration();
        TestStatusDurationTuIgnoresLegacyParamsDuration();
        TestDamageResolverReadsOnlyFormalDamageStatusParams();
        TestSkillTurnStatusUsesTypedFieldsNotParams();
        TestStatusEffectFromDictRequiresExplicitStatusId();
        TestLegacyStatusEffectMapKeysAreNotStatusIdFallbacks();
        TestNonDictionaryStatusEffectEntriesAreRejected();
        TestStatusEffectToDictFromDictRoundTripStillRestores();

        if (_failures.Count == 0)
        {
            GD.Print("Status effect semantics regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Status effect semantics regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestStatusSemanticTableIsPlainStaticTypedCSharp()
    {
        Type ruleType = typeof(BattleStatusSemanticTable);
        AssertTrue(ruleType.IsAbstract && ruleType.IsSealed, "状态语义表应是 plain static C# class。");
        AssertFalse(typeof(GodotObject).IsAssignableFrom(ruleType), "状态语义表不应继承 GodotObject/RefCounted。");
        AssertFalse(HasAttributeNamed(ruleType, "GlobalClassAttribute"), "状态语义表不应注册 GlobalClass。");
        AssertTrue(ruleType.GetMethod("get_semantic") == null, "状态语义表不应保留 Dictionary get_semantic 入口。");
        AssertEq(
            BattleStatusSemanticTable.GetSemantic("burning").TickMode,
            BattleStatusSemanticTable.TICK_TIMELINE_DAMAGE,
            "typed semantic 应暴露正式 tick mode。"
        );
    }

    private void TestStaggeredRefreshesWithoutStackingAndExpiresOnTuProgress()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildState(new Vector2I(4, 3));
        BattleUnitState striker = BuildUnit("staggered_source", new Vector2I(1, 1), 2);
        BattleUnitState target = BuildUnit("staggered_target", new Vector2I(2, 1), 2);
        target.faction_id = "enemy";

        AddUnit(runtime, state, striker);
        AddUnit(runtime, state, target);
        state.ally_unit_ids = new GStringNameArray { striker.unit_id };
        state.enemy_unit_ids = new GStringNameArray { target.unit_id };
        runtime._state = state;

        ApplyStatus(runtime, striker, target, "staggered", 15);
        ApplyStatus(runtime, striker, target, "staggered", 15);
        BattleStatusEffectState staggerEntry = target.get_status_effect("staggered");
        AssertTrue(staggerEntry != null, "重复施加 staggered 后应保留正式状态。");
        AssertEq(staggerEntry != null ? staggerEntry.stacks : -1, 1, "staggered 应按 refresh 语义而不是累加层数。");
        AssertEq(staggerEntry != null ? staggerEntry.duration : -1, 15, "staggered 应记录剩余 TU。");

        state.phase = "timeline_running";
        state.active_unit_id = "";
        state.timeline.ready_unit_ids.Clear();
        state.timeline.ready_unit_ids.Add(target.unit_id);
        runtime.advance(0);
        AssertEq(target.current_ap, 1, "staggered 刷新后仍只应在回合开始扣 1 点行动点。");

        BattleCommand waitCommand = BuildWaitCommand(target.unit_id);
        runtime.issue_command(waitCommand);
        AssertTrue(target.has_status_effect("staggered"), "staggered 不应在目标回合结束后被立即移除。");
        AdvanceTimelineTu(runtime, state, 15);
        AssertFalse(target.has_status_effect("staggered"), "staggered 应在 TU 走完后移除。");
    }

    private void TestMeteorConcussedSharesStaggeredApPenaltyGroup()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleUnitState target = BuildUnit("meteor_concussed_group_target", new Vector2I(1, 1), 3);
        SetStatusParams(target, "staggered", new GDictionary());
        SetStatusParams(target, "meteor_concussed", new GDictionary());
        BattleStatusEffectState meteorEntry = target.get_status_effect("meteor_concussed");
        if (meteorEntry != null)
        {
            meteorEntry.power = 2;
            target.set_status_effect(meteorEntry);
        }

        var batch = new BattleEventBatch();
        BattleStatusTickResult result = runtime._apply_turn_start_statuses_result(target, batch);
        AssertTrue(result.Changed, "meteor_concussed 参与回合开始结算后应报告 changed。");
        AssertEq(target.current_ap, 1, "meteor_concussed 与 staggered 同组时应只扣最高 AP 惩罚，而不是叠加扣 3。");
        AssertFalse(target.has_status_effect("meteor_concussed"), "meteor_concussed 应在参与回合开始 AP 惩罚后消耗。");
        AssertTrue(target.has_status_effect("staggered"), "同组结算不应顺带消耗普通 staggered。");
        AssertEq(batch.log_lines.Count, 1, "同组 AP 惩罚应只产生一条日志。");
        AssertTrue(
            batch.log_lines.Count > 0 && batch.log_lines[0].Contains("少 2 点 AP"),
            "同组 AP 惩罚日志应记录实际扣除值。"
        );
        AssertEq(
            BattleStatusSemanticTable.GetAttackRollPenalty(meteorEntry),
            2,
            "meteor_concussed 应提供 -2 攻击检定语义。"
        );
        AssertTrue(
            BattleStatusSemanticTable.IsHarmfulStatus("meteor_concussed"),
            "meteor_concussed 应计为有害状态。"
        );
        AssertTrue(
            BattleStatusSemanticTable.IsDispellableHarmfulStatus("meteor_concussed"),
            "meteor_concussed 应允许按有害魔法驱散。"
        );
    }

    private void TestMeteorConcussedConsumesWithoutZeroApLog()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleUnitState target = BuildUnit("meteor_concussed_zero_ap_target", new Vector2I(1, 1), 0);
        SetStatusParams(target, "meteor_concussed", new GDictionary());
        BattleStatusEffectState meteorEntry = target.get_status_effect("meteor_concussed");
        if (meteorEntry != null)
        {
            meteorEntry.power = 2;
            target.set_status_effect(meteorEntry);
        }

        var batch = new BattleEventBatch();
        BattleStatusTickResult result = runtime._apply_turn_start_statuses_result(target, batch);
        AssertTrue(result.Changed, "meteor_concussed 即使目标 AP 为 0，也应因状态消耗报告 changed。");
        AssertEq(target.current_ap, 0, "AP 为 0 时 meteor_concussed 不应产生负 AP。");
        AssertFalse(target.has_status_effect("meteor_concussed"), "AP 为 0 时 meteor_concussed 仍应完成一次性消耗。");
        AssertEq(batch.log_lines.Count, 0, "AP 为 0 时不应记录“少 AP”的误导日志。");
    }

    private void TestBurningStacksAndTicksOnTimelineInterval()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildState(new Vector2I(4, 3));
        BattleUnitState caster = BuildUnit("burning_source", new Vector2I(0, 1), 2);
        BattleUnitState target = BuildUnit("burning_target", new Vector2I(2, 1), 2);
        target.faction_id = "enemy";
        target.current_hp = 20;
        target.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 20);

        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, target);
        state.ally_unit_ids = new GStringNameArray { caster.unit_id };
        state.enemy_unit_ids = new GStringNameArray { target.unit_id };
        runtime._state = state;

        ApplyStatus(runtime, caster, target, "burning", 20, 1, 10);
        ApplyStatus(runtime, caster, target, "burning", 20, 1, 10);
        BattleStatusEffectState burningEntry = target.get_status_effect("burning");
        AssertTrue(burningEntry != null, "burning 应在重复施加后存在于正式状态字典中。");
        AssertEq(burningEntry != null ? burningEntry.stacks : -1, 2, "burning 应按 add 语义累加层数。");
        AssertEq(burningEntry != null ? burningEntry.duration : -1, 20, "burning 应沿用施加时给定的剩余 TU。");
        AssertEq(burningEntry != null ? burningEntry.tick_interval_tu : -1, 10, "burning 应记录正式周期 tick 间隔。");

        state.phase = "timeline_running";
        state.active_unit_id = "";
        state.timeline.ready_unit_ids.Clear();
        state.timeline.ready_unit_ids.Add(target.unit_id);
        runtime.advance(0);
        AssertEq(target.current_hp, 20, "burning 不应在回合开始隐式结算伤害。");
        runtime.issue_command(BuildWaitCommand(target.unit_id));
        burningEntry = target.get_status_effect("burning");
        AssertEq(burningEntry != null ? burningEntry.duration : -1, 20, "burning 不应在回合结束后递减 TU。");

        AdvanceTimelineTu(runtime, state, 10);
        burningEntry = target.get_status_effect("burning");
        AssertEq(burningEntry != null ? burningEntry.duration : -1, 10, "burning 应随时间轴推进递减剩余 TU。");
        AssertEq(target.current_hp, 18, "2 层 burning 应在第一个周期 tick 结算 2 点灼烧伤害。");

        state.phase = "timeline_running";
        state.active_unit_id = "";
        state.timeline.ready_unit_ids.Clear();
        state.timeline.ready_unit_ids.Add(target.unit_id);
        runtime.advance(0);
        AssertEq(target.current_hp, 18, "burning 不应因进入第二个行动窗口额外结算伤害。");
        runtime.issue_command(BuildWaitCommand(target.unit_id));
        AssertTrue(target.has_status_effect("burning"), "burning 不应在第二个回合结束时被 turn end 提前清除。");
        AdvanceTimelineTu(runtime, state, 10);
        AssertFalse(target.has_status_effect("burning"), "burning 到期后应按 TU 正式移除。");
        AssertEq(target.current_hp, 16, "2 层 burning 应在到期边界完成第二个周期 tick。");
    }

    private void TestShortBurningCanExpireBeforeFirstTick()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildState(new Vector2I(4, 3));
        BattleUnitState caster = BuildUnit("short_burning_source", new Vector2I(0, 1), 2);
        BattleUnitState target = BuildUnit("short_burning_target", new Vector2I(2, 1), 2);
        target.faction_id = "enemy";
        target.current_hp = 20;

        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, target);
        state.ally_unit_ids = new GStringNameArray { caster.unit_id };
        state.enemy_unit_ids = new GStringNameArray { target.unit_id };
        runtime._state = state;

        ApplyStatus(runtime, caster, target, "burning", 5, 1, 10);
        AdvanceTimelineTu(runtime, state, 5);
        AssertFalse(target.has_status_effect("burning"), "短于 tick 间隔的 burning 应按 TU 到期。");
        AssertEq(target.current_hp, 20, "短于 tick 间隔的 burning 不应保证至少触发一次伤害。");
    }

    private void TestSlowIncreasesMoveCostAndExpiresOnTuProgress()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildState(new Vector2I(5, 3));
        BattleUnitState source = BuildUnit("slow_source", new Vector2I(0, 1), 2);
        BattleUnitState target = BuildUnit("slow_target", new Vector2I(1, 1), 3);
        BattleUnitState enemy = BuildUnit("slow_enemy_anchor", new Vector2I(4, 1), 1);
        enemy.faction_id = "enemy";

        AddUnit(runtime, state, source);
        AddUnit(runtime, state, target);
        AddUnit(runtime, state, enemy);
        state.ally_unit_ids = new GStringNameArray { source.unit_id, target.unit_id };
        state.enemy_unit_ids = new GStringNameArray { enemy.unit_id };
        runtime._state = state;

        ApplyStatus(runtime, source, target, "slow", 15);
        state.phase = "timeline_running";
        state.active_unit_id = "";
        state.timeline.ready_unit_ids.Clear();
        state.timeline.ready_unit_ids.Add(target.unit_id);
        runtime.advance(0);
        AssertTrue(target.has_status_effect("slow"), "slow 应在受影响单位回合开始后仍保持生效。");

        var moveCommand = new BattleCommand
        {
            command_type = BattleCommand.TYPE_MOVE(),
            unit_id = target.unit_id,
            target_coord = new Vector2I(2, 1),
        };
        BattlePreview preview = runtime.preview_command(moveCommand);
        AssertTrue(preview != null && preview.allowed, "slow 状态下的相邻移动仍应合法。");
        AssertTrue(
            preview != null
                && preview.log_lines.Count > 0
                && preview.log_lines[0].ToString().Contains("距离消耗 2 点移动力"),
            "slow 应把基础 1 点移动力的平地移动提升为 2 点移动力。"
        );

        runtime.issue_command(moveCommand);
        AssertEq(target.current_move_points, 0, "移动成功后应耗尽本回合移动力，即使只移动 1 格。");
        AssertEq(target.current_ap, 3, "slow 只应抬高移动行动点消耗，不应继续扣除 AP。");
        runtime.issue_command(BuildWaitCommand(target.unit_id));
        AssertTrue(target.has_status_effect("slow"), "slow 不应在目标回合结束后按 turn end 立刻移除。");
        AdvanceTimelineTu(runtime, state, 15);
        AssertFalse(target.has_status_effect("slow"), "slow 应在 TU 走完后移除。");
    }

    private void TestRefreshTimelineStatusesKeepSingleStackAndMaxDuration()
    {
        (StringName StatusId, string Label)[] cases =
        {
            ("attack_up", "attack_up"),
            ("archer_pre_aim", "archer_pre_aim"),
            ("pinned", "pinned"),
            ("taunted", "taunted"),
        };
        foreach ((StringName statusId, string label) in cases)
        {
            var firstEffect = new CombatEffectDef
            {
                effect_type = "status",
                status_id = statusId,
                power = 1,
                duration_tu = 10,
            };
            var secondEffect = new CombatEffectDef
            {
                effect_type = "status",
                status_id = statusId,
                power = 2,
                duration_tu = 15,
            };

            AssertTrue(BattleStatusSemanticTable.HasSemantic(statusId), $"{label} 应注册正式状态语义。");
            BattleStatusEffectState merged = BattleStatusSemanticTable.MergeStatus(firstEffect, "source_a");
            merged = BattleStatusSemanticTable.MergeStatus(secondEffect, "source_b", merged);
            AssertTrue(merged != null, $"{label} 合并后应生成正式状态。");
            AssertEq(merged != null ? merged.stacks : -1, 1, $"{label} 应按 refresh 语义保持单层。");
            AssertEq(merged != null ? merged.power : -1, 2, $"{label} 应保留更高 power。");
            AssertEq(merged != null ? merged.duration : -1, 15, $"{label} 应保留更长的剩余 TU。");
            AssertEq(
                BattleStatusSemanticTable.GetTurnStartApPenalty(merged),
                0,
                $"{label} 不应附带 turn start AP penalty 语义。"
            );
            AssertEq(
                BattleStatusSemanticTable.GetTurnStartDamage(merged),
                0,
                $"{label} 不应附带 turn start damage 语义。"
            );
            AssertEq(
                BattleStatusSemanticTable.GetMoveCostDelta(merged),
                0,
                $"{label} 不应附带 move cost delta 语义。"
            );
        }
    }

    private void TestTauntedUsesTimelineDecayWithoutTurnEndDecay()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildState(new Vector2I(4, 3));
        BattleUnitState source = BuildUnit("taunted_source", new Vector2I(0, 1), 2);
        BattleUnitState target = BuildUnit("taunted_target", new Vector2I(2, 1), 2);
        target.faction_id = "enemy";

        AddUnit(runtime, state, source);
        AddUnit(runtime, state, target);
        state.ally_unit_ids = new GStringNameArray { source.unit_id };
        state.enemy_unit_ids = new GStringNameArray { target.unit_id };
        runtime._state = state;

        ApplyStatus(runtime, source, target, "taunted", 15);
        BattleStatusEffectState tauntedEntry = target.get_status_effect("taunted");
        AssertTrue(tauntedEntry != null, "taunted 应写入正式状态字典。");
        AssertEq(tauntedEntry != null ? tauntedEntry.duration : -1, 15, "taunted 应记录施加时的剩余 TU。");

        state.phase = "timeline_running";
        state.active_unit_id = "";
        state.timeline.ready_unit_ids.Clear();
        state.timeline.ready_unit_ids.Add(target.unit_id);
        runtime.advance(0);
        runtime.issue_command(BuildWaitCommand(target.unit_id));
        AssertTrue(target.has_status_effect("taunted"), "taunted 不应在目标回合结束后被 turn end 提前移除。");

        AdvanceTimelineTu(runtime, state, 15);
        AssertFalse(target.has_status_effect("taunted"), "taunted 应在 TU 走完后移除。");
    }

    private void TestStatusDurationIsNotBackfilledFromSemanticDefaults()
    {
        var effectDef = new CombatEffectDef
        {
            effect_type = "status",
            status_id = "pinned",
            power = 1,
        };

        BattleStatusEffectState merged = BattleStatusSemanticTable.MergeStatus(effectDef, "source_unit");
        AssertTrue(merged != null, "状态效果应能在缺少 duration_tu 时正常合并。");
        AssertTrue(merged != null && !merged.has_duration(), "缺少来源时长时，状态不应再从语义表回填默认 TU。");
    }

    private void TestStatusParamsDurationIsNotUsedAsRuntimeDuration()
    {
        var effectDef = new CombatEffectDef
        {
            effect_type = "status",
            status_id = "pinned",
            power = 1,
            @params = new GDictionary { ["duration"] = 15 },
        };

        BattleStatusEffectState merged = BattleStatusSemanticTable.MergeStatus(effectDef, "source_unit");
        AssertTrue(merged != null, "旧 params.duration 不应阻止状态对象合并。");
        AssertTrue(merged != null && !merged.has_duration(), "旧 params.duration 不应再恢复为状态剩余 TU。");
    }

    private void TestStatusDurationTuIgnoresLegacyParamsDuration()
    {
        var effectDef = new CombatEffectDef
        {
            effect_type = "status",
            status_id = "pinned",
            power = 1,
            duration_tu = 20,
            @params = new GDictionary { ["duration"] = 90 },
        };

        BattleStatusEffectState merged = BattleStatusSemanticTable.MergeStatus(effectDef, "source_unit");
        AssertTrue(merged != null, "正式 duration_tu 应继续生成状态对象。");
        AssertEq(merged != null ? merged.duration : -1, 20, "正式 duration_tu 应生效，旧 params.duration 不应覆盖。");
    }

    private void TestDamageResolverReadsOnlyFormalDamageStatusParams()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleUnitState source = BuildUnit("damage_alias_source", Vector2I.Zero, 2);
        CombatEffectDef physicalEffect = BuildDamageEffect(10, "physical_slash");

        BattleUnitState formalTagTarget = BuildUnit("formal_damage_tag_target", Vector2I.Zero, 2);
        SetStatusParams(
            formalTagTarget,
            "formal_fire_barrier",
            new GDictionary { ["damage_tag"] = "fire", ["mitigation_tier"] = "half" }
        );
        GDictionary formalTagResult = runtime._damage_resolver.resolve_effects(
            source,
            formalTagTarget,
            new GArray { physicalEffect }
        );
        AssertEq(DictInt(formalTagResult, "damage", -1), 10, "正式 damage_tag 不匹配时不应套用 mitigation_tier。");

        BattleUnitState legacyTagTarget = BuildUnit("legacy_tag_target", Vector2I.Zero, 2);
        SetStatusParams(
            legacyTagTarget,
            "legacy_fire_barrier",
            new GDictionary { ["tag"] = "fire", ["mitigation_tier"] = "half" }
        );
        GDictionary legacyTagResult = runtime._damage_resolver.resolve_effects(
            source,
            legacyTagTarget,
            new GArray { physicalEffect }
        );
        AssertEq(DictInt(legacyTagResult, "damage", -1), 5, "旧 params.tag 不应再被当作 damage_tag 过滤。");

        CombatEffectDef formalBypassEffect = BuildDamageEffect(10, "physical_slash");
        formalBypassEffect.dr_bypass_tag = "armor_pierce";
        BattleUnitState formalBypassTarget = BuildUnit("formal_bypass_target", Vector2I.Zero, 2);
        SetStatusParams(
            formalBypassTarget,
            "formal_content_dr",
            new GDictionary { ["content_dr"] = 4, ["dr_bypass_tag"] = "armor_pierce" }
        );
        GDictionary formalBypassResult = runtime._damage_resolver.resolve_effects(
            source,
            formalBypassTarget,
            new GArray { formalBypassEffect }
        );
        AssertEq(DictInt(formalBypassResult, "damage", -1), 10, "正式 dr_bypass_tag 匹配时应绕过 content_dr。");

        CombatEffectDef legacyEffectBypass = BuildDamageEffect(10, "physical_slash");
        legacyEffectBypass.@params["bypass_tag"] = "armor_pierce";
        BattleUnitState legacyEffectBypassTarget = BuildUnit("legacy_effect_bypass_target", Vector2I.Zero, 2);
        SetStatusParams(
            legacyEffectBypassTarget,
            "formal_content_dr",
            new GDictionary { ["content_dr"] = 4, ["dr_bypass_tag"] = "armor_pierce" }
        );
        GDictionary legacyEffectBypassResult = runtime._damage_resolver.resolve_effects(
            source,
            legacyEffectBypassTarget,
            new GArray { legacyEffectBypass }
        );
        AssertEq(DictInt(legacyEffectBypassResult, "damage", -1), 6, "旧 effect params.bypass_tag 不应再绕过 content_dr。");

        BattleUnitState legacyStatusBypassTarget = BuildUnit("legacy_status_bypass_target", Vector2I.Zero, 2);
        SetStatusParams(
            legacyStatusBypassTarget,
            "legacy_content_dr",
            new GDictionary { ["content_dr"] = 4, ["bypass_tag"] = "armor_pierce" }
        );
        GDictionary legacyStatusBypassResult = runtime._damage_resolver.resolve_effects(
            source,
            legacyStatusBypassTarget,
            new GArray { formalBypassEffect }
        );
        AssertEq(DictInt(legacyStatusBypassResult, "damage", -1), 6, "旧 status params.bypass_tag 不应再被当作 dr_bypass_tag。");

        CombatEffectDef formalLowHpEffect = BuildDamageEffect(10, "physical_slash");
        formalLowHpEffect.bonus_condition = "target_low_hp";
        formalLowHpEffect.hp_ratio_threshold_percent = 70;
        formalLowHpEffect.bonus_damage_dice_count = 4;
        formalLowHpEffect.bonus_damage_dice_sides = 1;
        BattleUnitState formalLowHpTarget = BuildUnit("formal_low_hp_target", Vector2I.Zero, 2);
        formalLowHpTarget.current_hp = 18;
        GDictionary formalLowHpResult = runtime._damage_resolver.resolve_effects(
            source,
            formalLowHpTarget,
            new GArray { formalLowHpEffect }
        );
        AssertEq(DictInt(formalLowHpResult, "damage", -1), 14, "正式 hp_ratio_threshold_percent 应控制低血追加伤害骰阈值。");
        BattleUnitState formalLowHpCritTarget = BuildUnit("formal_low_hp_crit_target", Vector2I.Zero, 2);
        formalLowHpCritTarget.current_hp = 18;
        GDictionary formalLowHpCritResult = runtime._damage_resolver.resolve_effects(
            source,
            formalLowHpCritTarget,
            new GArray { formalLowHpEffect },
            new GDictionary { ["critical_hit"] = true }
        );
        AssertEq(DictInt(formalLowHpCritResult, "damage", -1), 18, "低血暴击应额外掷一组处决追加骰。");

        CombatEffectDef legacyLowHpEffect = BuildDamageEffect(10, "physical_slash");
        legacyLowHpEffect.bonus_condition = "target_low_hp";
        legacyLowHpEffect.@params["low_hp_ratio"] = 0.7;
        legacyLowHpEffect.@params["bonus_damage_dice_count"] = 4;
        legacyLowHpEffect.@params["bonus_damage_dice_sides"] = 1;
        BattleUnitState legacyLowHpTarget = BuildUnit("legacy_low_hp_target", Vector2I.Zero, 2);
        legacyLowHpTarget.current_hp = 18;
        GDictionary legacyLowHpResult = runtime._damage_resolver.resolve_effects(
            source,
            legacyLowHpTarget,
            new GArray { legacyLowHpEffect }
        );
        AssertEq(DictInt(legacyLowHpResult, "damage", -1), 10, "旧 params.low_hp_ratio 不应再覆盖默认低血阈值或触发追加骰。");
    }

    private void TestSkillTurnStatusUsesTypedFieldsNotParams()
    {
        var resolver = new BattleRuntimeSkillTurnResolver();

        BattleUnitState legacyBoolUnit = BuildUnit("legacy_bool_param_unit", Vector2I.Zero, 2);
        SetStatusParams(legacyBoolUnit, "legacy_counter_lock", new GDictionary { ["lock_counterattack"] = true });
        AssertFalse(
            resolver.has_counterattack_lock_status(legacyBoolUnit),
            "status params.lock_counterattack 不应再驱动反击锁。"
        );

        BattleUnitState formalBoolUnit = BuildUnit("formal_bool_param_unit", Vector2I.Zero, 2);
        SetTypedStatusFields(formalBoolUnit, "formal_counter_lock", lockCounterattack: true);
        AssertTrue(
            resolver.has_counterattack_lock_status(formalBoolUnit),
            "typed lock_counterattack 字段应驱动反击锁。"
        );

        BattleUnitState legacyIntUnit = BuildUnit("legacy_int_param_unit", Vector2I.Zero, 2);
        SetStatusParams(
            legacyIntUnit,
            "legacy_main_skill_lock",
            new GDictionary { ["main_skill_lock_other_debuff_count"] = 2 }
        );
        AssertEq(
            resolver.get_main_skill_lock_other_debuff_count(legacyIntUnit),
            0,
            "status params.main_skill_lock_other_debuff_count 不应再驱动主技能锁。"
        );

        BattleUnitState formalIntUnit = BuildUnit("formal_int_param_unit", Vector2I.Zero, 2);
        SetTypedStatusFields(formalIntUnit, "formal_main_skill_lock", mainSkillLockOtherDebuffCount: 2);
        AssertEq(
            resolver.get_main_skill_lock_other_debuff_count(formalIntUnit),
            2,
            "typed main_skill_lock_other_debuff_count 字段应驱动主技能锁。"
        );

        BattleUnitState legacyCountsTrueUnit = BuildUnit("legacy_counts_true_unit", Vector2I.Zero, 2);
        SetStatusParams(legacyCountsTrueUnit, "custom_bad_debuff", new GDictionary { ["counts_as_debuff"] = true });
        AssertEq(
            resolver.count_debuff_statuses(legacyCountsTrueUnit),
            0,
            "status params.counts_as_debuff=true 不应再把自定义状态计为 debuff。"
        );

        BattleUnitState formalCountsTrueUnit = BuildUnit("formal_counts_true_unit", Vector2I.Zero, 2);
        SetTypedStatusFields(
            formalCountsTrueUnit,
            "custom_formal_debuff",
            countsAsDebuffOverride: true,
            countsAsDebuff: true
        );
        AssertEq(
            resolver.count_debuff_statuses(formalCountsTrueUnit),
            1,
            "typed counts_as_debuff=true 应继续把自定义状态计为 debuff。"
        );

        BattleUnitState legacyCountsFalseUnit = BuildUnit("legacy_counts_false_unit", Vector2I.Zero, 2);
        SetStatusParams(legacyCountsFalseUnit, "burning", new GDictionary { ["counts_as_debuff"] = false });
        AssertEq(
            resolver.count_debuff_statuses(legacyCountsFalseUnit),
            1,
            "status params.counts_as_debuff=false 不应再覆盖内建 debuff 表。"
        );

        BattleUnitState formalCountsFalseUnit = BuildUnit("formal_counts_false_unit", Vector2I.Zero, 2);
        SetTypedStatusFields(
            formalCountsFalseUnit,
            "burning",
            countsAsDebuffOverride: true,
            countsAsDebuff: false
        );
        AssertEq(
            resolver.count_debuff_statuses(formalCountsFalseUnit),
            0,
            "typed counts_as_debuff=false 应继续覆盖内建 debuff 表。"
        );
    }

    private void TestStatusEffectFromDictRequiresExplicitStatusId()
    {
        GDictionary missingStatusIdPayload = BuildStatusEffectPayload();
        missingStatusIdPayload.Remove("status_id");
        AssertTrue(
            BattleStatusEffectState.from_dict(missingStatusIdPayload) == null,
            "状态效果反序列化应拒绝缺少 status_id 的字典。"
        );

        GDictionary emptyStatusIdPayload = BuildStatusEffectPayload();
        emptyStatusIdPayload["status_id"] = "";
        AssertTrue(
            BattleStatusEffectState.from_dict(emptyStatusIdPayload) == null,
            "状态效果反序列化应拒绝空 status_id。"
        );

        GDictionary nonStringStatusIdPayload = BuildStatusEffectPayload();
        nonStringStatusIdPayload["status_id"] = 12;
        AssertTrue(
            BattleStatusEffectState.from_dict(nonStringStatusIdPayload) == null,
            "状态效果反序列化应拒绝非 String/StringName 的 status_id。"
        );

        GDictionary stringNameStatusIdPayload = BuildStatusEffectPayload();
        stringNameStatusIdPayload["status_id"] = new StringName("slow");
        stringNameStatusIdPayload["source_unit_id"] = new StringName("source");
        BattleStatusEffectState stringNameStatusId = BattleStatusEffectState.from_dict(
            stringNameStatusIdPayload
        );
        AssertTrue(
            stringNameStatusId != null && stringNameStatusId.status_id == "slow",
            "状态效果反序列化应接受显式 StringName status_id。"
        );
    }

    private void TestLegacyStatusEffectMapKeysAreNotStatusIdFallbacks()
    {
        BattleUnitState unit = BuildUnit("legacy_status_map_unit", new Vector2I(1, 1), 2);
        GDictionary payload = unit.to_dict();
        payload["status_effects"] = new GDictionary
        {
            ["burning"] = new GDictionary
            {
                ["power"] = 2,
                ["stacks"] = 1,
                ["duration"] = 10,
            },
        };

        AssertTrue(
            BattleUnitState.from_dict(payload) == null,
            "缺 status_id 的旧状态 map shape 应拒绝整份单位 payload。"
        );
    }

    private void TestNonDictionaryStatusEffectEntriesAreRejected()
    {
        BattleUnitState unit = BuildUnit("non_dict_status_entry_unit", new Vector2I(1, 1), 2);
        GDictionary payload = unit.to_dict();
        payload["status_effects"] = new GDictionary { ["burning"] = "legacy_entry" };

        AssertTrue(
            BattleUnitState.from_dict(payload) == null,
            "非 Dictionary status effect entry 应拒绝整份单位 payload。"
        );
    }

    private void TestStatusEffectToDictFromDictRoundTripStillRestores()
    {
        var effect = new BattleStatusEffectState
        {
            status_id = "burning",
            source_unit_id = "round_trip_source",
            power = 3,
            @params = new GDictionary { ["damage_tag"] = "fire" },
            stacks = 2,
            duration = 20,
            tick_interval_tu = 10,
            next_tick_at_tu = 15,
            skip_next_turn_end_decay = true,
        };

        BattleStatusEffectState restoredEffect = BattleStatusEffectState.from_dict(effect.to_dict());
        AssertTrue(restoredEffect != null, "正式状态 effect to_dict/from_dict 应继续恢复对象。");
        AssertEq(restoredEffect != null ? restoredEffect.status_id : "", new StringName("burning"), "正式状态 effect round trip 应保留 status_id。");
        AssertEq(restoredEffect != null ? restoredEffect.source_unit_id : "", new StringName("round_trip_source"), "正式状态 effect round trip 应保留来源单位。");
        AssertEq(restoredEffect != null ? restoredEffect.power : -1, 3, "正式状态 effect round trip 应保留 power。");
        AssertEq(restoredEffect != null ? restoredEffect.stacks : -1, 2, "正式状态 effect round trip 应保留 stacks。");
        AssertEq(restoredEffect != null ? restoredEffect.duration : -1, 20, "正式状态 effect round trip 应保留 duration。");
        AssertEq(restoredEffect != null ? restoredEffect.tick_interval_tu : -1, 10, "正式状态 effect round trip 应保留 tick interval。");
        AssertEq(restoredEffect != null ? restoredEffect.next_tick_at_tu : -1, 15, "正式状态 effect round trip 应保留 next tick。");
        AssertTrue(restoredEffect != null && restoredEffect.skip_next_turn_end_decay, "正式状态 effect round trip 应保留 turn end decay 标记。");

        BattleUnitState unit = BuildUnit("status_round_trip_unit", new Vector2I(1, 1), 2);
        unit.set_status_effect(effect);
        BattleUnitState restoredUnit = BattleUnitState.from_dict(unit.to_dict());
        BattleStatusEffectState unitEffect = restoredUnit?.get_status_effect("burning");
        AssertTrue(unitEffect != null, "正式 BattleUnitState 状态字典 round trip 应继续恢复状态。");
        AssertEq(unitEffect != null ? unitEffect.status_id : "", new StringName("burning"), "正式 BattleUnitState 状态 round trip 应保留 status_id。");
        AssertEq(unitEffect != null ? unitEffect.stacks : -1, 2, "正式 BattleUnitState 状态 round trip 应保留 stacks。");
    }

    private static GDictionary BuildStatusEffectPayload() =>
        new()
        {
            ["status_id"] = "burning",
            ["source_unit_id"] = "source",
            ["power"] = 2,
            ["params"] = new GDictionary(),
            ["stacks"] = 1,
        };

    private static void ApplyStatus(
        BattleRuntimeModule runtime,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        StringName statusId,
        int durationTu,
        int power = 1,
        int tickIntervalTu = 0
    )
    {
        var effectDef = new CombatEffectDef
        {
            effect_type = "status",
            status_id = statusId,
            power = power,
        };
        if (durationTu > 0)
            effectDef.duration_tu = durationTu;
        if (tickIntervalTu > 0)
            effectDef.tick_interval_tu = tickIntervalTu;
        GDictionary result = runtime._damage_resolver.resolve_effects(
            sourceUnit,
            targetUnit,
            new GArray { effectDef }
        );
        runtime.mark_applied_statuses_for_turn_timing(
            targetUnit,
            DictStringNameArray(result, "status_effect_ids")
        );
    }

    private static void SetStatusParams(
        BattleUnitState unit,
        StringName statusId,
        GDictionary parameters
    )
    {
        var statusEffect = new BattleStatusEffectState
        {
            status_id = statusId,
            power = 1,
            stacks = 1,
            @params = parameters?.Duplicate(true) ?? new GDictionary(),
        };
        unit.set_status_effect(statusEffect);
    }

    private static void SetTypedStatusFields(
        BattleUnitState unit,
        StringName statusId,
        bool lockCounterattack = false,
        int mainSkillLockOtherDebuffCount = 0,
        bool countsAsDebuffOverride = false,
        bool countsAsDebuff = false
    )
    {
        var statusEffect = new BattleStatusEffectState
        {
            status_id = statusId,
            power = 1,
            stacks = 1,
            lock_counterattack = lockCounterattack,
            main_skill_lock_other_debuff_count = mainSkillLockOtherDebuffCount,
            counts_as_debuff_override = countsAsDebuffOverride,
            counts_as_debuff = countsAsDebuff,
        };
        unit.set_status_effect(statusEffect);
    }

    private static CombatEffectDef BuildDamageEffect(int power, StringName damageTag) =>
        new()
        {
            effect_type = "damage",
            power = power,
            damage_tag = damageTag,
            @params = new GDictionary(),
        };

    private static BattleRuntimeModule BuildRuntime()
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup(null, new GDictionary(), new GDictionary(), new GDictionary());
        return runtime;
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "status_effect_semantics",
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
        state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
        return state;
    }

    private static BattleCellState BuildCell(Vector2I coord)
    {
        var cell = new BattleCellState
        {
            coord = coord,
            base_terrain = BattleCellState.TERRAIN_LAND(),
            base_height = 4,
            passable = true,
        };
        cell.recalculate_runtime_values();
        return cell;
    }

    private static void AdvanceTimelineTu(BattleRuntimeModule runtime, BattleState state, int totalTu)
    {
        if (runtime == null || state == null || totalTu <= 0)
            return;
        state.phase = "timeline_running";
        state.active_unit_id = "";
        state.timeline.ready_unit_ids.Clear();
        state.timeline.tu_per_tick = 5;
        foreach (Variant unitOption in state.units.Values)
        {
            BattleUnitState unitState = unitOption.AsGodotObject() as BattleUnitState;
            if (unitState != null)
                unitState.action_threshold = 1000000;
        }
        runtime.advance(totalTu / 5);
    }

    private static BattleUnitState BuildUnit(StringName unitId, Vector2I coord, int currentAp)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            source_member_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
            current_ap = currentAp,
            current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN(),
            current_hp = 30,
            current_mp = 4,
            current_aura = 0,
            current_stamina = 4,
            is_alive = true,
        };
        unit.set_anchor_coord(coord);
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 30);
        unit.attribute_snapshot.set_value(AttributeService.MP_MAX_ID(), 4);
        unit.attribute_snapshot.set_value(AttributeService.STAMINA_MAX_ID(), 4);
        unit.attribute_snapshot.set_value(AttributeService.ACTION_POINTS_ID(), Math.Max(currentAp, 1));
        return unit;
    }

    private static void AddUnit(BattleRuntimeModule runtime, BattleState state, BattleUnitState unit)
    {
        state.units[unit.unit_id] = unit;
        runtime._grid_service.place_unit(state, unit, unit.coord, true);
    }

    private static BattleCommand BuildWaitCommand(StringName unitId) =>
        new()
        {
            command_type = BattleCommand.TYPE_WAIT(),
            unit_id = unitId,
        };

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsInt32();
    }

    private static GStringNameArray DictStringNameArray(GDictionary dictionary, string key)
    {
        var result = new GStringNameArray();
        if (dictionary == null || !dictionary.ContainsKey(key))
            return result;
        Variant value = dictionary[key];
        if (value.VariantType != Variant.Type.Array)
            return result;
        foreach (Variant entry in value.AsGodotArray())
        {
            StringName statusId = ProgressionDataUtils.to_string_name(entry);
            if (statusId != "")
                result.Add(statusId);
        }
        return result;
    }

    private static bool HasAttributeNamed(Type type, string attributeTypeName)
    {
        foreach (object attribute in type.GetCustomAttributes(false))
        {
            if (attribute.GetType().Name == attributeTypeName)
                return true;
        }
        return false;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertFalse(bool condition, string message)
    {
        AssertTrue(!condition, message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
            _failures.Add($"{message} actual={actual} expected={expected}");
    }
}
