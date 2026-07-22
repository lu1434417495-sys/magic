using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_ai_trace_projection_lease_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        BattleAiTurnTraceProjection trace = BuildTrace();

        _test.True(
            Throws<InvalidOperationException>(
                () =>
                {
                    using GodotProjectionLease<GDictionary> rejected =
                        TraceDictionaryProjection.BuildLease(
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["level_1"] = new Dictionary<string, object>(
                                    StringComparer.Ordinal
                                )
                                {
                                    ["level_2"] = new List<object>
                                    {
                                        new Dictionary<string, object>(StringComparer.Ordinal)
                                        {
                                            ["level_3"] = new object(),
                                        },
                                    },
                                },
                            },
                        "unknown_trace_value_test",
                        LifetimeDomain.Request,
                        "unknown_trace_value_test"
                    );
                }
            ),
            "Deep unknown trace values must fail instead of silently stringifying."
        );
        AssertReturnedToBaseline(baseline, "rejected deep unknown value");

        GodotProjectionLease<GDictionary> traceLease =
            BattleAiTurnTracePayloadProjection.BuildLease(trace);
        try
        {
            LifecycleAuditSnapshot active = LifecycleAuditRegistry.Shared.CaptureSnapshot();
            int ownerDelta = active.ActiveOwnerCount - baseline.ActiveOwnerCount;
            _test.Eq(
                active.ActiveLeaseCount,
                baseline.ActiveLeaseCount + 1,
                "AI trace projection should register exactly one root lease."
            );
            _test.Eq(
                active.ActiveScopeCount,
                baseline.ActiveScopeCount,
                "AI trace projection should not register an unrelated native scope."
            );
            _test.Eq(
                active.ActiveContentBorrowerCount,
                baseline.ActiveContentBorrowerCount,
                "AI trace projection should not register content borrowers."
            );

            GDictionary payload = traceLease.Value;
            _test.Eq(
                ownerDelta,
                CountContainers(payload),
                "Every dictionary and array in the trace graph must belong to the same root lease."
            );
            AssertTopLevelOrder(payload);
            AssertFixedLegacyGolden(payload);
            AssertMeteorSchema(payload);
            AssertLayeredBarrierSchema(payload);
            AssertLegacyTraceReference(trace, payload);
            AssertFingerprint(
                payload,
                11967,
                "6530816ab192e9d20994f9026e429712d49812684a3c1bb4ac5530f908863426",
                "full AI trace payload"
            );
            AssertDictionaryKeysAreStrings(payload, "trace");
            using GArray actionTraces = payload["action_traces"].AsGodotArray();
            using GDictionary actionTrace = actionTraces[0].AsGodotDictionary();
            using GDictionary metadata = actionTrace["metadata"].AsGodotDictionary();
            _test.Eq(
                metadata["source_kind"].VariantType,
                Variant.Type.StringName,
                "StringName trace values must not be minimized to strings."
            );
        }
        finally
        {
            traceLease.Dispose();
        }

        _test.True(
            Throws<ObjectDisposedException>(() => _ = traceLease.Value),
            "Closed AI trace leases must reject Value access."
        );

        AssertReturnedToBaseline(baseline, "trace projection");
        AssertStandaloneScoreSchema(baseline, trace.ScoreInput);
        AssertProfileStringNameMaps(baseline);
        AssertRunMixedLegacySummaryView(trace);
        AssertFinalUnitsSnapshotIsolation();
        Dictionary<string, object> unitSnapshot = AssertRealUnitSnapshot(baseline);
        AssertSimulationReportLease(baseline, trace, unitSnapshot);
        AssertCompactSaveEstimateEdgeParity(baseline);
        AssertJsonSafeFileBoundary(baseline, trace, unitSnapshot);
        AssertProgressLogExceptionCleanup(baseline);
        RequestTestExit(_test.Finish("Battle AI trace projection lease regression"));
    }

    private void AssertRunMixedLegacySummaryView(BattleAiTurnTraceProjection source)
    {
        IReadOnlyList<BattleAiTurnTraceProjection> views =
            RunMixed6v12MirrorAnalysis.BuildLegacyTraceSummaryViews(new[] { source });
        _test.Eq(views.Count, 1, "RunMixed trace-summary view count.");
        BattleAiTurnTraceProjection view = views[0];
        _test.Eq(view.TurnStartedTu, source.TurnStartedTu, "Minimal view turn TU.");
        _test.Eq(view.ActionId, source.ActionId, "Minimal view action id.");
        _test.Eq(view.Command.CommandType, "", "Minimal view must not retain command details.");
        _test.Eq(view.ActionTraces.Count, 0, "Minimal view must not retain action traces.");
        _test.Eq(
            view.DecisionTargetSnapshots.Count,
            0,
            "Minimal view must not retain decision snapshots."
        );
        _test.True(view.ExecutionResult == null, "Minimal view must not retain execution results.");
        _test.Eq(
            view.ScoreInput.score_bucket_id,
            source.ScoreInput.score_bucket_id,
            "Minimal view score bucket."
        );
        _test.Eq(
            view.ScoreInput.target_count,
            source.ScoreInput.target_count,
            "Minimal view target count."
        );
        _test.Eq(
            view.ScoreInput.total_score,
            source.ScoreInput.total_score,
            "Minimal view total score."
        );
        _test.Eq(
            view.ScoreInput.estimated_damage,
            0,
            "Minimal view must not retain rich score details."
        );
    }

    private void AssertFinalUnitsSnapshotIsolation()
    {
        var nested = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["hp"] = 12,
            ["coords"] = new List<object> { new Vector2I(1, 2) },
        };
        var source = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["unit_id"] = "original",
            ["nested"] = nested,
        };
        var report = new BattleSimRunReport { FinalUnits = new[] { source } };
        source["unit_id"] = "mutated_source";
        nested["hp"] = 1;
        IReadOnlyList<Dictionary<string, object>> firstRead = report.FinalUnits;
        _test.Eq((string)firstRead[0]["unit_id"], "original", "FinalUnits setter deep snapshot.");
        var firstNested = (Dictionary<string, object>)firstRead[0]["nested"];
        _test.Eq((int)firstNested["hp"], 12, "FinalUnits nested setter snapshot.");
        firstRead[0]["unit_id"] = "mutated_getter";
        firstNested["hp"] = 2;
        IReadOnlyList<Dictionary<string, object>> secondRead = report.FinalUnits;
        _test.Eq(
            (string)secondRead[0]["unit_id"],
            "original",
            "FinalUnits getter must not expose internal dictionaries."
        );
        _test.Eq(
            (int)((Dictionary<string, object>)secondRead[0]["nested"])["hp"],
            12,
            "FinalUnits getter must deep detach nested dictionaries."
        );
    }

    private void AssertCompactSaveEstimateEdgeParity(LifecycleAuditSnapshot baseline)
    {
        var estimate = new BattleAiScoreService.DamageSaveEstimate
        {
            DamageBeforeSave = 8,
            DamageAfterSaveEstimate = 3,
            DamageOnSaveSuccess = 2,
            SaveSuccessRatePercent = 55,
            Dc = 14,
            Ability = null,
            SaveTag = null,
            AdvantageState = null,
            HitCount = 0,
        };
        var score = new BattleAiScoreInput { score_bucket_id = "damage", total_score = 11 };
        score.save_estimates_by_target_id["target"] = new List<
            BattleAiScoreService.DamageSaveEstimate
        > { estimate };
        var trace = new BattleAiTurnTraceProjection
        {
            FactionId = "hostile",
            ScoreInput = score,
        };
        BattleSimScenarioReport report = BuildSimulationReport(
            trace,
            new Dictionary<string, object>(StringComparer.Ordinal),
            includeUnknown: false
        );
        using (
            GodotProjectionLease<GDictionary> lease =
                new BattleSimTraceSummaryBuilder().BuildLease(
                report,
                "edge.json",
                new BattleSimTraceSummaryBuilder.TraceSummaryOptionsData
                {
                    FocusFactionId = "hostile",
                }
            )
        )
        {
            using GArray runs = lease.Value["runs"].AsGodotArray();
            using GDictionary run = runs[0].AsGodotDictionary();
            using GArray turns = run["focus_turns"].AsGodotArray();
            using GDictionary turn = turns[0].AsGodotDictionary();
            using GDictionary compactScore = turn["score"].AsGodotDictionary();
            using GDictionary estimates = compactScore[
                "save_estimates_by_target_id"
            ].AsGodotDictionary();
            using GArray targetEstimates = estimates["target"].AsGodotArray();
            using GDictionary compact = targetEstimates[0].AsGodotDictionary();
            Dictionary<string, object> legacy = estimate.ToTraceDictionary();
            _test.Eq(
                compact["hit_count"].AsInt32(),
                (int)legacy["hit_count"],
                "Typed compact save hit_count must retain f25 clamp semantics."
            );
            _test.Eq(
                compact["ability"].AsString(),
                (string)legacy["ability"],
                "Typed compact save ability null semantics."
            );
            _test.Eq(
                compact["save_tag"].AsString(),
                (string)legacy["save_tag"],
                "Save tag null semantics."
            );
            _test.Eq(
                compact["advantage_state"].AsString(),
                (string)legacy["advantage_state"],
                "Advantage-state null semantics."
            );
        }
        AssertReturnedToBaseline(baseline, "compact save edge parity");
    }

    private void AssertJsonSafeFileBoundary(
        LifecycleAuditSnapshot baseline,
        BattleAiTurnTraceProjection trace,
        IReadOnlyDictionary<string, object> unitSnapshot
    )
    {
        var facts = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["coord"] = new Vector2I(3, 4),
            ["name"] = new StringName("arcane"),
            ["coords"] = new List<Vector2I> { new(1, 2) },
        };
        using (
            GodotProjectionLease<GDictionary> lease =
                TraceDictionaryProjection.BuildJsonSafeLease(
                    facts,
                    "json-safe-file-fixture",
                    LifetimeDomain.Request,
                    "json-safe-file-fixture"
                )
        )
        {
            string json = Json.Stringify(lease.Value);
            _test.Eq(
                json,
                "{\"coord\":{\"x\":3,\"y\":4},\"coords\":[{\"x\":1,\"y\":2}],\"name\":\"arcane\"}",
                "JSON-safe writer must preserve f25 Vector2I/StringName serialized schema."
            );
        }
        AssertReturnedToBaseline(baseline, "JSON-safe scalar fixture");

        BattleSimRunReport traceRun = new() { Seed = 17 };
        traceRun.SetFinalDecision(
            BattleObjectiveTestFactory.CreateEliminationDecision(
                "hostile",
                decisionTu: 23
            )
        );
        using (
            GodotProjectionLease<GDictionary> traceLease =
                BattleSimFilePayloadProjection.BuildFlattenedTraceLease(
                    trace,
                    "scenario",
                    "profile",
                    traceRun
                )
        )
        {
            _test.Eq(
                traceLease.Value["objective_mode"].AsString(),
                "elimination",
                "File trace should preserve objective mode."
            );
            _test.Eq(
                traceLease.Value["outcome"].AsString(),
                "player_failure",
                "File trace should preserve typed outcome."
            );
            _test.Eq(
                traceLease.Value["end_reason"].AsString(),
                "elimination_allies_defeated",
                "File trace should preserve typed end reason."
            );
            _test.Eq(
                traceLease.Value["decision_tu"].AsInt32(),
                23,
                "File trace should preserve decision TU."
            );
            _test.Eq(
                traceLease.Value["winner_faction_id"].AsString(),
                "hostile",
                "File trace winner should be a typed-outcome projection."
            );
            using GDictionary command = traceLease.Value["command"].AsGodotDictionary();
            using GDictionary targetCoord = command["target_coord"].AsGodotDictionary();
            _test.Eq(targetCoord["x"].AsInt32(), 3, "File trace target coord x.");
            _test.Eq(targetCoord["y"].AsInt32(), 4, "File trace target coord y.");
            using GArray actions = traceLease.Value["action_traces"].AsGodotArray();
            using GDictionary action = actions[0].AsGodotDictionary();
            using GDictionary metadata = action["metadata"].AsGodotDictionary();
            _test.Eq(
                metadata["source_kind"].VariantType,
                Variant.Type.String,
                "File trace StringName values must serialize as strings."
            );
        }
        AssertReturnedToBaseline(baseline, "JSON-safe flattened trace");

        BattleSimScenarioReport report = BuildSimulationReport(
            trace,
            unitSnapshot,
            includeUnknown: false
        );
        Vector2I expectedCoord = (Vector2I)unitSnapshot["coord"];
        Vector2I storedCoord = (Vector2I)report.ProfileEntries[0].Runs[0].FinalUnits[0]["coord"];
        _test.Eq(storedCoord, expectedCoord, "FinalUnits must retain the managed coord value.");
        using (
            GodotProjectionLease<GDictionary> reportLease =
                BattleSimFilePayloadProjection.BuildReportLease(report)
        )
        {
            using GArray profiles = reportLease.Value["profile_entries"].AsGodotArray();
            using GDictionary entry = profiles[0].AsGodotDictionary();
            using GArray runs = entry["runs"].AsGodotArray();
            using GDictionary run = runs[0].AsGodotDictionary();
            using GArray units = run["final_units"].AsGodotArray();
            using GDictionary unit = units[0].AsGodotDictionary();
            _test.Eq(
                unit["coord"].VariantType,
                Variant.Type.Dictionary,
                "File report final-unit coord must be JSON-safe."
            );
            using GDictionary coord = unit["coord"].AsGodotDictionary();
            _test.Eq(coord["x"].AsInt32(), expectedCoord.X, "File report final-unit coord x.");
            _test.Eq(coord["y"].AsInt32(), expectedCoord.Y, "File report final-unit coord y.");
        }
        AssertReturnedToBaseline(baseline, "JSON-safe report file projection");
    }

    private void AssertTopLevelOrder(GDictionary payload)
    {
        var keys = new List<string>();
        foreach (Variant key in payload.Keys)
            keys.Add(key.AsString());
        _test.Eq(
            string.Join(",", keys),
            "battle_id,turn_started_tu,unit_id,unit_name,faction_id,brain_id,state_id,action_id,reason_text,command,transition,score_input,action_traces,decision_target_snapshots,execution_result",
            "AI trace projection must preserve authored insertion order and optional-key order."
        );
    }

    private void AssertFixedLegacyGolden(GDictionary payload)
    {
        string golden = string.Join(
            "|",
            ReadText(payload, "battle_id"),
            payload["turn_started_tu"].AsInt32(),
            ReadText(payload, "unit_id"),
            ReadText(payload, "faction_id"),
            ReadText(payload, "brain_id"),
            ReadText(payload, "state_id"),
            ReadText(payload, "action_id"),
            ReadText(payload, "reason_text")
        );
        _test.Eq(
            golden,
            "battle_1|15|caster|hostile|mage|engage|cast_arc_bolt|best score",
            "Trace scalar schema must match the fixed legacy golden."
        );

        using GDictionary command = payload["command"].AsGodotDictionary();
        AssertKeyOrder(
            command,
            "command_type,unit_id,skill_id,skill_variant_id,target_unit_id,target_unit_ids,target_coord,target_coords",
            "command"
        );
        _test.Eq(ReadText(command, "command_type"), "skill", "Command type golden.");
        _test.Eq(ReadText(command, "skill_id"), "arc_bolt", "Command skill golden.");
        _test.Eq(
            command["target_coord"].AsVector2I(),
            new Vector2I(3, 4),
            "Command target coord golden."
        );

        using GDictionary transition = payload["transition"].AsGodotDictionary();
        AssertKeyOrder(
            transition,
            "previous_state_id,state_id,rule_id,reason,matched_conditions",
            "transition"
        );
        using GArray conditions = transition["matched_conditions"].AsGodotArray();
        _test.Eq(conditions.Count, 1, "Transition golden should keep one condition.");
        using GDictionary condition = conditions[0].AsGodotDictionary();
        AssertKeyOrder(
            condition,
            "predicate,basis_points,max_distance,state_ids,affordances",
            "transition condition"
        );
        _test.Eq(ReadText(condition, "predicate"), "distance", "Predicate golden.");
        _test.Eq(condition["basis_points"].AsInt32(), 10000, "Basis-points golden.");
        _test.Eq(condition["max_distance"].AsInt32(), 6, "Distance golden.");

        using GArray actionTraces = payload["action_traces"].AsGodotArray();
        using GDictionary actionTrace = actionTraces[0].AsGodotDictionary();
        AssertKeyOrder(
            actionTrace,
            "trace_id,action_id,score_bucket_id,metadata,evaluation_count,blocked_count,preview_reject_count,candidate_count,block_reasons,top_candidates,chosen,best_reason_text,best_command,best_score_input,chosen_reason_text,chosen_command,chosen_score_input,candidate_trace_counters,gate_rejected,gate_rejection_reason",
            "action trace"
        );
        _test.Eq(ReadText(actionTrace, "trace_id"), "trace_1", "Action trace id golden.");
        _test.Eq(actionTrace["evaluation_count"].AsInt32(), 3, "Evaluation-count golden.");
        _test.True(actionTrace["chosen"].AsBool(), "Chosen flag golden.");

        using GDictionary execution = payload["execution_result"].AsGodotDictionary();
        AssertKeyOrder(
            execution,
            "command_type,skill_id,skill_variant_id,changed_unit_ids,tracked_unit_ids,unit_results,log_lines,report_entries",
            "execution result"
        );
        using GArray reportEntries = execution["report_entries"].AsGodotArray();
        using GDictionary reportEntry = reportEntries[0].AsGodotDictionary();
        _test.Eq(
            reportEntry["entry_type"].VariantType,
            Variant.Type.StringName,
            "Execution report StringName values must match the legacy schema."
        );
        using GArray parts = reportEntry["parts"].AsGodotArray();
        _test.Eq(parts.Count, 2, "Execution report parts golden.");
        _test.Eq(parts[1].AsInt32(), 7, "Execution report damage golden.");
    }

    private void AssertMeteorSchema(GDictionary payload)
    {
        using GDictionary scoreInput = payload["score_input"].AsGodotDictionary();
        using GArray summaries = scoreInput["target_numeric_summary"].AsGodotArray();
        _test.Eq(summaries.Count, 1, "Legacy trace filters null numeric summaries.");
        using GDictionary summary = summaries[0].AsGodotDictionary();
        _test.Eq(ReadText(summary, "target_unit_id"), "target", "Meteor target golden.");

        using GArray saveProfiles = summary["save_profile_ids"].AsGodotArray();
        _test.Eq(saveProfiles.Count, 2, "Legacy trace must preserve empty save profile ids.");
        _test.Eq(saveProfiles[0].AsString(), "", "Empty save profile golden.");
        _test.Eq(saveProfiles[1].AsString(), "reflex_half", "Save profile golden.");
        using GArray statuses = summary["status_effect_ids"].AsGodotArray();
        _test.Eq(statuses.Count, 2, "Legacy trace must preserve empty status ids.");
        _test.Eq(
            statuses[0].VariantType,
            Variant.Type.StringName,
            "Status ids must retain StringName payload type."
        );
        _test.Eq(statuses[0].AsStringName().ToString(), "", "Empty status id golden.");
        _test.Eq(statuses[1].AsStringName().ToString(), "burning", "Status id golden.");

        using GDictionary resistance =
            summary["resistance_tiers_by_damage_tag"].AsGodotDictionary();
        _test.Eq(resistance.Count, 1, "Empty resistance keys and tiers must be filtered.");
        _test.Eq(ReadText(resistance, "fire"), "half", "Resistance map golden.");

        using GArray components = summary["component_breakdown"].AsGodotArray();
        using GDictionary component = components[0].AsGodotDictionary();
        AssertKeyOrder(
            component,
            "component_id,role_label,damage_tag,expected_damage,worst_case_damage,post_save_expected_damage,post_save_worst_case_damage,pre_save_expected_damage,pre_save_worst_case_damage,resistance_tier,save_profile_id,save_estimate,worst_save_estimate,half_source_labels,double_source_labels,immune_source_labels,fixed_mitigation_source_labels,shield_absorbed_estimate,shield_absorbed_worst",
            "legacy trace meteor component"
        );
        _test.False(
            component.ContainsKey("mitigation_sources"),
            "Legacy turn trace must not expose standalone mitigation_sources."
        );
        AssertLegacyLabelArray(component, "half_source_labels", "", "fire_resistance");
        AssertLegacyLabelArray(
            component,
            "double_source_labels",
            "fire_vulnerability",
            ""
        );
        AssertLegacyLabelArray(component, "immune_source_labels", "", "flame_ward");
        AssertLegacyLabelArray(
            component,
            "fixed_mitigation_source_labels",
            "",
            "stoneskin"
        );

        using GDictionary facts =
            scoreInput["special_profile_preview_facts"].AsGodotDictionary();
        _test.Eq(facts["impact_count"].AsInt32(), 3, "Meteor facts impact-count golden.");
        using GArray factSummaries = facts["target_numeric_summary"].AsGodotArray();
        _test.Eq(factSummaries.Count, 1, "Meteor facts must use the same numeric schema.");
    }

    private void AssertLayeredBarrierSchema(GDictionary payload)
    {
        using GDictionary scoreInput = payload["score_input"].AsGodotDictionary();
        _test.True(
            scoreInput.ContainsKey("layered_barrier_projection"),
            "AI trace score schema must expose layered-barrier tactical projection."
        );
        using GDictionary projection =
            scoreInput["layered_barrier_projection"].AsGodotDictionary();
        _test.Eq(
            projection.Count,
            0,
            "Non-barrier score fixtures must project an empty layered-barrier value object."
        );
    }

    private void AssertLegacyLabelArray(
        GDictionary component,
        string key,
        string first,
        string second
    )
    {
        using GArray values = component[key].AsGodotArray();
        _test.Eq(values.Count, 2, $"Legacy {key} must preserve empty labels.");
        _test.Eq(values[0].AsString(), first, $"Legacy {key} first label golden.");
        _test.Eq(values[1].AsString(), second, $"Legacy {key} second label golden.");
    }

    private void AssertLegacyTraceReference(
        BattleAiTurnTraceProjection trace,
        GDictionary actual
    )
    {
        using GodotProjectionLease<GDictionary> expectedLease =
            TraceDictionaryProjection.BuildLease(
                trace.ToTraceDictionary(),
                "f25-turn-trace-reference",
                LifetimeDomain.Request,
                "f25ae938.BattleAiTurnTracePayloadProjection.Project"
            );
        _test.Eq(
            Json.Stringify(actual),
            Json.Stringify(expectedLease.Value),
            "Turn trace must equal the f25ae938 ToTraceDictionary reference projector."
        );
    }

    private void AssertStandaloneScoreSchema(
        LifecycleAuditSnapshot baseline,
        BattleAiScoreInput scoreInput
    )
    {
        using (GodotProjectionLease<GDictionary> lease =
            BattleAiScoreProjection.BuildLease(scoreInput))
        {
            using GArray summaries = lease.Value["target_numeric_summary"].AsGodotArray();
            using GDictionary summary = summaries[0].AsGodotDictionary();
            using GArray saveProfiles = summary["save_profile_ids"].AsGodotArray();
            using GArray statuses = summary["status_effect_ids"].AsGodotArray();
            _test.Eq(
                saveProfiles.Count,
                1,
                "Standalone f25 score projection filters empty save profile ids."
            );
            _test.Eq(
                statuses.Count,
                1,
                "Standalone f25 score projection filters empty status ids."
            );
            using GArray components = summary["component_breakdown"].AsGodotArray();
            using GDictionary component = components[0].AsGodotDictionary();
            using GArray mitigation = component["mitigation_sources"].AsGodotArray();
            _test.Eq(mitigation.Count, 3, "Standalone mitigation source count golden.");
            AssertMitigationSource(mitigation, 0, "half", "fire_resistance");
            AssertMitigationSource(mitigation, 1, "double", "fire_vulnerability");
            AssertMitigationSource(mitigation, 2, "immune", "flame_ward");
            using GArray fixedSources =
                component["fixed_mitigation_sources"].AsGodotArray();
            _test.Eq(fixedSources.Count, 1, "Standalone fixed mitigation count golden.");
            _test.True(
                lease.Value.ContainsKey("layered_barrier_projection"),
                "Standalone AI score schema must expose layered-barrier tactical projection."
            );
            using GDictionary barrierProjection =
                lease.Value["layered_barrier_projection"].AsGodotDictionary();
            _test.Eq(
                barrierProjection.Count,
                0,
                "Standalone non-barrier score must project an empty layered-barrier value object."
            );
            AssertFingerprint(
                lease.Value,
                9643,
                "de25e0c257cd77166495702c38be618a3856d1c52ca49c8ed7165cfc85951de5",
                "full standalone AI score payload"
            );
        }
        AssertReturnedToBaseline(baseline, "standalone AI score projection");
    }

    private void AssertMitigationSource(
        GArray mitigation,
        int index,
        string tier,
        string statusId
    )
    {
        using GDictionary source = mitigation[index].AsGodotDictionary();
        AssertKeyOrder(source, "tier,status_id", $"mitigation source {index}");
        _test.Eq(ReadText(source, "tier"), tier, $"Mitigation tier {index} golden.");
        _test.Eq(
            ReadText(source, "status_id"),
            statusId,
            $"Mitigation status {index} golden."
        );
    }

    private void AssertProfileStringNameMaps(LifecycleAuditSnapshot baseline)
    {
        using var profile = new BattleAiScoreProfile();
        profile.SetActionBaseScores(
            new[]
            {
                new KeyValuePair<StringName, int>("", 99),
                new KeyValuePair<StringName, int>("skill", 11),
            }
        );
        profile.SetBucketPriorities(
            new[]
            {
                new KeyValuePair<StringName, int>("", 99),
                new KeyValuePair<StringName, int>("offense", 77),
            }
        );

        using (GodotProjectionLease<GDictionary> lease =
            TraceDictionaryProjection.BuildLease(
                new Dictionary<string, object>(StringComparer.Ordinal),
                "profile-map-test",
                LifetimeDomain.Request,
                "profile-map-test"
            ))
        {
            GDictionary payload = BattleAiScoreProjection.WriteProfile(
                lease,
                BattleAiScoreProfileDefinition.FromResource(profile),
                "profile-map-test.payload"
            );
            using GDictionary actionScores = payload["action_base_scores"].AsGodotDictionary();
            using GDictionary bucketPriorities = payload["bucket_priorities"].AsGodotDictionary();
            AssertSingleStringNameMapEntry(actionScores, "skill", 11, "action base scores");
            AssertSingleStringNameMapEntry(
                bucketPriorities,
                "offense",
                77,
                "bucket priorities"
            );
            AssertFingerprint(
                payload,
                2377,
                "2f76391f375feecca1e7757d3bcbb802be773c353b7198017f9dcde75a06c89e",
                "full AI score profile payload"
            );
        }
        AssertReturnedToBaseline(baseline, "profile map projection");
    }

    private void AssertSingleStringNameMapEntry(
        GDictionary dictionary,
        StringName expectedKey,
        int expectedValue,
        string label
    )
    {
        _test.Eq(dictionary.Count, 1, $"{label} must filter empty keys.");
        Variant key = default;
        foreach (Variant candidate in dictionary.Keys)
        {
            key = candidate;
            break;
        }
        _test.Eq(
            key.VariantType,
            Variant.Type.StringName,
            $"{label} keys must retain StringName type."
        );
        _test.Eq(key.AsStringName(), expectedKey, $"{label} key golden.");
        _test.Eq(dictionary[key].AsInt32(), expectedValue, $"{label} value golden.");
    }

    private Dictionary<string, object> AssertRealUnitSnapshot(
        LifecycleAuditSnapshot baseline
    )
    {
        var rollValue = TraitRollValueState.CreateStringName("damage_kind", "fire");
        var trait = new BattleEffectiveTraitInstanceState
        {
            trait_id = "flame_trait",
            effective_instance_key = "flame_trait@weapon",
            source_type = "equipment",
            source_id = "staff",
            effect_type = "passive_stat",
            trigger_type = "passive",
            roll_values = new List<TraitRollValueState> { rollValue },
        };
        var status = new BattleStatusEffectState
        {
            status_id = "warded",
            source_unit_id = "caster",
            power = 2,
            save_bonus_by_tag = new Dictionary<StringName, int>
            {
                ["spell"] = 3,
            },
        };
        var unit = new BattleUnitState
        {
            unit_id = "caster",
            display_name = "Caster",
            faction_id = "hostile",
            control_mode = "ai",
            current_hp = 20,
            current_mp = 8,
            effective_trait_instances = new List<BattleEffectiveTraitInstanceState>
            {
                trait,
            },
        };
        unit.cooldowns.Put("arc_bolt", 9);
        unit.SetStatusEffect(status);

        Dictionary<string, object> snapshot = BattleUnitStatePlainSnapshot.Build(unit);
        rollValue.string_name_value = "cold";
        unit.cooldowns.Put("arc_bolt", 1);
        status.save_bonus_by_tag["spell"] = 8;

        var traits = (List<object>)snapshot["effective_trait_instances"];
        var traitPayload = (Dictionary<string, object>)traits[0];
        var rollValues = (Dictionary<StringName, object>)traitPayload["roll_values"];
        _test.Eq(
            (StringName)rollValues["damage_kind"],
            (StringName)"fire",
            "Unit snapshot roll values must be deeply detached from source mutation."
        );
        var cooldowns = (Dictionary<StringName, int>)snapshot["cooldowns"];
        _test.Eq(
            cooldowns["arc_bolt"],
            9,
            "Unit snapshot cooldowns must be deeply detached from source mutation."
        );
        var statusEffects = (Dictionary<string, object>)snapshot["status_effects"];
        var statusPayload = (Dictionary<string, object>)statusEffects["warded"];
        var statusParams = (Dictionary<string, object>)statusPayload["params"];
        var saveBonusByTag = (Dictionary<StringName, int>)statusParams["save_bonus_by_tag"];
        _test.Eq(
            saveBonusByTag["spell"],
            3,
            "Unit snapshot status maps must be deeply detached from source mutation."
        );

        using (GodotProjectionLease<GDictionary> lease =
            TraceDictionaryProjection.BuildLease(
                snapshot,
                "real-unit-snapshot-test",
                LifetimeDomain.Request,
                "real-unit-snapshot-test"
            ))
        {
            AssertKeyOrder(
                lease.Value,
                "unit_id,source_member_id,enemy_template_id,display_name,battle_sprite_texture_path,faction_id,control_mode,ai_brain_id,ai_state_id,coord,body_size,body_size_category,footprint_size,occupied_coords,is_alive,attribute_snapshot,equipment_view,current_hp,current_mp,current_stamina,current_aura,aura_max,current_ap,current_move_points,unlocked_combat_resource_ids,stamina_recovery_progress,is_resting,has_taken_action_this_turn,can_use_locked_move_points_this_turn,current_shield_hp,shield_max_hp,shield_duration,shield_family,shield_source_unit_id,shield_source_skill_id,action_progress,action_threshold,known_active_skill_ids,known_skill_level_map,known_skill_lock_hit_bonus_map,movement_tags,vision_tags,proficiency_tags,save_advantage_tags,save_disadvantage_tags,save_immunity_tags,damage_resistances,save_bonus_by_ability,effective_trait_instances,effective_trait_ids,equipment_ability_sources,creature_type_tags,versatility_pick,weapon_profile_kind,weapon_item_id,weapon_profile_type_id,weapon_range_type,weapon_family,weapon_current_grip,weapon_attack_range,weapon_one_handed_dice,weapon_two_handed_dice,weapon_is_versatile,weapon_uses_two_hands,weapon_physical_damage_tag,cooldowns,last_turn_tu,status_effects",
                "real unit snapshot"
            );
            using GDictionary projectedCooldowns =
                lease.Value["cooldowns"].AsGodotDictionary();
            AssertSingleStringNameMapEntry(
                projectedCooldowns,
                "arc_bolt",
                9,
                "unit cooldowns"
            );
            using GArray projectedTraits =
                lease.Value["effective_trait_instances"].AsGodotArray();
            using GDictionary projectedTrait = projectedTraits[0].AsGodotDictionary();
            using GDictionary projectedRollValues =
                projectedTrait["roll_values"].AsGodotDictionary();
            Variant rollKey = FirstKey(projectedRollValues);
            _test.Eq(
                rollKey.VariantType,
                Variant.Type.StringName,
                "Trait roll keys must retain legacy StringName type."
            );
            _test.Eq(
                projectedRollValues[rollKey].VariantType,
                Variant.Type.StringName,
                "Trait StringName roll values must retain legacy Variant type."
            );
            using GDictionary projectedStatuses =
                lease.Value["status_effects"].AsGodotDictionary();
            using GDictionary projectedStatus =
                projectedStatuses["warded"].AsGodotDictionary();
            using GDictionary projectedParams =
                projectedStatus["params"].AsGodotDictionary();
            using GDictionary projectedSaveBonuses =
                projectedParams["save_bonus_by_tag"].AsGodotDictionary();
            AssertSingleStringNameMapEntry(
                projectedSaveBonuses,
                "spell",
                3,
                "status save bonus map"
            );
            AssertFingerprint(
                lease.Value,
                2075,
                "032d4c35f911b427b10cf2d627be0db5f95a996badb4fdde42e5dfd86049c2c4",
                "full real unit snapshot payload"
            );
        }
        AssertReturnedToBaseline(baseline, "real unit snapshot projection");
        return snapshot;
    }

    private void AssertSimulationReportLease(
        LifecycleAuditSnapshot baseline,
        BattleAiTurnTraceProjection trace,
        IReadOnlyDictionary<string, object> unitSnapshot
    )
    {
        BattleSimScenarioReport report = BuildSimulationReport(
            trace,
            unitSnapshot,
            includeUnknown: false
        );
        BattleSimUnitMetricsSnapshot typedFaction =
            report.ProfileEntries[0].Runs[0].MetricsSnapshot.Factions["hostile"];
        _test.Eq(
            typedFaction.SkillAttemptCounts["arc_bolt"],
            2,
            "Internal typed faction snapshot may retain full skill metrics."
        );
        GodotProjectionLease<GDictionary> reportLease =
            BattleSimReportProjection.BuildLease(report);
        try
        {
            LifecycleAuditSnapshot active = LifecycleAuditRegistry.Shared.CaptureSnapshot();
            _test.Eq(
                active.ActiveOwnerCount - baseline.ActiveOwnerCount,
                CountContainers(reportLease.Value),
                "Simulation report must own its complete nested graph."
            );
            using GArray profiles = reportLease.Value["profile_entries"].AsGodotArray();
            using GDictionary profileEntry = profiles[0].AsGodotDictionary();
            using GArray runs = profileEntry["runs"].AsGodotArray();
            using GDictionary run = runs[0].AsGodotDictionary();
            using GDictionary metrics = run["metrics"].AsGodotDictionary();
            using GDictionary factions = metrics["factions"].AsGodotDictionary();
            using GDictionary hostile = factions["hostile"].AsGodotDictionary();
            AssertKeyOrder(
                hostile,
                "faction_id,unit_count,turn_count,action_counts,skill_attempt_counts,skill_success_counts,successful_skill_count,total_damage_done,total_healing_done,total_damage_taken,total_healing_received,kill_count,death_count",
                "simulation faction metrics"
            );
            AssertFactionCounterMaps(hostile, "simulation faction metrics");
            using GArray finalUnits = run["final_units"].AsGodotArray();
            _test.Eq(finalUnits.Count, 1, "Simulation final units golden.");
            using GDictionary startFailure = run["start_failure"].AsGodotDictionary();
            _test.Eq(
                startFailure.Count,
                0,
                "Successful simulation runs must expose an empty start failure payload."
            );
            AssertFingerprint(
                reportLease.Value,
                16171,
                "ef4ec5f1950e03d9199b40913c8ef94c44357b0f751f6891c395ef2c605deb7f",
                "full simulation report payload"
            );
        }
        finally
        {
            reportLease.Dispose();
        }
        _test.True(
            Throws<ObjectDisposedException>(() => _ = reportLease.Value),
            "Closed simulation report leases must reject Value access."
        );
        AssertReturnedToBaseline(baseline, "simulation report projection");

        var summaryBuilder = new BattleSimTraceSummaryBuilder();
        using (GodotProjectionLease<GDictionary> summaryLease =
            summaryBuilder.BuildLease(
                report,
                "fixed_source.json",
                new BattleSimTraceSummaryBuilder.TraceSummaryOptionsData
                {
                    FocusFactionId = "hostile",
                }
            ))
        {
            using GArray runs = summaryLease.Value["runs"].AsGodotArray();
            using GDictionary compactRun = runs[0].AsGodotDictionary();
            using GDictionary compactStartFailure = compactRun["start_failure"].AsGodotDictionary();
            _test.Eq(
                compactStartFailure.Count,
                0,
                "Successful compact runs must expose an empty start failure payload."
            );
            using GDictionary compactFactions = compactRun["factions"].AsGodotDictionary();
            using GDictionary compactHostile = compactFactions["hostile"].AsGodotDictionary();
            AssertKeyOrder(
                compactHostile,
                "faction_id,unit_count,turn_count,action_counts,skill_attempt_counts,skill_success_counts,successful_skill_count,total_damage_done,total_healing_done,total_damage_taken,total_healing_received,kill_count,death_count",
                "compact faction metrics"
            );
            AssertFactionCounterMaps(compactHostile, "compact faction metrics");
            using GArray turns = compactRun["focus_turns"].AsGodotArray();
            using GDictionary turn = turns[0].AsGodotDictionary();
            using GDictionary execution = turn["execution_result"].AsGodotDictionary();
            using GArray reportEntries = execution["report_entries"].AsGodotArray();
            using GDictionary reportEntry = reportEntries[0].AsGodotDictionary();
            _test.Eq(
                reportEntry["entry_type"].VariantType,
                Variant.Type.StringName,
                "Compact trace report entries must write typed managed facts directly."
            );
            AssertFingerprint(
                summaryLease.Value,
                6432,
                "297b77d61b13269a5d3c5d7eefc0578d7073fd8e16a26239bba0e0eb54621b25",
                "full compact trace summary payload"
            );
        }
        AssertReturnedToBaseline(baseline, "compact trace summary projection");

        _test.True(
            Throws<InvalidOperationException>(
                () =>
                {
                    using GodotProjectionLease<GDictionary> rejected =
                        BattleSimReportProjection.BuildLease(
                            BuildSimulationReport(
                                trace,
                                unitSnapshot,
                                includeUnknown: true
                            )
                        );
                }
            ),
            "Simulation report projection must fail on deep unsupported final-unit values."
        );
        AssertReturnedToBaseline(baseline, "rejected simulation report");
    }

    private static BattleSimScenarioReport BuildSimulationReport(
        BattleAiTurnTraceProjection trace,
        IReadOnlyDictionary<string, object> unitSnapshot,
        bool includeUnknown
    )
    {
        var metricsState = new BattleMetricsState { BattleId = "sim_battle", Seed = 17 };
        var unit = new BattleMetricEntry
        {
            UnitId = "caster",
            DisplayName = "Caster",
            FactionId = "hostile",
            ControlMode = "ai",
            TurnCount = 2,
            TotalDamageDone = 9,
        };
        unit.SkillAttemptCounts["arc_bolt"] = 2;
        unit.SkillSuccessCounts["arc_bolt"] = 1;
        metricsState.Units["caster"] = unit;
        var faction = new BattleMetricEntry
        {
            FactionId = "hostile",
            UnitCount = 1,
            TurnCount = 2,
            TotalDamageDone = 9,
        };
        faction.SkillAttemptCounts["arc_bolt"] = 2;
        faction.SkillSuccessCounts["arc_bolt"] = 1;
        faction.ActionCounts["skill"] = 2;
        metricsState.Factions["hostile"] = faction;

        var finalUnit = new Dictionary<string, object>(
            unitSnapshot ?? new Dictionary<string, object>(StringComparer.Ordinal),
            StringComparer.Ordinal
        );
        if (includeUnknown)
        {
            finalUnit["deep"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["values"] = new List<object> { "ok", new object() },
            };
        }

        var run = new BattleSimRunReport
        {
            ScenarioId = "scenario",
            ProfileId = "profile",
            Seed = 17,
            BattleId = "sim_battle",
            BattleEnded = true,
            MetricsSnapshot = BattleSimMetricsSnapshot.Capture(metricsState),
            AiTurnTraces = new[] { trace },
            FinalUnits = new[] { finalUnit },
        };
        run.SetFinalDecision(
            BattleObjectiveTestFactory.CreateEliminationDecision("hostile")
        );
        var entry = new BattleSimProfileReportEntry
        {
            Summary = new BattleSimProfileSummary(),
        };
        entry.Runs.Add(run);
        var report = new BattleSimScenarioReport();
        report.ProfileEntries.Add(entry);
        return report;
    }

    private void AssertProgressLogExceptionCleanup(LifecycleAuditSnapshot baseline)
    {
        const string path = "user://battle_sim_progress_scope_exception_regression.log";
        var runner = new BattleSimRunner(
            new BattleSimContentProvider(GameSessionTestFactory.GetProcessSnapshot())
        );
        runner.SetProgressLoggingEnabled(true);
        runner.SetProgressLogPath(path);
        _test.True(
            Throws<InvalidOperationException>(
                () =>
                    runner.RunInProgressLogScope<int>(
                        () => throw new InvalidOperationException("expected progress failure")
                    )
            ),
            "Progress-log scope must preserve the action exception."
        );
        AssertReturnedToBaseline(baseline, "progress-log exception cleanup");
        Error removeError = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        _test.True(
            removeError is Error.Ok or Error.FileNotFound,
            "Progress-log FileAccess must be closed before cleanup."
        );
    }

    private void AssertFingerprint(
        GDictionary payload,
        int expectedLength,
        string expectedSha256,
        string label
    )
    {
        string json = Json.Stringify(payload);
        string sha256 = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(json))
        ).ToLowerInvariant();
        _test.Eq(json.Length, expectedLength, $"{label} JSON length golden.");
        _test.Eq(sha256, expectedSha256, $"{label} SHA256 golden.");
    }

    private static Variant FirstKey(GDictionary dictionary)
    {
        foreach (Variant key in dictionary.Keys)
            return key;
        return default;
    }

    private void AssertKeyOrder(GDictionary dictionary, string expected, string label)
    {
        var keys = new List<string>();
        foreach (Variant key in dictionary.Keys)
            keys.Add(key.AsString());
        _test.Eq(
            string.Join(",", keys),
            expected,
            $"{label} must preserve the fixed legacy key order."
        );
    }

    private void AssertFactionCounterMaps(GDictionary faction, string label)
    {
        _test.Eq(ReadText(faction, "faction_id"), "hostile", $"{label} faction id.");
        using GDictionary actionCounts = faction["action_counts"].AsGodotDictionary();
        using GDictionary attemptCounts = faction["skill_attempt_counts"].AsGodotDictionary();
        using GDictionary successCounts = faction["skill_success_counts"].AsGodotDictionary();
        _test.Eq(actionCounts["skill"].AsInt32(), 2, $"{label} action counts.");
        _test.Eq(attemptCounts["arc_bolt"].AsInt32(), 2, $"{label} skill attempts.");
        _test.Eq(successCounts["arc_bolt"].AsInt32(), 1, $"{label} skill successes.");
    }

    private static string ReadText(GDictionary dictionary, string key)
    {
        Variant value = dictionary[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => value.ToString(),
        };
    }

    private void AssertDictionaryKeysAreStrings(GDictionary dictionary, string path)
    {
        foreach (Variant key in dictionary.Keys)
        {
            _test.Eq(
                key.VariantType,
                Variant.Type.String,
                $"{path} dictionary key should remain a string."
            );
            Variant value = dictionary[key];
            if (value.VariantType == Variant.Type.Dictionary)
            {
                using GDictionary nested = value.AsGodotDictionary();
                AssertDictionaryKeysAreStrings(nested, $"{path}.{key.AsString()}");
            }
            else if (value.VariantType == Variant.Type.Array)
            {
                using GArray nested = value.AsGodotArray();
                AssertArrayDictionaryKeysAreStrings(nested, $"{path}.{key.AsString()}");
            }
        }
    }

    private void AssertArrayDictionaryKeysAreStrings(GArray array, string path)
    {
        for (int index = 0; index < array.Count; index++)
        {
            Variant value = array[index];
            if (value.VariantType == Variant.Type.Dictionary)
            {
                using GDictionary nested = value.AsGodotDictionary();
                AssertDictionaryKeysAreStrings(nested, $"{path}[{index}]");
            }
            else if (value.VariantType == Variant.Type.Array)
            {
                using GArray nested = value.AsGodotArray();
                AssertArrayDictionaryKeysAreStrings(nested, $"{path}[{index}]");
            }
        }
    }

    private void AssertReturnedToBaseline(LifecycleAuditSnapshot baseline, string label)
    {
        LifecycleAuditSnapshot after = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(
            after.ActiveOwnerCount,
            baseline.ActiveOwnerCount,
            $"{label} should return owner count to baseline."
        );
        _test.Eq(
            after.ActiveLeaseCount,
            baseline.ActiveLeaseCount,
            $"{label} should return lease count to baseline."
        );
        _test.Eq(
            after.ActiveScopeCount,
            baseline.ActiveScopeCount,
            $"{label} should keep native scope count at baseline."
        );
        _test.Eq(
            after.ActiveContentBorrowerCount,
            baseline.ActiveContentBorrowerCount,
            $"{label} should keep content borrower count at baseline."
        );
    }

    private static int CountContainers(GDictionary dictionary)
    {
        int count = 1;
        foreach (Variant key in dictionary.Keys)
        {
            Variant value = dictionary[key];
            if (value.VariantType == Variant.Type.Dictionary)
            {
                using GDictionary nested = value.AsGodotDictionary();
                count += CountContainers(nested);
            }
            else if (value.VariantType == Variant.Type.Array)
            {
                using GArray nested = value.AsGodotArray();
                count += CountContainers(nested);
            }
        }
        return count;
    }

    private static int CountContainers(GArray array)
    {
        int count = 1;
        foreach (Variant value in array)
        {
            if (value.VariantType == Variant.Type.Dictionary)
            {
                using GDictionary nested = value.AsGodotDictionary();
                count += CountContainers(nested);
            }
            else if (value.VariantType == Variant.Type.Array)
            {
                using GArray nested = value.AsGodotArray();
                count += CountContainers(nested);
            }
        }
        return count;
    }

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    private static BattleAiTurnTraceProjection BuildTrace()
    {
        var command = new AiCommandSummary(
            "skill",
            "caster",
            "arc_bolt",
            "wide",
            "target",
            new[] { new StringName("target") },
            new Vector2I(3, 4),
            new[] { new Vector2I(3, 4) }
        );
        var candidate = new AiCandidateSummary(
            "arc_bolt@target",
            command,
            42,
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["score_bucket_id"] = "offense",
                ["target_ids"] = new List<StringName> { "target" },
            }
        );
        var actionTrace = new AiActionTrace(
            "trace_1",
            "cast_arc_bolt",
            "offense",
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["source_kind"] = new StringName("authored_action"),
                ["origin"] = new Vector2I(1, 2),
            }
        )
        {
            EvaluationCount = 3,
            CandidateCount = 1,
            Chosen = true,
            BestCommand = command,
            ChosenCommand = command,
        };
        actionTrace.TopCandidates.Add(candidate);
        actionTrace.BlockReasons["blocked"] = 1;

        var numericSummary = new MeteorSwarmNumericSummary
        {
            CandidateAnchorCoord = new Vector2I(3, 4),
            TargetUnitId = "target",
            TargetFactionId = "player",
            DistanceFromAnchor = 0,
            ComponentExpectedDamage = 7,
            ComponentWorstCaseDamage = 12,
            SaveProfileIds = new List<string> { "", "reflex_half" },
            ResistanceTiersByDamageTag = new Dictionary<StringName, StringName>
            {
                [""] = "half",
                ["fire"] = "half",
                ["cold"] = "",
            },
            StatusEffectIds = new List<StringName> { "", "burning" },
        };
        numericSummary.ComponentBreakdown.Add(
            new MeteorSwarmComponentBreakdownEntry
            {
                ComponentId = "center_direct",
                RoleLabel = "center",
                DamageTag = "fire",
                ExpectedDamage = 7,
                WorstCaseDamage = 12,
                HalfSourceLabels = new List<string> { "", "fire_resistance" },
                DoubleSourceLabels = new List<string> { "fire_vulnerability", "" },
                ImmuneSourceLabels = new List<string> { "", "flame_ward" },
                FixedMitigationSourceLabels = new List<string> { "", "stoneskin" },
            }
        );
        var meteorFacts = new MeteorSwarmPreviewFacts
        {
            profile_id = "meteor_swarm",
            skill_id = "meteor_swarm",
            preview_fact_id = "meteor_preview",
            resolved_anchor_coord = new Vector2I(3, 4),
            impact_count = 3,
            expected_target_count = 1,
            target_numeric_summaries = new List<MeteorSwarmNumericSummary>
            {
                numericSummary,
                null,
            },
            friendly_fire_numeric_summary = new List<MeteorSwarmNumericSummary>
            {
                numericSummary,
                null,
            },
        };

        return new BattleAiTurnTraceProjection
        {
            BattleId = "battle_1",
            TurnStartedTu = 15,
            UnitId = "caster",
            UnitName = "Caster",
            FactionId = "hostile",
            BrainId = "mage",
            StateId = "engage",
            ActionId = "cast_arc_bolt",
            ReasonText = "best score",
            Command = command,
            Transition = new BattleAiTraceTransitionProjection
            {
                PreviousStateId = "idle",
                StateId = "engage",
                RuleId = "enter_engage",
                Reason = "matched",
                MatchedConditions = new[]
                {
                    new BattleAiTraceTransitionConditionProjection
                    {
                        Predicate = "distance",
                        BasisPoints = 10000,
                        MaxDistance = 6,
                        StateIds = new[] { "idle" },
                        Affordances = new[] { "offense" },
                    },
                },
            },
            ScoreInput = new BattleAiScoreInput
            {
                action_kind = "skill",
                action_label = "Arc Bolt",
                score_bucket_id = "offense",
                target_unit_ids = new List<StringName> { "target" },
                target_coords = new List<Vector2I> { new(3, 4) },
                target_count = 1,
                special_profile_preview_facts = meteorFacts,
                target_numeric_summary = new List<MeteorSwarmNumericSummary>
                {
                    numericSummary,
                    null,
                },
                friendly_fire_numeric_summary = new List<MeteorSwarmNumericSummary>
                {
                    numericSummary,
                    null,
                },
                total_score = 42,
            },
            ActionTraces = new[] { actionTrace },
            DecisionTargetSnapshots = new[]
            {
                new BattleAiTraceUnitSnapshotProjection
                {
                    UnitId = "target",
                    DisplayName = "Target",
                    FactionId = "player",
                    Coord = "(3, 4)",
                    Alive = true,
                    Hp = 12,
                    HpMax = 20,
                },
            },
            ExecutionResult = new BattleAiTraceExecutionResultProjection
            {
                CommandType = "skill",
                SkillId = "arc_bolt",
                ChangedUnitIds = new[] { "target" },
                TrackedUnitIds = new[] { "caster", "target" },
                LogLines = new[] { "Target takes damage." },
                ReportEntries = new IReadOnlyDictionary<string, object>[]
                {
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["entry_type"] = new StringName("damage"),
                        ["parts"] = new List<object> { "target", 7 },
                    },
                },
            },
        };
    }
}
