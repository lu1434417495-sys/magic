using System.Collections.Generic;
using Godot;

internal sealed class EnemyAiGenerationSlotDefinition
{
    internal EnemyAiGenerationSlotDefinition(
        StringName slotId,
        StringName slotRole,
        int order,
        IReadOnlyList<StringName> allowedAffordances,
        IReadOnlyList<StringName> actionFamilies,
        StringName styleTemplateActionId,
        StringName scoreBucketId,
        StringName targetSelector,
        int desiredMinDistance,
        int desiredMaxDistance,
        StringName distanceReference,
        StringName suppressionPolicy
    )
    {
        SlotId = slotId;
        SlotRole = slotRole;
        Order = order;
        AllowedAffordances = EnemyDefinitionCollections.FreezeList(allowedAffordances);
        ActionFamilies = EnemyDefinitionCollections.FreezeList(actionFamilies);
        StyleTemplateActionId = styleTemplateActionId;
        ScoreBucketId = scoreBucketId;
        TargetSelector = targetSelector;
        DesiredMinDistance = desiredMinDistance;
        DesiredMaxDistance = desiredMaxDistance;
        DistanceReference = distanceReference;
        SuppressionPolicy = suppressionPolicy;
    }

    internal StringName SlotId { get; }
    internal StringName SlotRole { get; }
    internal EnemyAiGenerationSlotRole SlotRoleKind =>
        EnemyAiGenerationSlotDef.ToSlotRole(SlotRole);
    internal int Order { get; }
    internal IReadOnlyList<StringName> AllowedAffordances { get; }
    internal IReadOnlyList<StringName> ActionFamilies { get; }
    internal StringName StyleTemplateActionId { get; }
    internal StringName ScoreBucketId { get; }
    internal StringName TargetSelector { get; }
    internal int DesiredMinDistance { get; }
    internal int DesiredMaxDistance { get; }
    internal StringName DistanceReference { get; }
    internal EnemyAiDistanceReference DistanceReferenceKind =>
        EnemyAiDistanceReferences.ToKind(DistanceReference);
    internal StringName SuppressionPolicy { get; }
    internal EnemyAiGenerationSuppressionPolicy SuppressionPolicyKind =>
        EnemyAiGenerationSlotDef.ToSuppressionPolicy(SuppressionPolicy);

    internal bool MatchesAffordance(
        BattleAiSkillAffordanceRecord record,
        StringName actionFamily
    )
    {
        EnemyAiActionFamily family = EnemyAiGenerationSlotDef.ToActionFamily(actionFamily);
        if (record == null || !ContainsActionFamily(ActionFamilies, family))
            return false;
        foreach (StringName affordance in record.affordances)
        {
            if (ContainsAffordance(AllowedAffordances, EnemyAiGenerationSlotDef.ToAffordance(affordance)))
                return true;
        }
        return false;
    }

    internal string BuildSignature() =>
        string.Join(
            "|",
            new[]
            {
                $"slot_id={SlotId}",
                $"slot_role={SlotRole}",
                $"order={Order}",
                $"allowed_affordances={StringifySorted(AllowedAffordances)}",
                $"action_families={StringifySorted(ActionFamilies)}",
                $"style_template_action_id={StyleTemplateActionId}",
                $"score_bucket_id={ScoreBucketId}",
                $"target_selector={TargetSelector}",
                $"desired_min_distance={DesiredMinDistance}",
                $"desired_max_distance={DesiredMaxDistance}",
                $"distance_reference={DistanceReference}",
                $"suppression_policy={SuppressionPolicy}",
            }
        );

    private static bool ContainsActionFamily(
        IReadOnlyList<StringName> values,
        EnemyAiActionFamily expected
    )
    {
        if (expected == EnemyAiActionFamily.Unknown)
            return false;
        foreach (StringName value in values)
        {
            if (EnemyAiGenerationSlotDef.ToActionFamily(value) == expected)
                return true;
        }
        return false;
    }

    private static bool ContainsAffordance(
        IReadOnlyList<StringName> values,
        EnemyAiSkillAffordance expected
    )
    {
        if (expected == EnemyAiSkillAffordance.Unknown)
            return false;
        foreach (StringName value in values)
        {
            if (EnemyAiGenerationSlotDef.ToAffordance(value) == expected)
                return true;
        }
        return false;
    }

    private static string StringifySorted(IReadOnlyList<StringName> values)
    {
        var copy = new List<string>();
        foreach (StringName value in values)
            copy.Add(value.ToString());
        copy.Sort(System.StringComparer.Ordinal);
        return string.Join(",", copy);
    }
}
