using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_sim_trace_summary_builder_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestBuilderUsesPlainCSharpBoundary();
        TestCompactsTypedScenarioReport();
        TestCompactsRunnerProfileReport();

        return _test.Finish("Battle sim trace summary builder regression");
    }

    private void TestBuilderUsesPlainCSharpBoundary()
    {
        _test.True(
            typeof(BattleSimTraceSummaryBuilder).GetMethod("has_traces") == null
                && typeof(BattleSimTraceSummaryBuilder).GetMethod("build") == null,
            "BattleSimTraceSummaryBuilder 不应继续暴露 snake_case GDScript helper。"
        );
        _test.True(
            typeof(BattleSimTraceSummaryBuilder).GetMethod(
                "Build",
                new[]
                {
                    typeof(BattleSimScenarioReport),
                    typeof(string),
                    typeof(BattleSimTraceSummaryBuilder.TraceSummaryOptionsData),
                }
            ) != null,
            "BattleSimTraceSummaryBuilder 应继续提供 BattleSimScenarioReport typed overload。"
        );
        _test.True(
            typeof(BattleSimTraceSummaryBuilder).GetMethod(
                "HasTraces",
                new[] { typeof(GDictionary) }
            ) == null
                && typeof(BattleSimTraceSummaryBuilder).GetMethod(
                    "Build",
                    new[] { typeof(GDictionary), typeof(string), typeof(GDictionary) }
                ) == null,
            "BattleSimTraceSummaryBuilder 不应继续保留 top-level report GDictionary overload。"
        );
    }

    private void TestCompactsTypedScenarioReport()
    {
        var builder = new BattleSimTraceSummaryBuilder();
        BattleSimScenarioReport report = BuildTypedReport();
        GDictionary summary = builder.Build(report, "res://full_report.json");
        _test.True(builder.HasTraces(report), "builder 应能识别 typed scenario report 中的 trace。");
        _test.Eq(GetString(summary, "source_report"), "res://full_report.json", "summary 应保留完整报告路径。");
        _test.Eq(GetInt(summary, "profile_count"), 1, "typed summary 应保留 profile 数。");
        _test.Eq(GetInt(summary, "run_count"), 1, "typed summary 应保留 run 数。");
        _test.Eq(GetInt(summary, "trace_count"), 2, "summary 应统计 trace 数。");

        GDictionary compactRun = GetSelfDict(GetArray(summary, "runs")[0]);
        _test.Eq(GetInt(compactRun, "trace_count"), 2, "compact run 应统计自身 trace 数。");
        _test.Eq(GetInt(GetDict(GetDict(compactRun, "command_counts_by_faction"), "player"), "wait"), 1, "player wait command 应被聚合。");
        _test.Eq(GetInt(GetDict(GetDict(compactRun, "block_reasons_by_faction"), "player"), "体力不足，无法施放该技能。"), 1, "player 体力不足阻断应被聚合。");
        _test.Eq(GetArray(compactRun, "focus_turns").Count, 1, "默认 focus faction 应只保留 player 回合。");
        _test.Eq(GetArray(compactRun, "focus_wait_turns").Count, 1, "默认 focus wait 应只保留 player wait。");

        GDictionary focusTurn = GetSelfDict(GetArray(compactRun, "focus_turns")[0]);
        GDictionary compactScoreInput = GetDict(focusTurn, "score");
        GDictionary saveEstimatesByTarget = GetDict(compactScoreInput, "save_estimates_by_target_id");
        _test.True(saveEstimatesByTarget.ContainsKey("enemy_sword"), "summary 应保留 score_input 中的目标豁免概率估算。");
        if (saveEstimatesByTarget.ContainsKey("enemy_sword"))
        {
            GArray saveEstimates = saveEstimatesByTarget["enemy_sword"].AsGodotArray();
            GDictionary saveEstimate = GetSelfDict(saveEstimates[0]);
            _test.Eq(GetInt(saveEstimate, "save_success_rate_percent"), 50, "summary 应保留豁免成功率。");
            _test.Eq(GetInt(saveEstimate, "damage_after_save_estimate"), 9, "summary 应保留豁免加权后的期望伤害。");
        }
        _test.True(compactScoreInput.ContainsKey("has_post_action_threat_projection") && compactScoreInput["has_post_action_threat_projection"].AsBool(), "summary 应保留行动后威胁投影标记。");
        _test.Eq(GetInt(compactScoreInput, "post_action_remaining_threat_expected_damage"), 7, "summary 应保留行动后剩余威胁期望伤害。");
        _test.Eq(GetArray(compactScoreInput, "post_action_remaining_threat_unit_ids").Count, 1, "summary 应保留剩余威胁单位列表。");

        GArray decisionTargetSnapshots = GetArray(focusTurn, "decision_target_snapshots");
        _test.Eq(decisionTargetSnapshots.Count, 1, "summary 应保留决策目标执行前快照。");
        if (decisionTargetSnapshots.Count > 0)
            _test.Eq(GetInt(GetSelfDict(decisionTargetSnapshots[0]), "hp"), 12, "目标快照应保留执行前 HP。");

        GDictionary executionResult = GetDict(focusTurn, "execution_result");
        GArray unitResults = GetArray(executionResult, "unit_results");
        _test.Eq(unitResults.Count, 1, "summary 应保留执行结果中的单位资源变化。");
        if (unitResults.Count > 0)
        {
            GDictionary unitResult = GetSelfDict(unitResults[0]);
            _test.Eq(GetInt(unitResult, "hp_damage"), 7, "执行结果应保留实际 HP 伤害。");
            _test.Eq(GetInt(GetDict(unitResult, "after"), "hp"), 5, "执行结果应保留执行后 HP。");
        }

        GDictionary actionTrace = GetSelfDict(GetArray(focusTurn, "action_traces")[0]);
        _test.Eq(GetArray(actionTrace, "top_candidates").Count, 2, "action trace 默认只保留前 2 个 candidate。");
        GDictionary topCandidate = GetSelfDict(GetArray(actionTrace, "top_candidates")[0]);
        _test.Eq(GetInt(topCandidate, "screening_bonus"), 45, "summary 应保留 screening_bonus 便于分析守线命中。");
        _test.Eq(GetString(topCandidate, "screening_threat_unit_id"), "enemy_sword", "summary 应保留守线威胁单位。");
        _test.Eq(GetString(topCandidate, "screening_protected_unit_id"), "hero_archer", "summary 应保留被保护单位。");
        _test.True(topCandidate.ContainsKey("screening_can_counterattack") && topCandidate["screening_can_counterattack"].AsBool(), "summary 应保留守线后能否反击。");
        _test.True(topCandidate.ContainsKey("screening_hard_block") && topCandidate["screening_hard_block"].AsBool(), "summary 应保留守线是否造成硬阻断。");
        _test.True(topCandidate.ContainsKey("screening_distance_band_capped") && topCandidate["screening_distance_band_capped"].AsBool(), "summary 应保留守线分是否被接敌距离带截断。");
    }

    private void TestCompactsRunnerProfileReport()
    {
        var builder = new BattleSimTraceSummaryBuilder();
        BattleSimScenarioReport report = BuildTypedReport();
        GDictionary summary = builder.Build(
            report,
            "user://sample_report.json",
            new BattleSimTraceSummaryBuilder.TraceSummaryOptionsData { TopCandidateLimit = 1 }
        );
        _test.True(builder.HasTraces(report), "builder 应能识别 profile_entries 中的 trace。");
        _test.Eq(GetInt(summary, "profile_count"), 1, "profile_count 应来自 profile_entries。");
        GDictionary compactRun = GetSelfDict(GetArray(summary, "runs")[0]);
        _test.Eq(GetString(compactRun, "profile_id"), "baseline", "compact run 应保留 profile_id。");
        GDictionary actionTrace = GetSelfDict(GetArray(GetSelfDict(GetArray(compactRun, "focus_turns")[0]), "action_traces")[0]);
        _test.Eq(GetArray(actionTrace, "top_candidates").Count, 1, "自定义 top candidate 上限应生效。");
    }

    private static BattleSimScenarioReport BuildTypedReport()
    {
        var report = new BattleSimScenarioReport
        {
            ScenarioDef = new BattleSimScenarioDef { scenario_id = "sample" },
        };
        report.ProfileEntries.Add(
            new BattleSimProfileReportEntry
            {
                Profile = new BattleSimProfileDef { profile_id = "baseline" },
                Summary = new BattleSimProfileSummary { AverageIterations = 3.0f },
            }
        );
        report.ProfileEntries[0].Runs.Add(BuildTypedRun());
        return report;
    }

    private static BattleSimRunReport BuildTypedRun()
    {
        GDictionary run = BuildRun();
        return new BattleSimRunReport
        {
            Seed = GetInt(run, "seed"),
            WinnerFactionId = GetString(run, "winner_faction_id"),
            Iterations = GetInt(run, "iterations"),
            TimelineSteps = GetInt(run, "timeline_steps"),
            Metrics = new GDictionary
            {
                ["factions"] = GetDict(run, "factions"),
                ["units"] = GetDict(run, "units"),
            },
            AiTurnTraces = BuildTypedTraces(GetArray(run, "ai_turn_traces")),
        };
    }

    private static IReadOnlyList<BattleAiTurnTraceProjection> BuildTypedTraces(GArray traces)
    {
        var result = new List<BattleAiTurnTraceProjection>();
        foreach (Variant traceValue in traces)
        {
            GDictionary trace = GetSelfDict(traceValue);
            if (trace.Count == 0)
                continue;
            result.Add(
                new BattleAiTurnTraceProjection
                {
                    TurnStartedTu = GetInt(trace, "turn_started_tu", -1),
                    UnitId = GetString(trace, "unit_id"),
                    UnitName = GetString(trace, "unit_name"),
                    FactionId = GetString(trace, "faction_id"),
                    BrainId = GetString(trace, "brain_id"),
                    StateId = GetString(trace, "state_id"),
                    ActionId = GetString(trace, "action_id"),
                    ReasonText = GetString(trace, "reason_text"),
                    Command = BuildCommandSummary(GetDict(trace, "command")),
                    ScoreInput = BuildScoreInput(GetDict(trace, "score_input")),
                    ActionTraces = BuildActionTraces(GetArray(trace, "action_traces")),
                    DecisionTargetSnapshots = BuildSnapshots(GetArray(trace, "decision_target_snapshots")),
                    ExecutionResult = BuildExecutionResult(GetDict(trace, "execution_result")),
                }
            );
        }
        return result;
    }

    private static AiCommandSummary BuildCommandSummary(GDictionary command)
    {
        return new AiCommandSummary
        {
            CommandType = GetString(command, "command_type"),
            UnitId = GetString(command, "unit_id"),
            SkillId = GetString(command, "skill_id"),
            SkillVariantId = GetString(command, "skill_variant_id"),
            TargetUnitId = GetString(command, "target_unit_id"),
        };
    }

    private static BattleAiScoreInput BuildScoreInput(GDictionary score)
    {
        var result = new BattleAiScoreInput
        {
            action_kind = GetString(score, "action_kind"),
            action_label = GetString(score, "action_label"),
            action_intent = GetString(score, "action_intent"),
            score_bucket_id = GetString(score, "score_bucket_id"),
            score_bucket_priority = GetInt(score, "score_bucket_priority", 0),
            target_count = GetInt(score, "target_count", 0),
            effective_target_count = GetInt(score, "effective_target_count", 0),
            enemy_target_count = GetInt(score, "enemy_target_count", 0),
            ally_target_count = GetInt(score, "ally_target_count", 0),
            estimated_damage = GetInt(score, "estimated_damage", 0),
            estimated_hit_rate_percent = GetInt(score, "estimated_hit_rate_percent", 0),
            estimated_lethal_target_count = GetInt(score, "estimated_lethal_target_count", 0),
            estimated_lethal_threat_target_count = GetInt(score, "estimated_lethal_threat_target_count", 0),
            has_post_action_threat_projection = GetBool(score, "has_post_action_threat_projection"),
            projected_actor_coord = ParseCoord(GetString(score, "projected_actor_coord")),
            pre_action_threat_count = GetInt(score, "pre_action_threat_count", 0),
            pre_action_threat_expected_damage = GetInt(score, "pre_action_threat_expected_damage", 0),
            pre_action_survival_margin = GetInt(score, "pre_action_survival_margin", 0),
            pre_action_is_lethal_survival_risk = GetBool(score, "pre_action_is_lethal_survival_risk"),
            post_action_remaining_threat_count = GetInt(score, "post_action_remaining_threat_count", 0),
            post_action_remaining_threat_expected_damage = GetInt(score, "post_action_remaining_threat_expected_damage", 0),
            post_action_survival_margin = GetInt(score, "post_action_survival_margin", 0),
            post_action_is_lethal_survival_risk = GetBool(score, "post_action_is_lethal_survival_risk"),
            hit_payoff_score = GetInt(score, "hit_payoff_score", 0),
            position_objective_kind = GetString(score, "position_objective_kind"),
            position_objective_score = GetInt(score, "position_objective_score", 0),
            resource_cost_score = GetInt(score, "resource_cost_score", 0),
            distance_to_primary_coord = GetInt(score, "distance_to_primary_coord", -1),
            ap_cost = GetInt(score, "ap_cost", 0),
            stamina_cost = GetInt(score, "stamina_cost", 0),
            mp_cost = GetInt(score, "mp_cost", 0),
            aura_cost = GetInt(score, "aura_cost", 0),
            move_cost = GetInt(score, "move_cost", 0),
            total_score = GetInt(score, "total_score", 0),
        };
        result.target_unit_ids.AddRange(ToStringNames(GetArray(score, "target_unit_ids")));
        result.target_coords.AddRange(ToCoords(GetArray(score, "target_coords")));
        result.estimated_lethal_target_ids.AddRange(ToStringNames(GetArray(score, "estimated_lethal_target_ids")));
        result.estimated_lethal_threat_target_ids.AddRange(ToStringNames(GetArray(score, "estimated_lethal_threat_target_ids")));
        result.estimated_control_target_ids.AddRange(ToStringNames(GetArray(score, "estimated_control_target_ids")));
        result.estimated_control_threat_target_ids.AddRange(ToStringNames(GetArray(score, "estimated_control_threat_target_ids")));
        result.pre_action_threat_unit_ids.AddRange(ToStringNames(GetArray(score, "pre_action_threat_unit_ids")));
        result.post_action_remaining_threat_unit_ids.AddRange(ToStringNames(GetArray(score, "post_action_remaining_threat_unit_ids")));
        GDictionary saveEstimatesByTarget = GetDict(score, "save_estimates_by_target_id");
        foreach (Variant targetKey in saveEstimatesByTarget.Keys)
        {
            string key = targetKey.ToString();
            if (string.IsNullOrEmpty(key))
                continue;
            var estimates = new List<BattleAiScoreService.DamageSaveEstimate>();
            GArray estimateArray = saveEstimatesByTarget.ContainsKey(targetKey)
                ? saveEstimatesByTarget[targetKey].AsGodotArray()
                : new GArray();
            foreach (Variant estimateValue in estimateArray)
            {
                GDictionary estimate = GetSelfDict(estimateValue);
                if (estimate.Count == 0)
                    continue;
                estimates.Add(
                    new BattleAiScoreService.DamageSaveEstimate
                    {
                        DamageBeforeSave = GetInt(estimate, "damage_before_save", 0),
                        DamageAfterSaveEstimate = GetInt(estimate, "damage_after_save_estimate", 0),
                        DamageOnSaveSuccess = GetInt(estimate, "damage_on_save_success", 0),
                        SaveSuccessRatePercent = GetInt(estimate, "save_success_rate_percent", 0),
                        Dc = GetInt(estimate, "dc", 0),
                        Ability = GetString(estimate, "ability"),
                        SaveTag = GetString(estimate, "save_tag"),
                        AdvantageState = GetString(estimate, "advantage_state"),
                        Immune = GetBool(estimate, "immune"),
                        HitCount = GetInt(estimate, "hit_count", 1),
                    }
                );
            }
            result.save_estimates_by_target_id[key] = estimates;
        }
        return result;
    }

    private static IReadOnlyList<AiActionTrace> BuildActionTraces(GArray actionTraces)
    {
        var result = new List<AiActionTrace>();
        foreach (Variant actionTraceValue in actionTraces)
        {
            GDictionary actionTrace = GetSelfDict(actionTraceValue);
            if (actionTrace.Count == 0)
                continue;
            var trace = new AiActionTrace
            {
                TraceId = ProgressionDataUtils.to_string_name(actionTrace["trace_id"]),
                ActionId = GetString(actionTrace, "action_id"),
                ScoreBucketId = GetString(actionTrace, "score_bucket_id"),
                Chosen = GetBool(actionTrace, "chosen"),
                BlockedCount = GetInt(actionTrace, "blocked_count", 0),
                CandidateCount = GetInt(actionTrace, "candidate_count", 0),
                EvaluationCount = GetInt(actionTrace, "evaluation_count", 0),
                PreviewRejectCount = GetInt(actionTrace, "preview_reject_count", 0),
            };
            CopyObjectMap(trace.Metadata, TraceDictionaryProjection.FromDictionary(GetDict(actionTrace, "metadata")));
            foreach (Variant key in GetDict(actionTrace, "block_reasons").Keys)
                trace.BlockReasons[key.ToString()] = GetDict(actionTrace, "block_reasons")[key].AsInt32();
            foreach (Variant candidateValue in GetArray(actionTrace, "top_candidates"))
                trace.TopCandidates.Add(BuildCandidate(GetSelfDict(candidateValue)));
            result.Add(trace);
        }
        return result;
    }

    private static AiCandidateSummary BuildCandidate(GDictionary candidate)
    {
        var extra = TraceDictionaryProjection.FromDictionary(candidate);
        extra.Remove("label");
        extra.Remove("command");
        extra.Remove("total_score");
        extra.Remove("score_input");
        return new AiCandidateSummary(
            GetString(candidate, "label"),
            BuildCommandSummary(GetDict(candidate, "command")),
            GetInt(candidate, "total_score", 0),
            TraceDictionaryProjection.FromDictionary(GetDict(candidate, "score_input")),
            extra
        );
    }

    private static IReadOnlyList<BattleAiTraceUnitSnapshotProjection> BuildSnapshots(GArray snapshots)
    {
        var result = new List<BattleAiTraceUnitSnapshotProjection>();
        foreach (Variant snapshotValue in snapshots)
        {
            GDictionary snapshot = GetSelfDict(snapshotValue);
            if (snapshot.Count == 0)
                continue;
            result.Add(
                new BattleAiTraceUnitSnapshotProjection
                {
                    UnitId = GetString(snapshot, "unit_id"),
                    DisplayName = GetString(snapshot, "display_name"),
                    FactionId = GetString(snapshot, "faction_id"),
                    Coord = GetString(snapshot, "coord"),
                    Alive = GetBool(snapshot, "alive"),
                    Hp = GetInt(snapshot, "hp", 0),
                    HpMax = GetInt(snapshot, "hp_max", 0),
                    ShieldHp = GetInt(snapshot, "shield_hp", 0),
                    ShieldMaxHp = GetInt(snapshot, "shield_max_hp", 0),
                    Ap = GetInt(snapshot, "ap", 0),
                    MovePoints = GetInt(snapshot, "move_points", 0),
                }
            );
        }
        return result;
    }

    private static BattleAiTraceExecutionResultProjection BuildExecutionResult(GDictionary executionResult)
    {
        if (executionResult.Count == 0)
            return null;
        var result = new BattleAiTraceExecutionResultProjection
        {
            CommandType = GetString(executionResult, "command_type"),
            SkillId = GetString(executionResult, "skill_id"),
            SkillVariantId = GetString(executionResult, "skill_variant_id"),
            ChangedUnitIds = ToStringList(GetArray(executionResult, "changed_unit_ids")),
            TrackedUnitIds = ToStringList(GetArray(executionResult, "tracked_unit_ids")),
            LogLines = ToStringList(GetArray(executionResult, "log_lines")),
            ReportEntries = BuildReportEntries(GetArray(executionResult, "report_entries")),
        };
        var unitResults = new List<BattleAiTraceUnitResultProjection>();
        foreach (Variant unitResultValue in GetArray(executionResult, "unit_results"))
        {
            GDictionary unitResult = GetSelfDict(unitResultValue);
            if (unitResult.Count == 0)
                continue;
            unitResults.Add(
                new BattleAiTraceUnitResultProjection
                {
                    UnitId = GetString(unitResult, "unit_id"),
                    Before = BuildSnapshots(new GArray { GetDict(unitResult, "before") })[0],
                    After = BuildSnapshots(new GArray { GetDict(unitResult, "after") })[0],
                    HpDelta = GetInt(unitResult, "hp_delta", 0),
                    HpDamage = GetInt(unitResult, "hp_damage", 0),
                    HpHealing = GetInt(unitResult, "hp_healing", 0),
                    ShieldDelta = GetInt(unitResult, "shield_delta", 0),
                    ShieldDamage = GetInt(unitResult, "shield_damage", 0),
                    ShieldRestored = GetInt(unitResult, "shield_restored", 0),
                    Killed = GetBool(unitResult, "killed"),
                    Revived = GetBool(unitResult, "revived"),
                    Moved = GetBool(unitResult, "moved"),
                }
            );
        }
        result.UnitResults = unitResults;
        return result;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object>> BuildReportEntries(GArray entries)
    {
        var result = new List<IReadOnlyDictionary<string, object>>();
        foreach (Variant entryValue in entries)
        {
            GDictionary entry = GetSelfDict(entryValue);
            if (entry.Count > 0)
                result.Add(TraceDictionaryProjection.FromDictionary(entry));
        }
        return result;
    }

    private static List<string> ToStringList(GArray values)
    {
        var result = new List<string>();
        foreach (Variant value in values)
            result.Add(value.ToString());
        return result;
    }

    private static void CopyObjectMap(
        Dictionary<string, object> target,
        IReadOnlyDictionary<string, object> source
    )
    {
        foreach (KeyValuePair<string, object> entry in source ?? new Dictionary<string, object>())
            target[entry.Key] = entry.Value;
    }

    private static GDictionary BuildRun()
    {
        return new GDictionary
        {
            ["run_index"] = 0,
            ["seed"] = 123,
            ["winner_faction_id"] = "hostile",
            ["iterations"] = 9,
            ["timeline_steps"] = 4,
            ["factions"] = new GDictionary
            {
                ["player"] = new GDictionary { ["turn_count"] = 1 },
                ["hostile"] = new GDictionary { ["turn_count"] = 1 },
            },
            ["units"] = new GDictionary(),
            ["ai_turn_traces"] = new GArray
            {
                new GDictionary
                {
                    ["turn_started_tu"] = 10,
                    ["unit_id"] = "hero",
                    ["unit_name"] = "Hero",
                    ["faction_id"] = "player",
                    ["brain_id"] = "melee_aggressor",
                    ["state_id"] = "engage",
                    ["action_id"] = "wait_action",
                    ["reason_text"] = "wait",
                    ["command"] = new GDictionary { ["command_type"] = "wait", ["unit_id"] = "hero" },
                    ["score_input"] = new GDictionary
                    {
                        ["total_score"] = -40,
                        ["command_type"] = "wait",
                        ["estimated_lethal_target_ids"] = new GArray { "enemy_sword" },
                        ["estimated_lethal_threat_target_ids"] = new GArray { "enemy_sword" },
                        ["estimated_control_target_ids"] = new GArray { "enemy_archer" },
                        ["estimated_control_threat_target_ids"] = new GArray { "enemy_archer" },
                        ["has_post_action_threat_projection"] = true,
                        ["projected_actor_coord"] = "(2, 2)",
                        ["pre_action_threat_unit_ids"] = new GArray { "enemy_sword", "enemy_archer" },
                        ["pre_action_threat_count"] = 2,
                        ["pre_action_threat_expected_damage"] = 19,
                        ["pre_action_survival_margin"] = -4,
                        ["pre_action_is_lethal_survival_risk"] = true,
                        ["post_action_remaining_threat_unit_ids"] = new GArray { "enemy_archer" },
                        ["post_action_remaining_threat_count"] = 1,
                        ["post_action_remaining_threat_expected_damage"] = 7,
                        ["post_action_survival_margin"] = 8,
                        ["post_action_is_lethal_survival_risk"] = false,
                        ["save_estimates_by_target_id"] = new GDictionary
                        {
                            ["enemy_sword"] = new GArray
                            {
                                new GDictionary
                                {
                                    ["damage_before_save"] = 12,
                                    ["damage_after_save_estimate"] = 9,
                                    ["damage_on_save_success"] = 6,
                                    ["save_success_rate_percent"] = 50,
                                    ["dc"] = 11,
                                    ["ability"] = "agility",
                                    ["save_tag"] = "fireball",
                                    ["advantage_state"] = "normal",
                                    ["immune"] = false,
                                    ["hit_count"] = 1,
                                },
                            },
                        },
                    },
                    ["decision_target_snapshots"] = new GArray
                    {
                        new GDictionary
                        {
                            ["unit_id"] = "enemy_sword",
                            ["display_name"] = "Enemy Sword",
                            ["faction_id"] = "hostile",
                            ["coord"] = "(1, 1)",
                            ["alive"] = true,
                            ["hp"] = 12,
                            ["hp_max"] = 20,
                            ["shield_hp"] = 0,
                            ["shield_max_hp"] = 0,
                            ["ap"] = 1,
                            ["move_points"] = 2,
                        },
                    },
                    ["execution_result"] = new GDictionary
                    {
                        ["command_type"] = "skill",
                        ["skill_id"] = "sample_skill",
                        ["changed_unit_ids"] = new GArray { "enemy_sword" },
                        ["tracked_unit_ids"] = new GArray { "enemy_sword" },
                        ["unit_results"] = new GArray
                        {
                            new GDictionary
                            {
                                ["unit_id"] = "enemy_sword",
                                ["before"] = new GDictionary
                                {
                                    ["unit_id"] = "enemy_sword",
                                    ["display_name"] = "Enemy Sword",
                                    ["faction_id"] = "hostile",
                                    ["coord"] = "(1, 1)",
                                    ["alive"] = true,
                                    ["hp"] = 12,
                                    ["hp_max"] = 20,
                                    ["shield_hp"] = 0,
                                    ["shield_max_hp"] = 0,
                                    ["ap"] = 1,
                                    ["move_points"] = 2,
                                },
                                ["after"] = new GDictionary
                                {
                                    ["unit_id"] = "enemy_sword",
                                    ["display_name"] = "Enemy Sword",
                                    ["faction_id"] = "hostile",
                                    ["coord"] = "(1, 1)",
                                    ["alive"] = true,
                                    ["hp"] = 5,
                                    ["hp_max"] = 20,
                                    ["shield_hp"] = 0,
                                    ["shield_max_hp"] = 0,
                                    ["ap"] = 1,
                                    ["move_points"] = 2,
                                },
                                ["hp_delta"] = -7,
                                ["hp_damage"] = 7,
                                ["hp_healing"] = 0,
                                ["shield_delta"] = 0,
                                ["shield_damage"] = 0,
                                ["shield_restored"] = 0,
                                ["killed"] = false,
                                ["revived"] = false,
                                ["moved"] = false,
                            },
                        },
                        ["log_lines"] = new GArray { "Hero 对 Enemy Sword 造成 7 点伤害。" },
                        ["report_entries"] = new GArray(),
                    },
                    ["action_traces"] = new GArray
                    {
                        new GDictionary
                        {
                            ["trace_id"] = "skill_1",
                            ["action_id"] = "skill_action",
                            ["block_reasons"] = new GDictionary { ["体力不足，无法施放该技能。"] = 1 },
                            ["blocked_count"] = 1,
                            ["candidate_count"] = 3,
                            ["top_candidates"] = new GArray
                            {
                                new GDictionary
                                {
                                    ["label"] = "a",
                                    ["total_score"] = 3,
                                    ["command"] = new GDictionary { ["command_type"] = "skill", ["skill_id"] = "a" },
                                    ["score_input"] = new GDictionary { ["total_score"] = 3 },
                                    ["screening_bonus"] = 45,
                                    ["screening_penalty"] = 0,
                                    ["screening_path_cost_delta"] = 1,
                                    ["screening_base_path_cost"] = 3,
                                    ["screening_blocked_path_cost"] = 4,
                                    ["screening_current_bonus"] = 0,
                                    ["screening_candidate_bonus"] = 45,
                                    ["screening_uncapped_bonus"] = 75,
                                    ["screening_threat_unit_id"] = "enemy_sword",
                                    ["screening_protected_unit_id"] = "hero_archer",
                                    ["screening_on_shortest_path"] = false,
                                    ["screening_keeps_contact"] = true,
                                    ["screening_can_counterattack"] = true,
                                    ["screening_hard_block"] = true,
                                    ["screening_distance_band_capped"] = true,
                                },
                                new GDictionary
                                {
                                    ["label"] = "b",
                                    ["total_score"] = 2,
                                    ["command"] = new GDictionary { ["command_type"] = "skill", ["skill_id"] = "b" },
                                    ["score_input"] = new GDictionary { ["total_score"] = 2 },
                                },
                                new GDictionary
                                {
                                    ["label"] = "c",
                                    ["total_score"] = 1,
                                    ["command"] = new GDictionary { ["command_type"] = "skill", ["skill_id"] = "c" },
                                    ["score_input"] = new GDictionary { ["total_score"] = 1 },
                                },
                            },
                        },
                    },
                },
                new GDictionary
                {
                    ["turn_started_tu"] = 15,
                    ["unit_id"] = "enemy",
                    ["unit_name"] = "Enemy",
                    ["faction_id"] = "hostile",
                    ["brain_id"] = "melee_aggressor",
                    ["state_id"] = "engage",
                    ["action_id"] = "attack",
                    ["reason_text"] = "attack",
                    ["command"] = new GDictionary
                    {
                        ["command_type"] = "skill",
                        ["unit_id"] = "enemy",
                        ["skill_id"] = "basic_attack",
                    },
                    ["score_input"] = new GDictionary
                    {
                        ["total_score"] = 10,
                        ["command_type"] = "skill",
                        ["skill_id"] = "basic_attack",
                    },
                    ["action_traces"] = new GArray(),
                },
            },
        };
    }

    private static GArray GetArray(GDictionary source, string key)
    {
        return source != null
            && source.ContainsKey(key)
            && source[key].VariantType == Variant.Type.Array
            ? source[key].AsGodotArray()
            : new GArray();
    }

    private static GDictionary GetDict(GDictionary source, string key)
    {
        return source != null
            && source.ContainsKey(key)
            && source[key].VariantType == Variant.Type.Dictionary
            ? source[key].AsGodotDictionary()
            : new GDictionary();
    }

    private static GDictionary GetSelfDict(Variant value)
    {
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static int GetInt(GDictionary source, string key, int fallback = 0)
    {
        return source != null && source.ContainsKey(key) ? source[key].AsInt32() : fallback;
    }

    private static bool GetBool(GDictionary source, string key)
    {
        return source != null && source.ContainsKey(key) && source[key].AsBool();
    }

    private static string GetString(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
            return "";
        Variant value = source[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => value.ToString(),
        };
    }

    private static List<StringName> ToStringNames(GArray values)
    {
        var result = new List<StringName>();
        foreach (Variant value in values)
        {
            string text = value.ToString();
            if (!string.IsNullOrEmpty(text))
                result.Add(text);
        }
        return result;
    }

    private static List<Vector2I> ToCoords(GArray values)
    {
        var result = new List<Vector2I>();
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.Vector2I)
                result.Add(value.AsVector2I());
        }
        return result;
    }

    private static Vector2I ParseCoord(string value)
    {
        if (string.IsNullOrEmpty(value))
            return new Vector2I(-1, -1);
        string trimmed = value.Trim().Trim('(', ')');
        string[] parts = trimmed.Split(',');
        if (parts.Length != 2)
            return new Vector2I(-1, -1);
        return int.TryParse(parts[0].Trim(), out int x) && int.TryParse(parts[1].Trim(), out int y)
            ? new Vector2I(x, y)
            : new Vector2I(-1, -1);
    }

}
