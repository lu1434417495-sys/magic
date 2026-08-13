using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_status_source_contribution_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            TestSameSourceCapsAndDifferentSourcesStackIndependently();
            TestSourceDurationsAdvanceIndependently();
            TestRuntimeTicksSourcesIndependentlyAfterSourceDeath();
            TestSnapshotRoundTripPreservesDetachedContributions();
            TestAggregateEraseRemovesEveryContribution();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Battle status source contribution regression"));
    }

    private void TestSameSourceCapsAndDifferentSourcesStackIndependently()
    {
        BattleStatusSemantic semantic = BattleStatusSemanticTable.GetSemantic("burning");
        _test.Eq(
            semantic.StackingScope,
            BattleStatusStackingScope.SourceDefinition,
            "燃烧必须按来源单位与来源定义分桶。"
        );

        CombatEffectDefinition effect = BuildBurningEffect(power: 1, durationTu: 20);
        BattleStatusSourceIdentity sourceSkillA = BattleStatusSourceIdentity.Skill(
            "caster_a",
            "skill_a"
        );
        BattleStatusEffectState merged = null;
        for (int index = 0; index < 4; index++)
        {
            merged = BattleStatusSemanticTable.MergeStatus(
                effect,
                "caster_a",
                merged,
                "burning",
                sourceSkillA
            );
        }

        _test.True(merged != null, "同源燃烧应生成正式状态。" );
        BattleStatusSourceContributionState contributionA =
            merged?.GetSourceContributionTyped(sourceSkillA);
        _test.Eq(contributionA?.Stacks ?? -1, 3, "同一单位同一技能最多3层。" );
        _test.Eq(
            merged?.GetSourceContributionsTyped().Count ?? -1,
            1,
            "重复施放不得伪造新的来源桶。"
        );

        BattleStatusSourceIdentity sourceSkillB = BattleStatusSourceIdentity.Skill(
            "caster_a",
            "skill_b"
        );
        merged = BattleStatusSemanticTable.MergeStatus(
            BuildBurningEffect(power: 1, durationTu: 30),
            "caster_a",
            merged,
            "burning",
            sourceSkillB
        );
        BattleStatusSourceIdentity otherCasterSkillA = BattleStatusSourceIdentity.Skill(
            "caster_b",
            "skill_a"
        );
        merged = BattleStatusSemanticTable.MergeStatus(
            BuildBurningEffect(power: 1, durationTu: 40),
            "caster_b",
            merged,
            "burning",
            otherCasterSkillA
        );

        _test.Eq(
            merged?.GetSourceContributionsTyped().Count ?? -1,
            3,
            "同单位不同技能、不同单位同技能都必须成为独立来源。"
        );
        _test.Eq(merged?.stacks ?? -1, 5, "聚合层数应为全部来源层数之和。" );
        _test.Eq(
            BattleStatusSemanticTable.GetTimelineTickDamage(merged),
            5,
            "一次时间轴结算应汇总三个来源各自的边际伤害。"
        );
    }

    private void TestSourceDurationsAdvanceIndependently()
    {
        BattleStatusSourceIdentity shortSource = BattleStatusSourceIdentity.Skill(
            "caster_a",
            "short_burn"
        );
        BattleStatusSourceIdentity longSource = BattleStatusSourceIdentity.Skill(
            "caster_b",
            "long_burn"
        );
        BattleStatusEffectState merged = BattleStatusSemanticTable.MergeStatus(
            BuildBurningEffect(power: 1, durationTu: 20),
            "caster_a",
            null,
            "burning",
            shortSource
        );
        merged = BattleStatusSemanticTable.MergeStatus(
            BuildBurningEffect(power: 2, durationTu: 40),
            "caster_b",
            merged,
            "burning",
            longSource
        );

        BattleStatusDurationAdvanceResult advance =
            BattleStatusSemanticTable.AdvanceTimelineDurationResult(merged, 20);
        _test.False(advance.Expired, "短来源结束时，仍有长来源就不能移除聚合状态。" );
        _test.True(advance.Changed, "独立来源时长变化必须报告 changed。" );
        _test.True(
            merged.GetSourceContributionTyped(shortSource) == null,
            "20TU 来源必须独立到期。"
        );
        _test.Eq(
            merged.GetSourceContributionTyped(longSource)?.DurationTu ?? -1,
            20,
            "40TU 来源经过20TU后应保留20TU。"
        );
        _test.Eq(merged.GetSourceContributionsTyped().Count, 1, "只应保留尚未到期的来源。" );
    }

    private void TestSnapshotRoundTripPreservesDetachedContributions()
    {
        BattleStatusSourceIdentity sourceA = BattleStatusSourceIdentity.Skill(
            "caster_a",
            "skill_a"
        );
        BattleStatusSourceIdentity sourceB = BattleStatusSourceIdentity.Skill(
            "caster_b",
            "skill_a"
        );
        BattleStatusEffectState original = BattleStatusSemanticTable.MergeStatus(
            BuildBurningEffect(power: 1, durationTu: 20),
            "caster_a",
            null,
            "burning",
            sourceA
        );
        original = BattleStatusSemanticTable.MergeStatus(
            BuildBurningEffect(power: 2, durationTu: 30),
            "caster_b",
            original,
            "burning",
            sourceB
        );

        using GodotProjectionLease<GDictionary> lease = original.ToDictionaryLease();
        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(lease.Value);
        _test.True(restored != null, "含来源贡献的当前快照必须可严格恢复。" );
        _test.Eq(
            restored?.GetSourceContributionsTyped().Count ?? -1,
            2,
            "快照往返必须保留两个来源。"
        );
        _test.Eq(
            restored?.GetSourceContributionTyped(sourceB)?.Power ?? -1,
            2,
            "快照往返必须保留每来源强度。"
        );

        original.GetSourceContributionTyped(sourceA).Stacks = 3;
        original.RebuildSourceContributionAggregateTyped();
        _test.Eq(
            restored?.GetSourceContributionTyped(sourceA)?.Stacks ?? -1,
            1,
            "恢复结果必须与原对象完全脱离。"
        );
    }

    private void TestRuntimeTicksSourcesIndependentlyAfterSourceDeath()
    {
        BattleRuntimeModule runtime = new();
        runtime.setup(null, new System.Collections.Generic.Dictionary<StringName, SkillDefinition>());
        BattleState state = BuildFlatState(new Vector2I(5, 3));
        BattleUnitState sourceA = BuildUnit("runtime_source_a", "player", new Vector2I(0, 1));
        BattleUnitState sourceB = BuildUnit("runtime_source_b", "player", new Vector2I(1, 1));
        BattleUnitState target = BuildUnit("runtime_burning_target", "enemy", new Vector2I(3, 1));
        sourceA.source_member_id = "runtime_member_a";
        sourceB.source_member_id = "runtime_member_b";
        target.SetDamageResistanceTyped("fire", "half");
        AddUnit(runtime, state, sourceA);
        AddUnit(runtime, state, sourceB);
        AddUnit(runtime, state, target);
        runtime.SetupStateForTests(state);

        BattleStatusSourceIdentity identityA = BattleStatusSourceIdentity.Skill(
            sourceA.unit_id,
            "skill_a"
        );
        BattleStatusSourceIdentity identityB = BattleStatusSourceIdentity.Skill(
            sourceB.unit_id,
            "skill_b"
        );
        BattleStatusEffectState burning = BattleStatusSemanticTable.MergeStatus(
            BuildBurningEffect(power: 1, durationTu: 30),
            sourceA.unit_id,
            null,
            "burning",
            identityA
        );
        burning = BattleStatusSemanticTable.MergeStatus(
            BuildBurningEffect(power: 2, durationTu: 40),
            sourceB.unit_id,
            burning,
            "burning",
            identityB
        );
        burning.GetSourceContributionTyped(identityA).NextTickAtTu = 10;
        burning.GetSourceContributionTyped(identityB).NextTickAtTu = 20;
        burning.RebuildSourceContributionAggregateTyped();
        target.SetStatusEffect(burning);
        sourceA.MarkDead();
        runtime._skill_mastery_service.RecordMasteryAmount("skill_a", 7);

        AdvanceTimelineTu(runtime, state, 10);
        _test.Eq(
            target.GetCurrentHp(),
            50,
            "10TU时只结算来源A；1点火焰经half抗性后应降为0。"
        );
        _test.True(target.HasStatusEffect("burning"), "来源死亡不得移除已经施加的燃烧。" );
        _test.Eq(
            target.GetStatusEffect("burning")?.GetSourceContributionTyped(identityA)?.NextTickAtTu ?? -1,
            20,
            "来源A应独立推进到下一结算点。"
        );
        _test.Eq(
            target.GetStatusEffect("burning")?.GetSourceContributionTyped(identityB)?.NextTickAtTu ?? -1,
            20,
            "尚未结算的来源B应保留自己的锚点。"
        );

        AdvanceTimelineTu(runtime, state, 10);
        _test.Eq(
            target.GetCurrentHp(),
            49,
            "20TU时两个来源应分别结算，并各自经过火焰抗性链（0点+1点）。"
        );
        _test.Eq(
            runtime._skill_mastery_service.ResolveActiveSkillMasteryAmount(),
            7,
            "燃烧来源的后续时间轴跳伤不得重复增长施加技能的熟练度。"
        );
        runtime.Dispose();
    }

    private void TestAggregateEraseRemovesEveryContribution()
    {
        BattleUnitState target = new() { unit_id = "burning_target" };
        BattleStatusEffectState merged = BattleStatusSemanticTable.MergeStatus(
            BuildBurningEffect(power: 1, durationTu: 20),
            "caster_a",
            null,
            "burning",
            BattleStatusSourceIdentity.Skill("caster_a", "skill_a")
        );
        merged = BattleStatusSemanticTable.MergeStatus(
            BuildBurningEffect(power: 1, durationTu: 20),
            "caster_b",
            merged,
            "burning",
            BattleStatusSourceIdentity.Skill("caster_b", "skill_a")
        );
        target.SetStatusEffect(merged);
        System.Collections.Generic.IReadOnlyList<BattleHudStatusEffectSnapshot> hudStatuses =
            BattleHudAdapter.BuildStatusEffectSnapshots(target);
        BattleHudStatusEffectSnapshot burningHud = hudStatuses.Count > 0
            ? hudStatuses[0]
            : null;
        _test.Eq(burningHud?.SourceContributionCount ?? -1, 2, "HUD应投影燃烧来源数量。" );
        _test.True(
            burningHud?.TooltipText.Contains("解除该状态会清除全部来源") == true,
            "HUD应解释来源独立而驱散清除全部。"
        );
        target.EraseStatusEffect("burning");
        _test.True(
            target.GetStatusEffect("burning") == null,
            "驱散/删除聚合燃烧时必须一次移除全部来源贡献。"
        );
    }

    private static CombatEffectDefinition BuildBurningEffect(int power, int durationTu) =>
        TestSkillDefinitionProjection.BuildEffect(
            "status",
            statusId: "burning",
            power: power,
            durationTu: durationTu,
            tickIntervalTu: 10,
            damageTag: "fire"
        );

    private static BattleState BuildFlatState(Vector2I mapSize)
    {
        BattleState state = new()
        {
            battle_id = "source_contribution_runtime",
            phase = "timeline_running",
            map_size = mapSize,
            timeline = new BattleTimelineState { tu_per_tick = 5 },
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                BattleCellState cell = new()
                {
                    coord = new Vector2I(x, y),
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 4,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(cell.coord, cell);
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
        BattleUnitState unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
        }.WithCombatResourcesForTest(hp: 50, mp: 0, stamina: 0, ap: 2, isAlive: true);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 50);
        unit.SetAnchorCoord(coord);
        unit.SetActionThresholdTyped(1000000);
        return unit;
    }

    private static void AddUnit(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit
    )
    {
        state.SetUnit(unit);
        if (unit.faction_id == "enemy")
            state.enemy_unit_ids.Add(unit.unit_id);
        else
            state.ally_unit_ids.Add(unit.unit_id);
        runtime._grid_service.PlaceUnit(state, unit, unit.GetAnchorCoord(), true);
    }

    private static void AdvanceTimelineTu(
        BattleRuntimeModule runtime,
        BattleState state,
        int totalTu
    )
    {
        state.phase = "timeline_running";
        state.active_unit_id = "";
        state.timeline.ready_unit_ids.Clear();
        runtime.advance(totalTu / state.timeline.tu_per_tick);
    }
}
