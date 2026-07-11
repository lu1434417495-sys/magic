using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_meteor_swarm_commit_payload_boundary_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestCommitterCommitsTypedMeteorResultAndCopiesReports();
            TestMeteorDtosPreserveTypedMutationContracts();
            RequestTestExit(_test.Finish("Meteor swarm commit payload boundary regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Meteor swarm commit payload boundary regression", 1));
        }
    }

    private void TestCommitterCommitsTypedMeteorResultAndCopiesReports()
    {
        BattleUnitState caster = BuildUnit("meteor_commit_caster", "player", Vector2I.Zero);
        BattleUnitState target = BuildUnit(
            "meteor_commit_target",
            "enemy",
            new Vector2I(1, 0)
        );
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "meteor_commit_payload_boundary_regression",
            new Vector2I(5, 5),
            new[] { caster },
            new[] { target }
        );
        BattleRuntimeModule runtime = fixture.Runtime;

        var committer = new BattleSkillOutcomeCommitter();
        committer.Setup(runtime);

        var result = new MeteorSwarmCommitResult
        {
            plan = new MeteorSwarmTargetPlan
            {
                source_unit_id = caster.unit_id,
                skill_id = "mage_meteor_swarm",
                final_anchor_coord = new Vector2I(2, 2),
                nominal_plan_signature = "nominal",
                final_plan_signature = "final",
            },
            total_damage = 12,
            total_healing = 1,
        };
        result.AddChangedUnitId(caster.unit_id);
        result.AddChangedCoord(new Vector2I(2, 2));
        result.log_lines.Add("commit_log_fixture");
        var targetOutcome = new MeteorSwarmTargetOutcome
        {
            target_unit_id = target.unit_id,
            total_damage = 12,
            total_healing = 0,
            defeated = false,
        };
        targetOutcome.AddStatusEffectId("meteor_concussed");
        result.target_outcomes.Add(targetOutcome);
        var reportComponent = new MeteorSwarmComponentFact
        {
            component_id = "core",
            role_label = "impact",
            damage_tag = "fire",
            damage = 12,
        };
        var reportEntry = new MeteorSwarmReportEntry
        {
            entry_type = "meteor_swarm_impact_summary",
            skill_id = "mage_meteor_swarm",
            source_unit_id = caster.unit_id,
            anchor_coord = new Vector2I(2, 2),
            nominal_anchor_coord = new Vector2I(2, 2),
            nominal_plan_signature = "nominal",
            final_plan_signature = "final",
            target_count = 1,
            terrain_effect_count = 0,
            total_damage = 12,
            defeated_count = 0,
            text = "original summary",
            terrain_summary = new MeteorSwarmTerrainSummaryFact(),
        };
        reportEntry.component_breakdown.Add(reportComponent);
        reportEntry.target_summaries.Add(targetOutcome);
        result.AddReportEntry(reportEntry);
        _test.Eq(result.GetReportEntriesTyped().Count, 1, "typed report entry 应进入只读 report 集合。");
        reportEntry.text = "mutated summary";
        reportComponent.damage = 99;

        var batch = new BattleEventBatch();
        _test.True(
            committer.CommitMeteorSwarmResult(result, batch),
            "committer 应直接从 typed MeteorSwarmCommitResult 提交 common outcome。"
        );
        _test.True(batch.changed_unit_ids.Contains(caster.unit_id), "提交应记录施法者变更。");
        _test.True(batch.changed_unit_ids.Contains(target.unit_id), "提交应记录目标变更。");
        _test.True(batch.changed_coords.Contains(new Vector2I(2, 2)), "提交应记录目标地格变更。");
        _test.True(
            batch.LogLinesTyped.Contains("commit_log_fixture"),
            $"提交应追加 typed log line。actual={string.Join("|", batch.LogLinesTyped)}"
        );
        _test.True(
            batch.LogLinesTyped.Contains("original summary"),
            $"提交 report entry 时应把 report text 追加为 batch log。actual={string.Join("|", batch.LogLinesTyped)}"
        );
        _test.Eq(batch.report_entries.Count, 1, "提交应追加 report entry。");

        IReadOnlyDictionary<string, object> committedEntry = batch.report_entries[0];
        _test.Eq(
            committedEntry["text"]?.ToString(),
            "original summary",
            "report entry text 应在 committer 边界投影复制，不能被 result 后续修改污染。"
        );
        IReadOnlyList<object> committedComponents =
            committedEntry["component_breakdown"] as IReadOnlyList<object>;
        IReadOnlyDictionary<string, object> committedComponent =
            committedComponents?[0] as IReadOnlyDictionary<string, object>;
        _test.Eq(
            Convert.ToInt32(committedComponent?["damage"] ?? 0),
            12,
            "report entry component breakdown 应在 committer 边界投影复制。"
        );
    }

    private void TestMeteorDtosPreserveTypedMutationContracts()
    {
        var plan = new MeteorSwarmTargetPlan();
        plan.ring_by_coord[new Vector2I(1, 1)] = 2;
        plan.unit_distance_by_id["enemy_a"] = 3;
        plan.unit_primary_coord_by_id["enemy_a"] = new Vector2I(4, 5);

        _test.Eq(plan.GetRingForCoord(new Vector2I(1, 1)), 2, "typed target plan 应返回 ring。");
        _test.Eq(plan.GetDistanceForUnit("enemy_a"), 3, "typed target plan 应返回单位距离。");
        _test.Eq(
            plan.GetPrimaryCoordForUnit("enemy_a"),
            new Vector2I(4, 5),
            "typed target plan 应返回单位主坐标。"
        );

        var outcome = new MeteorSwarmTargetOutcome();
        outcome.AddStatusEffectId("meteor_concussed");
        outcome.AddStatusEffectId("meteor_concussed");
        _test.Eq(outcome.status_effect_ids.Count, 1, "typed target outcome 应去重 status。");

        var commit = new MeteorSwarmCommitResult();
        commit.AddChangedUnitId("enemy_a");
        commit.AddChangedUnitId("enemy_a");
        commit.AddChangedCoord(new Vector2I(1, 2));
        commit.AddChangedCoord(new Vector2I(1, 2));
        commit.AddDefeatedUnitId("enemy_a");
        commit.AddDefeatedUnitId("enemy_a");
        _test.Eq(commit.changed_unit_ids.Count, 1, "typed commit result 应去重 changed unit。");
        _test.Eq(commit.changed_coords.Count, 1, "typed commit result 应去重 changed coord。");
        _test.Eq(commit.defeated_unit_ids.Count, 1, "typed commit result 应去重 defeated unit。");

        var castContext = new MeteorSwarmCastContext
        {
            nominal_anchor_coord = new Vector2I(2, 2),
            final_anchor_coord = new Vector2I(3, 2),
        };
        _test.True(castContext.HasDrift(), "typed cast context 应反映 drift。");
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = factionId == "enemy" ? "ai" : "manual",
            current_hp = 30,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        return unit;
    }

}
