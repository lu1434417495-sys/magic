using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_status_effect_semantics_regression : SceneTree
{
    private readonly TestHarness _test = new();
    private readonly List<BattleRuntimeModule> _ownedRuntimes = new();

    public override void _Initialize()
    {
        try
        {
            RunTests();
        }
        finally
        {
            DisposeOwnedRuntimes();
            GodotSharpCleanup.CollectPendingFinalizers();
        }

        Quit(_test.Finish("Status effect semantics regression"));
    }

    private void RunTests()
    {
        TestStatusSemanticTableExposesTypedSemantics();
        TestPhantasmalKillStatusSemantics();
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
        TestStatusLegacyParamsDurationTuIsNotUsedAsRuntimeDuration();
        TestStatusLegacyParamsTickIntervalTuIsNotUsedAsRuntimeTickInterval();
        TestMergedStatusCarriesTypedStackMetadata();
        TestSoulFractureSemantic();
        TestDamageResolverReadsOnlyFormalDamageStatusParams();
        TestSkillTurnStatusUsesTypedFieldsNotParams();
        TestStatusEffectFromDictRequiresExplicitStatusId();
        TestLegacyStatusEffectMapKeysAreNotStatusIdFallbacks();
        TestNonDictionaryStatusEffectEntriesAreRejected();
        TestStatusEffectToDictFromDictRoundTripStillRestores();
    }



    private void TestStatusSemanticTableExposesTypedSemantics()
    {
        _test.Eq(
            BattleStatusSemanticTable.GetSemantic("burning").TickMode,
            BattleStatusSemanticTable.TICK_TIMELINE_DAMAGE,
            "typed semantic 应暴露正式 tick mode。"
        );
    }

    private void TestSoulFractureSemantic()
    {
        BattleStatusSemantic semantic = BattleStatusSemanticTable.GetSemantic(
            BattleStatusSemanticTable.STATUS_SOUL_FRACTURE
        );

        _test.True(semantic.Defined, "soul_fracture should have a formal semantic.");
        _test.Eq(
            semantic.StackMode,
            BattleStatusSemanticTable.STACK_REFRESH,
            "soul_fracture should refresh."
        );
        _test.Eq(semantic.MaxStacks, 1, "soul_fracture max stack should be 1.");
        _test.Eq(
            semantic.TickMode,
            BattleStatusSemanticTable.TICK_NONE,
            "soul_fracture should not tick."
        );
        _test.Eq(semantic.DisplayLabel, "灵魂裂解", "soul_fracture should have player-facing label.");
        _test.True(
            BattleStatusSemanticTable.IsHarmfulStatus(BattleStatusSemanticTable.STATUS_SOUL_FRACTURE),
            "soul_fracture is harmful."
        );
        _test.True(
            BattleStatusSemanticTable.IsCleansableHarmfulStatus(
                BattleStatusSemanticTable.STATUS_SOUL_FRACTURE
            ),
            "soul_fracture is cleansable harmful."
        );
        _test.True(
            BattleStatusSemanticTable.IsDispellableHarmfulStatus(
                BattleStatusSemanticTable.STATUS_SOUL_FRACTURE
            ),
            "soul_fracture is dispellable harmful."
        );
    }

    private void TestPhantasmalKillStatusSemantics()
    {
        (StringName StatusId, string Label)[] cases =
        {
            ("aftershock", "余悸"),
            ("reaction_lock", "反应封锁"),
            ("frightened", "恐惧"),
            ("stunned", "震慑"),
        };

        foreach ((StringName statusId, string label) in cases)
        {
            BattleStatusSemantic semantic = BattleStatusSemanticTable.GetSemantic(statusId);
            _test.True(semantic.Defined, $"{statusId} should have a formal semantic.");
            _test.Eq(
                semantic.StackMode,
                BattleStatusSemanticTable.STACK_REFRESH,
                $"{statusId} should refresh rather than stack."
            );
            _test.Eq(semantic.MaxStacks, 1, $"{statusId} max stack should be 1.");
            _test.Eq(
                semantic.TickMode,
                BattleStatusSemanticTable.TICK_NONE,
                $"{statusId} should not tick."
            );
            _test.Eq(semantic.DisplayLabel, label, $"{statusId} should expose its display label.");
            _test.True(
                BattleStatusSemanticTable.IsHarmfulStatus(statusId),
                $"{statusId} should count as harmful."
            );
            _test.True(
                BattleStatusSemanticTable.IsCleansableHarmfulStatus(statusId),
                $"{statusId} should be cleansable harmful."
            );
            _test.True(
                BattleStatusSemanticTable.IsDispellableHarmfulStatus(statusId),
                $"{statusId} should be dispellable harmful by default."
            );
            var statusEntry = new BattleStatusEffectState
            {
                status_id = statusId,
                power = 1,
                stacks = 1,
            };
            _test.True(
                BattleStatusSemanticTable.IsDispellableHarmfulStatusEntry(statusEntry),
                $"{statusId} concrete state should be dispellable unless undispellable is set."
            );
            statusEntry.undispellable = true;
            _test.False(
                BattleStatusSemanticTable.IsDispellableHarmfulStatusEntry(statusEntry),
                $"{statusId} concrete state should honor undispellable."
            );

            BattleStatusEffectState merged = BattleStatusSemanticTable.MergeStatus(
                new CombatEffectDef
                {
                    effect_type = "status",
                    status_id = statusId,
                    power = 1,
                    duration_tu = 10,
                },
                "source_a"
            );
            merged = BattleStatusSemanticTable.MergeStatus(
                new CombatEffectDef
                {
                    effect_type = "status",
                    status_id = statusId,
                    power = 2,
                    duration_tu = 15,
                },
                "source_b",
                merged
            );
            _test.True(merged != null, $"{statusId} should merge into a status state.");
            _test.Eq(merged != null ? merged.stacks : -1, 1, $"{statusId} should keep one stack.");
            _test.Eq(
                merged != null ? merged.duration : -1,
                15,
                $"{statusId} should refresh to the longest duration."
            );
            _test.Eq(
                merged != null ? merged.tick_interval_tu : -1,
                0,
                $"{statusId} should not configure a tick interval."
            );
        }
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
        runtime.SetupStateForTests(state);

        ApplyStatus(runtime, striker, target, "staggered", 15);
        ApplyStatus(runtime, striker, target, "staggered", 15);
        BattleStatusEffectState staggerEntry = target.GetStatusEffect("staggered");
        _test.True(staggerEntry != null, "重复施加 staggered 后应保留正式状态。");
        _test.Eq(staggerEntry != null ? staggerEntry.stacks : -1, 1, "staggered 应按 refresh 语义而不是累加层数。");
        _test.Eq(staggerEntry != null ? staggerEntry.duration : -1, 15, "staggered 应记录剩余 TU。");

        state.phase = "timeline_running";
        state.active_unit_id = "";
        state.timeline.ready_unit_ids.Clear();
        state.timeline.ready_unit_ids.Add(target.unit_id);
        runtime.advance(0);
        _test.Eq(target.current_ap, 1, "staggered 刷新后仍只应在回合开始扣 1 点行动点。");

        BattleCommand waitCommand = BuildWaitCommand(target.unit_id);
        runtime.IssueCommand(waitCommand);
        _test.True(target.HasStatusEffect("staggered"), "staggered 不应在目标回合结束后被立即移除。");
        AdvanceTimelineTu(runtime, state, 15);
        _test.False(target.HasStatusEffect("staggered"), "staggered 应在 TU 走完后移除。");
    }

    private void TestMeteorConcussedSharesStaggeredApPenaltyGroup()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleUnitState target = BuildUnit("meteor_concussed_group_target", new Vector2I(1, 1), 3);
        SetStatusParams(target, "staggered", new GDictionary());
        SetStatusParams(target, "meteor_concussed", new GDictionary());
        BattleStatusEffectState meteorEntry = target.GetStatusEffect("meteor_concussed");
        if (meteorEntry != null)
        {
            meteorEntry.power = 2;
            target.SetStatusEffect(meteorEntry);
        }

        var batch = new BattleEventBatch();
        BattleStatusTickResult result = runtime._apply_turn_start_statuses_result(target, batch);
        _test.True(result.Changed, "meteor_concussed 参与回合开始结算后应报告 changed。");
        _test.Eq(target.current_ap, 1, "meteor_concussed 与 staggered 同组时应只扣最高 AP 惩罚，而不是叠加扣 3。");
        _test.False(target.HasStatusEffect("meteor_concussed"), "meteor_concussed 应在参与回合开始 AP 惩罚后消耗。");
        _test.True(target.HasStatusEffect("staggered"), "同组结算不应顺带消耗普通 staggered。");
        _test.Eq(batch.log_lines.Count, 1, "同组 AP 惩罚应只产生一条日志。");
        _test.Eq(
            BattleStatusSemanticTable.GetAttackRollPenalty(meteorEntry),
            2,
            "meteor_concussed 应提供 -2 攻击检定语义。"
        );
        _test.True(
            BattleStatusSemanticTable.IsHarmfulStatus("meteor_concussed"),
            "meteor_concussed 应计为有害状态。"
        );
        _test.True(
            BattleStatusSemanticTable.IsDispellableHarmfulStatus("meteor_concussed"),
            "meteor_concussed 应允许按有害魔法驱散。"
        );
    }

    private void TestMeteorConcussedConsumesWithoutZeroApLog()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleUnitState target = BuildUnit("meteor_concussed_zero_ap_target", new Vector2I(1, 1), 0);
        SetStatusParams(target, "meteor_concussed", new GDictionary());
        BattleStatusEffectState meteorEntry = target.GetStatusEffect("meteor_concussed");
        if (meteorEntry != null)
        {
            meteorEntry.power = 2;
            target.SetStatusEffect(meteorEntry);
        }

        var batch = new BattleEventBatch();
        BattleStatusTickResult result = runtime._apply_turn_start_statuses_result(target, batch);
        _test.True(result.Changed, "meteor_concussed 即使目标 AP 为 0，也应因状态消耗报告 changed。");
        _test.Eq(target.current_ap, 0, "AP 为 0 时 meteor_concussed 不应产生负 AP。");
        _test.False(target.HasStatusEffect("meteor_concussed"), "AP 为 0 时 meteor_concussed 仍应完成一次性消耗。");
        _test.Eq(batch.log_lines.Count, 0, "AP 为 0 时不应记录“少 AP”的误导日志。");
    }

    private void TestBurningStacksAndTicksOnTimelineInterval()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleState state = BuildState(new Vector2I(4, 3));
        BattleUnitState caster = BuildUnit("burning_source", new Vector2I(0, 1), 2);
        BattleUnitState target = BuildUnit("burning_target", new Vector2I(2, 1), 2);
        target.faction_id = "enemy";
        target.current_hp = 20;
        target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 20);

        AddUnit(runtime, state, caster);
        AddUnit(runtime, state, target);
        state.ally_unit_ids = new GStringNameArray { caster.unit_id };
        state.enemy_unit_ids = new GStringNameArray { target.unit_id };
        runtime.SetupStateForTests(state);

        ApplyStatus(runtime, caster, target, "burning", 20, 1, 10);
        ApplyStatus(runtime, caster, target, "burning", 20, 1, 10);
        BattleStatusEffectState burningEntry = target.GetStatusEffect("burning");
        _test.True(burningEntry != null, "burning 应在重复施加后存在于正式状态字典中。");
        _test.Eq(burningEntry != null ? burningEntry.stacks : -1, 2, "burning 应按 add 语义累加层数。");
        _test.Eq(burningEntry != null ? burningEntry.duration : -1, 20, "burning 应沿用施加时给定的剩余 TU。");
        _test.Eq(burningEntry != null ? burningEntry.tick_interval_tu : -1, 10, "burning 应记录正式周期 tick 间隔。");

        state.phase = "timeline_running";
        state.active_unit_id = "";
        state.timeline.ready_unit_ids.Clear();
        state.timeline.ready_unit_ids.Add(target.unit_id);
        runtime.advance(0);
        _test.Eq(target.current_hp, 20, "burning 不应在回合开始隐式结算伤害。");
        runtime.IssueCommand(BuildWaitCommand(target.unit_id));
        burningEntry = target.GetStatusEffect("burning");
        _test.Eq(burningEntry != null ? burningEntry.duration : -1, 20, "burning 不应在回合结束后递减 TU。");

        AdvanceTimelineTu(runtime, state, 10);
        burningEntry = target.GetStatusEffect("burning");
        _test.Eq(burningEntry != null ? burningEntry.duration : -1, 10, "burning 应随时间轴推进递减剩余 TU。");
        _test.Eq(target.current_hp, 18, "2 层 burning 应在第一个周期 tick 结算 2 点灼烧伤害。");

        state.phase = "timeline_running";
        state.active_unit_id = "";
        state.timeline.ready_unit_ids.Clear();
        state.timeline.ready_unit_ids.Add(target.unit_id);
        runtime.advance(0);
        _test.Eq(target.current_hp, 18, "burning 不应因进入第二个行动窗口额外结算伤害。");
        runtime.IssueCommand(BuildWaitCommand(target.unit_id));
        _test.True(target.HasStatusEffect("burning"), "burning 不应在第二个回合结束时被 turn end 提前清除。");
        AdvanceTimelineTu(runtime, state, 10);
        _test.False(target.HasStatusEffect("burning"), "burning 到期后应按 TU 正式移除。");
        _test.Eq(target.current_hp, 16, "2 层 burning 应在到期边界完成第二个周期 tick。");
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
        runtime.SetupStateForTests(state);

        ApplyStatus(runtime, caster, target, "burning", 5, 1, 10);
        AdvanceTimelineTu(runtime, state, 5);
        _test.False(target.HasStatusEffect("burning"), "短于 tick 间隔的 burning 应按 TU 到期。");
        _test.Eq(target.current_hp, 20, "短于 tick 间隔的 burning 不应保证至少触发一次伤害。");
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
        runtime.SetupStateForTests(state);

        ApplyStatus(runtime, source, target, "slow", 15);
        state.phase = "timeline_running";
        state.active_unit_id = "";
        state.timeline.ready_unit_ids.Clear();
        state.timeline.ready_unit_ids.Add(target.unit_id);
        runtime.advance(0);
        _test.True(target.HasStatusEffect("slow"), "slow 应在受影响单位回合开始后仍保持生效。");

        var moveCommand = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Move),
            unit_id = target.unit_id,
            target_coord = new Vector2I(2, 1),
        };
        BattlePreview preview = runtime.PreviewCommand(moveCommand);
        _test.True(preview != null && preview.allowed, "slow 状态下的相邻移动仍应合法。");

        runtime.IssueCommand(moveCommand);
        _test.Eq(target.current_move_points, 0, "移动成功后应耗尽本回合移动力，即使只移动 1 格。");
        _test.Eq(target.current_ap, 3, "slow 只应抬高移动行动点消耗，不应继续扣除 AP。");
        runtime.IssueCommand(BuildWaitCommand(target.unit_id));
        _test.True(target.HasStatusEffect("slow"), "slow 不应在目标回合结束后按 turn end 立刻移除。");
        AdvanceTimelineTu(runtime, state, 15);
        _test.False(target.HasStatusEffect("slow"), "slow 应在 TU 走完后移除。");
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

            _test.True(BattleStatusSemanticTable.HasSemantic(statusId), $"{label} 应注册正式状态语义。");
            BattleStatusEffectState merged = BattleStatusSemanticTable.MergeStatus(firstEffect, "source_a");
            merged = BattleStatusSemanticTable.MergeStatus(secondEffect, "source_b", merged);
            _test.True(merged != null, $"{label} 合并后应生成正式状态。");
            _test.Eq(merged != null ? merged.stacks : -1, 1, $"{label} 应按 refresh 语义保持单层。");
            _test.Eq(merged != null ? merged.power : -1, 2, $"{label} 应保留更高 power。");
            _test.Eq(merged != null ? merged.duration : -1, 15, $"{label} 应保留更长的剩余 TU。");
            _test.Eq(
                BattleStatusSemanticTable.GetTurnStartApPenalty(merged),
                0,
                $"{label} 不应附带 turn start AP penalty 语义。"
            );
            _test.Eq(
                BattleStatusSemanticTable.GetTurnStartDamage(merged),
                0,
                $"{label} 不应附带 turn start damage 语义。"
            );
            _test.Eq(
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
        runtime.SetupStateForTests(state);

        ApplyStatus(runtime, source, target, "taunted", 15);
        BattleStatusEffectState tauntedEntry = target.GetStatusEffect("taunted");
        _test.True(tauntedEntry != null, "taunted 应写入正式状态字典。");
        _test.Eq(tauntedEntry != null ? tauntedEntry.duration : -1, 15, "taunted 应记录施加时的剩余 TU。");

        state.phase = "timeline_running";
        state.active_unit_id = "";
        state.timeline.ready_unit_ids.Clear();
        state.timeline.ready_unit_ids.Add(target.unit_id);
        runtime.advance(0);
        runtime.IssueCommand(BuildWaitCommand(target.unit_id));
        _test.True(target.HasStatusEffect("taunted"), "taunted 不应在目标回合结束后被 turn end 提前移除。");

        AdvanceTimelineTu(runtime, state, 15);
        _test.False(target.HasStatusEffect("taunted"), "taunted 应在 TU 走完后移除。");
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
        _test.True(merged != null, "状态效果应能在缺少 duration_tu 时正常合并。");
        _test.True(merged != null && !merged.HasDuration(), "缺少来源时长时，状态不应再从语义表回填默认 TU。");
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
        _test.True(merged != null, "旧 params.duration 不应阻止状态对象合并。");
        _test.True(merged != null && !merged.HasDuration(), "旧 params.duration 不应再恢复为状态剩余 TU。");
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
        _test.True(merged != null, "正式 duration_tu 应继续生成状态对象。");
        _test.Eq(merged != null ? merged.duration : -1, 20, "正式 duration_tu 应生效，旧 params.duration 不应覆盖。");
    }

    private void TestStatusLegacyParamsDurationTuIsNotUsedAsRuntimeDuration()
    {
        var effectDef = new CombatEffectDef
        {
            effect_type = "status",
            status_id = "pinned",
            power = 1,
            @params = new GDictionary { ["duration_tu"] = 20 },
        };

        BattleStatusEffectState merged = BattleStatusSemanticTable.MergeStatus(effectDef, "source_unit");
        _test.True(merged != null, "旧 params.duration_tu 不应阻止状态对象合并。");
        _test.True(
            merged != null && !merged.HasDuration(),
            "旧 params.duration_tu 不应继续驱动正式状态剩余 TU。"
        );
    }

    private void TestStatusLegacyParamsTickIntervalTuIsNotUsedAsRuntimeTickInterval()
    {
        var effectDef = new CombatEffectDef
        {
            effect_type = "status",
            status_id = "burning",
            power = 1,
            duration_tu = 20,
            @params = new GDictionary { ["tick_interval_tu"] = 10 },
        };

        BattleStatusEffectState merged = BattleStatusSemanticTable.MergeStatus(effectDef, "source_unit");
        _test.True(merged != null, "旧 params.tick_interval_tu 不应阻止状态对象合并。");
        _test.Eq(
            merged != null ? merged.tick_interval_tu : -1,
            0,
            "旧 params.tick_interval_tu 不应继续驱动正式周期 tick 间隔。"
        );
    }

    private void TestMergedStatusCarriesTypedStackMetadata()
    {
        var semanticEffect = new CombatEffectDef
        {
            effect_type = "status",
            status_id = "burning",
            power = 1,
            duration_tu = 20,
        };

        BattleStatusEffectState semanticMerged = BattleStatusSemanticTable.MergeStatus(
            semanticEffect,
            "source_unit"
        );
        _test.True(semanticMerged != null, "语义状态应能正常合并。");
        _test.Eq(
            semanticMerged != null ? semanticMerged.stack_behavior : "",
            new StringName("add"),
            "有正式 semantic 的状态应把生效 stack_behavior 写入 typed 字段。"
        );
        _test.Eq(
            semanticMerged != null ? semanticMerged.stack_limit : -1,
            3,
            "有正式 semantic 的状态应把生效 stack_limit 写入 typed 字段。"
        );

        var genericEffect = new CombatEffectDef
        {
            effect_type = "status",
            status_id = "custom_stack_status",
            power = 1,
            stack_behavior = "refresh",
            stack_limit = 7,
        };

        BattleStatusEffectState genericMerged = BattleStatusSemanticTable.MergeStatus(
            genericEffect,
            "source_unit"
        );
        _test.True(genericMerged != null, "无正式 semantic 的状态也应能正常合并。");
        _test.Eq(
            genericMerged != null ? genericMerged.stack_behavior : "",
            new StringName("refresh"),
            "无正式 semantic 的状态应保留 CombatEffectDef.stack_behavior。"
        );
        _test.Eq(
            genericMerged != null ? genericMerged.stack_limit : -1,
            7,
            "无正式 semantic 的状态应保留 CombatEffectDef.stack_limit。"
        );
    }

    private void TestDamageResolverReadsOnlyFormalDamageStatusParams()
    {
        BattleRuntimeModule runtime = BuildRuntime();
        BattleUnitState source = BuildUnit("damage_alias_source", Vector2I.Zero, 2);
        CombatEffectDef physicalEffect = BuildDamageEffect(10, "physical_slash");

        BattleUnitState formalTagTarget = BuildUnit("formal_damage_tag_target", Vector2I.Zero, 2);
        SetTypedStatusFields(
            formalTagTarget,
            "formal_fire_barrier",
            contentDr: 4,
            damageTag: "fire"
        );
        GDictionary formalTagResult = AttackEffectResolutionResultReader.BuildGodotPayload(runtime._damage_resolver.ResolveEffects(
            source,
            formalTagTarget,
            new GArray { physicalEffect }
        ));
        _test.Eq(DictInt(formalTagResult, "damage", -1), 10, "正式 damage_tag 不匹配时不应套用 mitigation_tier。");

        BattleUnitState legacyFormalTagTarget = BuildUnit("legacy_formal_tag_target", Vector2I.Zero, 2);
        legacyFormalTagTarget.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "legacy_formal_fire_barrier",
                power = 1,
                stacks = 1,
                content_dr = 4,
                @params = new GDictionary { ["damage_tag"] = "fire" },
            }
        );
        GDictionary legacyFormalTagResult = AttackEffectResolutionResultReader.BuildGodotPayload(runtime._damage_resolver.ResolveEffects(
            source,
            legacyFormalTagTarget,
            new GArray { physicalEffect }
        ));
        _test.Eq(
            DictInt(legacyFormalTagResult, "damage", -1),
            6,
            "旧 params.damage_tag 不应继续驱动正式 mitigation damage filter。"
        );

        BattleUnitState legacyTagTarget = BuildUnit("legacy_tag_target", Vector2I.Zero, 2);
        SetStatusParams(
            legacyTagTarget,
            "legacy_fire_barrier",
            new GDictionary { ["tag"] = "fire" }
        );
        legacyTagTarget.GetStatusEffect("legacy_fire_barrier").content_dr = 4;
        GDictionary legacyTagResult = AttackEffectResolutionResultReader.BuildGodotPayload(runtime._damage_resolver.ResolveEffects(
            source,
            legacyTagTarget,
            new GArray { physicalEffect }
        ));
        _test.Eq(DictInt(legacyTagResult, "damage", -1), 6, "旧 params.tag 不应再被当作 damage_tag 过滤。");

        CombatEffectDef formalBypassEffect = BuildDamageEffect(10, "physical_slash");
        formalBypassEffect.dr_bypass_tag = "armor_pierce";
        BattleUnitState formalBypassTarget = BuildUnit("formal_bypass_target", Vector2I.Zero, 2);
        SetTypedStatusFields(
            formalBypassTarget,
            "formal_content_dr",
            contentDr: 4,
            drBypassTag: "armor_pierce"
        );
        GDictionary formalBypassResult = AttackEffectResolutionResultReader.BuildGodotPayload(runtime._damage_resolver.ResolveEffects(
            source,
            formalBypassTarget,
            new GArray { formalBypassEffect }
        ));
        _test.Eq(DictInt(formalBypassResult, "damage", -1), 10, "正式 dr_bypass_tag 匹配时应绕过 content_dr。");

        CombatEffectDef legacyEffectBypass = BuildDamageEffect(10, "physical_slash");
        legacyEffectBypass.@params["bypass_tag"] = "armor_pierce";
        BattleUnitState legacyEffectBypassTarget = BuildUnit("legacy_effect_bypass_target", Vector2I.Zero, 2);
        SetStatusParams(
            legacyEffectBypassTarget,
            "formal_content_dr",
            new GDictionary { ["content_dr"] = 4, ["dr_bypass_tag"] = "armor_pierce" }
        );
        GDictionary legacyEffectBypassResult = AttackEffectResolutionResultReader.BuildGodotPayload(runtime._damage_resolver.ResolveEffects(
            source,
            legacyEffectBypassTarget,
            new GArray { legacyEffectBypass }
        ));
        _test.Eq(DictInt(legacyEffectBypassResult, "damage", -1), 10, "旧 status params.content_dr 不应继续驱动正式固定减伤。");

        BattleUnitState legacyStatusBypassTarget = BuildUnit("legacy_status_bypass_target", Vector2I.Zero, 2);
        SetStatusParams(
            legacyStatusBypassTarget,
            "legacy_content_dr",
            new GDictionary { ["content_dr"] = 4, ["bypass_tag"] = "armor_pierce" }
        );
        GDictionary legacyStatusBypassResult = AttackEffectResolutionResultReader.BuildGodotPayload(runtime._damage_resolver.ResolveEffects(
            source,
            legacyStatusBypassTarget,
            new GArray { formalBypassEffect }
        ));
        _test.Eq(DictInt(legacyStatusBypassResult, "damage", -1), 10, "旧 status params.content_dr/bypass_tag 不应再被当作正式减伤契约。");

        BattleUnitState formalPassiveTarget = BuildUnit("formal_passive_target", Vector2I.Zero, 2);
        SetTypedStatusFields(formalPassiveTarget, "formal_vajra", passiveReduction: 3);
        GDictionary formalPassiveResult = AttackEffectResolutionResultReader.BuildGodotPayload(runtime._damage_resolver.ResolveEffects(
            source,
            formalPassiveTarget,
            new GArray { physicalEffect }
        ));
        _test.Eq(DictInt(formalPassiveResult, "damage", -1), 7, "正式 passive_reduction 字段应减少固定伤害。");

        BattleUnitState legacyPassiveTarget = BuildUnit("legacy_passive_target", Vector2I.Zero, 2);
        SetStatusParams(legacyPassiveTarget, "legacy_vajra", new GDictionary { ["passive_reduction"] = 3 });
        GDictionary legacyPassiveResult = AttackEffectResolutionResultReader.BuildGodotPayload(runtime._damage_resolver.ResolveEffects(
            source,
            legacyPassiveTarget,
            new GArray { physicalEffect }
        ));
        _test.Eq(DictInt(legacyPassiveResult, "damage", -1), 10, "旧 status params.passive_reduction 不应继续驱动正式减伤。");

        CombatEffectDef formalLowHpEffect = BuildDamageEffect(10, "physical_slash");
        formalLowHpEffect.bonus_condition = "target_low_hp";
        formalLowHpEffect.hp_ratio_threshold_percent = 70;
        formalLowHpEffect.bonus_damage_dice_count = 4;
        formalLowHpEffect.bonus_damage_dice_sides = 1;
        BattleUnitState formalLowHpTarget = BuildUnit("formal_low_hp_target", Vector2I.Zero, 2);
        formalLowHpTarget.current_hp = 18;
        GDictionary formalLowHpResult = AttackEffectResolutionResultReader.BuildGodotPayload(runtime._damage_resolver.ResolveEffects(
            source,
            formalLowHpTarget,
            new GArray { formalLowHpEffect }
        ));
        _test.Eq(DictInt(formalLowHpResult, "damage", -1), 14, "正式 hp_ratio_threshold_percent 应控制低血追加伤害骰阈值。");
        BattleUnitState formalLowHpCritTarget = BuildUnit("formal_low_hp_crit_target", Vector2I.Zero, 2);
        formalLowHpCritTarget.current_hp = 18;
        GDictionary formalLowHpCritResult = AttackEffectResolutionResultReader.BuildGodotPayload(runtime._damage_resolver.ResolveEffects(
            source,
            formalLowHpCritTarget,
            new GArray { formalLowHpEffect },
            new GDictionary { ["critical_hit"] = true }
        ));
        _test.Eq(DictInt(formalLowHpCritResult, "damage", -1), 18, "低血暴击应额外掷一组处决追加骰。");

        CombatEffectDef legacyLowHpEffect = BuildDamageEffect(10, "physical_slash");
        legacyLowHpEffect.bonus_condition = "target_low_hp";
        legacyLowHpEffect.@params["low_hp_ratio"] = 0.7;
        legacyLowHpEffect.@params["bonus_damage_dice_count"] = 4;
        legacyLowHpEffect.@params["bonus_damage_dice_sides"] = 1;
        BattleUnitState legacyLowHpTarget = BuildUnit("legacy_low_hp_target", Vector2I.Zero, 2);
        legacyLowHpTarget.current_hp = 18;
        GDictionary legacyLowHpResult = AttackEffectResolutionResultReader.BuildGodotPayload(runtime._damage_resolver.ResolveEffects(
            source,
            legacyLowHpTarget,
            new GArray { legacyLowHpEffect }
        ));
        _test.Eq(DictInt(legacyLowHpResult, "damage", -1), 10, "旧 params.low_hp_ratio 不应再覆盖默认低血阈值或触发追加骰。");
    }

    private void TestSkillTurnStatusUsesTypedFieldsNotParams()
    {
        var resolver = new BattleRuntimeSkillTurnResolver();

        BattleUnitState legacyBoolUnit = BuildUnit("legacy_bool_param_unit", Vector2I.Zero, 2);
        SetStatusParams(legacyBoolUnit, "legacy_counter_lock", new GDictionary { ["lock_counterattack"] = true });
        _test.False(
            resolver.HasCounterattackLockStatus(legacyBoolUnit),
            "status params.lock_counterattack 不应再驱动反击锁。"
        );

        BattleUnitState formalBoolUnit = BuildUnit("formal_bool_param_unit", Vector2I.Zero, 2);
        SetTypedStatusFields(formalBoolUnit, "formal_counter_lock", lockCounterattack: true);
        _test.True(
            resolver.HasCounterattackLockStatus(formalBoolUnit),
            "typed lock_counterattack 字段应驱动反击锁。"
        );

        BattleUnitState legacyIntUnit = BuildUnit("legacy_int_param_unit", Vector2I.Zero, 2);
        SetStatusParams(
            legacyIntUnit,
            "legacy_main_skill_lock",
            new GDictionary { ["main_skill_lock_other_debuff_count"] = 2 }
        );
        _test.Eq(
            resolver.GetMainSkillLockOtherDebuffCount(legacyIntUnit),
            0,
            "status params.main_skill_lock_other_debuff_count 不应再驱动主技能锁。"
        );

        BattleUnitState formalIntUnit = BuildUnit("formal_int_param_unit", Vector2I.Zero, 2);
        SetTypedStatusFields(formalIntUnit, "formal_main_skill_lock", mainSkillLockOtherDebuffCount: 2);
        _test.Eq(
            resolver.GetMainSkillLockOtherDebuffCount(formalIntUnit),
            2,
            "typed main_skill_lock_other_debuff_count 字段应驱动主技能锁。"
        );

        BattleUnitState legacyCountsTrueUnit = BuildUnit("legacy_counts_true_unit", Vector2I.Zero, 2);
        SetStatusParams(legacyCountsTrueUnit, "custom_bad_debuff", new GDictionary { ["counts_as_debuff"] = true });
        _test.Eq(
            resolver.CountDebuffStatuses(legacyCountsTrueUnit),
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
        _test.Eq(
            resolver.CountDebuffStatuses(formalCountsTrueUnit),
            1,
            "typed counts_as_debuff=true 应继续把自定义状态计为 debuff。"
        );

        BattleUnitState legacyCountsFalseUnit = BuildUnit("legacy_counts_false_unit", Vector2I.Zero, 2);
        SetStatusParams(legacyCountsFalseUnit, "burning", new GDictionary { ["counts_as_debuff"] = false });
        _test.Eq(
            resolver.CountDebuffStatuses(legacyCountsFalseUnit),
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
        _test.Eq(
            resolver.CountDebuffStatuses(formalCountsFalseUnit),
            0,
            "typed counts_as_debuff=false 应继续覆盖内建 debuff 表。"
        );
    }

    private void TestStatusEffectFromDictRequiresExplicitStatusId()
    {
        GDictionary missingStatusIdPayload = BuildStatusEffectPayload();
        missingStatusIdPayload.Remove("status_id");
        _test.True(
            BattleStatusEffectState.FromDictionary(missingStatusIdPayload) == null,
            "状态效果反序列化应拒绝缺少 status_id 的字典。"
        );

        GDictionary emptyStatusIdPayload = BuildStatusEffectPayload();
        emptyStatusIdPayload["status_id"] = "";
        _test.True(
            BattleStatusEffectState.FromDictionary(emptyStatusIdPayload) == null,
            "状态效果反序列化应拒绝空 status_id。"
        );

        GDictionary nonStringStatusIdPayload = BuildStatusEffectPayload();
        nonStringStatusIdPayload["status_id"] = 12;
        _test.True(
            BattleStatusEffectState.FromDictionary(nonStringStatusIdPayload) == null,
            "状态效果反序列化应拒绝非 String/StringName 的 status_id。"
        );

        GDictionary stringNameStatusIdPayload = BuildStatusEffectPayload();
        stringNameStatusIdPayload["status_id"] = new StringName("slow");
        stringNameStatusIdPayload["source_unit_id"] = new StringName("source");
        BattleStatusEffectState stringNameStatusId = BattleStatusEffectState.FromDictionary(
            stringNameStatusIdPayload
        );
        _test.True(
            stringNameStatusId != null && stringNameStatusId.status_id == "slow",
            "状态效果反序列化应接受显式 StringName status_id。"
        );
    }

    private void TestLegacyStatusEffectMapKeysAreNotStatusIdFallbacks()
    {
        BattleUnitState unit = BuildUnit("legacy_status_map_unit", new Vector2I(1, 1), 2);
        GDictionary payload = unit.ToDictionary();
        payload["status_effects"] = new GDictionary
        {
            ["burning"] = new GDictionary
            {
                ["power"] = 2,
                ["stacks"] = 1,
                ["duration"] = 10,
            },
        };

        _test.True(
            BattleUnitState.FromDictionary(payload) == null,
            "缺 status_id 的旧状态 map shape 应拒绝整份单位 payload。"
        );
    }

    private void TestNonDictionaryStatusEffectEntriesAreRejected()
    {
        BattleUnitState unit = BuildUnit("non_dict_status_entry_unit", new Vector2I(1, 1), 2);
        GDictionary payload = unit.ToDictionary();
        payload["status_effects"] = new GDictionary { ["burning"] = "legacy_entry" };

        _test.True(
            BattleUnitState.FromDictionary(payload) == null,
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

        BattleStatusEffectState restoredEffect = BattleStatusEffectState.FromDictionary(effect.ToDictionary());
        _test.True(restoredEffect != null, "正式状态 effect to_dict/from_dict 应继续恢复对象。");
        _test.Eq(restoredEffect != null ? restoredEffect.status_id : "", new StringName("burning"), "正式状态 effect round trip 应保留 status_id。");
        _test.Eq(restoredEffect != null ? restoredEffect.source_unit_id : "", new StringName("round_trip_source"), "正式状态 effect round trip 应保留来源单位。");
        _test.Eq(restoredEffect != null ? restoredEffect.power : -1, 3, "正式状态 effect round trip 应保留 power。");
        _test.Eq(restoredEffect != null ? restoredEffect.stacks : -1, 2, "正式状态 effect round trip 应保留 stacks。");
        _test.Eq(restoredEffect != null ? restoredEffect.duration : -1, 20, "正式状态 effect round trip 应保留 duration。");
        _test.Eq(restoredEffect != null ? restoredEffect.tick_interval_tu : -1, 10, "正式状态 effect round trip 应保留 tick interval。");
        _test.Eq(restoredEffect != null ? restoredEffect.next_tick_at_tu : -1, 15, "正式状态 effect round trip 应保留 next tick。");
        _test.True(restoredEffect != null && restoredEffect.skip_next_turn_end_decay, "正式状态 effect round trip 应保留 turn end decay 标记。");

        BattleUnitState unit = BuildUnit("status_round_trip_unit", new Vector2I(1, 1), 2);
        unit.SetStatusEffect(effect);
        BattleUnitState restoredUnit = BattleUnitState.FromDictionary(unit.ToDictionary());
        BattleStatusEffectState unitEffect = restoredUnit?.GetStatusEffect("burning");
        _test.True(unitEffect != null, "正式 BattleUnitState 状态字典 round trip 应继续恢复状态。");
        _test.Eq(unitEffect != null ? unitEffect.status_id : "", new StringName("burning"), "正式 BattleUnitState 状态 round trip 应保留 status_id。");
        _test.Eq(unitEffect != null ? unitEffect.stacks : -1, 2, "正式 BattleUnitState 状态 round trip 应保留 stacks。");
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
        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(runtime._damage_resolver.ResolveEffects(
            sourceUnit,
            targetUnit,
            new GArray { effectDef }
        ));
        runtime.MarkAppliedStatusesForTurnTiming(
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
        unit.SetStatusEffect(statusEffect);
    }

    private static void SetTypedStatusFields(
        BattleUnitState unit,
        StringName statusId,
        bool lockCounterattack = false,
        int mainSkillLockOtherDebuffCount = 0,
        bool countsAsDebuffOverride = false,
        bool countsAsDebuff = false,
        StringName damageTag = default,
        IEnumerable<StringName> damageTags = null,
        StringName damageCategory = default,
        StringName mitigationTier = default,
        StringName drBypassTag = default,
        int passiveReduction = 0,
        int contentDr = 0,
        int guardBlock = 0
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
            damage_tag = damageTag,
            damage_tags = damageTags != null ? new List<StringName>(damageTags) : new List<StringName>(),
            damage_category = damageCategory,
            mitigation_tier = mitigationTier,
            dr_bypass_tag = drBypassTag,
            passive_reduction = passiveReduction,
            content_dr = contentDr,
            guard_block = guardBlock,
        };
        unit.SetStatusEffect(statusEffect);
    }

    private static CombatEffectDef BuildDamageEffect(int power, StringName damageTag) =>
        new()
        {
            effect_type = "damage",
            power = power,
            damage_tag = damageTag,
            @params = new GDictionary(),
        };

    private BattleRuntimeModule BuildRuntime()
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup();
        _ownedRuntimes.Add(runtime);
        return runtime;
    }

    private void DisposeOwnedRuntimes()
    {
        foreach (BattleRuntimeModule runtime in _ownedRuntimes)
            runtime?.Dispose();
        _ownedRuntimes.Clear();
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "status_effect_semantics",
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
            passable = true,
        };
        cell.RecalculateRuntimeValues();
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
        foreach (Variant unitOption in state.Units())
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
            current_move_points = BattleUnitState.DefaultMovePointsPerTurn,
            current_hp = 30,
            current_mp = 4,
            current_aura = 0,
            current_stamina = 4,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 30);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 4);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 4);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), Math.Max(currentAp, 1));
        return unit;
    }

    private static void AddUnit(BattleRuntimeModule runtime, BattleState state, BattleUnitState unit)
    {
        state.SetUnit(unit);
        runtime._grid_service.PlaceUnit(state, unit, unit.coord, true);
    }

    private static BattleCommand BuildWaitCommand(StringName unitId) =>
        new()
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Wait),
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


}
