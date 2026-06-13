using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

public sealed class BattleSimTraceSummaryBuilder
{
    private const string DefaultFocusFactionId = "player";

    private const int DefaultTopCandidatesPerAction = 2;

    public bool HasTraces(BattleSimScenarioReport report)
    {
        if (report == null)
            return false;

        foreach (BattleSimProfileReportEntry profileEntry in report.ProfileEntries)
        {
            if (profileEntry == null)
                continue;

            foreach (BattleSimRunReport run in profileEntry.Runs)
            {
                if (run?.AiTurnTraces == null)
                    continue;

                foreach (BattleAiTurnTraceProjection traceEntry in run.AiTurnTraces)
                {
                    if (traceEntry != null)
                        return true;
                }
            }
        }

        return false;
    }

    public Dictionary Build(
        BattleSimScenarioReport report,
        string sourceReportPath = "",
        TraceSummaryOptionsData options = null
    )
    {
        TraceSummaryOptionsData summaryOptions = options ?? new TraceSummaryOptionsData();
        var compactRuns = new List<CompactRunTraceData>();
        int traceCount = 0;
        if (report != null)
        {
            foreach (BattleSimProfileReportEntry profileEntry in report.ProfileEntries)
            {
                if (profileEntry == null)
                    continue;

                string profileId = profileEntry.Profile?.profile_id.ToString() ?? "";
                foreach (BattleSimRunReport run in profileEntry.Runs)
                {
                    if (run == null)
                        continue;

                    CompactRunTraceData compactRun = BuildCompactRunTraceData(
                        run,
                        profileId,
                        summaryOptions.ResolvedFocusFactionId,
                        summaryOptions.ResolvedTopCandidateLimit
                    );
                    traceCount += compactRun.TraceCount;
                    compactRuns.Add(compactRun);
                }
            }
        }

        var comparisons = new Godot.Collections.Array();
        var profileSummaries = new Godot.Collections.Array();
        if (report != null)
        {
            foreach (BattleSimProfileComparison comparison in report.Comparisons)
                comparisons.Add(comparison?.ToDictionary() ?? new Dictionary());
            foreach (BattleSimProfileReportEntry entry in report.ProfileEntries)
            {
                profileSummaries.Add(
                    new Dictionary
                    {
                        ["profile"] = entry?.Profile?.ToDict() ?? new Dictionary(),
                        ["summary"] = entry?.Summary?.ToDictionary() ?? new Dictionary(),
                    }
                );
            }
        }

        return new Dictionary
        {
            ["source_report"] = sourceReportPath,
            ["scenario"] = report?.ScenarioDef?.ToDictionary() ?? new Dictionary(),
            ["batch_id"] = 0,
            ["generated_at_unix"] = report?.GeneratedAtUnix ?? 0,
            ["profile_count"] = report?.ProfileEntries.Count ?? 0,
            ["run_count"] = compactRuns.Count,
            ["trace_count"] = traceCount,
            ["elapsed_seconds"] = 0.0f,
            ["ended_count"] = 0,
            ["avg_iterations"] = 0.0f,
            ["avg_timeline_steps"] = 0.0f,
            ["win_rate"] = new Dictionary(),
            ["comparisons"] = comparisons,
            ["profile_summaries"] = profileSummaries,
            ["global"] = new Dictionary(),
            ["player"] = new Dictionary(),
            ["hostile"] = new Dictionary(),
            ["trace_compaction"] = summaryOptions.ToDictionary(),
            ["runs"] = ToGodotArray(compactRuns),
        };
    }

    private CompactRunTraceData BuildCompactRunTraceData(
        BattleSimRunReport runEntry,
        string profileId,
        string focusFactionId,
        int topCandidateLimit
    )
    {
        var result = new CompactRunTraceData
        {
            ProfileId = profileId,
            RunIndex = 0,
            Seed = runEntry?.Seed ?? 0,
            BattleEnded = runEntry?.BattleEnded ?? false,
            WinnerFactionId = runEntry?.WinnerFactionId ?? "",
            FinalTu = runEntry?.FinalTu ?? 0,
            Iterations = runEntry?.Iterations ?? 0,
            TimelineSteps = runEntry?.TimelineSteps ?? 0,
        };

        if (runEntry?.AiTurnTraces != null)
        {
            foreach (BattleAiTurnTraceProjection traceEntryProjection in runEntry.AiTurnTraces)
            {
                if (traceEntryProjection == null)
                    continue;
                Dictionary traceEntry = TraceDictionaryProjection.ToDictionary(
                    traceEntryProjection.ToTraceDictionary()
                );
                result.TraceCount++;
                string factionId = ReadString(traceEntry, "faction_id");
                string actionId = ReadString(traceEntry, "action_id");
                CompactCommandSummaryData commandSummary = SummarizeTraceCommandData(
                    ReadDictionary(traceEntry, "command")
                );
                string commandType = commandSummary.CommandType;

                IncrementNestedCounterData(result.ActionCountsByFaction, factionId, actionId);
                IncrementNestedCounterData(result.CommandCountsByFaction, factionId, commandType);
                if (commandType == "wait")
                    IncrementNestedCounterData(result.WaitCountsByFaction, factionId, actionId);

                List<CompactActionTraceData> actionTraces = SummarizeActionTracesData(
                    ReadArray(traceEntry, "action_traces"),
                    factionId,
                    result.BlockReasonsByFaction,
                    topCandidateLimit
                );

                if (factionId != focusFactionId)
                    continue;

                var turnSummary = new CompactTurnTraceData
                {
                    TurnStartedTu = ReadInt(traceEntry, "turn_started_tu", -1),
                    UnitId = ReadString(traceEntry, "unit_id"),
                    UnitName = ReadString(traceEntry, "unit_name"),
                    FactionId = factionId,
                    BrainId = ReadString(traceEntry, "brain_id"),
                    StateId = ReadString(traceEntry, "state_id"),
                    ActionId = actionId,
                    ReasonText = ReadString(traceEntry, "reason_text"),
                    Command = commandSummary,
                    Score = SummarizeScoreInputData(traceEntryProjection.ScoreInput),
                    DecisionTargetSnapshots = SummarizeUnitSnapshotsData(
                        ReadArray(traceEntry, "decision_target_snapshots")
                    ),
                    ExecutionResult = SummarizeExecutionResultData(
                        ReadDictionary(traceEntry, "execution_result")
                    ),
                    ActionTraces = actionTraces,
                };
                result.FocusTurns.Add(turnSummary);
                if (commandType == "wait")
                    result.FocusWaitTurns.Add(turnSummary);
            }
        }

        if (runEntry?.Metrics != null)
        {
            if (TryDictionaryValue(runEntry.Metrics, "factions", out Dictionary metricFactions))
                result.Factions = metricFactions;
            if (TryDictionaryValue(runEntry.Metrics, "units", out Dictionary metricUnits))
                result.Units = metricUnits;
        }

        return result;
    }

    private List<CompactActionTraceData> SummarizeActionTracesData(
        object actionTracesValue,
        string factionId,
        System.Collections.Generic.Dictionary<
            string,
            System.Collections.Generic.Dictionary<string, int>
        > blockReasonsByFaction,
        int topCandidateLimit
    )
    {
        var summaries = new List<CompactActionTraceData>();
        if (!TryRawArray(actionTracesValue, out Godot.Collections.Array actionTraces))
            return summaries;

        foreach (object actionTraceValue in actionTraces)
        {
            if (!TryRawDictionary(actionTraceValue, out Dictionary actionTrace))
                continue;

            Dictionary blockReasons = ReadDictionary(actionTrace, "block_reasons");
            foreach (Variant reasonKey in blockReasons.Keys)
            {
                IncrementNestedCounterData(
                    blockReasonsByFaction,
                    factionId,
                    AsString(reasonKey),
                    ReadInt(blockReasons, reasonKey, 0)
                );
            }

            summaries.Add(
                new CompactActionTraceData
                {
                    TraceId = ReadString(actionTrace, "trace_id"),
                    ActionId = ReadString(actionTrace, "action_id"),
                    Chosen = ReadBool(actionTrace, "chosen"),
                    ScoreBucketId = ReadString(actionTrace, "score_bucket_id"),
                    Metadata = ReadDictionary(actionTrace, "metadata"),
                    BlockReasons = ToStringIntMap(blockReasons),
                    BlockedCount = ReadInt(actionTrace, "blocked_count", 0),
                    CandidateCount = ReadInt(actionTrace, "candidate_count", 0),
                    EvaluationCount = ReadInt(actionTrace, "evaluation_count", 0),
                    PreviewRejectCount = ReadInt(actionTrace, "preview_reject_count", 0),
                    TopCandidates = SummarizeTopCandidatesData(
                        ReadArray(actionTrace, "top_candidates"),
                        topCandidateLimit
                    ),
                }
            );
        }

        return summaries;
    }

    private List<CompactTopCandidateData> SummarizeTopCandidatesData(
        object candidatesValue,
        int limit
    )
    {
        var summaries = new List<CompactTopCandidateData>();
        if (!TryRawArray(candidatesValue, out Godot.Collections.Array candidates))
            return summaries;

        foreach (object candidateValue in candidates)
        {
            if (!TryRawDictionary(candidateValue, out Dictionary candidate))
                continue;
            if (summaries.Count >= limit)
                break;

            CompactScoreInputData scoreSummary = SummarizeScoreInputData(
                ReadDictionary(candidate, "score_input")
            );
            var candidateSummary = new CompactTopCandidateData
            {
                Label = ReadString(candidate, "label"),
                TotalScore = ReadInt(candidate, "total_score", scoreSummary.TotalScore),
                PredictedDistance = candidate.ContainsKey("predicted_distance")
                    ? ReadInt(candidate, "predicted_distance", -1)
                    : -1,
                Command = SummarizeTraceCommandData(
                    ReadDictionary(candidate, "command")
                ),
                Score = scoreSummary,
            };
            CopyOptionalCandidateIntData(candidateSummary, candidate, "screening_bonus");
            CopyOptionalCandidateIntData(candidateSummary, candidate, "screening_penalty");
            CopyOptionalCandidateIntData(candidateSummary, candidate, "screening_path_cost_delta");
            CopyOptionalCandidateIntData(candidateSummary, candidate, "screening_base_path_cost");
            CopyOptionalCandidateIntData(candidateSummary, candidate, "screening_blocked_path_cost");
            CopyOptionalCandidateIntData(candidateSummary, candidate, "screening_current_bonus");
            CopyOptionalCandidateIntData(candidateSummary, candidate, "screening_candidate_bonus");
            CopyOptionalCandidateIntData(candidateSummary, candidate, "screening_uncapped_bonus");
            CopyOptionalCandidateStringData(candidateSummary, candidate, "screening_threat_unit_id");
            CopyOptionalCandidateStringData(
                candidateSummary,
                candidate,
                "screening_protected_unit_id"
            );
            CopyOptionalCandidateBoolData(candidateSummary, candidate, "screening_on_shortest_path");
            CopyOptionalCandidateBoolData(candidateSummary, candidate, "screening_keeps_contact");
            CopyOptionalCandidateBoolData(candidateSummary, candidate, "screening_can_counterattack");
            CopyOptionalCandidateBoolData(candidateSummary, candidate, "screening_hard_block");
            CopyOptionalCandidateBoolData(
                candidateSummary,
                candidate,
                "screening_distance_band_capped"
            );
            summaries.Add(candidateSummary);
        }

        return summaries;
    }

    private CompactCommandSummaryData SummarizeTraceCommandData(object commandValue)
    {
        if (!TryRawDictionary(commandValue, out Dictionary command))
            return new CompactCommandSummaryData();

        return new CompactCommandSummaryData
        {
            CommandType = ReadString(command, "command_type"),
            UnitId = ReadString(command, "unit_id"),
            SkillId = ReadString(command, "skill_id"),
            SkillVariantId = ReadString(command, "skill_variant_id"),
            TargetUnitId = ReadString(command, "target_unit_id"),
            TargetUnitIds = StringifyArrayList(ReadArray(command, "target_unit_ids")),
            TargetCoord = ReadString(command, "target_coord"),
            TargetCoords = StringifyArrayList(ReadArray(command, "target_coords")),
        };
    }

    private CompactExecutionResultData SummarizeExecutionResultData(object resultValue)
    {
        if (!TryRawDictionary(resultValue, out Dictionary result))
            return new CompactExecutionResultData();

        return new CompactExecutionResultData
        {
            CommandType = ReadString(result, "command_type"),
            SkillId = ReadString(result, "skill_id"),
            SkillVariantId = ReadString(result, "skill_variant_id"),
            ChangedUnitIds = StringifyArrayList(ReadArray(result, "changed_unit_ids")),
            TrackedUnitIds = StringifyArrayList(ReadArray(result, "tracked_unit_ids")),
            UnitResults = SummarizeUnitResultsData(ReadArray(result, "unit_results")),
            LogLines = StringifyArrayList(ReadArray(result, "log_lines")),
            ReportEntries = ReadArray(result, "report_entries"),
        };
    }

    private List<CompactUnitResultData> SummarizeUnitResultsData(object resultsValue)
    {
        var summaries = new List<CompactUnitResultData>();
        if (!TryRawArray(resultsValue, out Godot.Collections.Array results))
            return summaries;

        foreach (object resultValue in results)
        {
            if (!TryRawDictionary(resultValue, out Dictionary result))
                continue;

            summaries.Add(
                new CompactUnitResultData
                {
                    UnitId = ReadString(result, "unit_id"),
                    Before = SummarizeUnitSnapshotData(ReadDictionary(result, "before")),
                    After = SummarizeUnitSnapshotData(ReadDictionary(result, "after")),
                    HpDelta = ReadInt(result, "hp_delta", 0),
                    HpDamage = ReadInt(result, "hp_damage", 0),
                    HpHealing = ReadInt(result, "hp_healing", 0),
                    ShieldDelta = ReadInt(result, "shield_delta", 0),
                    ShieldDamage = ReadInt(result, "shield_damage", 0),
                    ShieldRestored = ReadInt(result, "shield_restored", 0),
                    Killed = ReadBool(result, "killed"),
                    Revived = ReadBool(result, "revived"),
                    Moved = ReadBool(result, "moved"),
                }
            );
        }

        return summaries;
    }

    private List<CompactUnitSnapshotData> SummarizeUnitSnapshotsData(object snapshotsValue)
    {
        var summaries = new List<CompactUnitSnapshotData>();
        if (!TryRawArray(snapshotsValue, out Godot.Collections.Array snapshots))
            return summaries;

        foreach (object snapshotValue in snapshots)
        {
            CompactUnitSnapshotData summary = SummarizeUnitSnapshotData(snapshotValue);
            if (!summary.IsEmpty)
                summaries.Add(summary);
        }
        return summaries;
    }

    private CompactUnitSnapshotData SummarizeUnitSnapshotData(object snapshotValue)
    {
        if (!TryRawDictionary(snapshotValue, out Dictionary snapshot))
            return new CompactUnitSnapshotData();

        return new CompactUnitSnapshotData
        {
            UnitId = ReadString(snapshot, "unit_id"),
            DisplayName = ReadString(snapshot, "display_name"),
            FactionId = ReadString(snapshot, "faction_id"),
            Coord = ReadString(snapshot, "coord"),
            Alive = ReadBool(snapshot, "alive"),
            Hp = ReadInt(snapshot, "hp", 0),
            HpMax = ReadInt(snapshot, "hp_max", 0),
            ShieldHp = ReadInt(snapshot, "shield_hp", 0),
            ShieldMaxHp = ReadInt(snapshot, "shield_max_hp", 0),
            Ap = ReadInt(snapshot, "ap", 0),
            MovePoints = ReadInt(snapshot, "move_points", 0),
        };
    }

    private CompactScoreInputData SummarizeScoreInputData(object scoreValue)
    {
        if (scoreValue is BattleAiScoreInput typedScore)
            return SummarizeScoreInputData(typedScore);
        if (!TryRawDictionary(scoreValue, out Dictionary score))
            return new CompactScoreInputData();

        return new CompactScoreInputData
        {
            TotalScore = ReadInt(score, "total_score", 0),
            ScoreBucketId = ReadString(score, "score_bucket_id"),
            ScoreBucketPriority = ReadInt(score, "score_bucket_priority", 0),
            CommandType = ReadString(score, "command_type"),
            SkillId = ReadString(score, "skill_id"),
            TargetCount = ReadInt(score, "target_count", 0),
            EffectiveTargetCount = ReadInt(score, "effective_target_count", 0),
            EnemyTargetCount = ReadInt(score, "enemy_target_count", 0),
            AllyTargetCount = ReadInt(score, "ally_target_count", 0),
            TargetUnitIds = StringifyArrayList(ReadArray(score, "target_unit_ids")),
            TargetCoords = StringifyArrayList(ReadArray(score, "target_coords")),
            EstimatedDamage = ReadInt(score, "estimated_damage", 0),
            EstimatedHitRatePercent = ReadInt(score, "estimated_hit_rate_percent", 0),
            SaveEstimatesByTargetId = SummarizeSaveEstimatesByTargetIdData(
                ReadDictionary(score, "save_estimates_by_target_id")
            ),
            EstimatedLethalTargetCount = ReadInt(score, "estimated_lethal_target_count", 0),
            EstimatedLethalThreatTargetCount = ReadInt(
                score,
                "estimated_lethal_threat_target_count",
                0
            ),
            EstimatedLethalTargetIds = StringifyArrayList(
                ReadArray(score, "estimated_lethal_target_ids")
            ),
            EstimatedLethalThreatTargetIds = StringifyArrayList(
                ReadArray(score, "estimated_lethal_threat_target_ids")
            ),
            EstimatedControlTargetIds = StringifyArrayList(
                ReadArray(score, "estimated_control_target_ids")
            ),
            EstimatedControlThreatTargetIds = StringifyArrayList(
                ReadArray(score, "estimated_control_threat_target_ids")
            ),
            HasPostActionThreatProjection = ReadBool(
                score,
                "has_post_action_threat_projection"
            ),
            ProjectedActorCoord = ReadString(score, "projected_actor_coord"),
            PreActionThreatUnitIds = StringifyArrayList(
                ReadArray(score, "pre_action_threat_unit_ids")
            ),
            PreActionThreatCount = ReadInt(score, "pre_action_threat_count", 0),
            PreActionThreatExpectedDamage = ReadInt(
                score,
                "pre_action_threat_expected_damage",
                0
            ),
            PreActionSurvivalMargin = ReadInt(score, "pre_action_survival_margin", 0),
            PreActionIsLethalSurvivalRisk = ReadBool(
                score,
                "pre_action_is_lethal_survival_risk"
            ),
            PostActionRemainingThreatUnitIds = StringifyArrayList(
                ReadArray(score, "post_action_remaining_threat_unit_ids")
            ),
            PostActionRemainingThreatCount = ReadInt(
                score,
                "post_action_remaining_threat_count",
                0
            ),
            PostActionRemainingThreatExpectedDamage = ReadInt(
                score,
                "post_action_remaining_threat_expected_damage",
                0
            ),
            PostActionSurvivalMargin = ReadInt(score, "post_action_survival_margin", 0),
            PostActionIsLethalSurvivalRisk = ReadBool(
                score,
                "post_action_is_lethal_survival_risk"
            ),
            HitPayoffScore = ReadInt(score, "hit_payoff_score", 0),
            PositionObjectiveKind = ReadString(score, "position_objective_kind"),
            PositionObjectiveScore = ReadInt(score, "position_objective_score", 0),
            ResourceCostScore = ReadInt(score, "resource_cost_score", 0),
            DistanceToPrimaryCoord = ReadInt(score, "distance_to_primary_coord", -1),
            ApCost = ReadInt(score, "ap_cost", 0),
            StaminaCost = ReadInt(score, "stamina_cost", 0),
            MpCost = ReadInt(score, "mp_cost", 0),
            AuraCost = ReadInt(score, "aura_cost", 0),
            MoveCost = ReadInt(score, "move_cost", 0),
        };
    }

    private CompactScoreInputData SummarizeScoreInputData(BattleAiScoreInput score)
    {
        if (score == null)
            return new CompactScoreInputData();

        return new CompactScoreInputData
        {
            TotalScore = score.total_score,
            ScoreBucketId = score.score_bucket_id.ToString(),
            ScoreBucketPriority = score.score_bucket_priority,
            CommandType = score.command?.command_type.ToString() ?? "",
            SkillId = !string.IsNullOrEmpty(score.command?.skill_id.ToString())
                ? score.command.skill_id.ToString()
                : !string.IsNullOrEmpty(score.runtime_action_metadata?.skill_id.ToString())
                    ? score.runtime_action_metadata.skill_id.ToString()
                    : score.skill_def?.skill_id.ToString() ?? "",
            TargetCount = score.target_count,
            EffectiveTargetCount = score.effective_target_count,
            EnemyTargetCount = score.enemy_target_count,
            AllyTargetCount = score.ally_target_count,
            TargetUnitIds = StringifyStringNames(score.target_unit_ids),
            TargetCoords = StringifyCoords(score.target_coords),
            EstimatedDamage = score.estimated_damage,
            EstimatedHitRatePercent = score.estimated_hit_rate_percent,
            SaveEstimatesByTargetId = SummarizeSaveEstimatesByTargetIdData(score),
            EstimatedLethalTargetCount = score.estimated_lethal_target_count,
            EstimatedLethalThreatTargetCount = score.estimated_lethal_threat_target_count,
            EstimatedLethalTargetIds = StringifyStringNames(score.estimated_lethal_target_ids),
            EstimatedLethalThreatTargetIds = StringifyStringNames(
                score.estimated_lethal_threat_target_ids
            ),
            EstimatedControlTargetIds = StringifyStringNames(score.estimated_control_target_ids),
            EstimatedControlThreatTargetIds = StringifyStringNames(
                score.estimated_control_threat_target_ids
            ),
            HasPostActionThreatProjection = score.has_post_action_threat_projection,
            ProjectedActorCoord = AsString(score.projected_actor_coord),
            PreActionThreatUnitIds = StringifyStringNames(score.pre_action_threat_unit_ids),
            PreActionThreatCount = score.pre_action_threat_count,
            PreActionThreatExpectedDamage = score.pre_action_threat_expected_damage,
            PreActionSurvivalMargin = score.pre_action_survival_margin,
            PreActionIsLethalSurvivalRisk = score.pre_action_is_lethal_survival_risk,
            PostActionRemainingThreatUnitIds = StringifyStringNames(
                score.post_action_remaining_threat_unit_ids
            ),
            PostActionRemainingThreatCount = score.post_action_remaining_threat_count,
            PostActionRemainingThreatExpectedDamage =
                score.post_action_remaining_threat_expected_damage,
            PostActionSurvivalMargin = score.post_action_survival_margin,
            PostActionIsLethalSurvivalRisk = score.post_action_is_lethal_survival_risk,
            HitPayoffScore = score.hit_payoff_score,
            PositionObjectiveKind = score.position_objective_kind.ToString(),
            PositionObjectiveScore = score.position_objective_score,
            ResourceCostScore = score.resource_cost_score,
            DistanceToPrimaryCoord = score.distance_to_primary_coord,
            ApCost = score.ap_cost,
            StaminaCost = score.stamina_cost,
            MpCost = score.mp_cost,
            AuraCost = score.aura_cost,
            MoveCost = score.move_cost,
        };
    }

    private System.Collections.Generic.Dictionary<
        string,
        List<CompactSaveEstimateData>
    > SummarizeSaveEstimatesByTargetIdData(
        object value
    )
    {
        var summary = new System.Collections.Generic.Dictionary<string, List<CompactSaveEstimateData>>(
            StringComparer.Ordinal
        );
        if (!TryRawDictionary(value, out Dictionary estimatesByTarget))
            return summary;

        var targetKeys = new List<object>();
        foreach (object targetKey in estimatesByTarget.Keys)
            targetKeys.Add(targetKey);
        targetKeys.Sort((a, b) => string.Compare(AsString(a), AsString(b), StringComparison.Ordinal));

        foreach (object targetKey in targetKeys)
        {
            string targetKeyStr = AsString(targetKey);
            if (!TryRawArray(ReadValue(estimatesByTarget, targetKey, new Godot.Collections.Array()), out var estimates))
                continue;

            var compactEstimates = new List<CompactSaveEstimateData>();
            foreach (object estimateValue in estimates)
            {
                if (!TryRawDictionary(estimateValue, out Dictionary estimate))
                    continue;

                compactEstimates.Add(
                    new CompactSaveEstimateData
                    {
                        DamageBeforeSave = ReadInt(estimate, "damage_before_save", 0),
                        DamageAfterSaveEstimate = ReadInt(
                            estimate,
                            "damage_after_save_estimate",
                            0
                        ),
                        DamageOnSaveSuccess = ReadInt(estimate, "damage_on_save_success", 0),
                        SaveSuccessRatePercent = ReadInt(
                            estimate,
                            "save_success_rate_percent",
                            0
                        ),
                        Dc = ReadInt(estimate, "dc", 0),
                        Ability = ReadString(estimate, "ability"),
                        SaveTag = ReadString(estimate, "save_tag"),
                        AdvantageState = ReadString(estimate, "advantage_state"),
                        Immune = ReadBool(estimate, "immune"),
                        HitCount = ReadInt(estimate, "hit_count", 1),
                    }
                );
            }

            if (compactEstimates.Count > 0)
                summary[targetKeyStr] = compactEstimates;
        }

        return summary;
    }

    private System.Collections.Generic.Dictionary<
        string,
        List<CompactSaveEstimateData>
    > SummarizeSaveEstimatesByTargetIdData(
        BattleAiScoreInput score
    )
    {
        var summary = new System.Collections.Generic.Dictionary<string, List<CompactSaveEstimateData>>(
            StringComparer.Ordinal
        );
        if (score?.save_estimates_by_target_id == null)
            return summary;

        var targetKeys = new List<StringName>(score.save_estimates_by_target_id.Keys);
        targetKeys.Sort((a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal));

        foreach (StringName targetKey in targetKeys)
        {
            string targetKeyStr = targetKey.ToString();
            if (
                string.IsNullOrEmpty(targetKeyStr)
                || !score.save_estimates_by_target_id.TryGetValue(targetKey, out List<BattleAiScoreService.DamageSaveEstimate> estimates)
            )
            {
                continue;
            }

            var compactEstimates = new List<CompactSaveEstimateData>();
            foreach (BattleAiScoreService.DamageSaveEstimate estimate in estimates ?? new List<BattleAiScoreService.DamageSaveEstimate>())
            {
                if (estimate == null)
                    continue;

                compactEstimates.Add(
                    new CompactSaveEstimateData
                    {
                        DamageBeforeSave = estimate.DamageBeforeSave,
                        DamageAfterSaveEstimate = estimate.DamageAfterSaveEstimate,
                        DamageOnSaveSuccess = estimate.DamageOnSaveSuccess,
                        SaveSuccessRatePercent = estimate.SaveSuccessRatePercent,
                        Dc = estimate.Dc,
                        Ability = estimate.Ability.ToString(),
                        SaveTag = estimate.SaveTag.ToString(),
                        AdvantageState = estimate.AdvantageState.ToString(),
                        Immune = estimate.Immune,
                        HitCount = estimate.HitCount,
                    }
                );
            }

            if (compactEstimates.Count > 0)
                summary[targetKeyStr] = compactEstimates;
        }

        return summary;
    }

    private static void CopyOptionalCandidateIntData(
        CompactTopCandidateData target,
        Dictionary source,
        string key
    )
    {
        if (source.ContainsKey(key))
            target.OptionalInts[key] = (int)source[key];
    }

    private static void CopyOptionalCandidateStringData(
        CompactTopCandidateData target,
        Dictionary source,
        string key
    )
    {
        if (source.ContainsKey(key))
            target.OptionalStrings[key] = source[key].ToString();
    }

    private static void CopyOptionalCandidateBoolData(
        CompactTopCandidateData target,
        Dictionary source,
        string key
    )
    {
        if (source.ContainsKey(key))
            target.OptionalBools[key] = (bool)source[key];
    }

    private static void IncrementNestedCounterData(
        System.Collections.Generic.Dictionary<
            string,
            System.Collections.Generic.Dictionary<string, int>
        > counters,
        string outerKey,
        string innerKey,
        int amount = 1
    )
    {
        if (string.IsNullOrEmpty(outerKey) || string.IsNullOrEmpty(innerKey) || amount == 0)
            return;
        if (
            !counters.TryGetValue(
                outerKey,
                out System.Collections.Generic.Dictionary<string, int> inner
            )
        )
        {
            inner = new System.Collections.Generic.Dictionary<string, int>(
                StringComparer.Ordinal
            );
            counters[outerKey] = inner;
        }
        inner[innerKey] = (inner.TryGetValue(innerKey, out int existing) ? existing : 0) + amount;
    }

    private List<string> StringifyArrayList(object value)
    {
        var results = new List<string>();
        if (!TryRawArray(value, out Godot.Collections.Array values))
            return results;
        foreach (object entry in values)
            results.Add(AsString(entry));
        return results;
    }

    private List<string> StringifyStringNames(IEnumerable<StringName> values)
    {
        var results = new List<string>();
        foreach (StringName value in values ?? System.Array.Empty<StringName>())
            results.Add(AsString(value));
        return results;
    }

    private List<string> StringifyCoords(IEnumerable<Vector2I> values)
    {
        var results = new List<string>();
        foreach (Vector2I value in values ?? System.Array.Empty<Vector2I>())
            results.Add(AsString(value));
        return results;
    }

    private static Godot.Collections.Array ToGodotArray<T>(IEnumerable<T> entries)
        where T : IGodotDictionaryConvertible
    {
        var result = new Godot.Collections.Array();
        if (entries == null)
            return result;
        foreach (T entry in entries)
            result.Add(entry?.ToDictionary() ?? new Dictionary());
        return result;
    }

    private static Godot.Collections.Array ToGodotArray(IEnumerable<string> entries)
    {
        var result = new Godot.Collections.Array();
        if (entries == null)
            return result;
        foreach (string entry in entries)
            result.Add(entry ?? "");
        return result;
    }

    private static Godot.Collections.Dictionary ToStringIntDictionary(
        IReadOnlyDictionary<string, int> source
    )
    {
        var result = new Dictionary();
        if (source == null)
            return result;
        foreach (KeyValuePair<string, int> entry in source)
            result[entry.Key] = entry.Value;
        return result;
    }

    private static System.Collections.Generic.Dictionary<string, int> ToStringIntMap(
        Godot.Collections.Dictionary source
    )
    {
        var result = new System.Collections.Generic.Dictionary<string, int>(
            StringComparer.Ordinal
        );
        if (source == null)
            return result;
        foreach (Variant key in source.Keys)
            result[key.ToString()] = ReadInt(source, key, 0);
        return result;
    }

    private static Godot.Collections.Dictionary ToNestedStringIntDictionary(
        IReadOnlyDictionary<string, System.Collections.Generic.Dictionary<string, int>> source
    )
    {
        var result = new Dictionary();
        if (source == null)
            return result;
        foreach (
            KeyValuePair<string, System.Collections.Generic.Dictionary<string, int>> entry in source
        )
            result[entry.Key] = ToStringIntDictionary(entry.Value);
        return result;
    }

    private static Godot.Collections.Dictionary ToSaveEstimatesDictionary(
        IReadOnlyDictionary<string, List<CompactSaveEstimateData>> source
    )
    {
        var result = new Dictionary();
        if (source == null)
            return result;
        foreach (KeyValuePair<string, List<CompactSaveEstimateData>> entry in source)
            result[entry.Key] = ToGodotArray(entry.Value);
        return result;
    }

    private interface IGodotDictionaryConvertible
    {
        Dictionary ToDictionary();
    }

    public sealed class TraceSummaryOptionsData
    {
        public string FocusFactionId { get; set; } = DefaultFocusFactionId;

        public int TopCandidateLimit { get; set; } = DefaultTopCandidatesPerAction;

        internal string ResolvedFocusFactionId =>
            string.IsNullOrEmpty(FocusFactionId) ? DefaultFocusFactionId : FocusFactionId;

        internal int ResolvedTopCandidateLimit => Mathf.Max(TopCandidateLimit, 0);

        public Dictionary ToDictionary() =>
            new()
            {
                ["full_trace_embedded_in_source_report"] = true,
                ["focus_faction_id"] = ResolvedFocusFactionId,
                ["focus_turns_keep_action_trace_summaries"] = true,
                ["top_candidates_per_action_trace"] = ResolvedTopCandidateLimit,
            };
    }

    private sealed class CompactRunTraceData : IGodotDictionaryConvertible
    {
        public string ProfileId { get; set; } = "";
        public int RunIndex { get; set; }
        public long Seed { get; set; }
        public bool BattleEnded { get; set; }
        public string WinnerFactionId { get; set; } = "";
        public int FinalTu { get; set; }
        public int Iterations { get; set; }
        public int TimelineSteps { get; set; }
        public int TraceCount { get; set; }
        public Dictionary Factions { get; set; } = new();
        public Dictionary Units { get; set; } = new();
        public System.Collections.Generic.Dictionary<
            string,
            System.Collections.Generic.Dictionary<string, int>
        > ActionCountsByFaction { get; } = new(StringComparer.Ordinal);
        public System.Collections.Generic.Dictionary<
            string,
            System.Collections.Generic.Dictionary<string, int>
        > CommandCountsByFaction { get; } = new(StringComparer.Ordinal);
        public System.Collections.Generic.Dictionary<
            string,
            System.Collections.Generic.Dictionary<string, int>
        > WaitCountsByFaction { get; } = new(StringComparer.Ordinal);
        public System.Collections.Generic.Dictionary<
            string,
            System.Collections.Generic.Dictionary<string, int>
        > BlockReasonsByFaction { get; } = new(StringComparer.Ordinal);
        public List<CompactTurnTraceData> FocusTurns { get; } = new();
        public List<CompactTurnTraceData> FocusWaitTurns { get; } = new();

        public Dictionary ToDictionary() =>
            new()
            {
                ["profile_id"] = ProfileId,
                ["run_index"] = RunIndex,
                ["seed"] = Seed,
                ["battle_ended"] = BattleEnded,
                ["winner_faction_id"] = WinnerFactionId,
                ["final_tu"] = FinalTu,
                ["iterations"] = Iterations,
                ["timeline_steps"] = TimelineSteps,
                ["trace_count"] = TraceCount,
                ["factions"] = Factions,
                ["units"] = Units,
                ["action_counts_by_faction"] = ToNestedStringIntDictionary(ActionCountsByFaction),
                ["command_counts_by_faction"] = ToNestedStringIntDictionary(CommandCountsByFaction),
                ["wait_counts_by_faction"] = ToNestedStringIntDictionary(WaitCountsByFaction),
                ["block_reasons_by_faction"] = ToNestedStringIntDictionary(BlockReasonsByFaction),
                ["focus_turns"] = ToGodotArray(FocusTurns),
                ["focus_wait_turns"] = ToGodotArray(FocusWaitTurns),
            };
    }

    private sealed class CompactTurnTraceData : IGodotDictionaryConvertible
    {
        public int TurnStartedTu { get; set; }
        public string UnitId { get; set; } = "";
        public string UnitName { get; set; } = "";
        public string FactionId { get; set; } = "";
        public string BrainId { get; set; } = "";
        public string StateId { get; set; } = "";
        public string ActionId { get; set; } = "";
        public string ReasonText { get; set; } = "";
        public CompactCommandSummaryData Command { get; set; } = new();
        public CompactScoreInputData Score { get; set; } = new();
        public List<CompactUnitSnapshotData> DecisionTargetSnapshots { get; set; } = new();
        public CompactExecutionResultData ExecutionResult { get; set; } = new();
        public List<CompactActionTraceData> ActionTraces { get; set; } = new();

        public Dictionary ToDictionary() =>
            new()
            {
                ["turn_started_tu"] = TurnStartedTu,
                ["unit_id"] = UnitId,
                ["unit_name"] = UnitName,
                ["faction_id"] = FactionId,
                ["brain_id"] = BrainId,
                ["state_id"] = StateId,
                ["action_id"] = ActionId,
                ["reason_text"] = ReasonText,
                ["command"] = Command.ToDictionary(),
                ["score"] = Score.ToDictionary(),
                ["decision_target_snapshots"] = ToGodotArray(DecisionTargetSnapshots),
                ["execution_result"] = ExecutionResult.ToDictionary(),
                ["action_traces"] = ToGodotArray(ActionTraces),
            };
    }

    private sealed class CompactActionTraceData : IGodotDictionaryConvertible
    {
        public string TraceId { get; set; } = "";
        public string ActionId { get; set; } = "";
        public bool Chosen { get; set; }
        public string ScoreBucketId { get; set; } = "";
        public Dictionary Metadata { get; set; } = new();
        public System.Collections.Generic.Dictionary<string, int> BlockReasons { get; set; } =
            new(StringComparer.Ordinal);
        public int BlockedCount { get; set; }
        public int CandidateCount { get; set; }
        public int EvaluationCount { get; set; }
        public int PreviewRejectCount { get; set; }
        public List<CompactTopCandidateData> TopCandidates { get; set; } = new();

        public Dictionary ToDictionary() =>
            new()
            {
                ["trace_id"] = TraceId,
                ["action_id"] = ActionId,
                ["chosen"] = Chosen,
                ["score_bucket_id"] = ScoreBucketId,
                ["metadata"] = Metadata,
                ["block_reasons"] = ToStringIntDictionary(BlockReasons),
                ["blocked_count"] = BlockedCount,
                ["candidate_count"] = CandidateCount,
                ["evaluation_count"] = EvaluationCount,
                ["preview_reject_count"] = PreviewRejectCount,
                ["top_candidates"] = ToGodotArray(TopCandidates),
            };
    }

    private sealed class CompactTopCandidateData : IGodotDictionaryConvertible
    {
        public string Label { get; set; } = "";
        public int TotalScore { get; set; }
        public int PredictedDistance { get; set; } = -1;
        public CompactCommandSummaryData Command { get; set; } = new();
        public CompactScoreInputData Score { get; set; } = new();
        public System.Collections.Generic.Dictionary<string, int> OptionalInts { get; } =
            new(StringComparer.Ordinal);
        public System.Collections.Generic.Dictionary<string, string> OptionalStrings { get; } =
            new(StringComparer.Ordinal);
        public System.Collections.Generic.Dictionary<string, bool> OptionalBools { get; } =
            new(StringComparer.Ordinal);

        public Dictionary ToDictionary()
        {
            var payload = new Dictionary
            {
                ["label"] = Label,
                ["total_score"] = TotalScore,
                ["predicted_distance"] = PredictedDistance,
                ["command"] = Command.ToDictionary(),
                ["score"] = Score.ToDictionary(),
            };
            foreach (KeyValuePair<string, int> entry in OptionalInts)
                payload[entry.Key] = entry.Value;
            foreach (KeyValuePair<string, string> entry in OptionalStrings)
                payload[entry.Key] = entry.Value;
            foreach (KeyValuePair<string, bool> entry in OptionalBools)
                payload[entry.Key] = entry.Value;
            return payload;
        }
    }

    private sealed class CompactCommandSummaryData : IGodotDictionaryConvertible
    {
        public string CommandType { get; set; } = "";
        public string UnitId { get; set; } = "";
        public string SkillId { get; set; } = "";
        public string SkillVariantId { get; set; } = "";
        public string TargetUnitId { get; set; } = "";
        public List<string> TargetUnitIds { get; set; } = new();
        public string TargetCoord { get; set; } = "";
        public List<string> TargetCoords { get; set; } = new();

        public Dictionary ToDictionary() =>
            new()
            {
                ["command_type"] = CommandType,
                ["unit_id"] = UnitId,
                ["skill_id"] = SkillId,
                ["skill_variant_id"] = SkillVariantId,
                ["target_unit_id"] = TargetUnitId,
                ["target_unit_ids"] = ToGodotArray(TargetUnitIds),
                ["target_coord"] = TargetCoord,
                ["target_coords"] = ToGodotArray(TargetCoords),
            };
    }

    private sealed class CompactExecutionResultData : IGodotDictionaryConvertible
    {
        public string CommandType { get; set; } = "";
        public string SkillId { get; set; } = "";
        public string SkillVariantId { get; set; } = "";
        public List<string> ChangedUnitIds { get; set; } = new();
        public List<string> TrackedUnitIds { get; set; } = new();
        public List<CompactUnitResultData> UnitResults { get; set; } = new();
        public List<string> LogLines { get; set; } = new();
        public Godot.Collections.Array ReportEntries { get; set; } = new();

        public Dictionary ToDictionary() =>
            new()
            {
                ["command_type"] = CommandType,
                ["skill_id"] = SkillId,
                ["skill_variant_id"] = SkillVariantId,
                ["changed_unit_ids"] = ToGodotArray(ChangedUnitIds),
                ["tracked_unit_ids"] = ToGodotArray(TrackedUnitIds),
                ["unit_results"] = ToGodotArray(UnitResults),
                ["log_lines"] = ToGodotArray(LogLines),
                ["report_entries"] = ReportEntries,
            };
    }

    private sealed class CompactUnitResultData : IGodotDictionaryConvertible
    {
        public string UnitId { get; set; } = "";
        public CompactUnitSnapshotData Before { get; set; } = new();
        public CompactUnitSnapshotData After { get; set; } = new();
        public int HpDelta { get; set; }
        public int HpDamage { get; set; }
        public int HpHealing { get; set; }
        public int ShieldDelta { get; set; }
        public int ShieldDamage { get; set; }
        public int ShieldRestored { get; set; }
        public bool Killed { get; set; }
        public bool Revived { get; set; }
        public bool Moved { get; set; }

        public Dictionary ToDictionary() =>
            new()
            {
                ["unit_id"] = UnitId,
                ["before"] = Before.ToDictionary(),
                ["after"] = After.ToDictionary(),
                ["hp_delta"] = HpDelta,
                ["hp_damage"] = HpDamage,
                ["hp_healing"] = HpHealing,
                ["shield_delta"] = ShieldDelta,
                ["shield_damage"] = ShieldDamage,
                ["shield_restored"] = ShieldRestored,
                ["killed"] = Killed,
                ["revived"] = Revived,
                ["moved"] = Moved,
            };
    }

    private sealed class CompactUnitSnapshotData : IGodotDictionaryConvertible
    {
        public string UnitId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string FactionId { get; set; } = "";
        public string Coord { get; set; } = "";
        public bool Alive { get; set; }
        public int Hp { get; set; }
        public int HpMax { get; set; }
        public int ShieldHp { get; set; }
        public int ShieldMaxHp { get; set; }
        public int Ap { get; set; }
        public int MovePoints { get; set; }
        public bool IsEmpty => string.IsNullOrEmpty(UnitId) && string.IsNullOrEmpty(DisplayName);

        public Dictionary ToDictionary() =>
            new()
            {
                ["unit_id"] = UnitId,
                ["display_name"] = DisplayName,
                ["faction_id"] = FactionId,
                ["coord"] = Coord,
                ["alive"] = Alive,
                ["hp"] = Hp,
                ["hp_max"] = HpMax,
                ["shield_hp"] = ShieldHp,
                ["shield_max_hp"] = ShieldMaxHp,
                ["ap"] = Ap,
                ["move_points"] = MovePoints,
            };
    }

    private sealed class CompactScoreInputData : IGodotDictionaryConvertible
    {
        public int TotalScore { get; set; }
        public string ScoreBucketId { get; set; } = "";
        public int ScoreBucketPriority { get; set; }
        public string CommandType { get; set; } = "";
        public string SkillId { get; set; } = "";
        public int TargetCount { get; set; }
        public int EffectiveTargetCount { get; set; }
        public int EnemyTargetCount { get; set; }
        public int AllyTargetCount { get; set; }
        public List<string> TargetUnitIds { get; set; } = new();
        public List<string> TargetCoords { get; set; } = new();
        public int EstimatedDamage { get; set; }
        public int EstimatedHitRatePercent { get; set; }
        public System.Collections.Generic.Dictionary<
            string,
            List<CompactSaveEstimateData>
        > SaveEstimatesByTargetId { get; set; } = new(StringComparer.Ordinal);
        public int EstimatedLethalTargetCount { get; set; }
        public int EstimatedLethalThreatTargetCount { get; set; }
        public List<string> EstimatedLethalTargetIds { get; set; } = new();
        public List<string> EstimatedLethalThreatTargetIds { get; set; } = new();
        public List<string> EstimatedControlTargetIds { get; set; } = new();
        public List<string> EstimatedControlThreatTargetIds { get; set; } = new();
        public bool HasPostActionThreatProjection { get; set; }
        public string ProjectedActorCoord { get; set; } = "";
        public List<string> PreActionThreatUnitIds { get; set; } = new();
        public int PreActionThreatCount { get; set; }
        public int PreActionThreatExpectedDamage { get; set; }
        public int PreActionSurvivalMargin { get; set; }
        public bool PreActionIsLethalSurvivalRisk { get; set; }
        public List<string> PostActionRemainingThreatUnitIds { get; set; } = new();
        public int PostActionRemainingThreatCount { get; set; }
        public int PostActionRemainingThreatExpectedDamage { get; set; }
        public int PostActionSurvivalMargin { get; set; }
        public bool PostActionIsLethalSurvivalRisk { get; set; }
        public int HitPayoffScore { get; set; }
        public string PositionObjectiveKind { get; set; } = "";
        public int PositionObjectiveScore { get; set; }
        public int ResourceCostScore { get; set; }
        public int DistanceToPrimaryCoord { get; set; } = -1;
        public int ApCost { get; set; }
        public int StaminaCost { get; set; }
        public int MpCost { get; set; }
        public int AuraCost { get; set; }
        public int MoveCost { get; set; }

        public Dictionary ToDictionary() =>
            new()
            {
                ["total_score"] = TotalScore,
                ["score_bucket_id"] = ScoreBucketId,
                ["score_bucket_priority"] = ScoreBucketPriority,
                ["command_type"] = CommandType,
                ["skill_id"] = SkillId,
                ["target_count"] = TargetCount,
                ["effective_target_count"] = EffectiveTargetCount,
                ["enemy_target_count"] = EnemyTargetCount,
                ["ally_target_count"] = AllyTargetCount,
                ["target_unit_ids"] = ToGodotArray(TargetUnitIds),
                ["target_coords"] = ToGodotArray(TargetCoords),
                ["estimated_damage"] = EstimatedDamage,
                ["estimated_hit_rate_percent"] = EstimatedHitRatePercent,
                ["save_estimates_by_target_id"] = ToSaveEstimatesDictionary(SaveEstimatesByTargetId),
                ["estimated_lethal_target_count"] = EstimatedLethalTargetCount,
                ["estimated_lethal_threat_target_count"] = EstimatedLethalThreatTargetCount,
                ["estimated_lethal_target_ids"] = ToGodotArray(EstimatedLethalTargetIds),
                ["estimated_lethal_threat_target_ids"] = ToGodotArray(EstimatedLethalThreatTargetIds),
                ["estimated_control_target_ids"] = ToGodotArray(EstimatedControlTargetIds),
                ["estimated_control_threat_target_ids"] = ToGodotArray(EstimatedControlThreatTargetIds),
                ["has_post_action_threat_projection"] = HasPostActionThreatProjection,
                ["projected_actor_coord"] = ProjectedActorCoord,
                ["pre_action_threat_unit_ids"] = ToGodotArray(PreActionThreatUnitIds),
                ["pre_action_threat_count"] = PreActionThreatCount,
                ["pre_action_threat_expected_damage"] = PreActionThreatExpectedDamage,
                ["pre_action_survival_margin"] = PreActionSurvivalMargin,
                ["pre_action_is_lethal_survival_risk"] = PreActionIsLethalSurvivalRisk,
                ["post_action_remaining_threat_unit_ids"] = ToGodotArray(PostActionRemainingThreatUnitIds),
                ["post_action_remaining_threat_count"] = PostActionRemainingThreatCount,
                ["post_action_remaining_threat_expected_damage"] = PostActionRemainingThreatExpectedDamage,
                ["post_action_survival_margin"] = PostActionSurvivalMargin,
                ["post_action_is_lethal_survival_risk"] = PostActionIsLethalSurvivalRisk,
                ["hit_payoff_score"] = HitPayoffScore,
                ["position_objective_kind"] = PositionObjectiveKind,
                ["position_objective_score"] = PositionObjectiveScore,
                ["resource_cost_score"] = ResourceCostScore,
                ["distance_to_primary_coord"] = DistanceToPrimaryCoord,
                ["ap_cost"] = ApCost,
                ["stamina_cost"] = StaminaCost,
                ["mp_cost"] = MpCost,
                ["aura_cost"] = AuraCost,
                ["move_cost"] = MoveCost,
            };
    }

    private sealed class CompactSaveEstimateData : IGodotDictionaryConvertible
    {
        public int DamageBeforeSave { get; set; }
        public int DamageAfterSaveEstimate { get; set; }
        public int DamageOnSaveSuccess { get; set; }
        public int SaveSuccessRatePercent { get; set; }
        public int Dc { get; set; }
        public string Ability { get; set; } = "";
        public string SaveTag { get; set; } = "";
        public string AdvantageState { get; set; } = "";
        public bool Immune { get; set; }
        public int HitCount { get; set; } = 1;

        public Dictionary ToDictionary() =>
            new()
            {
                ["damage_before_save"] = DamageBeforeSave,
                ["damage_after_save_estimate"] = DamageAfterSaveEstimate,
                ["damage_on_save_success"] = DamageOnSaveSuccess,
                ["save_success_rate_percent"] = SaveSuccessRatePercent,
                ["dc"] = Dc,
                ["ability"] = Ability,
                ["save_tag"] = SaveTag,
                ["advantage_state"] = AdvantageState,
                ["immune"] = Immune,
                ["hit_count"] = HitCount,
            };
    }

    private string AsString(object rawValue)
    {
        return rawValue switch
        {
            Variant value => value.VariantType == Variant.Type.Nil ? "" : value.ToString(),
            StringName stringName => stringName.ToString(),
            _ => rawValue?.ToString() ?? "",
        };
    }

    private static object ReadValue(Dictionary source, object key, object fallback = null)
    {
        if (
            source != null
            && key != null
            && TryGetVariantKey(key, out Variant variantKey)
            && source.ContainsKey(variantKey)
        )
        {
            return source[variantKey];
        }
        return fallback;
    }

    private static bool TryGetVariantKey(object key, out Variant variantKey)
    {
        switch (key)
        {
            case Variant value:
                variantKey = value;
                return true;
            case string text:
                variantKey = text;
                return true;
            case StringName stringName:
                variantKey = stringName;
                return true;
            case int intValue:
                variantKey = intValue;
                return true;
            case long longValue:
                variantKey = longValue;
                return true;
            case bool boolValue:
                variantKey = boolValue;
                return true;
            case Vector2I vectorValue:
                variantKey = vectorValue;
                return true;
            default:
                variantKey = default;
                return false;
        }
    }

    private string ReadString(Dictionary source, string key, string fallback = "")
    {
        return AsString(ReadValue(source, key, fallback));
    }

    private static int ReadInt(Dictionary source, object key, int fallback = 0)
    {
        object rawValue = ReadValue(source, key, fallback);
        return rawValue switch
        {
            Variant value => value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32(),
            int intValue => intValue,
            long longValue => (int)longValue,
            float floatValue => (int)floatValue,
            double doubleValue => (int)doubleValue,
            _ => int.TryParse(rawValue?.ToString(), out int parsed) ? parsed : fallback,
        };
    }

    private static bool ReadBool(Dictionary source, object key, bool fallback = false)
    {
        object rawValue = ReadValue(source, key, fallback);
        return rawValue switch
        {
            Variant value => value.VariantType == Variant.Type.Nil ? fallback : value.AsBool(),
            bool boolValue => boolValue,
            _ => bool.TryParse(rawValue?.ToString(), out bool parsed) ? parsed : fallback,
        };
    }

    private static Dictionary ReadDictionary(Dictionary source, object key)
    {
        object rawValue = ReadValue(source, key, new Dictionary());
        return TryRawDictionary(rawValue, out Dictionary values) ? values : new Dictionary();
    }

    private static Godot.Collections.Array ReadArray(Dictionary source, object key)
    {
        object rawValue = ReadValue(source, key, new Godot.Collections.Array());
        return TryRawArray(rawValue, out Godot.Collections.Array values)
            ? values
            : new Godot.Collections.Array();
    }

    private static bool TryRawArray(object rawValue, out Godot.Collections.Array values)
    {
        if (rawValue is Variant value && value.VariantType == Variant.Type.Array)
        {
            values = value.AsGodotArray();
            return true;
        }
        if (rawValue is Godot.Collections.Array array)
        {
            values = array;
            return true;
        }
        if (rawValue is System.Collections.IEnumerable enumerable && rawValue is not string)
        {
            values = new Godot.Collections.Array();
            foreach (object entry in enumerable)
                AddArrayEntry(values, entry);
            return true;
        }
        values = new Godot.Collections.Array();
        return false;
    }

    private static void AddArrayEntry(Godot.Collections.Array values, object entry)
    {
        switch (entry)
        {
            case null:
                values.Add("");
                break;
            case Variant variant:
                values.Add(variant);
                break;
            case string text:
                values.Add(text);
                break;
            case StringName stringName:
                values.Add(stringName);
                break;
            case int intValue:
                values.Add(intValue);
                break;
            case long longValue:
                values.Add(longValue);
                break;
            case float floatValue:
                values.Add(floatValue);
                break;
            case double doubleValue:
                values.Add(doubleValue);
                break;
            case bool boolValue:
                values.Add(boolValue);
                break;
            case Godot.Collections.Dictionary dictionary:
                values.Add(dictionary);
                break;
            case AiCommandSummary command:
                values.Add(TraceDictionaryProjection.ToDictionary(command.ToTraceDictionary()));
                break;
            case AiCandidateSummary candidate:
                values.Add(TraceDictionaryProjection.ToDictionary(candidate.ToTraceDictionary()));
                break;
            case AiActionTrace actionTrace:
                values.Add(TraceDictionaryProjection.ToDictionary(actionTrace.ToTraceDictionary()));
                break;
            case BattleAiTurnTraceProjection turnTrace:
                values.Add(TraceDictionaryProjection.ToDictionary(turnTrace.ToTraceDictionary()));
                break;
            case BattleAiTraceTransitionProjection transition:
                values.Add(TraceDictionaryProjection.ToDictionary(transition.ToTraceDictionary()));
                break;
            case BattleAiTraceTransitionConditionProjection condition:
                values.Add(TraceDictionaryProjection.ToDictionary(condition.ToTraceDictionary()));
                break;
            case BattleAiTraceUnitSnapshotProjection snapshot:
                values.Add(TraceDictionaryProjection.ToDictionary(snapshot.ToTraceDictionary()));
                break;
            case BattleAiTraceUnitResultProjection unitResult:
                values.Add(TraceDictionaryProjection.ToDictionary(unitResult.ToTraceDictionary()));
                break;
            case BattleAiTraceExecutionResultProjection executionResult:
                values.Add(TraceDictionaryProjection.ToDictionary(executionResult.ToTraceDictionary()));
                break;
            case IReadOnlyDictionary<string, object> objectDictionary:
                values.Add(TraceDictionaryProjection.ToDictionary(objectDictionary));
                break;
            default:
                values.Add(entry.ToString());
                break;
        }
    }

    private static bool TryRawDictionary(object rawValue, out Dictionary values)
    {
        if (rawValue is Variant value && value.VariantType == Variant.Type.Dictionary)
        {
            values = value.AsGodotDictionary();
            return true;
        }
        if (rawValue is Dictionary dictionary)
        {
            values = dictionary;
            return true;
        }
        if (rawValue is AiCommandSummary command)
        {
            values = TraceDictionaryProjection.ToDictionary(command.ToTraceDictionary());
            return true;
        }
        if (rawValue is AiCandidateSummary candidate)
        {
            values = TraceDictionaryProjection.ToDictionary(candidate.ToTraceDictionary());
            return true;
        }
        if (rawValue is AiActionTrace actionTrace)
        {
            values = TraceDictionaryProjection.ToDictionary(actionTrace.ToTraceDictionary());
            return true;
        }
        if (rawValue is BattleAiTurnTraceProjection turnTrace)
        {
            values = TraceDictionaryProjection.ToDictionary(turnTrace.ToTraceDictionary());
            return true;
        }
        if (rawValue is BattleAiTraceTransitionProjection transition)
        {
            values = TraceDictionaryProjection.ToDictionary(transition.ToTraceDictionary());
            return true;
        }
        if (rawValue is BattleAiTraceTransitionConditionProjection condition)
        {
            values = TraceDictionaryProjection.ToDictionary(condition.ToTraceDictionary());
            return true;
        }
        if (rawValue is BattleAiTraceUnitSnapshotProjection snapshot)
        {
            values = TraceDictionaryProjection.ToDictionary(snapshot.ToTraceDictionary());
            return true;
        }
        if (rawValue is BattleAiTraceUnitResultProjection unitResult)
        {
            values = TraceDictionaryProjection.ToDictionary(unitResult.ToTraceDictionary());
            return true;
        }
        if (rawValue is BattleAiTraceExecutionResultProjection executionResult)
        {
            values = TraceDictionaryProjection.ToDictionary(executionResult.ToTraceDictionary());
            return true;
        }
        values = new Dictionary();
        return false;
    }

    private static bool TryDictionaryValue(Dictionary data, string key, out Dictionary values)
    {
        if (data != null && data.ContainsKey(key))
            return TryRawDictionary(data[key], out values);
        values = new Dictionary();
        return false;
    }

}
