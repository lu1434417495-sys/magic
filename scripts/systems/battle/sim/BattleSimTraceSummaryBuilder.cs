using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleSimTraceSummaryBuilder : RefCounted
{
    private const string DefaultFocusFactionId = "player";
    private const int DefaultTopCandidatesPerAction = 2;

    public bool HasTraces(Dictionary report)
    {
        foreach (var entry in CollectRunEntries(report))
        {
            if (entry.TryGetValue("run", out Variant runValue) && runValue.VariantType == Variant.Type.Dictionary)
            {
                var runEntry = runValue.AsGodotDictionary();
                if (runEntry.TryGetValue("ai_turn_traces", out Variant tracesValue) && tracesValue.VariantType == Variant.Type.Array)
                {
                    var traceArray = tracesValue.AsGodotArray();
                    foreach (var traceEntry in traceArray)
                    {
                        if (traceEntry.VariantType == Variant.Type.Dictionary)
                            return true;
                    }
                }
            }
        }
        return false;
    }

    public Dictionary Build(Dictionary report, string sourceReportPath = "", Dictionary options = null)
    {
        options ??= new Dictionary();
        string focusFactionId = AsString(options.GetValueOrDefault("focus_faction_id", DefaultFocusFactionId));
        int topCandidateLimit = Mathf.Max((int)options.GetValueOrDefault("top_candidates_per_action", DefaultTopCandidatesPerAction), 0);

        var compactRuns = new Godot.Collections.Array();
        int traceCount = 0;
        foreach (var entry in CollectRunEntries(report))
        {
            var runEntry = entry.GetValueOrDefault("run", new Dictionary()).AsGodotDictionary();
            var compactRun = BuildCompactRunTrace(
                runEntry,
                AsString(entry.GetValueOrDefault("profile_id", "")),
                focusFactionId,
                topCandidateLimit
            );
            traceCount += (int)compactRun.GetValueOrDefault("trace_count", 0);
            compactRuns.Add(compactRun);
        }

        return new Dictionary
        {
            ["source_report"] = sourceReportPath,
            ["scenario"] = report.GetValueOrDefault("scenario", new Dictionary()),
            ["batch_id"] = (int)report.GetValueOrDefault("batch_id", 0),
            ["generated_at_unix"] = (int)report.GetValueOrDefault("generated_at_unix", 0),
            ["profile_count"] = CountProfiles(report),
            ["run_count"] = compactRuns.Count,
            ["trace_count"] = traceCount,
            ["elapsed_seconds"] = (float)report.GetValueOrDefault("elapsed_seconds", 0.0),
            ["ended_count"] = (int)report.GetValueOrDefault("ended_count", 0),
            ["avg_iterations"] = (float)report.GetValueOrDefault("avg_iterations", 0.0),
            ["avg_timeline_steps"] = (float)report.GetValueOrDefault("avg_timeline_steps", 0.0),
            ["win_rate"] = report.GetValueOrDefault("win_rate", new Dictionary()),
            ["comparisons"] = report.GetValueOrDefault("comparisons", new Array()),
            ["profile_summaries"] = CollectProfileSummaries(report),
            ["global"] = report.GetValueOrDefault("global", new Dictionary()),
            ["player"] = report.GetValueOrDefault("player", new Dictionary()),
            ["hostile"] = report.GetValueOrDefault("hostile", new Dictionary()),
            ["trace_compaction"] = new Dictionary
            {
                ["full_trace_embedded_in_source_report"] = true,
                ["focus_faction_id"] = focusFactionId,
                ["focus_turns_keep_action_trace_summaries"] = true,
                ["top_candidates_per_action_trace"] = topCandidateLimit,
            },
            ["runs"] = compactRuns,
        };
    }

    private List<Dictionary> CollectRunEntries(Dictionary report)
    {
        var entries = new List<Dictionary>();
        if (report.TryGetValue("profile_entries", out Variant profileEntriesValue) && profileEntriesValue.VariantType == Variant.Type.Array)
        {
            var profileEntries = profileEntriesValue.AsGodotArray();
            if (profileEntries.Count > 0)
            {
                foreach (var profileEntryVariant in profileEntries)
                {
                    if (profileEntryVariant.VariantType != Variant.Type.Dictionary)
                        continue;
                    var profileEntry = profileEntryVariant.AsGodotDictionary();
                    var profile = profileEntry.GetValueOrDefault("profile", new Dictionary()).AsGodotDictionary();
                    string profileId = AsString(profile.GetValueOrDefault("profile_id", ""));
                    if (profileEntry.TryGetValue("runs", out Variant runsValue) && runsValue.VariantType == Variant.Type.Array)
                    {
                        foreach (var runEntryVariant in runsValue.AsGodotArray())
                        {
                            if (runEntryVariant.VariantType == Variant.Type.Dictionary)
                            {
                                entries.Add(new Dictionary
                                {
                                    ["profile_id"] = profileId,
                                    ["run"] = runEntryVariant.AsGodotDictionary(),
                                });
                            }
                        }
                    }
                }
                return entries;
            }
        }
        if (report.TryGetValue("runs", out Variant runsValue2) && runsValue2.VariantType == Variant.Type.Array)
        {
            foreach (var runEntryVariant in runsValue2.AsGodotArray())
            {
                if (runEntryVariant.VariantType == Variant.Type.Dictionary)
                {
                    var runEntry = runEntryVariant.AsGodotDictionary();
                    entries.Add(new Dictionary
                    {
                        ["profile_id"] = AsString(runEntry.GetValueOrDefault("profile_id", "")),
                        ["run"] = runEntry,
                    });
                }
            }
        }
        return entries;
    }

    private int CountProfiles(Dictionary report)
    {
        if (report.TryGetValue("profile_entries", out Variant profileEntriesValue) && profileEntriesValue.VariantType == Variant.Type.Array)
            return profileEntriesValue.AsGodotArray().Count;
        return 0;
    }

    private Godot.Collections.Array CollectProfileSummaries(Dictionary report)
    {
        var summaries = new Array();
        if (report.TryGetValue("profile_entries", out Variant profileEntriesValue) && profileEntriesValue.VariantType == Variant.Type.Array)
        {
            foreach (var profileEntryVariant in profileEntriesValue.AsGodotArray())
            {
                if (profileEntryVariant.VariantType != Variant.Type.Dictionary)
                    continue;
                var profileEntry = profileEntryVariant.AsGodotDictionary();
                summaries.Add(new Dictionary
                {
                    ["profile"] = profileEntry.GetValueOrDefault("profile", new Dictionary()),
                    ["summary"] = profileEntry.GetValueOrDefault("summary", new Dictionary()),
                });
            }
        }
        return summaries;
    }

    private Dictionary BuildCompactRunTrace(
        Dictionary runEntry,
        string profileId,
        string focusFactionId,
        int topCandidateLimit
    )
    {
        var actionCountsByFaction = new Dictionary();
        var commandCountsByFaction = new Dictionary();
        var blockReasonsByFaction = new Dictionary();
        var waitCountsByFaction = new Dictionary();
        var focusTurns = new Array();
        var focusWaitTurns = new Array();
        int traceCount = 0;

        if (runEntry.TryGetValue("ai_turn_traces", out Variant tracesValue) && tracesValue.VariantType == Variant.Type.Array)
        {
            foreach (var traceEntryVariant in tracesValue.AsGodotArray())
            {
                if (traceEntryVariant.VariantType != Variant.Type.Dictionary)
                    continue;
                var traceEntry = traceEntryVariant.AsGodotDictionary();
                traceCount++;
                string factionId = AsString(traceEntry.GetValueOrDefault("faction_id", ""));
                string actionId = AsString(traceEntry.GetValueOrDefault("action_id", ""));
                var commandSummary = SummarizeTraceCommand(traceEntry.GetValueOrDefault("command", new Dictionary()));
                string commandType = AsString(commandSummary.GetValueOrDefault("command_type", ""));

                IncrementNestedCounter(actionCountsByFaction, factionId, actionId);
                IncrementNestedCounter(commandCountsByFaction, factionId, commandType);
                if (commandType == "wait")
                    IncrementNestedCounter(waitCountsByFaction, factionId, actionId);

                var actionTraces = SummarizeActionTraces(
                    traceEntry.GetValueOrDefault("action_traces", new Array()),
                    factionId,
                    blockReasonsByFaction,
                    topCandidateLimit
                );

                if (factionId != focusFactionId)
                    continue;

                var turnSummary = new Dictionary
                {
                    ["turn_started_tu"] = (int)traceEntry.GetValueOrDefault("turn_started_tu", -1),
                    ["unit_id"] = AsString(traceEntry.GetValueOrDefault("unit_id", "")),
                    ["unit_name"] = AsString(traceEntry.GetValueOrDefault("unit_name", "")),
                    ["faction_id"] = factionId,
                    ["brain_id"] = AsString(traceEntry.GetValueOrDefault("brain_id", "")),
                    ["state_id"] = AsString(traceEntry.GetValueOrDefault("state_id", "")),
                    ["action_id"] = actionId,
                    ["reason_text"] = AsString(traceEntry.GetValueOrDefault("reason_text", "")),
                    ["command"] = commandSummary,
                    ["score"] = SummarizeScoreInput(traceEntry.GetValueOrDefault("score_input", new Dictionary())),
                    ["decision_target_snapshots"] = SummarizeUnitSnapshots(traceEntry.GetValueOrDefault("decision_target_snapshots", new Array())),
                    ["execution_result"] = SummarizeExecutionResult(traceEntry.GetValueOrDefault("execution_result", new Dictionary())),
                    ["action_traces"] = actionTraces,
                };
                focusTurns.Add(turnSummary);
                if (commandType == "wait")
                    focusWaitTurns.Add(turnSummary);
            }
        }

        var factionsValue = runEntry.GetValueOrDefault("factions", new Dictionary());
        if (factionsValue.VariantType != Variant.Type.Dictionary)
        {
            if (runEntry.TryGetValue("metrics", out Variant metricsValue) && metricsValue.VariantType == Variant.Type.Dictionary)
            {
                var metrics = metricsValue.AsGodotDictionary();
                factionsValue = metrics.GetValueOrDefault("factions", new Dictionary());
            }
        }

        var unitsValue = runEntry.GetValueOrDefault("units", new Dictionary());
        if (unitsValue.VariantType != Variant.Type.Dictionary)
        {
            if (runEntry.TryGetValue("metrics", out Variant metricsValue) && metricsValue.VariantType == Variant.Type.Dictionary)
            {
                var metrics = metricsValue.AsGodotDictionary();
                unitsValue = metrics.GetValueOrDefault("units", new Dictionary());
            }
        }

        return new Dictionary
        {
            ["profile_id"] = profileId,
            ["run_index"] = (int)runEntry.GetValueOrDefault("run_index", 0),
            ["seed"] = (int)runEntry.GetValueOrDefault("seed", 0),
            ["battle_ended"] = (bool)runEntry.GetValueOrDefault("battle_ended", false),
            ["winner_faction_id"] = AsString(runEntry.GetValueOrDefault("winner_faction_id", "")),
            ["final_tu"] = (int)runEntry.GetValueOrDefault("final_tu", 0),
            ["iterations"] = (int)runEntry.GetValueOrDefault("iterations", 0),
            ["timeline_steps"] = (int)runEntry.GetValueOrDefault("timeline_steps", 0),
            ["trace_count"] = traceCount,
            ["factions"] = factionsValue,
            ["units"] = unitsValue,
            ["action_counts_by_faction"] = actionCountsByFaction,
            ["command_counts_by_faction"] = commandCountsByFaction,
            ["wait_counts_by_faction"] = waitCountsByFaction,
            ["block_reasons_by_faction"] = blockReasonsByFaction,
            ["focus_turns"] = focusTurns,
            ["focus_wait_turns"] = focusWaitTurns,
        };
    }

    private Godot.Collections.Array SummarizeActionTraces(
        Variant actionTracesValue,
        string factionId,
        Dictionary blockReasonsByFaction,
        int topCandidateLimit
    )
    {
        var summaries = new Array();
        if (actionTracesValue.VariantType != Variant.Type.Array)
            return summaries;

        foreach (var actionTraceVariant in actionTracesValue.AsGodotArray())
        {
            if (actionTraceVariant.VariantType != Variant.Type.Dictionary)
                continue;
            var actionTrace = actionTraceVariant.AsGodotDictionary();
            var blockReasons = actionTrace.GetValueOrDefault("block_reasons", new Dictionary()).AsGodotDictionary();
            foreach (var reasonKey in blockReasons.Keys)
            {
                IncrementNestedCounter(blockReasonsByFaction, factionId, AsString(reasonKey), (int)blockReasons.GetValueOrDefault(reasonKey, 0));
            }
            summaries.Add(new Dictionary
            {
                ["trace_id"] = AsString(actionTrace.GetValueOrDefault("trace_id", "")),
                ["action_id"] = AsString(actionTrace.GetValueOrDefault("action_id", "")),
                ["chosen"] = (bool)actionTrace.GetValueOrDefault("chosen", false),
                ["score_bucket_id"] = AsString(actionTrace.GetValueOrDefault("score_bucket_id", "")),
                ["metadata"] = actionTrace.GetValueOrDefault("metadata", new Dictionary()),
                ["block_reasons"] = blockReasons,
                ["blocked_count"] = (int)actionTrace.GetValueOrDefault("blocked_count", 0),
                ["candidate_count"] = (int)actionTrace.GetValueOrDefault("candidate_count", 0),
                ["evaluation_count"] = (int)actionTrace.GetValueOrDefault("evaluation_count", 0),
                ["preview_reject_count"] = (int)actionTrace.GetValueOrDefault("preview_reject_count", 0),
                ["top_candidates"] = SummarizeTopCandidates(actionTrace.GetValueOrDefault("top_candidates", new Array()), topCandidateLimit),
            });
        }
        return summaries;
    }

    private Godot.Collections.Array SummarizeTopCandidates(Variant candidatesValue, int limit)
    {
        var summaries = new Array();
        if (candidatesValue.VariantType != Variant.Type.Array)
            return summaries;

        foreach (var candidateVariant in candidatesValue.AsGodotArray())
        {
            if (candidateVariant.VariantType != Variant.Type.Dictionary)
                continue;
            if (summaries.Count >= limit)
                break;
            var candidate = candidateVariant.AsGodotDictionary();
            var scoreSummary = SummarizeScoreInput(candidate.GetValueOrDefault("score_input", new Dictionary()));
            var candidateSummary = new Dictionary
            {
                ["label"] = AsString(candidate.GetValueOrDefault("label", "")),
                ["total_score"] = (int)candidate.GetValueOrDefault("total_score", scoreSummary.GetValueOrDefault("total_score", 0)),
                ["predicted_distance"] = candidate.ContainsKey("predicted_distance") ? (int)candidate.GetValueOrDefault("predicted_distance", -1) : -1,
                ["command"] = SummarizeTraceCommand(candidate.GetValueOrDefault("command", new Dictionary())),
                ["score"] = scoreSummary,
            };
            CopyOptionalCandidateInt(candidateSummary, candidate, "screening_bonus");
            CopyOptionalCandidateInt(candidateSummary, candidate, "screening_penalty");
            CopyOptionalCandidateInt(candidateSummary, candidate, "screening_path_cost_delta");
            CopyOptionalCandidateInt(candidateSummary, candidate, "screening_base_path_cost");
            CopyOptionalCandidateInt(candidateSummary, candidate, "screening_blocked_path_cost");
            CopyOptionalCandidateInt(candidateSummary, candidate, "screening_current_bonus");
            CopyOptionalCandidateInt(candidateSummary, candidate, "screening_candidate_bonus");
            CopyOptionalCandidateInt(candidateSummary, candidate, "screening_uncapped_bonus");
            CopyOptionalCandidateString(candidateSummary, candidate, "screening_threat_unit_id");
            CopyOptionalCandidateString(candidateSummary, candidate, "screening_protected_unit_id");
            CopyOptionalCandidateBool(candidateSummary, candidate, "screening_on_shortest_path");
            CopyOptionalCandidateBool(candidateSummary, candidate, "screening_keeps_contact");
            CopyOptionalCandidateBool(candidateSummary, candidate, "screening_can_counterattack");
            CopyOptionalCandidateBool(candidateSummary, candidate, "screening_hard_block");
            CopyOptionalCandidateBool(candidateSummary, candidate, "screening_distance_band_capped");
            summaries.Add(candidateSummary);
        }
        return summaries;
    }

    private void CopyOptionalCandidateInt(Dictionary target, Dictionary source, string key)
    {
        if (source.ContainsKey(key))
            target[key] = (int)source[key];
    }

    private void CopyOptionalCandidateString(Dictionary target, Dictionary source, string key)
    {
        if (source.ContainsKey(key))
            target[key] = AsString(source[key]);
    }

    private void CopyOptionalCandidateBool(Dictionary target, Dictionary source, string key)
    {
        if (source.ContainsKey(key))
            target[key] = (bool)source[key];
    }

    private Dictionary SummarizeTraceCommand(Variant commandValue)
    {
        if (commandValue.VariantType != Variant.Type.Dictionary)
            return new Dictionary();
        var command = commandValue.AsGodotDictionary();
        return new Dictionary
        {
            ["command_type"] = AsString(command.GetValueOrDefault("command_type", "")),
            ["unit_id"] = AsString(command.GetValueOrDefault("unit_id", "")),
            ["skill_id"] = AsString(command.GetValueOrDefault("skill_id", "")),
            ["skill_variant_id"] = AsString(command.GetValueOrDefault("skill_variant_id", "")),
            ["target_unit_id"] = AsString(command.GetValueOrDefault("target_unit_id", "")),
            ["target_unit_ids"] = StringifyArray(command.GetValueOrDefault("target_unit_ids", new Array())),
            ["target_coord"] = AsString(command.GetValueOrDefault("target_coord", "")),
            ["target_coords"] = StringifyArray(command.GetValueOrDefault("target_coords", new Array())),
        };
    }

    private Dictionary SummarizeExecutionResult(Variant resultValue)
    {
        if (resultValue.VariantType != Variant.Type.Dictionary)
            return new Dictionary();
        var result = resultValue.AsGodotDictionary();
        return new Dictionary
        {
            ["command_type"] = AsString(result.GetValueOrDefault("command_type", "")),
            ["skill_id"] = AsString(result.GetValueOrDefault("skill_id", "")),
            ["skill_variant_id"] = AsString(result.GetValueOrDefault("skill_variant_id", "")),
            ["changed_unit_ids"] = StringifyArray(result.GetValueOrDefault("changed_unit_ids", new Array())),
            ["tracked_unit_ids"] = StringifyArray(result.GetValueOrDefault("tracked_unit_ids", new Array())),
            ["unit_results"] = SummarizeUnitResults(result.GetValueOrDefault("unit_results", new Array())),
            ["log_lines"] = StringifyArray(result.GetValueOrDefault("log_lines", new Array())),
            ["report_entries"] = result.GetValueOrDefault("report_entries", new Array()),
        };
    }

    private Godot.Collections.Array SummarizeUnitResults(Variant resultsValue)
    {
        var summaries = new Array();
        if (resultsValue.VariantType != Variant.Type.Array)
            return summaries;

        foreach (var resultVariant in resultsValue.AsGodotArray())
        {
            if (resultVariant.VariantType != Variant.Type.Dictionary)
                continue;
            var result = resultVariant.AsGodotDictionary();
            summaries.Add(new Dictionary
            {
                ["unit_id"] = AsString(result.GetValueOrDefault("unit_id", "")),
                ["before"] = SummarizeUnitSnapshot(result.GetValueOrDefault("before", new Dictionary())),
                ["after"] = SummarizeUnitSnapshot(result.GetValueOrDefault("after", new Dictionary())),
                ["hp_delta"] = (int)result.GetValueOrDefault("hp_delta", 0),
                ["hp_damage"] = (int)result.GetValueOrDefault("hp_damage", 0),
                ["hp_healing"] = (int)result.GetValueOrDefault("hp_healing", 0),
                ["shield_delta"] = (int)result.GetValueOrDefault("shield_delta", 0),
                ["shield_damage"] = (int)result.GetValueOrDefault("shield_damage", 0),
                ["shield_restored"] = (int)result.GetValueOrDefault("shield_restored", 0),
                ["killed"] = (bool)result.GetValueOrDefault("killed", false),
                ["revived"] = (bool)result.GetValueOrDefault("revived", false),
                ["moved"] = (bool)result.GetValueOrDefault("moved", false),
            });
        }
        return summaries;
    }

    private Godot.Collections.Array SummarizeUnitSnapshots(Variant snapshotsValue)
    {
        var summaries = new Array();
        if (snapshotsValue.VariantType != Variant.Type.Array)
            return summaries;

        foreach (var snapshotVariant in snapshotsValue.AsGodotArray())
        {
            var summary = SummarizeUnitSnapshot(snapshotVariant);
            if (summary.Count > 0)
                summaries.Add(summary);
        }
        return summaries;
    }

    private Dictionary SummarizeUnitSnapshot(Variant snapshotValue)
    {
        if (snapshotValue.VariantType != Variant.Type.Dictionary)
            return new Dictionary();
        var snapshot = snapshotValue.AsGodotDictionary();
        return new Dictionary
        {
            ["unit_id"] = AsString(snapshot.GetValueOrDefault("unit_id", "")),
            ["display_name"] = AsString(snapshot.GetValueOrDefault("display_name", "")),
            ["faction_id"] = AsString(snapshot.GetValueOrDefault("faction_id", "")),
            ["coord"] = AsString(snapshot.GetValueOrDefault("coord", "")),
            ["alive"] = (bool)snapshot.GetValueOrDefault("alive", false),
            ["hp"] = (int)snapshot.GetValueOrDefault("hp", 0),
            ["hp_max"] = (int)snapshot.GetValueOrDefault("hp_max", 0),
            ["shield_hp"] = (int)snapshot.GetValueOrDefault("shield_hp", 0),
            ["shield_max_hp"] = (int)snapshot.GetValueOrDefault("shield_max_hp", 0),
            ["ap"] = (int)snapshot.GetValueOrDefault("ap", 0),
            ["move_points"] = (int)snapshot.GetValueOrDefault("move_points", 0),
        };
    }

    private Dictionary SummarizeScoreInput(Variant scoreValue)
    {
        if (scoreValue.VariantType != Variant.Type.Dictionary)
            return new Dictionary();
        var score = scoreValue.AsGodotDictionary();
        var result = new Dictionary
        {
            ["total_score"] = (int)score.GetValueOrDefault("total_score", 0),
            ["score_bucket_id"] = AsString(score.GetValueOrDefault("score_bucket_id", "")),
            ["score_bucket_priority"] = (int)score.GetValueOrDefault("score_bucket_priority", 0),
            ["command_type"] = AsString(score.GetValueOrDefault("command_type", "")),
            ["skill_id"] = AsString(score.GetValueOrDefault("skill_id", "")),
            ["target_count"] = (int)score.GetValueOrDefault("target_count", 0),
            ["effective_target_count"] = (int)score.GetValueOrDefault("effective_target_count", 0),
            ["enemy_target_count"] = (int)score.GetValueOrDefault("enemy_target_count", 0),
            ["ally_target_count"] = (int)score.GetValueOrDefault("ally_target_count", 0),
            ["target_unit_ids"] = StringifyArray(score.GetValueOrDefault("target_unit_ids", new Array())),
            ["target_coords"] = StringifyArray(score.GetValueOrDefault("target_coords", new Array())),
            ["estimated_damage"] = (int)score.GetValueOrDefault("estimated_damage", 0),
            ["estimated_hit_rate_percent"] = (int)score.GetValueOrDefault("estimated_hit_rate_percent", 0),
            ["save_estimates_by_target_id"] = SummarizeSaveEstimatesByTargetId(score.GetValueOrDefault("save_estimates_by_target_id", new Dictionary())),
            ["estimated_lethal_target_count"] = (int)score.GetValueOrDefault("estimated_lethal_target_count", 0),
            ["estimated_lethal_threat_target_count"] = (int)score.GetValueOrDefault("estimated_lethal_threat_target_count", 0),
            ["estimated_lethal_target_ids"] = StringifyArray(score.GetValueOrDefault("estimated_lethal_target_ids", new Array())),
            ["estimated_lethal_threat_target_ids"] = StringifyArray(score.GetValueOrDefault("estimated_lethal_threat_target_ids", new Array())),
            ["estimated_control_target_ids"] = StringifyArray(score.GetValueOrDefault("estimated_control_target_ids", new Array())),
            ["estimated_control_threat_target_ids"] = StringifyArray(score.GetValueOrDefault("estimated_control_threat_target_ids", new Array())),
            ["has_post_action_threat_projection"] = (bool)score.GetValueOrDefault("has_post_action_threat_projection", false),
            ["projected_actor_coord"] = AsString(score.GetValueOrDefault("projected_actor_coord", "")),
            ["pre_action_threat_unit_ids"] = StringifyArray(score.GetValueOrDefault("pre_action_threat_unit_ids", new Array())),
            ["pre_action_threat_count"] = (int)score.GetValueOrDefault("pre_action_threat_count", 0),
            ["pre_action_threat_expected_damage"] = (int)score.GetValueOrDefault("pre_action_threat_expected_damage", 0),
            ["pre_action_survival_margin"] = (int)score.GetValueOrDefault("pre_action_survival_margin", 0),
            ["pre_action_is_lethal_survival_risk"] = (bool)score.GetValueOrDefault("pre_action_is_lethal_survival_risk", false),
            ["post_action_remaining_threat_unit_ids"] = StringifyArray(score.GetValueOrDefault("post_action_remaining_threat_unit_ids", new Array())),
            ["post_action_remaining_threat_count"] = (int)score.GetValueOrDefault("post_action_remaining_threat_count", 0),
            ["post_action_remaining_threat_expected_damage"] = (int)score.GetValueOrDefault("post_action_remaining_threat_expected_damage", 0),
            ["post_action_survival_margin"] = (int)score.GetValueOrDefault("post_action_survival_margin", 0),
            ["post_action_is_lethal_survival_risk"] = (bool)score.GetValueOrDefault("post_action_is_lethal_survival_risk", false),
            ["hit_payoff_score"] = (int)score.GetValueOrDefault("hit_payoff_score", 0),
            ["position_objective_kind"] = AsString(score.GetValueOrDefault("position_objective_kind", "")),
            ["position_objective_score"] = (int)score.GetValueOrDefault("position_objective_score", 0),
            ["resource_cost_score"] = (int)score.GetValueOrDefault("resource_cost_score", 0),
            ["distance_to_primary_coord"] = (int)score.GetValueOrDefault("distance_to_primary_coord", -1),
            ["ap_cost"] = (int)score.GetValueOrDefault("ap_cost", 0),
            ["stamina_cost"] = (int)score.GetValueOrDefault("stamina_cost", 0),
            ["mp_cost"] = (int)score.GetValueOrDefault("mp_cost", 0),
            ["aura_cost"] = (int)score.GetValueOrDefault("aura_cost", 0),
            ["move_cost"] = (int)score.GetValueOrDefault("move_cost", 0),
        };
        return result;
    }

    private Dictionary SummarizeSaveEstimatesByTargetId(Variant value)
    {
        var summary = new Dictionary();
        if (value.VariantType != Variant.Type.Dictionary)
            return summary;
        var estimatesByTarget = value.AsGodotDictionary();
        var targetKeys = estimatesByTarget.Keys.Cast<Variant>().ToList();
        targetKeys.Sort((a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal));

        foreach (var targetKey in targetKeys)
        {
            string targetKeyStr = AsString(targetKey);
            var estimatesValue = estimatesByTarget.GetValueOrDefault(targetKey, new Array());
            if (estimatesValue.VariantType != Variant.Type.Array)
                continue;
            var compactEstimates = new Array();
            foreach (var estimateVariant in estimatesValue.AsGodotArray())
            {
                if (estimateVariant.VariantType != Variant.Type.Dictionary)
                    continue;
                var estimate = estimateVariant.AsGodotDictionary();
                compactEstimates.Add(new Dictionary
                {
                    ["damage_before_save"] = (int)estimate.GetValueOrDefault("damage_before_save", 0),
                    ["damage_after_save_estimate"] = (int)estimate.GetValueOrDefault("damage_after_save_estimate", 0),
                    ["damage_on_save_success"] = (int)estimate.GetValueOrDefault("damage_on_save_success", 0),
                    ["save_success_rate_percent"] = (int)estimate.GetValueOrDefault("save_success_rate_percent", 0),
                    ["dc"] = (int)estimate.GetValueOrDefault("dc", 0),
                    ["ability"] = AsString(estimate.GetValueOrDefault("ability", "")),
                    ["save_tag"] = AsString(estimate.GetValueOrDefault("save_tag", "")),
                    ["advantage_state"] = AsString(estimate.GetValueOrDefault("advantage_state", "")),
                    ["immune"] = (bool)estimate.GetValueOrDefault("immune", false),
                    ["hit_count"] = (int)estimate.GetValueOrDefault("hit_count", 1),
                });
            }
            if (compactEstimates.Count > 0)
                summary[targetKeyStr] = compactEstimates;
        }
        return summary;
    }

    private void IncrementNestedCounter(Dictionary counters, string outerKey, string innerKey, int amount = 1)
    {
        if (string.IsNullOrEmpty(outerKey) || string.IsNullOrEmpty(innerKey) || amount == 0)
            return;
        var inner = counters.GetValueOrDefault(outerKey, new Dictionary()).AsGodotDictionary();
        inner[innerKey] = (int)inner.GetValueOrDefault(innerKey, 0) + amount;
        counters[outerKey] = inner;
    }

    private Godot.Collections.Array StringifyArray(Variant value)
    {
        var results = new Array();
        if (value.VariantType != Variant.Type.Array)
            return results;
        foreach (var entry in value.AsGodotArray())
        {
            results.Add(AsString(entry));
        }
        return results;
    }

    private string AsString(Variant value)
    {
        if (value.VariantType == Variant.Type.Nil)
            return "";
        return value.ToString();
    }
}
