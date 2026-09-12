using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public readonly record struct ContingencyTemplateStoredSpellInfo(
    StringName StoredSkillId,
    int MaxCastLevel
);

public static class ContingencyContentRules
{
    public static int ResolveReservedMpMax(ContingencySetupTemplateDefinition template)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (template.MatrixLoad <= 0 || template.ReservedMpPerMatrixLoad <= 0)
        {
            throw new InvalidOperationException(
                $"Contingency template '{template.TemplateId}' must define positive matrix load and reserved MP per matrix load."
            );
        }
        return checked(template.MatrixLoad * template.ReservedMpPerMatrixLoad);
    }

    public static IReadOnlyList<ContingencyMaterialCostState> BuildChargeCosts(
        ContingencySetupTemplateDefinition template
    )
    {
        ArgumentNullException.ThrowIfNull(template);
        if (template.ChargeMaterialCosts == null || template.ChargeMaterialCosts.Count == 0)
        {
            throw new InvalidOperationException(
                $"Contingency template '{template.TemplateId}' must define at least one charge material cost."
            );
        }
        var result = new List<ContingencyMaterialCostState>(template.ChargeMaterialCosts.Count);
        for (int index = 0; index < template.ChargeMaterialCosts.Count; index++)
        {
            ContingencyMaterialCostDefinition definition = template.ChargeMaterialCosts[index];
            if (definition == null || definition.ItemId == "" || definition.Quantity <= 0)
            {
                throw new InvalidOperationException(
                    $"Contingency template '{template.TemplateId}' has an invalid charge material cost at index {index}."
                );
            }
            ContingencyMaterialCostState state = ContingencyMaterialCostState.Create(
                definition.ItemId,
                definition.Quantity
            );
            if (state == null)
            {
                throw new InvalidOperationException(
                    $"Contingency template '{template.TemplateId}' charge material cost at index {index} could not be projected."
                );
            }
            result.Add(state);
        }
        return new ReadOnlyCollection<ContingencyMaterialCostState>(result);
    }

    public static IReadOnlyList<ContingencyTemplateStoredSpellInfo> GetTemplateStoredSpellsTyped(
        ContingencySetupTemplateDefinition template
    )
    {
        if (template?.StoredSpells == null)
            return System.Array.Empty<ContingencyTemplateStoredSpellInfo>();

        var result = new List<ContingencyTemplateStoredSpellInfo>(template.StoredSpells.Count);
        foreach (ContingencyStoredSpellTemplateDefinition storedSpell in template.StoredSpells)
        {
            if (storedSpell == null || storedSpell.StoredSkillId == "")
                return System.Array.Empty<ContingencyTemplateStoredSpellInfo>();
            result.Add(
                new ContingencyTemplateStoredSpellInfo(
                    storedSpell.StoredSkillId,
                    Mathf.Max(storedSpell.MaxCastLevel, 1)
                )
            );
        }
        return new ReadOnlyCollection<ContingencyTemplateStoredSpellInfo>(result);
    }

    public static ContingencyMatrixSetupState BuildSetupStateFromTemplate(
        ContingencySetupTemplateDefinition template,
        int sourceSkillLevel,
        IReadOnlyDictionary<StringName, int> castLevelsByStoredSkillId
    )
    {
        if (template == null)
            return null;

        IReadOnlyDictionary<string, object> payload = BuildSetupPlainPayload(
            template,
            sourceSkillLevel,
            castLevelsByStoredSkillId
        );
        if (payload == null)
            return null;

        using GodotProjectionLease<GDictionary> lease =
            RuntimePlainPayload.ProjectDictionaryLease(
                payload,
                $"contingency-template:{template.TemplateId}",
                LifetimeDomain.Request,
                "ContingencyContentRules.BuildSetupStateFromTemplate"
            );
        return ContingencyMatrixSetupState.FromDictionary(lease.Value);
    }

    private static IReadOnlyDictionary<string, object> BuildSetupPlainPayload(
        ContingencySetupTemplateDefinition template,
        int sourceSkillLevel,
        IReadOnlyDictionary<StringName, int> castLevelsByStoredSkillId
    )
    {
        var storedSpells = new List<object>(template.StoredSpells.Count);
        foreach (ContingencyStoredSpellTemplateDefinition storedSpell in template.StoredSpells)
        {
            if (storedSpell == null || storedSpell.StoredSkillId == "")
                return null;

            int castLevel = 1;
            if (
                castLevelsByStoredSkillId != null
                && castLevelsByStoredSkillId.TryGetValue(
                    storedSpell.StoredSkillId,
                    out int resolvedLevel
                )
            )
            {
                castLevel = Mathf.Clamp(
                    resolvedLevel,
                    1,
                    Mathf.Max(storedSpell.MaxCastLevel, 1)
                );
            }

            storedSpells.Add(
                new Dictionary<string, object>(System.StringComparer.Ordinal)
                {
                    ["stored_skill_id"] = storedSpell.StoredSkillId.ToString(),
                    ["cast_level"] = castLevel,
                    ["order"] = storedSpell.Order,
                    ["target_resolver"] = BuildTargetResolverPlain(storedSpell.TargetResolver),
                    ["parameter_bindings"] = storedSpell.ParameterBindings,
                    ["fallback_policy"] = storedSpell.FallbackPolicy.ToString(),
                }
            );
        }

        return new ReadOnlyDictionary<string, object>(
            new Dictionary<string, object>(System.StringComparer.Ordinal)
            {
                ["setup_id"] = template.TemplateId.ToString(),
                ["display_name"] = template.DisplayName ?? "",
                ["enabled"] = true,
                ["charged"] = false,
                ["source_skill_id"] = template.SourceSkillId.ToString(),
                ["source_skill_level"] = Mathf.Max(sourceSkillLevel, 1),
                ["matrix_load"] = template.MatrixLoad,
                ["reserved_mp_max"] = 0,
                ["material_costs"] = System.Array.Empty<object>(),
                ["trigger"] = BuildTriggerPlain(template.Trigger),
                ["release_mode"] = template.ReleaseMode.ToString(),
                ["stored_spells"] = new ReadOnlyCollection<object>(storedSpells),
            }
        );
    }

    private static IReadOnlyDictionary<string, object> BuildTriggerPlain(
        ContingencyTriggerDefinition trigger
    )
    {
        if (trigger == null)
            return new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

        var result = new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["type"] = trigger.Type.ToString(),
        };
        switch (trigger.TriggerKind)
        {
            case ContingencyTriggerKind.CombatStarted:
            case ContingencyTriggerKind.FatalDamageIncoming:
            case ContingencyTriggerKind.OwnerTurnStarted:
                result["subject"] = trigger.Subject.ToString();
                result["timing"] = trigger.Timing.ToString();
                break;
            case ContingencyTriggerKind.HpBelowPercent:
                result["subject"] = trigger.Subject.ToString();
                result["percent"] = trigger.Percent;
                result["crossing_only"] = trigger.CrossingOnly;
                result["timing"] = trigger.Timing.ToString();
                break;
            case ContingencyTriggerKind.IncomingDamagePercent:
                result["subject"] = trigger.Subject.ToString();
                result["damage_percent"] = trigger.DamagePercent;
                result["damage_basis"] = trigger.DamageBasis.ToString();
                result["damage_amount_mode"] = trigger.DamageAmountMode.ToString();
                result["timing"] = trigger.Timing.ToString();
                break;
            case ContingencyTriggerKind.EnemyEnterRadius:
                result["center"] = trigger.Center.ToString();
                result["radius"] = trigger.Radius;
                result["radius_metric"] = trigger.RadiusMetric.ToString();
                result["source_team"] = trigger.SourceTeam.ToString();
                result["timing"] = trigger.Timing.ToString();
                break;
            case ContingencyTriggerKind.StatusApplied:
                result["subject"] = trigger.Subject.ToString();
                result["status_tags"] = ToPlainStringList(trigger.StatusTags);
                result["application_match"] = trigger.ApplicationMatch.ToString();
                result["timing"] = trigger.Timing.ToString();
                break;
            case ContingencyTriggerKind.AffectedBySpell:
                result["subject"] = trigger.Subject.ToString();
                result["source_team"] = trigger.SourceTeam.ToString();
                result["spell_match"] = trigger.SpellMatch.ToString();
                result["timing"] = trigger.Timing.ToString();
                break;
        }
        return new ReadOnlyDictionary<string, object>(result);
    }

    private static IReadOnlyDictionary<string, object> BuildTargetResolverPlain(
        ContingencyTargetResolverDefinition resolver
    )
    {
        if (resolver == null)
            return new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

        var result = new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["type"] = resolver.Type.ToString(),
        };
        if (resolver.ResolverKind == ContingencyTargetResolverKind.EmptyCellNearOwner)
        {
            result["preference"] = resolver.Preference.ToString();
            result["max_distance"] = resolver.MaxDistance;
        }
        return new ReadOnlyDictionary<string, object>(result);
    }

    private static IReadOnlyList<object> ToPlainStringList(IReadOnlyList<StringName> values)
    {
        if (values == null || values.Count == 0)
            return System.Array.Empty<object>();
        var result = new List<object>(values.Count);
        foreach (StringName value in values)
            result.Add(value.ToString());
        return new ReadOnlyCollection<object>(result);
    }
}
