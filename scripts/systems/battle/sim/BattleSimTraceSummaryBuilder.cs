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

    internal GodotProjectionLease<Dictionary> BuildLease(
        BattleSimScenarioReport report,
        string sourceReportPath = "",
        TraceSummaryOptionsData options = null
    ) =>
        TraceDictionaryProjection.BuildLease(
            BuildPlainSummary(report, sourceReportPath, options),
            "battle_sim_trace_summary",
            LifetimeDomain.Request,
            "BattleSimTraceSummaryBuilder.BuildLease"
        );

    internal GodotProjectionLease<Dictionary> BuildFileLease(
        BattleSimScenarioReport report,
        string sourceReportPath = "",
        TraceSummaryOptionsData options = null
    ) =>
        TraceDictionaryProjection.BuildJsonSafeLease(
            BuildPlainSummary(report, sourceReportPath, options),
            "battle-sim-trace-summary-file",
            LifetimeDomain.Request,
            "BattleSimTraceSummaryBuilder.BuildFileLease"
        );

    private System.Collections.Generic.Dictionary<string, object> BuildPlainSummary(
        BattleSimScenarioReport report,
        string sourceReportPath,
        TraceSummaryOptionsData options
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
                string profileId = profileEntry.Profile?.ProfileId.ToString() ?? "";
                foreach (BattleSimRunReport run in profileEntry.Runs)
                {
                    if (run == null)
                        continue;
                    CompactRunTraceData compactRun = BuildCompactRunTraceDataManaged(
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

        var comparisons = new List<object>();
        var profileSummaries = new List<object>();
        if (report != null)
        {
            foreach (BattleSimProfileComparison comparison in report.Comparisons)
                comparisons.Add(BattleSimFilePayloadProjection.BuildComparisonFacts(comparison));
            foreach (BattleSimProfileReportEntry entry in report.ProfileEntries)
            {
                profileSummaries.Add(
                    PlainMap(
                        ("profile", BattleSimFilePayloadProjection.BuildProfileFacts(entry?.Profile)),
                        ("summary", BattleSimFilePayloadProjection.BuildSummaryFacts(entry?.Summary))
                    )
                );
            }
        }

        var runs = new List<object>();
        foreach (CompactRunTraceData compactRun in compactRuns)
            runs.Add(compactRun.ToPlainDictionary());
        return PlainMap(
            ("source_report", sourceReportPath ?? ""),
            ("scenario", BattleSimFilePayloadProjection.BuildScenarioFacts(report?.ScenarioDef)),
            ("batch_id", 0),
            ("generated_at_unix", report?.GeneratedAtUnix ?? 0),
            ("profile_count", report?.ProfileEntries.Count ?? 0),
            ("run_count", compactRuns.Count),
            ("trace_count", traceCount),
            ("elapsed_seconds", 0.0f),
            ("ended_count", 0),
            ("avg_iterations", 0.0f),
            ("avg_timeline_steps", 0.0f),
            ("win_rate", PlainMap()),
            ("comparisons", comparisons),
            ("profile_summaries", profileSummaries),
            ("global", PlainMap()),
            ("player", PlainMap()),
            ("hostile", PlainMap()),
            ("trace_compaction", summaryOptions.ToPlainDictionary()),
            ("runs", runs)
        );
    }

    private CompactRunTraceData BuildCompactRunTraceDataManaged(
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

        foreach (
            BattleAiTurnTraceProjection trace
            in runEntry?.AiTurnTraces ?? System.Array.Empty<BattleAiTurnTraceProjection>()
        )
        {
            if (trace == null)
                continue;
            result.TraceCount++;
            string factionId = trace.FactionId ?? "";
            string actionId = trace.ActionId ?? "";
            CompactCommandSummaryData commandSummary = SummarizeTraceCommandData(trace.Command);
            string commandType = commandSummary.CommandType;
            IncrementNestedCounterData(result.ActionCountsByFaction, factionId, actionId);
            IncrementNestedCounterData(result.CommandCountsByFaction, factionId, commandType);
            if (commandType == "wait")
                IncrementNestedCounterData(result.WaitCountsByFaction, factionId, actionId);

            List<CompactActionTraceData> actionTraces = SummarizeActionTracesData(
                trace.ActionTraces,
                factionId,
                result.BlockReasonsByFaction,
                topCandidateLimit
            );
            if (factionId != focusFactionId)
                continue;

            var turnSummary = new CompactTurnTraceData
            {
                TurnStartedTu = trace.TurnStartedTu,
                UnitId = trace.UnitId ?? "",
                UnitName = trace.UnitName ?? "",
                FactionId = factionId,
                BrainId = trace.BrainId ?? "",
                StateId = trace.StateId ?? "",
                ActionId = actionId,
                ReasonText = trace.ReasonText ?? "",
                Command = commandSummary,
                Score = SummarizeScoreInputData(trace.ScoreInput),
                DecisionTargetSnapshots = SummarizeUnitSnapshotsData(
                    trace.DecisionTargetSnapshots
                ),
                ExecutionResult = SummarizeExecutionResultData(trace.ExecutionResult),
                ActionTraces = actionTraces,
            };
            result.FocusTurns.Add(turnSummary);
            if (commandType == "wait")
                result.FocusWaitTurns.Add(turnSummary);
        }

        foreach (
            (string factionId, BattleSimUnitMetricsSnapshot faction)
            in runEntry?.MetricsSnapshot?.Factions
                ?? new System.Collections.Generic.Dictionary<
                    string,
                    BattleSimUnitMetricsSnapshot
                >(StringComparer.Ordinal)
        )
        {
            result.FactionFacts[factionId] = faction?.BuildFactionPlain() ?? PlainMap();
        }
        foreach (
            (string unitId, BattleSimUnitMetricsSnapshot unit)
            in runEntry?.MetricsSnapshot?.Units
                ?? new System.Collections.Generic.Dictionary<
                    string,
                    BattleSimUnitMetricsSnapshot
                >(StringComparer.Ordinal)
        )
        {
            result.UnitFacts[unitId] = unit?.BuildPlain() ?? PlainMap();
        }
        return result;
    }

    private List<CompactActionTraceData> SummarizeActionTracesData(
        IReadOnlyList<AiActionTrace> actionTraces,
        string factionId,
        System.Collections.Generic.Dictionary<
            string,
            System.Collections.Generic.Dictionary<string, int>
        > blockReasonsByFaction,
        int topCandidateLimit
    )
    {
        var summaries = new List<CompactActionTraceData>();
        foreach (AiActionTrace trace in actionTraces ?? System.Array.Empty<AiActionTrace>())
        {
            if (trace == null)
                continue;
            foreach ((string reason, int count) in trace.BlockReasons)
                IncrementNestedCounterData(blockReasonsByFaction, factionId, reason, count);
            summaries.Add(
                new CompactActionTraceData
                {
                    TraceId = trace.TraceId.ToString(),
                    ActionId = trace.ActionId ?? "",
                    Chosen = trace.Chosen,
                    ScoreBucketId = trace.ScoreBucketId ?? "",
                    MetadataFacts = new System.Collections.Generic.Dictionary<string, object>(
                        trace.Metadata,
                        StringComparer.Ordinal
                    ),
                    BlockReasons = new System.Collections.Generic.Dictionary<string, int>(
                        trace.BlockReasons,
                        StringComparer.Ordinal
                    ),
                    BlockedCount = trace.BlockedCount,
                    CandidateCount = trace.CandidateCount,
                    EvaluationCount = trace.EvaluationCount,
                    PreviewRejectCount = trace.PreviewRejectCount,
                    TopCandidates = SummarizeTopCandidatesData(
                        trace.TopCandidates,
                        topCandidateLimit
                    ),
                }
            );
        }
        return summaries;
    }

    private List<CompactTopCandidateData> SummarizeTopCandidatesData(
        IReadOnlyList<AiCandidateSummary> candidates,
        int limit
    )
    {
        var summaries = new List<CompactTopCandidateData>();
        foreach (
            AiCandidateSummary candidate
            in candidates ?? System.Array.Empty<AiCandidateSummary>()
        )
        {
            if (candidate == null || summaries.Count >= limit)
                break;
            CompactScoreInputData scoreSummary = SummarizeScoreInputData(candidate.ScoreInput);
            var summary = new CompactTopCandidateData
            {
                Label = candidate.Label ?? "",
                TotalScore = candidate.TotalScore,
                PredictedDistance = ReadPlainInt(
                    candidate.ExtraFields,
                    "predicted_distance",
                    -1
                ),
                Command = SummarizeTraceCommandData(candidate.Command),
                Score = scoreSummary,
            };
            CopyOptionalCandidateIntData(summary, candidate.ExtraFields, "screening_bonus");
            CopyOptionalCandidateIntData(summary, candidate.ExtraFields, "screening_penalty");
            CopyOptionalCandidateIntData(
                summary,
                candidate.ExtraFields,
                "screening_path_cost_delta"
            );
            CopyOptionalCandidateIntData(
                summary,
                candidate.ExtraFields,
                "screening_base_path_cost"
            );
            CopyOptionalCandidateIntData(
                summary,
                candidate.ExtraFields,
                "screening_blocked_path_cost"
            );
            CopyOptionalCandidateIntData(
                summary,
                candidate.ExtraFields,
                "screening_current_bonus"
            );
            CopyOptionalCandidateIntData(
                summary,
                candidate.ExtraFields,
                "screening_candidate_bonus"
            );
            CopyOptionalCandidateIntData(
                summary,
                candidate.ExtraFields,
                "screening_uncapped_bonus"
            );
            CopyOptionalCandidateStringData(
                summary,
                candidate.ExtraFields,
                "screening_threat_unit_id"
            );
            CopyOptionalCandidateStringData(
                summary,
                candidate.ExtraFields,
                "screening_protected_unit_id"
            );
            CopyOptionalCandidateBoolData(
                summary,
                candidate.ExtraFields,
                "screening_on_shortest_path"
            );
            CopyOptionalCandidateBoolData(
                summary,
                candidate.ExtraFields,
                "screening_keeps_contact"
            );
            CopyOptionalCandidateBoolData(
                summary,
                candidate.ExtraFields,
                "screening_can_counterattack"
            );
            CopyOptionalCandidateBoolData(
                summary,
                candidate.ExtraFields,
                "screening_hard_block"
            );
            CopyOptionalCandidateBoolData(
                summary,
                candidate.ExtraFields,
                "screening_distance_band_capped"
            );
            summaries.Add(summary);
        }
        return summaries;
    }

    private CompactCommandSummaryData SummarizeTraceCommandData(AiCommandSummary command)
    {
        if (command == null)
            return new CompactCommandSummaryData();
        return new CompactCommandSummaryData
        {
            CommandType = command.CommandType ?? "",
            UnitId = command.UnitId ?? "",
            SkillId = command.SkillId ?? "",
            SkillVariantId = command.SkillVariantId ?? "",
            TargetUnitId = command.TargetUnitId ?? "",
            TargetUnitIds = StringifyStringNames(command.TargetUnitIds),
            TargetCoord = AsString(command.TargetCoord),
            TargetCoords = StringifyCoords(command.TargetCoords),
        };
    }

    private CompactExecutionResultData SummarizeExecutionResultData(
        BattleAiTraceExecutionResultProjection result
    )
    {
        if (result == null)
            return new CompactExecutionResultData();
        var unitResults = new List<CompactUnitResultData>();
        foreach (
            BattleAiTraceUnitResultProjection unitResult
            in result.UnitResults ?? System.Array.Empty<BattleAiTraceUnitResultProjection>()
        )
        {
            if (unitResult != null)
                unitResults.Add(SummarizeUnitResultData(unitResult));
        }
        return new CompactExecutionResultData
        {
            CommandType = result.CommandType ?? "",
            SkillId = result.SkillId ?? "",
            SkillVariantId = result.SkillVariantId ?? "",
            ChangedUnitIds = new List<string>(result.ChangedUnitIds ?? System.Array.Empty<string>()),
            TrackedUnitIds = new List<string>(result.TrackedUnitIds ?? System.Array.Empty<string>()),
            UnitResults = unitResults,
            LogLines = new List<string>(result.LogLines ?? System.Array.Empty<string>()),
            ReportEntries = result.ReportEntries
                ?? System.Array.Empty<IReadOnlyDictionary<string, object>>(),
        };
    }

    private CompactUnitResultData SummarizeUnitResultData(
        BattleAiTraceUnitResultProjection result
    ) =>
        new()
        {
            UnitId = result?.UnitId ?? "",
            Before = SummarizeUnitSnapshotData(result?.Before),
            After = SummarizeUnitSnapshotData(result?.After),
            HpDelta = result?.HpDelta ?? 0,
            HpDamage = result?.HpDamage ?? 0,
            HpHealing = result?.HpHealing ?? 0,
            ShieldDelta = result?.ShieldDelta ?? 0,
            ShieldDamage = result?.ShieldDamage ?? 0,
            ShieldRestored = result?.ShieldRestored ?? 0,
            Killed = result?.Killed ?? false,
            Revived = result?.Revived ?? false,
            Moved = result?.Moved ?? false,
        };

    private List<CompactUnitSnapshotData> SummarizeUnitSnapshotsData(
        IReadOnlyList<BattleAiTraceUnitSnapshotProjection> snapshots
    )
    {
        var result = new List<CompactUnitSnapshotData>();
        foreach (
            BattleAiTraceUnitSnapshotProjection snapshot
            in snapshots ?? System.Array.Empty<BattleAiTraceUnitSnapshotProjection>()
        )
        {
            CompactUnitSnapshotData compact = SummarizeUnitSnapshotData(snapshot);
            if (!compact.IsEmpty)
                result.Add(compact);
        }
        return result;
    }

    private static CompactUnitSnapshotData SummarizeUnitSnapshotData(
        BattleAiTraceUnitSnapshotProjection snapshot
    ) =>
        new()
        {
            UnitId = snapshot?.UnitId ?? "",
            DisplayName = snapshot?.DisplayName ?? "",
            FactionId = snapshot?.FactionId ?? "",
            Coord = snapshot?.Coord ?? "",
            Alive = snapshot?.Alive ?? false,
            Hp = snapshot?.Hp ?? 0,
            HpMax = snapshot?.HpMax ?? 0,
            ShieldHp = snapshot?.ShieldHp ?? 0,
            ShieldMaxHp = snapshot?.ShieldMaxHp ?? 0,
            Ap = snapshot?.Ap ?? 0,
            MovePoints = snapshot?.MovePoints ?? 0,
        };

    private CompactScoreInputData SummarizeScoreInputData(
        IReadOnlyDictionary<string, object> score
    )
    {
        if (score == null)
            return new CompactScoreInputData();
        return new CompactScoreInputData
        {
            TotalScore = ReadPlainInt(score, "total_score"),
            ScoreBucketId = ReadPlainString(score, "score_bucket_id"),
            ScoreBucketPriority = ReadPlainInt(score, "score_bucket_priority"),
            CommandType = ReadPlainString(score, "command_type"),
            SkillId = ReadPlainString(score, "skill_id"),
            TargetCount = ReadPlainInt(score, "target_count"),
            EffectiveTargetCount = ReadPlainInt(score, "effective_target_count"),
            EnemyTargetCount = ReadPlainInt(score, "enemy_target_count"),
            AllyTargetCount = ReadPlainInt(score, "ally_target_count"),
            TargetUnitIds = ReadPlainStringList(score, "target_unit_ids"),
            TargetCoords = ReadPlainStringList(score, "target_coords"),
            EstimatedDamage = ReadPlainInt(score, "estimated_damage"),
            EstimatedHitRatePercent = ReadPlainInt(score, "estimated_hit_rate_percent"),
            SaveEstimatesByTargetId = SummarizeSaveEstimatesByTargetIdData(
                ReadPlainDictionary(score, "save_estimates_by_target_id")
            ),
            EstimatedLethalTargetCount = ReadPlainInt(
                score,
                "estimated_lethal_target_count"
            ),
            EstimatedLethalThreatTargetCount = ReadPlainInt(
                score,
                "estimated_lethal_threat_target_count"
            ),
            EstimatedLethalTargetIds = ReadPlainStringList(
                score,
                "estimated_lethal_target_ids"
            ),
            EstimatedLethalThreatTargetIds = ReadPlainStringList(
                score,
                "estimated_lethal_threat_target_ids"
            ),
            EstimatedControlTargetIds = ReadPlainStringList(
                score,
                "estimated_control_target_ids"
            ),
            EstimatedControlThreatTargetIds = ReadPlainStringList(
                score,
                "estimated_control_threat_target_ids"
            ),
            HasPostActionThreatProjection = ReadPlainBool(
                score,
                "has_post_action_threat_projection"
            ),
            ProjectedActorCoord = ReadPlainString(score, "projected_actor_coord"),
            PreActionThreatUnitIds = ReadPlainStringList(
                score,
                "pre_action_threat_unit_ids"
            ),
            PreActionThreatCount = ReadPlainInt(score, "pre_action_threat_count"),
            PreActionThreatExpectedDamage = ReadPlainInt(
                score,
                "pre_action_threat_expected_damage"
            ),
            PreActionSurvivalMargin = ReadPlainInt(score, "pre_action_survival_margin"),
            PreActionIsLethalSurvivalRisk = ReadPlainBool(
                score,
                "pre_action_is_lethal_survival_risk"
            ),
            PostActionRemainingThreatUnitIds = ReadPlainStringList(
                score,
                "post_action_remaining_threat_unit_ids"
            ),
            PostActionRemainingThreatCount = ReadPlainInt(
                score,
                "post_action_remaining_threat_count"
            ),
            PostActionRemainingThreatExpectedDamage = ReadPlainInt(
                score,
                "post_action_remaining_threat_expected_damage"
            ),
            PostActionSurvivalMargin = ReadPlainInt(
                score,
                "post_action_survival_margin"
            ),
            PostActionIsLethalSurvivalRisk = ReadPlainBool(
                score,
                "post_action_is_lethal_survival_risk"
            ),
            HitPayoffScore = ReadPlainInt(score, "hit_payoff_score"),
            PositionObjectiveKind = ReadPlainString(score, "position_objective_kind"),
            PositionObjectiveScore = ReadPlainInt(score, "position_objective_score"),
            ResourceCostScore = ReadPlainInt(score, "resource_cost_score"),
            DistanceToPrimaryCoord = ReadPlainInt(score, "distance_to_primary_coord", -1),
            ApCost = ReadPlainInt(score, "ap_cost"),
            StaminaCost = ReadPlainInt(score, "stamina_cost"),
            MpCost = ReadPlainInt(score, "mp_cost"),
            AuraCost = ReadPlainInt(score, "aura_cost"),
            MoveCost = ReadPlainInt(score, "move_cost"),
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
                    : score.skill_id.ToString(),
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
                        Ability = estimate.Ability ?? "",
                        SaveTag = estimate.SaveTag ?? "",
                        AdvantageState = estimate.AdvantageState ?? "",
                        Immune = estimate.Immune,
                        HitCount = Math.Max(estimate.HitCount, 1),
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
        IReadOnlyDictionary<string, object> estimatesByTarget
    )
    {
        var summary = new System.Collections.Generic.Dictionary<
            string,
            List<CompactSaveEstimateData>
        >(StringComparer.Ordinal);
        if (estimatesByTarget == null)
            return summary;
        var targetKeys = new List<string>(estimatesByTarget.Keys);
        targetKeys.Sort(StringComparer.Ordinal);
        foreach (string targetId in targetKeys)
        {
            if (
                string.IsNullOrEmpty(targetId)
                || !estimatesByTarget.TryGetValue(targetId, out object rawEstimates)
                || rawEstimates is not System.Collections.IEnumerable estimates
                || rawEstimates is string
            )
            {
                continue;
            }
            var compactEstimates = new List<CompactSaveEstimateData>();
            foreach (object rawEstimate in estimates)
            {
                if (rawEstimate is not IReadOnlyDictionary<string, object> estimate)
                    continue;
                compactEstimates.Add(
                    new CompactSaveEstimateData
                    {
                        DamageBeforeSave = ReadPlainInt(estimate, "damage_before_save"),
                        DamageAfterSaveEstimate = ReadPlainInt(
                            estimate,
                            "damage_after_save_estimate"
                        ),
                        DamageOnSaveSuccess = ReadPlainInt(
                            estimate,
                            "damage_on_save_success"
                        ),
                        SaveSuccessRatePercent = ReadPlainInt(
                            estimate,
                            "save_success_rate_percent"
                        ),
                        Dc = ReadPlainInt(estimate, "dc"),
                        Ability = ReadPlainString(estimate, "ability"),
                        SaveTag = ReadPlainString(estimate, "save_tag"),
                        AdvantageState = ReadPlainString(estimate, "advantage_state"),
                        Immune = ReadPlainBool(estimate, "immune"),
                        HitCount = ReadPlainInt(estimate, "hit_count", 1),
                    }
                );
            }
            if (compactEstimates.Count > 0)
                summary[targetId] = compactEstimates;
        }
        return summary;
    }

    private static void CopyOptionalCandidateIntData(
        CompactTopCandidateData target,
        IReadOnlyDictionary<string, object> source,
        string key
    )
    {
        if (source != null && source.ContainsKey(key))
            target.OptionalInts[key] = ReadPlainInt(source, key);
    }

    private static void CopyOptionalCandidateStringData(
        CompactTopCandidateData target,
        IReadOnlyDictionary<string, object> source,
        string key
    )
    {
        if (source != null && source.ContainsKey(key))
            target.OptionalStrings[key] = ReadPlainString(source, key);
    }

    private static void CopyOptionalCandidateBoolData(
        CompactTopCandidateData target,
        IReadOnlyDictionary<string, object> source,
        string key
    )
    {
        if (source != null && source.ContainsKey(key))
            target.OptionalBools[key] = ReadPlainBool(source, key);
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

    public sealed class TraceSummaryOptionsData
    {
        public string FocusFactionId { get; set; } = DefaultFocusFactionId;

        public int TopCandidateLimit { get; set; } = DefaultTopCandidatesPerAction;

        internal string ResolvedFocusFactionId =>
            string.IsNullOrEmpty(FocusFactionId) ? DefaultFocusFactionId : FocusFactionId;

        internal int ResolvedTopCandidateLimit => Mathf.Max(TopCandidateLimit, 0);


        internal System.Collections.Generic.Dictionary<string, object> ToPlainDictionary() =>
            PlainMap(
                ("full_trace_embedded_in_source_report", true),
                ("focus_faction_id", ResolvedFocusFactionId),
                ("focus_turns_keep_action_trace_summaries", true),
                ("top_candidates_per_action_trace", ResolvedTopCandidateLimit)
            );
    }

    private sealed class CompactRunTraceData
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
        public System.Collections.Generic.Dictionary<string, object> FactionFacts { get; } =
            new(StringComparer.Ordinal);
        public System.Collections.Generic.Dictionary<string, object> UnitFacts { get; } =
            new(StringComparer.Ordinal);
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


        public System.Collections.Generic.Dictionary<string, object> ToPlainDictionary()
        {
            var focusTurns = new List<object>();
            foreach (CompactTurnTraceData turn in FocusTurns)
                focusTurns.Add(turn.ToPlainDictionary());
            var focusWaitTurns = new List<object>();
            foreach (CompactTurnTraceData turn in FocusWaitTurns)
                focusWaitTurns.Add(turn.ToPlainDictionary());
            return PlainMap(
                ("profile_id", ProfileId),
                ("run_index", RunIndex),
                ("seed", Seed),
                ("battle_ended", BattleEnded),
                ("winner_faction_id", WinnerFactionId),
                ("final_tu", FinalTu),
                ("iterations", Iterations),
                ("timeline_steps", TimelineSteps),
                ("trace_count", TraceCount),
                ("factions", FactionFacts),
                ("units", UnitFacts),
                ("action_counts_by_faction", BoxNestedIntMap(ActionCountsByFaction)),
                ("command_counts_by_faction", BoxNestedIntMap(CommandCountsByFaction)),
                ("wait_counts_by_faction", BoxNestedIntMap(WaitCountsByFaction)),
                ("block_reasons_by_faction", BoxNestedIntMap(BlockReasonsByFaction)),
                ("focus_turns", focusTurns),
                ("focus_wait_turns", focusWaitTurns)
            );
        }
    }

    private sealed class CompactTurnTraceData
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


        public System.Collections.Generic.Dictionary<string, object> ToPlainDictionary()
        {
            var snapshots = new List<object>();
            foreach (CompactUnitSnapshotData snapshot in DecisionTargetSnapshots)
                snapshots.Add(snapshot.ToPlainDictionary());
            var actionTraces = new List<object>();
            foreach (CompactActionTraceData trace in ActionTraces)
                actionTraces.Add(trace.ToPlainDictionary());
            return PlainMap(
                ("turn_started_tu", TurnStartedTu),
                ("unit_id", UnitId),
                ("unit_name", UnitName),
                ("faction_id", FactionId),
                ("brain_id", BrainId),
                ("state_id", StateId),
                ("action_id", ActionId),
                ("reason_text", ReasonText),
                ("command", Command.ToPlainDictionary()),
                ("score", Score.ToPlainDictionary()),
                ("decision_target_snapshots", snapshots),
                ("execution_result", ExecutionResult.ToPlainDictionary()),
                ("action_traces", actionTraces)
            );
        }
    }

    private sealed class CompactActionTraceData
    {
        public string TraceId { get; set; } = "";
        public string ActionId { get; set; } = "";
        public bool Chosen { get; set; }
        public string ScoreBucketId { get; set; } = "";
        public System.Collections.Generic.Dictionary<string, object> MetadataFacts { get; set; } =
            new(StringComparer.Ordinal);
        public System.Collections.Generic.Dictionary<string, int> BlockReasons { get; set; } =
            new(StringComparer.Ordinal);
        public int BlockedCount { get; set; }
        public int CandidateCount { get; set; }
        public int EvaluationCount { get; set; }
        public int PreviewRejectCount { get; set; }
        public List<CompactTopCandidateData> TopCandidates { get; set; } = new();


        public System.Collections.Generic.Dictionary<string, object> ToPlainDictionary()
        {
            var candidates = new List<object>();
            foreach (CompactTopCandidateData candidate in TopCandidates)
                candidates.Add(candidate.ToPlainDictionary());
            return PlainMap(
                ("trace_id", TraceId),
                ("action_id", ActionId),
                ("chosen", Chosen),
                ("score_bucket_id", ScoreBucketId),
                ("metadata", MetadataFacts),
                ("block_reasons", BoxIntMap(BlockReasons)),
                ("blocked_count", BlockedCount),
                ("candidate_count", CandidateCount),
                ("evaluation_count", EvaluationCount),
                ("preview_reject_count", PreviewRejectCount),
                ("top_candidates", candidates)
            );
        }
    }

    private sealed class CompactTopCandidateData
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


        public System.Collections.Generic.Dictionary<string, object> ToPlainDictionary()
        {
            System.Collections.Generic.Dictionary<string, object> result = PlainMap(
                ("label", Label),
                ("total_score", TotalScore),
                ("predicted_distance", PredictedDistance),
                ("command", Command.ToPlainDictionary()),
                ("score", Score.ToPlainDictionary())
            );
            foreach ((string key, int value) in OptionalInts)
                result[key] = value;
            foreach ((string key, string value) in OptionalStrings)
                result[key] = value;
            foreach ((string key, bool value) in OptionalBools)
                result[key] = value;
            return result;
        }
    }

    private sealed class CompactCommandSummaryData
    {
        public string CommandType { get; set; } = "";
        public string UnitId { get; set; } = "";
        public string SkillId { get; set; } = "";
        public string SkillVariantId { get; set; } = "";
        public string TargetUnitId { get; set; } = "";
        public List<string> TargetUnitIds { get; set; } = new();
        public string TargetCoord { get; set; } = "";
        public List<string> TargetCoords { get; set; } = new();


        public System.Collections.Generic.Dictionary<string, object> ToPlainDictionary() =>
            PlainMap(
                ("command_type", CommandType),
                ("unit_id", UnitId),
                ("skill_id", SkillId),
                ("skill_variant_id", SkillVariantId),
                ("target_unit_id", TargetUnitId),
                ("target_unit_ids", new List<string>(TargetUnitIds)),
                ("target_coord", TargetCoord),
                ("target_coords", new List<string>(TargetCoords))
            );
    }

    private sealed class CompactExecutionResultData
    {
        public string CommandType { get; set; } = "";
        public string SkillId { get; set; } = "";
        public string SkillVariantId { get; set; } = "";
        public List<string> ChangedUnitIds { get; set; } = new();
        public List<string> TrackedUnitIds { get; set; } = new();
        public List<CompactUnitResultData> UnitResults { get; set; } = new();
        public List<string> LogLines { get; set; } = new();
        public IReadOnlyList<IReadOnlyDictionary<string, object>> ReportEntries { get; set; } =
            System.Array.Empty<IReadOnlyDictionary<string, object>>();


        public System.Collections.Generic.Dictionary<string, object> ToPlainDictionary()
        {
            var unitResults = new List<object>();
            foreach (CompactUnitResultData unitResult in UnitResults)
                unitResults.Add(unitResult.ToPlainDictionary());
            var reportEntries = new List<object>();
            foreach (
                IReadOnlyDictionary<string, object> reportEntry
                in ReportEntries ?? System.Array.Empty<IReadOnlyDictionary<string, object>>()
            )
            {
                reportEntries.Add(reportEntry ?? PlainMap());
            }
            return PlainMap(
                ("command_type", CommandType),
                ("skill_id", SkillId),
                ("skill_variant_id", SkillVariantId),
                ("changed_unit_ids", new List<string>(ChangedUnitIds)),
                ("tracked_unit_ids", new List<string>(TrackedUnitIds)),
                ("unit_results", unitResults),
                ("log_lines", new List<string>(LogLines)),
                ("report_entries", reportEntries)
            );
        }
    }

    private sealed class CompactUnitResultData
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


        public System.Collections.Generic.Dictionary<string, object> ToPlainDictionary() =>
            PlainMap(
                ("unit_id", UnitId),
                ("before", Before.ToPlainDictionary()),
                ("after", After.ToPlainDictionary()),
                ("hp_delta", HpDelta),
                ("hp_damage", HpDamage),
                ("hp_healing", HpHealing),
                ("shield_delta", ShieldDelta),
                ("shield_damage", ShieldDamage),
                ("shield_restored", ShieldRestored),
                ("killed", Killed),
                ("revived", Revived),
                ("moved", Moved)
            );
    }

    private sealed class CompactUnitSnapshotData
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


        public System.Collections.Generic.Dictionary<string, object> ToPlainDictionary() =>
            PlainMap(
                ("unit_id", UnitId),
                ("display_name", DisplayName),
                ("faction_id", FactionId),
                ("coord", Coord),
                ("alive", Alive),
                ("hp", Hp),
                ("hp_max", HpMax),
                ("shield_hp", ShieldHp),
                ("shield_max_hp", ShieldMaxHp),
                ("ap", Ap),
                ("move_points", MovePoints)
            );
    }

    private sealed class CompactScoreInputData
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


        public System.Collections.Generic.Dictionary<string, object> ToPlainDictionary()
        {
            var saveEstimates = new System.Collections.Generic.Dictionary<string, object>(
                StringComparer.Ordinal
            );
            foreach ((string targetId, List<CompactSaveEstimateData> estimates) in SaveEstimatesByTargetId)
            {
                var values = new List<object>();
                foreach (CompactSaveEstimateData estimate in estimates)
                    values.Add(estimate.ToPlainDictionary());
                saveEstimates[targetId] = values;
            }
            return PlainMap(
                ("total_score", TotalScore),
                ("score_bucket_id", ScoreBucketId),
                ("score_bucket_priority", ScoreBucketPriority),
                ("command_type", CommandType),
                ("skill_id", SkillId),
                ("target_count", TargetCount),
                ("effective_target_count", EffectiveTargetCount),
                ("enemy_target_count", EnemyTargetCount),
                ("ally_target_count", AllyTargetCount),
                ("target_unit_ids", new List<string>(TargetUnitIds)),
                ("target_coords", new List<string>(TargetCoords)),
                ("estimated_damage", EstimatedDamage),
                ("estimated_hit_rate_percent", EstimatedHitRatePercent),
                ("save_estimates_by_target_id", saveEstimates),
                ("estimated_lethal_target_count", EstimatedLethalTargetCount),
                ("estimated_lethal_threat_target_count", EstimatedLethalThreatTargetCount),
                ("estimated_lethal_target_ids", new List<string>(EstimatedLethalTargetIds)),
                (
                    "estimated_lethal_threat_target_ids",
                    new List<string>(EstimatedLethalThreatTargetIds)
                ),
                ("estimated_control_target_ids", new List<string>(EstimatedControlTargetIds)),
                (
                    "estimated_control_threat_target_ids",
                    new List<string>(EstimatedControlThreatTargetIds)
                ),
                ("has_post_action_threat_projection", HasPostActionThreatProjection),
                ("projected_actor_coord", ProjectedActorCoord),
                ("pre_action_threat_unit_ids", new List<string>(PreActionThreatUnitIds)),
                ("pre_action_threat_count", PreActionThreatCount),
                ("pre_action_threat_expected_damage", PreActionThreatExpectedDamage),
                ("pre_action_survival_margin", PreActionSurvivalMargin),
                ("pre_action_is_lethal_survival_risk", PreActionIsLethalSurvivalRisk),
                (
                    "post_action_remaining_threat_unit_ids",
                    new List<string>(PostActionRemainingThreatUnitIds)
                ),
                ("post_action_remaining_threat_count", PostActionRemainingThreatCount),
                (
                    "post_action_remaining_threat_expected_damage",
                    PostActionRemainingThreatExpectedDamage
                ),
                ("post_action_survival_margin", PostActionSurvivalMargin),
                ("post_action_is_lethal_survival_risk", PostActionIsLethalSurvivalRisk),
                ("hit_payoff_score", HitPayoffScore),
                ("position_objective_kind", PositionObjectiveKind),
                ("position_objective_score", PositionObjectiveScore),
                ("resource_cost_score", ResourceCostScore),
                ("distance_to_primary_coord", DistanceToPrimaryCoord),
                ("ap_cost", ApCost),
                ("stamina_cost", StaminaCost),
                ("mp_cost", MpCost),
                ("aura_cost", AuraCost),
                ("move_cost", MoveCost)
            );
        }
    }

    private sealed class CompactSaveEstimateData
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


        public System.Collections.Generic.Dictionary<string, object> ToPlainDictionary() =>
            PlainMap(
                ("damage_before_save", DamageBeforeSave),
                ("damage_after_save_estimate", DamageAfterSaveEstimate),
                ("damage_on_save_success", DamageOnSaveSuccess),
                ("save_success_rate_percent", SaveSuccessRatePercent),
                ("dc", Dc),
                ("ability", Ability),
                ("save_tag", SaveTag),
                ("advantage_state", AdvantageState),
                ("immune", Immune),
                ("hit_count", HitCount)
            );
    }

    private static System.Collections.Generic.Dictionary<string, object> PlainMap(
        params (string Key, object Value)[] entries
    )
    {
        var result = new System.Collections.Generic.Dictionary<string, object>(
            StringComparer.Ordinal
        );
        foreach ((string key, object value) in entries)
            result[key] = value;
        return result;
    }

    private static System.Collections.Generic.Dictionary<string, object> BoxIntMap(
        IReadOnlyDictionary<string, int> source
    )
    {
        var result = new System.Collections.Generic.Dictionary<string, object>(
            StringComparer.Ordinal
        );
        if (source != null)
            foreach ((string key, int value) in source)
                result[key] = value;
        return result;
    }

    private static System.Collections.Generic.Dictionary<string, object> BoxNestedIntMap(
        IReadOnlyDictionary<
            string,
            System.Collections.Generic.Dictionary<string, int>
        > source
    )
    {
        var result = new System.Collections.Generic.Dictionary<string, object>(
            StringComparer.Ordinal
        );
        if (source != null)
            foreach ((string key, System.Collections.Generic.Dictionary<string, int> value) in source)
                result[key] = BoxIntMap(value);
        return result;
    }

    private static object ReadPlainObject(
        IReadOnlyDictionary<string, object> source,
        string key
    ) =>
        source != null && source.TryGetValue(key, out object value) ? value : null;

    private static IReadOnlyDictionary<string, object> ReadPlainDictionary(
        IReadOnlyDictionary<string, object> source,
        string key
    ) => ReadPlainObject(source, key) as IReadOnlyDictionary<string, object>;

    private static string ReadPlainString(
        IReadOnlyDictionary<string, object> source,
        string key,
        string fallback = ""
    )
    {
        object value = ReadPlainObject(source, key);
        return value switch
        {
            null => fallback,
            string text => text,
            StringName name => name.ToString(),
            _ => value.ToString() ?? fallback,
        };
    }

    private static int ReadPlainInt(
        IReadOnlyDictionary<string, object> source,
        string key,
        int fallback = 0
    )
    {
        object value = ReadPlainObject(source, key);
        return value switch
        {
            int number => number,
            long number => (int)number,
            float number => (int)number,
            double number => (int)number,
            _ => int.TryParse(value?.ToString(), out int parsed) ? parsed : fallback,
        };
    }

    private static bool ReadPlainBool(
        IReadOnlyDictionary<string, object> source,
        string key,
        bool fallback = false
    )
    {
        object value = ReadPlainObject(source, key);
        return value switch
        {
            bool flag => flag,
            _ => bool.TryParse(value?.ToString(), out bool parsed) ? parsed : fallback,
        };
    }

    private List<string> ReadPlainStringList(
        IReadOnlyDictionary<string, object> source,
        string key
    )
    {
        var result = new List<string>();
        object raw = ReadPlainObject(source, key);
        if (raw is not System.Collections.IEnumerable values || raw is string)
            return result;
        foreach (object value in values)
            result.Add(AsString(value));
        return result;
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


}
