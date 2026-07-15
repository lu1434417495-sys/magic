using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_projection_lease_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            RunAssertions();
        }
        catch (Exception error)
        {
            _test.True(false, $"Unhandled projection regression exception: {error}");
            RequestTestExit(_test.Finish("Battle projection lease regression"));
        }
    }

    private void RunAssertions()
    {
        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        BattleEventBatch batch = BuildBatch();
        BattlePreview preview = BuildPreview();

        AssertMutationIsolation();
        AssertLoggerScalarSchema();

        for (int index = 0; index < 8; index++)
        {
            _ = batch.ReportEntriesTyped.Count;
            _ = preview.LogLinesTyped.Count;
            _ = preview.DamagePreviewTyped;
            _ = preview.SaveBranchPreviewTyped;
        }
        AssertReturnedToBaseline(baseline, "plain core reads");

        _test.True(
            Throws<InvalidOperationException>(
                () =>
                {
                    BattlePreview rejected = BuildPreview();
                    rejected.SetSaveBranchPreview(
                        new BattleSaveBranchPreviewData
                        {
                            Kind = "test",
                            ResidualValues = new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["unknown"] = new object(),
                            },
                        }
                    );
                    using GodotProjectionLease<GDictionary> lease =
                        BattlePreviewProjection.BuildLease(rejected);
                }
            ),
            "Unknown preview values must fail instead of silently stringifying."
        );
        AssertReturnedToBaseline(baseline, "rejected preview");

        AssertStableBatchProjection(baseline, batch);
        AssertStablePreviewProjection(baseline, preview);
        AssertStableDamageResultProjection(baseline);
        AssertReturnedToBaseline(baseline, "all battle projections");
        RequestTestExit(_test.Finish("Battle projection lease regression"));
    }

    private void AssertStableBatchProjection(
        LifecycleAuditSnapshot baseline,
        BattleEventBatch batch
    )
    {
        string fingerprint;
        int ownerDelta;
        using (GodotProjectionLease<GDictionary> lease =
            BattleEventBatchProjection.BuildLease(batch))
        {
            LifecycleAuditSnapshot active = LifecycleAuditRegistry.Shared.CaptureSnapshot();
            ownerDelta = active.ActiveOwnerCount - baseline.ActiveOwnerCount;
            AssertSingleRootLease(baseline, active, "event batch");
            _test.Eq(
                ownerDelta,
                CountContainers(lease.Value),
                "Every event batch container must belong to its root lease."
            );
            AssertOrder(
                lease.Value,
                "phase_changed,battle_ended,changed_unit_ids,changed_coords,log_lines,report_entries,progression_deltas,modal_requested",
                "event batch"
            );
            using GArray unitIds = lease.Value["changed_unit_ids"].AsGodotArray();
            _test.Eq(
                unitIds[0].VariantType,
                Variant.Type.StringName,
                "Event batch StringName values must keep their Variant type."
            );
            AssertBatchNestedSchema(lease.Value);
            AssertGolden(
                lease.Value,
                "1242:45ece03f9ebc37f0bf9805b429515be1b8788094ef2da18673fa9022c4396d1f",
                "event batch fixed JSON golden"
            );
            fingerprint = Json.Stringify(lease.Value);
        }
        AssertReturnedToBaseline(baseline, "event batch first projection");

        GodotProjectionLease<GDictionary> repeated =
            BattleEventBatchProjection.BuildLease(batch);
        try
        {
            LifecycleAuditSnapshot active = LifecycleAuditRegistry.Shared.CaptureSnapshot();
            _test.Eq(
                active.ActiveOwnerCount - baseline.ActiveOwnerCount,
                ownerDelta,
                "Repeated event batch projection must own the same container count."
            );
            _test.Eq(
                Json.Stringify(repeated.Value),
                fingerprint,
                "Repeated event batch projection must preserve its fingerprint."
            );
        }
        finally
        {
            repeated.Dispose();
        }
        _test.True(
            Throws<ObjectDisposedException>(() => _ = repeated.Value),
            "Closed event batch leases must reject Value access."
        );
        AssertReturnedToBaseline(baseline, "event batch repeated projection");
    }

    private void AssertStablePreviewProjection(
        LifecycleAuditSnapshot baseline,
        BattlePreview preview
    )
    {
        string fingerprint;
        int ownerDelta;
        using (GodotProjectionLease<GDictionary> lease =
            BattlePreviewProjection.BuildLease(preview))
        {
            LifecycleAuditSnapshot active = LifecycleAuditRegistry.Shared.CaptureSnapshot();
            ownerDelta = active.ActiveOwnerCount - baseline.ActiveOwnerCount;
            AssertSingleRootLease(baseline, active, "preview");
            _test.Eq(
                ownerDelta,
                CountContainers(lease.Value),
                "Every preview container must belong to its root lease."
            );
            AssertOrder(
                lease.Value,
                "allowed,log_lines,target_unit_ids,target_coords,random_chain_candidate_unit_ids,resolved_anchor_coord,move_cost,hit_preview,damage_preview,fate_preview,save_branch_preview,special_profile_gate_result,special_profile_preview_facts",
                "preview"
            );
            using GDictionary saveBranch =
                lease.Value["save_branch_preview"].AsGodotDictionary();
            _test.Eq(
                saveBranch["variant_id"].VariantType,
                Variant.Type.StringName,
                "Preview residual StringName values must keep their Variant type."
            );
            AssertPreviewNestedSchema(lease.Value);
            AssertGolden(
                lease.Value,
                "1847:bb912941748d95f11c0a016d19888f00f3811414ae4decde081fcec16e02d8fe",
                "preview fixed JSON golden"
            );
            fingerprint = Json.Stringify(lease.Value);
        }
        AssertReturnedToBaseline(baseline, "preview first projection");

        GodotProjectionLease<GDictionary> repeated = BattlePreviewProjection.BuildLease(preview);
        try
        {
            LifecycleAuditSnapshot active = LifecycleAuditRegistry.Shared.CaptureSnapshot();
            _test.Eq(
                active.ActiveOwnerCount - baseline.ActiveOwnerCount,
                ownerDelta,
                "Repeated preview projection must own the same container count."
            );
            _test.Eq(
                Json.Stringify(repeated.Value),
                fingerprint,
                "Repeated preview projection must preserve its fingerprint."
            );
        }
        finally
        {
            repeated.Dispose();
        }
        _test.True(
            Throws<ObjectDisposedException>(() => _ = repeated.Value),
            "Closed preview leases must reject Value access."
        );
        AssertReturnedToBaseline(baseline, "preview repeated projection");
    }

    private void AssertStableDamageResultProjection(LifecycleAuditSnapshot baseline)
    {
        BattleDamagePreviewSaveEstimate saveEstimate =
            BattleDamagePreviewSaveEstimate.Create(
                hasSave: true,
                damageBeforeSave: 7,
                damageAfterSave: 5,
                damageAfterSaveEstimate: 5,
                damageAfterSaveWorst: 7,
                damageOnSaveFailure: 7,
                damageOnSaveSuccess: 3,
                savePartialOnSuccess: true,
                saveSuccessProbabilityBasisPoints: 6200,
                saveSuccessRatePercent: 62,
                saveFailureProbabilityBasisPoints: 3800,
                dc: 14,
                ability: "dexterity",
                saveTag: "reflex",
                advantageState: "advantage",
                abilityValue: 16,
                abilityModifier: 3,
                bonus: 2,
                immune: false,
                sources: new[]
                {
                    new BattleSaveSource("status_haste", "status", "reflex", "bonus"),
                }
            );
        BattleDamagePreviewResult damage = BattleDamagePreviewResult.Create(
            applied: true,
            rollMode: "average",
            saveMode: "expected",
            preSaveDamage: 7,
            postSaveDamage: 5,
            hpDamage: 4,
            damage: 4,
            incomingBudgetDamage: 5,
            shieldAbsorbed: 1,
            damageEvents: new List<object>
            {
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["damage"] = 4,
                    ["tags"] = new List<object> { "fire" },
                },
            },
            saveEstimate: saveEstimate,
            saveEstimates: new[] { saveEstimate },
            sourcePreviewAfter: BuildUnit("source_after", "player", new Vector2I(1, 2)),
            targetPreviewAfter: BuildUnit("target_after", "enemy", new Vector2I(2, 2))
        );
        string fingerprint;
        int ownerDelta;
        using (GodotProjectionLease<GDictionary> lease =
            BattleDamagePreviewProjection.BuildLease(damage))
        {
            LifecycleAuditSnapshot active = LifecycleAuditRegistry.Shared.CaptureSnapshot();
            ownerDelta = active.ActiveOwnerCount - baseline.ActiveOwnerCount;
            AssertSingleRootLease(baseline, active, "damage result");
            _test.Eq(
                ownerDelta,
                CountContainers(lease.Value),
                "Every damage result container must belong to its root lease."
            );
            AssertOrder(
                lease.Value,
                "applied,pre_save_damage,post_save_damage,damage,hp_damage,healing,incoming_budget_damage,shield_absorbed,shield_broken,shield_hp_before,shield_hp_after,damage_events,equipment_durability_events,dispel_events,damage_dice_high_total_roll,skill_damage_dice_is_max,weapon_damage_dice_is_max,status_effect_ids,removed_status_effect_ids,source_status_effect_ids,terrain_effect_ids,height_delta,diagnostics,save_estimates,stable_lethal,lethal_probability_basis_points,roll_mode,save_mode,save_estimate,source_preview_after,target_preview_after",
                "damage result"
            );
            using GDictionary sourceAfter =
                lease.Value["source_preview_after"].AsGodotDictionary();
            AssertOrder(
                sourceAfter,
                "unit_id,source_member_id,enemy_template_id,display_name,battle_sprite_texture_path,faction_id,control_mode,ai_brain_id,ai_state_id,coord,body_size,body_size_category,footprint_size,occupied_coords,is_alive,attribute_snapshot,equipment_view,current_hp,current_mp,current_stamina,current_aura,aura_max,current_ap,current_move_points,unlocked_combat_resource_ids,stamina_recovery_progress,is_resting,has_taken_action_this_turn,can_use_locked_move_points_this_turn,current_shield_hp,shield_max_hp,shield_duration,shield_family,shield_source_unit_id,shield_source_skill_id,action_progress,action_threshold,known_active_skill_ids,known_skill_level_map,known_skill_lock_hit_bonus_map,movement_tags,vision_tags,proficiency_tags,save_advantage_tags,damage_resistances,save_bonus_by_ability,effective_trait_instances,effective_trait_ids,equipment_ability_sources,creature_type_tags,versatility_pick,weapon_profile_kind,weapon_item_id,weapon_profile_type_id,weapon_range_type,weapon_family,weapon_current_grip,weapon_attack_range,weapon_one_handed_dice,weapon_two_handed_dice,weapon_is_versatile,weapon_uses_two_hands,weapon_physical_damage_tag,cooldowns,last_turn_tu,status_effects",
                "damage source unit snapshot"
            );
            AssertDamageNestedSchema(lease.Value);
            AssertGolden(
                lease.Value,
                "5213:de0257820a594a817df26e74c3c8607564f7e2bd38e91e153d00179c05085b0a",
                "damage result fixed JSON golden"
            );
            fingerprint = Json.Stringify(lease.Value);
        }
        AssertReturnedToBaseline(baseline, "damage result first projection");

        using GodotProjectionLease<GDictionary> repeated =
            BattleDamagePreviewProjection.BuildLease(damage);
        LifecycleAuditSnapshot repeatedActive = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(
            repeatedActive.ActiveOwnerCount - baseline.ActiveOwnerCount,
            ownerDelta,
            "Repeated damage result projection must own the same container count."
        );
        _test.Eq(
            Json.Stringify(repeated.Value),
            fingerprint,
            "Repeated damage result projection must preserve its fingerprint."
        );
    }

    private void AssertMutationIsolation()
    {
        var nestedReport = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["damage"] = 7,
        };
        var reportParts = new List<object> { "unit_a", nestedReport };
        var report = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["entry_type"] = "damage",
            ["parts"] = reportParts,
        };

        var batch = new BattleEventBatch();
        batch.AddReportEntry(report);
        nestedReport["damage"] = 99;
        reportParts.Add("late");
        IReadOnlyDictionary<string, object> batchSnapshot = batch.ReportEntriesTyped[0];
        var batchParts = (List<object>)batchSnapshot["parts"];
        _test.Eq(batchParts.Count, 2, "Batch report input list must be detached.");
        _test.Eq(
            ReadPlainInt((IReadOnlyDictionary<string, object>)batchParts[1], "damage"),
            7,
            "Batch report input dictionary must be detached."
        );
        batchParts.Add("getter mutation");
        ((Dictionary<string, object>)batchParts[1])["damage"] = 88;
        IReadOnlyDictionary<string, object> batchAfterGetterMutation =
            batch.ReportEntriesTyped[0];
        var batchAfterParts = (List<object>)batchAfterGetterMutation["parts"];
        _test.Eq(batchAfterParts.Count, 2, "Batch report getter list must be detached.");
        _test.Eq(
            ReadPlainInt((IReadOnlyDictionary<string, object>)batchAfterParts[1], "damage"),
            7,
            "Batch report getter dictionary must be detached."
        );

        var sourceDelta = new CharacterProgressionDelta { member_id = "hero" };
        sourceDelta.AddLeveledSkillId("skill_a");
        batch.AddProgressionDelta(sourceDelta);
        sourceDelta.AddLeveledSkillId("late_skill");
        CharacterProgressionDelta returnedDelta = batch.ProgressionDeltasTyped[0];
        _test.Eq(
            returnedDelta.LeveledSkillIdsTyped.Count,
            1,
            "Batch progression input must be detached."
        );
        returnedDelta.AddLeveledSkillId("getter_skill");
        _test.Eq(
            batch.ProgressionDeltasTyped[0].LeveledSkillIdsTyped.Count,
            1,
            "Batch progression getter must be detached."
        );

        var state = new BattleState();
        state.AddReportEntry(report);
        IReadOnlyDictionary<string, object> stateSnapshot = state.ReportEntriesTyped[0];
        var stateParts = (List<object>)stateSnapshot["parts"];
        stateParts.Clear();
        _test.Eq(
            ((List<object>)state.ReportEntriesTyped[0]["parts"]).Count,
            3,
            "BattleState report getter must be detached from stored report facts."
        );

        var outcome = new BattleCommonSkillOutcome();
        outcome.AddReportEntry(report);
        var outcomeParts = (List<object>)outcome.ReportEntriesTyped[0]["parts"];
        outcomeParts.Clear();
        _test.Eq(
            ((List<object>)outcome.ReportEntriesTyped[0]["parts"]).Count,
            3,
            "BattleCommonSkillOutcome report getter must be detached."
        );

        var sourceRanges = new List<BattleDamagePreviewRangeService.DamageEffectRange>
        {
            BuildDamageRange(),
        };
        var preview = new BattlePreview();
        preview.SetDamagePreview(
            new BattleDamagePreviewRangeService.SkillDamagePreview(true, 3, 8, sourceRanges)
        );
        sourceRanges.Clear();
        BattleDamagePreviewRangeService.SkillDamagePreview returnedPreview =
            preview.DamagePreviewTyped.Value;
        _test.Eq(
            returnedPreview.DamageRanges.Count,
            1,
            "Damage preview setter must detach caller-owned ranges."
        );
        ((List<BattleDamagePreviewRangeService.DamageEffectRange>)returnedPreview.DamageRanges)
            .Clear();
        _test.Eq(
            preview.DamagePreviewTyped.Value.DamageRanges.Count,
            1,
            "Damage preview getter must detach stored ranges."
        );

        var residualDetail = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["value"] = 3,
        };
        var residualDetails = new List<object> { residualDetail };
        var residual = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["details"] = residualDetails,
        };
        preview.SetSaveBranchPreview(
            new BattleSaveBranchPreviewData { Kind = "save", ResidualValues = residual }
        );
        residualDetail["value"] = 99;
        residualDetails.Clear();
        var returnedResidual = (Dictionary<string, object>)
            preview.SaveBranchPreviewTyped.ResidualValues;
        var returnedDetails = (List<object>)returnedResidual["details"];
        _test.Eq(returnedDetails.Count, 1, "Save residual input must be deep detached.");
        returnedDetails.Clear();
        _test.Eq(
            ((List<object>)preview.SaveBranchPreviewTyped.ResidualValues["details"]).Count,
            1,
            "Save residual getter must be deep detached."
        );
    }

    private void AssertLoggerScalarSchema()
    {
        using GDictionary entry = GameRuntimeCommandLogger.NormalizeReportEntryForLog(
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["name"] = new StringName("fixture_name"),
                ["coord"] = new Vector2I(4, 5),
                ["nested"] = new List<object>
                {
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["name"] = new StringName("nested_name"),
                        ["coord"] = new Vector2I(6, 7),
                    },
                },
            }
        );
        _test.Eq(
            entry["name"].VariantType,
            Variant.Type.String,
            "Logger must keep legacy StringName-to-string normalization."
        );
        using GDictionary coord = entry["coord"].AsGodotDictionary();
        AssertOrder(coord, "x,y", "logger Vector2I");
        _test.Eq(coord["x"].AsInt32(), 4, "Logger Vector2I x schema drifted.");
        _test.Eq(coord["y"].AsInt32(), 5, "Logger Vector2I y schema drifted.");
        using GArray nested = entry["nested"].AsGodotArray();
        using GDictionary nestedEntry = nested[0].AsGodotDictionary();
        _test.Eq(
            nestedEntry["name"].VariantType,
            Variant.Type.String,
            "Logger nested StringName must keep legacy string schema."
        );
        _test.Eq(
            Json.Stringify(entry),
            "{\"coord\":{\"x\":4,\"y\":5},\"name\":\"fixture_name\",\"nested\":[{\"coord\":{\"x\":6,\"y\":7},\"name\":\"nested_name\"}]}",
            "Logger StringName/Vector2I fixed schema drifted."
        );
    }

    private void AssertBatchNestedSchema(GDictionary root)
    {
        using GArray reports = root["report_entries"].AsGodotArray();
        using GDictionary report = reports[0].AsGodotDictionary();
        AssertOrder(report, "entry_type,parts", "event batch report entry");
        using GArray deltas = root["progression_deltas"].AsGodotArray();
        using GDictionary delta = deltas[0].AsGodotDictionary();
        AssertOrder(
            delta,
            "member_id,leveled_skill_ids,granted_skill_ids,changed_profession_ids,character_level_before,character_level_after,pending_profession_choices,needs_promotion_modal,unlocked_achievement_ids,mastery_changes,knowledge_changes,attribute_changes",
            "event batch progression delta"
        );
        using GArray choices = delta["pending_profession_choices"].AsGodotArray();
        using GDictionary choice = choices[0].AsGodotDictionary();
        AssertOrder(
            choice,
            "trigger_skill_ids,candidate_profession_ids,target_rank_map,qualifier_skill_pool_ids,assignable_skill_candidate_ids,required_qualifier_count,required_assigned_core_count",
            "event batch pending profession choice"
        );
        using GArray mastery = delta["mastery_changes"].AsGodotArray();
        using GDictionary masteryEntry = mastery[0].AsGodotDictionary();
        AssertOrder(
            masteryEntry,
            "skill_id,skill_name,mastery_amount,source_type,source_label,reason_text",
            "event batch mastery change"
        );
        using GArray knowledge = delta["knowledge_changes"].AsGodotArray();
        using GDictionary knowledgeEntry = knowledge[0].AsGodotDictionary();
        AssertOrder(
            knowledgeEntry,
            "knowledge_id,knowledge_label,reason_text",
            "event batch knowledge change"
        );
        using GArray attributes = delta["attribute_changes"].AsGodotArray();
        using GDictionary attribute = attributes[0].AsGodotDictionary();
        AssertOrder(
            attribute,
            "attribute_id,attribute_label,delta,reason_text,progress_delta,progress_before,progress_after,attribute_before,attribute_after",
            "event batch attribute change with optionals"
        );
    }

    private void AssertPreviewNestedSchema(GDictionary root)
    {
        using GDictionary hit = root["hit_preview"].AsGodotDictionary();
        AssertOrder(
            hit,
            "summary_text,source,hit_rate_percent,success_rate_percent,base_hit_rate_percent,force_hit_no_crit,force_critical_on_hit,crit_locked,stage_hit_rates,stage_success_rates,stage_base_hit_rates,stage_required_rolls,stage_preview_texts,attack_roll_modifier_breakdown",
            "preview hit facts"
        );
        using GDictionary damage = root["damage_preview"].AsGodotDictionary();
        AssertOrder(
            damage,
            "has_damage,min_damage,max_damage,summary_text,damage_ranges",
            "preview damage range summary"
        );
        using GArray ranges = damage["damage_ranges"].AsGodotArray();
        _test.Eq(ranges.Count, 1, "Preview golden must exercise a non-empty damage range.");
        using GDictionary range = ranges[0].AsGodotDictionary();
        AssertOrder(
            range,
            "effect_index,power,add_weapon_dice,min_damage,max_damage,damage_dice_count,damage_dice_sides,damage_dice_bonus,damage_dice_min,damage_dice_max,weapon_damage_dice_count,weapon_damage_dice_sides,weapon_damage_dice_bonus,weapon_damage_dice_min,weapon_damage_dice_max",
            "preview damage range"
        );
        using GDictionary fate = root["fate_preview"].AsGodotDictionary();
        AssertOrder(
            fate,
            "uses_fate_attack,force_hit_no_crit,force_critical_on_hit,is_disadvantage,effective_luck,crit_gate_die,fumble_low_end,crit_threshold,crit_locked,mercy_active",
            "preview fate facts"
        );
        using GDictionary save = root["save_branch_preview"].AsGodotDictionary();
        AssertOrder(
            save,
            "kind,branch,save_tag,save_ability,save_dc,save_advantage_state,save_success_chance_basis_points,hit_chance_basis_points,threshold,current_hp,max_hp,failure_branch_text,success_branch_text,summary_text,variant_id,details",
            "preview save branch with residual"
        );
    }

    private void AssertDamageNestedSchema(GDictionary root)
    {
        using GDictionary estimate = root["save_estimate"].AsGodotDictionary();
        AssertOrder(
            estimate,
            "has_save,damage_before_save,damage_after_save,damage_after_save_estimate,damage_after_save_worst,damage_on_save_failure,damage_on_save_success,save_partial_on_success,save_success_probability_basis_points,save_success_rate_percent,save_failure_probability_basis_points,dc,ability,save_tag,advantage_state,ability_value,ability_modifier,bonus,immune,sources",
            "damage save estimate"
        );
        _test.True(estimate["has_save"].AsBool(), "Damage golden must exercise HasSave.");
        using GArray sources = estimate["sources"].AsGodotArray();
        _test.Eq(sources.Count, 1, "Damage golden must exercise save sources.");
        using GDictionary source = sources[0].AsGodotDictionary();
        AssertOrder(source, "source_id,type,tag,mode", "damage save source");
        using GArray estimates = root["save_estimates"].AsGodotArray();
        _test.Eq(estimates.Count, 1, "Damage golden save estimate list drifted.");
    }

    private void AssertGolden(GDictionary payload, string expected, string label)
    {
        string json = Json.Stringify(payload);
        string hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(json))
        ).ToLowerInvariant();
        _test.Eq($"{json.Length}:{hash}", expected, label);
    }

    private static int ReadPlainInt(
        IReadOnlyDictionary<string, object> source,
        string key
    ) =>
        source != null && source.TryGetValue(key, out object value) && value is int number
            ? number
            : 0;

    private void AssertSingleRootLease(
        LifecycleAuditSnapshot baseline,
        LifecycleAuditSnapshot active,
        string label
    )
    {
        _test.Eq(
            active.ActiveLeaseCount,
            baseline.ActiveLeaseCount + 1,
            $"{label} should register one root lease."
        );
        _test.Eq(
            active.ActiveScopeCount,
            baseline.ActiveScopeCount,
            $"{label} should not register an unrelated scope."
        );
        _test.Eq(
            active.ActiveContentBorrowerCount,
            baseline.ActiveContentBorrowerCount,
            $"{label} should not register content borrowers."
        );
    }

    private void AssertOrder(
        GDictionary payload,
        string expected,
        string label
    )
    {
        var keys = new List<string>();
        foreach (Variant key in payload.Keys)
            keys.Add(key.AsString());
        _test.Eq(
            string.Join(",", keys),
            expected,
            $"{label} must preserve authored key order."
        );
    }

    private void AssertReturnedToBaseline(LifecycleAuditSnapshot baseline, string label)
    {
        LifecycleAuditSnapshot after = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(after.ActiveOwnerCount, baseline.ActiveOwnerCount, $"{label}: owners");
        _test.Eq(after.ActiveLeaseCount, baseline.ActiveLeaseCount, $"{label}: leases");
        _test.Eq(after.ActiveScopeCount, baseline.ActiveScopeCount, $"{label}: scopes");
        _test.Eq(
            after.ActiveContentBorrowerCount,
            baseline.ActiveContentBorrowerCount,
            $"{label}: borrowers"
        );
    }

    private static BattleEventBatch BuildBatch()
    {
        var batch = new BattleEventBatch
        {
            phase_changed = true,
            modal_requested = true,
        };
        batch.AddChangedUnitId("unit_a");
        batch.AddChangedCoord(new Vector2I(2, 3));
        batch.AddLogLine("fixture log");
        batch.AddReportEntry(
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["entry_type"] = new StringName("damage"),
                ["parts"] = new List<object>
                {
                    "unit_a",
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["damage"] = 7,
                    },
                },
            }
        );
        var choice = new PendingProfessionChoice
        {
            required_qualifier_count = 1,
            required_assigned_core_count = 1,
        };
        choice.AddTriggerSkillId("skill_a");
        choice.AddCandidateProfessionId("mage");
        choice.SetTargetRank("mage", 2);
        choice.AddQualifierSkillPoolId("skill_a");
        choice.AddAssignableSkillCandidateId("skill_b");
        var delta = new CharacterProgressionDelta
        {
            member_id = "hero",
            character_level_before = 1,
            character_level_after = 2,
            needs_promotion_modal = true,
        };
        delta.AddLeveledSkillId("skill_a");
        delta.AddGrantedSkillId("skill_b");
        delta.AddChangedProfessionId("mage");
        delta.AddUnlockedAchievementId("level_two");
        delta.AddPendingProfessionChoice(choice);
        delta.AddMasteryChange(
            new CharacterMasteryChangeFact("skill_a", "Skill A", 2, "battle", "Battle", "hit")
        );
        delta.AddKnowledgeChange(
            new CharacterKnowledgeChangeFact("arcana", "Arcana", "level up")
        );
        delta.AddAttributeChange(
            new CharacterAttributeChangeFact(
                "intelligence",
                "Intelligence",
                1,
                "growth",
                progressDelta: 4,
                progressBefore: 6,
                progressAfter: 10,
                attributeBefore: 15,
                attributeAfter: 16
            )
        );
        batch.AddProgressionDelta(delta);
        return batch;
    }

    private static BattlePreview BuildPreview()
    {
        var preview = new BattlePreview
        {
            allowed = true,
            resolved_anchor_coord = new Vector2I(2, 3),
            move_cost = 2,
            hit_preview = new AttackPreviewData
            {
                SummaryText = "75%",
                HitRatePercent = 75,
                SuccessRatePercent = 75,
                BaseHitRatePercent = 70,
                Stages = new List<AttackPreviewStage>
                {
                    new(75, 75, 70, 6, 6, "75%"),
                },
            },
            special_profile_gate_result = new BattleSpecialProfileGateResult
            {
                Allowed = true,
                ProfileId = "fixture",
                SkillId = "skill_a",
            },
        };
        preview.AddLogLine("preview log");
        preview.AddTargetUnitId("unit_a");
        preview.AddTargetCoord(new Vector2I(2, 3));
        preview.AddRandomChainCandidateUnitId("unit_b");
        preview.SetDamagePreview(
            new BattleDamagePreviewRangeService.SkillDamagePreview(
                true,
                3,
                8,
                new List<BattleDamagePreviewRangeService.DamageEffectRange>
                {
                    BuildDamageRange(),
                }
            )
        );
        preview.SetFatePreview(BattleFatePreviewData.ForceHitNoCritPreview());
        preview.SetSaveBranchPreview(
            new BattleSaveBranchPreviewData
            {
                Kind = "save",
                Branch = "graded_save",
                SaveTag = "reflex",
                SaveAbility = "dexterity",
                SaveDc = 14,
                SaveAdvantageState = "advantage",
                SaveSuccessChanceBasisPoints = 6200,
                HitChanceBasisPoints = 3800,
                Threshold = 8,
                CurrentHp = 12,
                MaxHp = 20,
                FailureBranchText = "full damage",
                SuccessBranchText = "half damage",
                SummaryText = "fixture",
                ResidualValues = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["variant_id"] = new StringName("wide"),
                    ["details"] = new List<object>
                    {
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["value"] = 3,
                        },
                    },
                },
            }
        );
        preview.special_profile_gate_result.DebugDetails["labels"] =
            new List<string> { "fixture" };
        return preview;
    }

    private static BattleDamagePreviewRangeService.DamageEffectRange BuildDamageRange() =>
        new(
            EffectIndex: 0,
            Power: 2,
            AddWeaponDice: true,
            MinDamage: 3,
            MaxDamage: 8,
            SkillDiceRange: new BattleDamagePreviewRangeService.DiceRange(1, 6, 1, 2, 7),
            WeaponDiceRange: new BattleDamagePreviewRangeService.DiceRange(1, 1, 0, 1, 1)
        );

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            current_hp = 12,
            current_mp = 7,
            current_stamina = 5,
            current_aura = 3,
            current_ap = 2,
            current_move_points = 1,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue("hp_max", 20);
        unit.attribute_snapshot.SetValue("aura_max", 6);
        unit.known_active_skill_ids.Add("skill_a");
        unit.known_skill_level_map["skill_a"] = 2;
        unit.damage_resistances["fire"] = "half";
        unit.cooldowns["skill_a"] = 1;
        return unit;
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
}
