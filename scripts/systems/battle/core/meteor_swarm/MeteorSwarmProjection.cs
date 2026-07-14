using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class MeteorSwarmProjection
{
    internal static GodotProjectionLease<GDictionary> BuildLease(
        BattleSpecialProfilePreviewFacts facts
    ) =>
        TraceDictionaryProjection.BuildLease(
            BuildPlain(facts),
            "meteor-swarm-preview-facts",
            LifetimeDomain.Request,
            "MeteorSwarmProjection.preview_facts"
        );

    internal static GodotProjectionLease<GDictionary> BuildLease(
        MeteorSwarmNumericSummary summary
    ) =>
        TraceDictionaryProjection.BuildLease(
            BuildPlain(summary),
            "meteor-swarm-numeric-summary",
            LifetimeDomain.Request,
            "MeteorSwarmProjection.numeric_summary"
        );

    internal static GodotProjectionLease<GDictionary> BuildLease(
        MeteorSwarmReportEntry entry
    ) =>
        TraceDictionaryProjection.BuildLease(
            BuildPlain(entry),
            "meteor-swarm-report-entry",
            LifetimeDomain.Request,
            "MeteorSwarmProjection.report_entry"
        );

    internal static Dictionary<string, object> BuildPlain(
        BattleSpecialProfilePreviewFacts facts
    )
    {
        if (facts == null)
            return new Dictionary<string, object>(StringComparer.Ordinal);

        var result = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["profile_id"] = facts.profile_id.ToString(),
            ["skill_id"] = facts.skill_id.ToString(),
            ["preview_fact_id"] = facts.preview_fact_id.ToString(),
            ["nominal_plan_signature"] = facts.nominal_plan_signature ?? "",
            ["final_plan_signature"] = facts.final_plan_signature ?? "",
            ["resolved_anchor_coord"] = facts.resolved_anchor_coord,
            ["target_unit_ids"] = BuildStringNameList(facts.target_unit_ids),
            ["target_coords"] = BuildVectorList(facts.target_coords),
            ["terrain_summary"] =
                facts.terrain_summary?.ToTraceDictionary()
                ?? new Dictionary<string, object>(StringComparer.Ordinal),
            ["friendly_fire_numeric_summary"] = BuildNumericSummaryListPlain(
                facts.friendly_fire_numeric_summary
            ),
            ["attack_roll_modifier_breakdown"] = BuildModifierList(
                facts.attack_roll_modifier_breakdown
            ),
        };
        if (facts is not MeteorSwarmPreviewFacts meteorFacts)
            return result;

        result["impact_count"] = meteorFacts.impact_count;
        result["expected_target_count"] = meteorFacts.expected_target_count;
        result["expected_terrain_effect_count"] =
            meteorFacts.expected_terrain_effect_count;
        result["friendly_fire_risk_percent"] = meteorFacts.friendly_fire_risk_percent;
        result["component_preview"] = BuildComponentFactList(meteorFacts.component_preview);
        result["target_numeric_summary"] = BuildNumericSummaryListPlain(
            meteorFacts.target_numeric_summaries
        );
        return result;
    }

    internal static Dictionary<string, object> BuildPlain(MeteorSwarmNumericSummary summary)
    {
        if (summary == null)
            return new Dictionary<string, object>(StringComparer.Ordinal);

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["candidate_anchor_coord"] = summary.CandidateAnchorCoord,
            ["target_unit_id"] = summary.TargetUnitId.ToString(),
            ["ally_unit_id"] = summary.AllyUnitId.ToString(),
            ["target_faction_id"] = summary.TargetFactionId.ToString(),
            ["is_ally"] = summary.IsAlly,
            ["distance_from_anchor"] = summary.DistanceFromAnchor,
            ["component_expected_damage"] = summary.ComponentExpectedDamage,
            ["component_worst_case_damage"] = summary.ComponentWorstCaseDamage,
            ["component_breakdown"] = BuildComponentBreakdownList(
                summary.ComponentBreakdown
            ),
            ["lethal_probability_percent"] = summary.LethalProbabilityPercent,
            ["save_profile_ids"] = BuildStringList(summary.SaveProfileIds),
            ["resistance_tiers_by_damage_tag"] = BuildResistanceTiers(
                summary.ResistanceTiersByDamageTag
            ),
            ["shield_hp"] = summary.ShieldHp,
            ["guard_block_estimate"] = summary.GuardBlockEstimate,
            ["status_effect_ids"] = BuildStringNameList(summary.StatusEffectIds),
            ["ap_penalty"] = summary.ApPenalty,
            ["hostile_terrain_consequence"] =
                summary.HostileTerrain?.ToTraceDictionary()
                ?? new Dictionary<string, object>(StringComparer.Ordinal),
            ["expected_damage_hp_percent"] = summary.ExpectedDamageHpPercent,
            ["worst_case_damage_hp_percent"] = summary.WorstCaseDamageHpPercent,
            ["hard_reject"] = summary.HardReject,
            ["soft_penalty"] = summary.SoftPenalty,
        };
    }

    internal static Dictionary<string, object> BuildPlain(MeteorSwarmReportEntry entry)
    {
        if (entry == null)
            return new Dictionary<string, object>(StringComparer.Ordinal);
        var componentBreakdown = new List<object>();
        foreach (
            MeteorSwarmComponentFact component in
            entry.component_breakdown ?? new List<MeteorSwarmComponentFact>()
        )
        {
            if (component != null)
                componentBreakdown.Add(component.ToTraceDictionary());
        }
        var targetSummaries = new List<object>();
        foreach (
            MeteorSwarmTargetOutcome outcome in
            entry.target_summaries ?? new List<MeteorSwarmTargetOutcome>()
        )
        {
            if (outcome != null)
                targetSummaries.Add(BuildTargetSummaryPlain(outcome));
        }
        return new Dictionary<string, object>(StringComparer.Ordinal)
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
            ["component_breakdown"] = componentBreakdown,
            ["target_summaries"] = targetSummaries,
            ["terrain_summary"] =
                entry.terrain_summary?.ToTraceDictionary()
                ?? new Dictionary<string, object>(StringComparer.Ordinal),
            ["text"] = entry.text ?? "",
        };
    }

    internal static List<object> BuildNumericSummaryListPlain(
        IEnumerable<MeteorSwarmNumericSummary> summaries
    )
    {
        var result = new List<object>();
        foreach (
            MeteorSwarmNumericSummary summary in
            summaries ?? Array.Empty<MeteorSwarmNumericSummary>()
        )
        {
            if (summary != null)
                result.Add(BuildPlain(summary));
        }
        return result;
    }

    private static List<object> BuildComponentBreakdownList(
        IEnumerable<MeteorSwarmComponentBreakdownEntry> values
    )
    {
        var result = new List<object>();
        foreach (
            MeteorSwarmComponentBreakdownEntry value in
            values ?? Array.Empty<MeteorSwarmComponentBreakdownEntry>()
        )
        {
            if (value != null)
                result.Add(BuildComponentBreakdown(value));
        }
        return result;
    }

    private static Dictionary<string, object> BuildComponentBreakdown(
        MeteorSwarmComponentBreakdownEntry entry
    )
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
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
            ["save_estimate"] =
                entry.SaveEstimate?.ToTraceDictionary()
                ?? new Dictionary<string, object>(StringComparer.Ordinal),
            ["worst_save_estimate"] =
                entry.WorstSaveEstimate?.ToTraceDictionary()
                ?? new Dictionary<string, object>(StringComparer.Ordinal),
            ["mitigation_sources"] = BuildMitigationSources(entry),
            ["fixed_mitigation_sources"] = BuildFixedMitigationSources(
                entry.FixedMitigationSourceLabels
            ),
            ["shield_absorbed_estimate"] = entry.ShieldAbsorbedEstimate,
            ["shield_absorbed_worst"] = entry.ShieldAbsorbedWorst,
        };
    }

    private static List<object> BuildMitigationSources(
        MeteorSwarmComponentBreakdownEntry entry
    )
    {
        var result = new List<object>();
        AppendMitigationSources(result, entry.HalfSourceLabels, "half");
        AppendMitigationSources(result, entry.DoubleSourceLabels, "double");
        AppendMitigationSources(result, entry.ImmuneSourceLabels, "immune");
        return result;
    }

    private static void AppendMitigationSources(
        List<object> target,
        IEnumerable<string> labels,
        string tier
    )
    {
        foreach (string label in labels ?? Array.Empty<string>())
        {
            if (string.IsNullOrEmpty(label))
                continue;
            target.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["tier"] = tier,
                    ["status_id"] = label,
                }
            );
        }
    }

    private static List<object> BuildFixedMitigationSources(IEnumerable<string> labels)
    {
        var result = new List<object>();
        foreach (string label in labels ?? Array.Empty<string>())
        {
            if (string.IsNullOrEmpty(label))
                continue;
            result.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["status_id"] = label,
                }
            );
        }
        return result;
    }

    private static List<object> BuildComponentFactList(
        IEnumerable<MeteorSwarmComponentFact> values
    )
    {
        var result = new List<object>();
        foreach (
            MeteorSwarmComponentFact value in
            values ?? Array.Empty<MeteorSwarmComponentFact>()
        )
        {
            if (value != null)
                result.Add(value.ToTraceDictionary());
        }
        return result;
    }

    private static Dictionary<string, object> BuildTargetSummaryPlain(
        MeteorSwarmTargetOutcome outcome
    )
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["target_unit_id"] = outcome.target_unit_id.ToString(),
            ["target_coord"] = outcome.target_coord,
            ["target_faction_id"] = outcome.target_faction_id.ToString(),
            ["distance_from_anchor"] = outcome.distance_from_anchor,
            ["total_damage"] = outcome.total_damage,
            ["total_healing"] = outcome.total_healing,
            ["defeated"] = outcome.defeated,
            ["status_effect_ids"] = BuildStringNameList(outcome.status_effect_ids),
            ["component_breakdown"] = BuildComponentFactList(
                outcome.report_component_breakdown
            ),
        };
    }

    private static List<object> BuildModifierList(
        IEnumerable<BattleAttackRollModifierSpec> values
    )
    {
        var result = new List<object>();
        foreach (
            BattleAttackRollModifierSpec value in
            values ?? Array.Empty<BattleAttackRollModifierSpec>()
        )
        {
            if (value != null)
                result.Add(value.ToTraceDictionary());
        }
        return result;
    }

    private static List<string> BuildStringList(IEnumerable<string> values)
    {
        var result = new List<string>();
        foreach (string value in values ?? Array.Empty<string>())
        {
            if (!string.IsNullOrEmpty(value))
                result.Add(value);
        }
        return result;
    }

    private static List<StringName> BuildStringNameList(IEnumerable<StringName> values)
    {
        var result = new List<StringName>();
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value != "")
                result.Add(value);
        }
        return result;
    }

    private static List<Vector2I> BuildVectorList(IEnumerable<Vector2I> values) =>
        values != null ? new List<Vector2I>(values) : new List<Vector2I>();

    private static Dictionary<string, object> BuildResistanceTiers(
        IReadOnlyDictionary<StringName, StringName> values
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
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
