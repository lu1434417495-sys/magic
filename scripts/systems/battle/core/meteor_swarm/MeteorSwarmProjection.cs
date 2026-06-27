using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal static class MeteorSwarmProjection
{
    internal static GDictionary Project(BattleSpecialProfilePreviewFacts facts)
    {
        if (facts == null)
            return new GDictionary();

        GDictionary payload = ProjectBasePreviewFacts(facts);
        if (facts is MeteorSwarmPreviewFacts meteorFacts)
        {
            payload["impact_count"] = meteorFacts.impact_count;
            payload["expected_target_count"] = meteorFacts.expected_target_count;
            payload["expected_terrain_effect_count"] =
                meteorFacts.expected_terrain_effect_count;
            payload["friendly_fire_risk_percent"] = meteorFacts.friendly_fire_risk_percent;
            payload["component_preview"] = ProjectComponentFactArray(
                meteorFacts.component_preview
            );
            payload["target_numeric_summary"] = ProjectNumericSummaryArray(
                meteorFacts.target_numeric_summaries
            );
        }
        return payload;
    }

    internal static GDictionary Project(MeteorSwarmComponentFact fact)
    {
        if (fact == null)
            return new GDictionary();
        return new GDictionary
        {
            ["component_id"] = fact.component_id.ToString(),
            ["role_label"] = fact.role_label.ToString(),
            ["damage_tag"] = fact.damage_tag.ToString(),
            ["base_power"] = fact.base_power,
            ["dice_count"] = fact.dice_count,
            ["dice_sides"] = fact.dice_sides,
            ["damage_scale"] = fact.damage_scale,
            ["save_profile_id"] = fact.save_profile_id.ToString(),
            ["can_crit"] = fact.can_crit,
            ["mastery_weight"] = fact.mastery_weight,
            ["ring_min"] = fact.ring_min,
            ["ring_max"] = fact.ring_max,
            ["distance_from_anchor"] = fact.distance_from_anchor,
            ["damage"] = fact.damage,
            ["healing"] = fact.healing,
        };
    }

    internal static GDictionary Project(MeteorSwarmTerrainSummaryFact fact)
    {
        if (fact == null)
            return new GDictionary();
        return new GDictionary
        {
            ["coverage_shape_id"] = fact.coverage_shape_id.ToString(),
            ["radius"] = fact.radius,
            ["affected_coord_count"] = fact.affected_coord_count,
            ["terrain_effect_count"] = fact.terrain_effect_count,
            ["crater_count"] = fact.crater_count,
            ["rubble_count"] = fact.rubble_count,
            ["dust_count"] = fact.dust_count,
        };
    }

    internal static GDictionary Project(MeteorSwarmReportEntry entry)
    {
        if (entry == null)
            return new GDictionary();

        return new GDictionary
        {
            ["entry_type"] = entry.entry_type.ToString(),
            ["skill_id"] = entry.skill_id.ToString(),
            ["source_unit_id"] = entry.source_unit_id.ToString(),
            ["anchor_coord"] = entry.anchor_coord,
            ["nominal_anchor_coord"] = entry.nominal_anchor_coord,
            ["nominal_plan_signature"] = entry.nominal_plan_signature ?? "",
            ["final_plan_signature"] = entry.final_plan_signature ?? "",
            ["target_count"] = entry.target_count,
            ["terrain_effect_count"] = entry.terrain_effect_count,
            ["total_damage"] = entry.total_damage,
            ["defeated_count"] = entry.defeated_count,
            ["component_breakdown"] = ProjectComponentFactArray(entry.component_breakdown),
            ["target_summaries"] = ProjectTargetSummaryArray(entry.target_summaries),
            ["terrain_summary"] = Project(entry.terrain_summary),
            ["text"] = entry.text ?? "",
        };
    }

    internal static GDictionary Project(MeteorSwarmTargetPlan plan)
    {
        if (plan == null)
            return new GDictionary();

        var ringPayload = new GDictionary();
        foreach (Vector2I coord in plan.affected_coords ?? new List<Vector2I>())
        {
            ringPayload[$"{coord.X}:{coord.Y}"] = plan.GetRingForCoord(coord);
        }

        return new GDictionary
        {
            ["skill_id"] = plan.skill_id.ToString(),
            ["source_unit_id"] = plan.source_unit_id.ToString(),
            ["final_anchor_coord"] = plan.final_anchor_coord,
            ["nominal_anchor_coord"] = plan.nominal_anchor_coord,
            ["coverage_shape_id"] = plan.coverage_shape_id.ToString(),
            ["radius"] = plan.radius,
            ["affected_coords"] = ToVector2IArray(plan.affected_coords),
            ["ring_by_coord"] = ringPayload,
            ["target_unit_ids"] = ToStringNameArray(plan.target_unit_ids),
            ["drift_applied"] = plan.drift_applied,
            ["drift_from_coord"] = plan.drift_from_coord,
            ["nominal_plan_signature"] = plan.nominal_plan_signature,
            ["final_plan_signature"] = plan.final_plan_signature,
        };
    }

    internal static GDictionary ProjectSummary(MeteorSwarmTargetOutcome outcome)
    {
        if (outcome == null)
            return new GDictionary();

        return new GDictionary
        {
            ["target_unit_id"] = outcome.target_unit_id.ToString(),
            ["target_coord"] = outcome.target_coord,
            ["target_faction_id"] = outcome.target_faction_id.ToString(),
            ["distance_from_anchor"] = outcome.distance_from_anchor,
            ["total_damage"] = outcome.total_damage,
            ["total_healing"] = outcome.total_healing,
            ["defeated"] = outcome.defeated,
            ["status_effect_ids"] = ToStringNameArray(outcome.status_effect_ids),
            ["component_breakdown"] = ProjectComponentFactArray(
                outcome.report_component_breakdown
            ),
        };
    }

    internal static GDictionary Project(MeteorSwarmHostileTerrainConsequence consequence)
    {
        if (consequence == null)
            return new GDictionary();
        return new GDictionary
        {
            ["move_cost_delta"] = consequence.MoveCostDelta,
            ["creates_dust"] = consequence.CreatesDust,
            ["creates_crater"] = consequence.CreatesCrater,
            ["creates_rubble"] = consequence.CreatesRubble,
        };
    }

    internal static GDictionary Project(MeteorSwarmComponentBreakdownEntry entry)
    {
        if (entry == null)
            return new GDictionary();

        return new GDictionary
        {
            ["component_id"] = entry.ComponentId.ToString(),
            ["role_label"] = entry.RoleLabel.ToString(),
            ["damage_tag"] = entry.DamageTag.ToString(),
            ["expected_damage"] = entry.ExpectedDamage,
            ["worst_case_damage"] = entry.WorstCaseDamage,
            ["post_save_expected_damage"] = entry.PostSaveExpectedDamage,
            ["post_save_worst_case_damage"] = entry.PostSaveWorstCaseDamage,
            ["pre_save_expected_damage"] = entry.PreSaveExpectedDamage,
            ["pre_save_worst_case_damage"] = entry.PreSaveWorstCaseDamage,
            ["resistance_tier"] = entry.ResistanceTier.ToString(),
            ["save_profile_id"] = entry.SaveProfileId ?? "",
            ["save_estimate"] = BattleDamagePreviewProjection.Project(entry.SaveEstimate),
            ["worst_save_estimate"] = BattleDamagePreviewProjection.Project(
                entry.WorstSaveEstimate
            ),
            ["mitigation_sources"] = AttackEffectResolutionResultReader.BuildMitigationSourcesPayload(
                entry.HalfSourceLabels?.ToArray() ?? Array.Empty<string>(),
                entry.DoubleSourceLabels?.ToArray() ?? Array.Empty<string>(),
                entry.ImmuneSourceLabels?.ToArray() ?? Array.Empty<string>()
            ),
            ["fixed_mitigation_sources"] =
                AttackEffectResolutionResultReader.BuildFixedMitigationSourcesPayload(
                    entry.FixedMitigationSourceLabels?.ToArray() ?? Array.Empty<string>()
                ),
            ["shield_absorbed_estimate"] = entry.ShieldAbsorbedEstimate,
            ["shield_absorbed_worst"] = entry.ShieldAbsorbedWorst,
        };
    }

    internal static GDictionary Project(MeteorSwarmNumericSummary summary)
    {
        if (summary == null)
            return new GDictionary();

        return new GDictionary
        {
            ["candidate_anchor_coord"] = summary.CandidateAnchorCoord,
            ["target_unit_id"] = summary.TargetUnitId.ToString(),
            ["ally_unit_id"] = summary.AllyUnitId.ToString(),
            ["target_faction_id"] = summary.TargetFactionId.ToString(),
            ["is_ally"] = summary.IsAlly,
            ["distance_from_anchor"] = summary.DistanceFromAnchor,
            ["component_expected_damage"] = summary.ComponentExpectedDamage,
            ["component_worst_case_damage"] = summary.ComponentWorstCaseDamage,
            ["component_breakdown"] = ProjectComponentBreakdownArray(
                summary.ComponentBreakdown
            ),
            ["lethal_probability_percent"] = summary.LethalProbabilityPercent,
            ["save_profile_ids"] = ToStringArray(summary.SaveProfileIds),
            ["resistance_tiers_by_damage_tag"] = ProjectResistanceTiers(
                summary.ResistanceTiersByDamageTag
            ),
            ["shield_hp"] = summary.ShieldHp,
            ["guard_block_estimate"] = summary.GuardBlockEstimate,
            ["status_effect_ids"] = ToStringNameArray(summary.StatusEffectIds),
            ["ap_penalty"] = summary.ApPenalty,
            ["hostile_terrain_consequence"] = Project(summary.HostileTerrain),
            ["expected_damage_hp_percent"] = summary.ExpectedDamageHpPercent,
            ["worst_case_damage_hp_percent"] = summary.WorstCaseDamageHpPercent,
            ["hard_reject"] = summary.HardReject,
            ["soft_penalty"] = summary.SoftPenalty,
        };
    }

    internal static GDictArray ProjectNumericSummaryArray(
        IEnumerable<MeteorSwarmNumericSummary> summaries
    )
    {
        var result = new GDictArray();
        foreach (
            MeteorSwarmNumericSummary summary
            in summaries ?? Array.Empty<MeteorSwarmNumericSummary>()
        )
        {
            if (summary != null)
                result.Add(Project(summary));
        }
        return result;
    }

    internal static GDictArray ProjectComponentFactArray(
        IEnumerable<MeteorSwarmComponentFact> values
    )
    {
        var result = new GDictArray();
        foreach (
            MeteorSwarmComponentFact value
            in values ?? Array.Empty<MeteorSwarmComponentFact>()
        )
        {
            if (value != null)
                result.Add(Project(value));
        }
        return result;
    }

    private static GDictArray ProjectTargetSummaryArray(
        IEnumerable<MeteorSwarmTargetOutcome> values
    )
    {
        var result = new GDictArray();
        foreach (
            MeteorSwarmTargetOutcome value in values ?? Array.Empty<MeteorSwarmTargetOutcome>()
        )
        {
            if (value != null)
                result.Add(ProjectSummary(value));
        }
        return result;
    }

    private static GDictionary ProjectBasePreviewFacts(BattleSpecialProfilePreviewFacts facts) =>
        new()
        {
            ["profile_id"] = facts.profile_id.ToString(),
            ["skill_id"] = facts.skill_id.ToString(),
            ["preview_fact_id"] = facts.preview_fact_id.ToString(),
            ["nominal_plan_signature"] = facts.nominal_plan_signature,
            ["final_plan_signature"] = facts.final_plan_signature,
            ["resolved_anchor_coord"] = facts.resolved_anchor_coord,
            ["target_unit_ids"] = ToStringNameArray(facts.target_unit_ids),
            ["target_coords"] = ToVector2IArray(facts.target_coords),
            ["terrain_summary"] = Project(facts.terrain_summary),
            ["friendly_fire_numeric_summary"] = ProjectNumericSummaryArray(
                facts.friendly_fire_numeric_summary
            ),
            ["attack_roll_modifier_breakdown"] =
                AttackPreviewData.BuildAttackRollModifierBreakdownPayload(
                    facts.attack_roll_modifier_breakdown
                ),
        };

    private static GDictArray ProjectComponentBreakdownArray(
        IEnumerable<MeteorSwarmComponentBreakdownEntry> values
    )
    {
        var result = new GDictArray();
        foreach (
            MeteorSwarmComponentBreakdownEntry value
            in values ?? Array.Empty<MeteorSwarmComponentBreakdownEntry>()
        )
        {
            if (value != null)
                result.Add(Project(value));
        }
        return result;
    }

    private static GStringArray ToStringArray(IEnumerable<string> values)
    {
        var result = new GStringArray();
        foreach (string value in values ?? Array.Empty<string>())
        {
            if (!string.IsNullOrEmpty(value))
                result.Add(value);
        }
        return result;
    }

    private static GStringNameArray ToStringNameArray(IEnumerable<StringName> values)
    {
        var result = new GStringNameArray();
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value != "")
                result.Add(value);
        }
        return result;
    }

    private static Godot.Collections.Array<Vector2I> ToVector2IArray(
        IEnumerable<Vector2I> values
    ) => new Vector2IList(values ?? Array.Empty<Vector2I>()).ToGodotArray();

    private static GDictionary ProjectResistanceTiers(
        Dictionary<StringName, StringName> values
    )
    {
        var result = new GDictionary();
        foreach (
            KeyValuePair<StringName, StringName> entry
            in values ?? new Dictionary<StringName, StringName>()
        )
        {
            if (entry.Key != "" && entry.Value != "")
                result[entry.Key.ToString()] = entry.Value.ToString();
        }
        return result;
    }
}
