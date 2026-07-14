using System.Collections.Generic;
using Godot;

internal sealed class CharacterTraitService
{
    internal interface ICharacterTraitGateway
    {
        RaceDefinition GetRaceDefForTraitAggregation(StringName memberId);
        SubraceDefinition GetSubraceDefForTraitAggregation(StringName memberId);
        BloodlineDefinition GetBloodlineDefForTraitAggregation(StringName memberId);
        BloodlineStageDefinition GetBloodlineStageDefForTraitAggregation(StringName memberId);
        AscensionDefinition GetAscensionDefForTraitAggregation(StringName memberId);
        AscensionStageDefinition GetAscensionStageDefForTraitAggregation(StringName memberId);
        PartyMemberState GetMemberStateForTraitAggregation(StringName memberId);
        EquipmentState GetEquipmentStateForTraitAggregation(StringName memberId);
        ItemDefinition GetItemDefForTraitAggregation(StringName itemId);
    }

    private readonly List<TraitDefinition> _traitDefinitions;
    private readonly ICharacterTraitGateway _gateway;

    public CharacterTraitService(
        IEnumerable<TraitDefinition> traitDefinitions,
        ICharacterTraitGateway gateway
    )
    {
        _traitDefinitions = new List<TraitDefinition>();
        if (traitDefinitions != null)
        {
            foreach (TraitDefinition traitDefinition in traitDefinitions)
                if (traitDefinition != null)
                    _traitDefinitions.Add(traitDefinition);
        }
        _gateway = gateway;
    }

    public EffectiveTraitSet BuildEffectiveTraits(
        StringName memberId,
        EquipmentState equipmentOverride = null
    )
    {
        List<EffectiveTraitInstance> raw = new();
        if (_gateway == null || memberId == "")
            return new EffectiveTraitSet(raw);

        CollectIdentity(memberId, raw);
        CollectCharacter(memberId, raw);
        CollectEquipment(memberId, equipmentOverride, raw);
        return new EffectiveTraitSet(ApplyStackPolicies(raw));
    }

    public IReadOnlyList<AttributeModifierDefinition> ResolveTraitAttributeModifiers(
        EffectiveTraitSet set
    )
    {
        List<AttributeModifierDefinition> result = new();
        if (set == null)
            return System.Array.Empty<AttributeModifierDefinition>();

        foreach (EffectiveTraitInstance instance in set.Instances)
            AppendAttributeModifiers(result, instance);
        return result.Count > 0
            ? result.AsReadOnly()
            : System.Array.Empty<AttributeModifierDefinition>();
    }

    private void CollectIdentity(StringName memberId, List<EffectiveTraitInstance> raw)
    {
        RaceDefinition raceDef = _gateway.GetRaceDefForTraitAggregation(memberId);
        SubraceDefinition subraceDef = _gateway.GetSubraceDefForTraitAggregation(memberId);
        BloodlineDefinition bloodlineDef = _gateway.GetBloodlineDefForTraitAggregation(memberId);
        BloodlineStageDefinition bloodlineStageDef =
            _gateway.GetBloodlineStageDefForTraitAggregation(memberId);
        AscensionDefinition ascensionDef = _gateway.GetAscensionDefForTraitAggregation(memberId);
        AscensionStageDefinition ascensionStageDef =
            _gateway.GetAscensionStageDefForTraitAggregation(memberId);

        if (ascensionDef?.SuppressesOriginalRaceTraits != true)
        {
            AppendDefinitionTraits(
                raw,
                raceDef?.TraitIds,
                TraitSourceKind.Identity,
                raceDef?.RaceId ?? ""
            );
            AppendDefinitionTraits(
                raw,
                subraceDef?.TraitIds,
                TraitSourceKind.Identity,
                subraceDef?.SubraceId ?? ""
            );
        }

        AppendDefinitionTraits(
            raw,
            bloodlineDef?.TraitIds,
            TraitSourceKind.Identity,
            bloodlineDef?.BloodlineId ?? ""
        );
        AppendDefinitionTraits(
            raw,
            bloodlineStageDef?.TraitIds,
            TraitSourceKind.Identity,
            bloodlineStageDef?.StageId ?? ""
        );
        AppendDefinitionTraits(
            raw,
            ascensionDef?.TraitIds,
            TraitSourceKind.Identity,
            ascensionDef?.AscensionId ?? ""
        );
        AppendDefinitionTraits(
            raw,
            ascensionStageDef?.TraitIds,
            TraitSourceKind.Identity,
            ascensionStageDef?.StageId ?? ""
        );
    }

    private void CollectCharacter(StringName memberId, List<EffectiveTraitInstance> raw)
    {
        PartyMemberState member = _gateway.GetMemberStateForTraitAggregation(memberId);
        if (member?.trait_instances == null)
            return;

        foreach (TraitInstanceState instance in member.trait_instances)
            AppendInstanceTrait(raw, instance, TraitSourceKind.Character);
    }

    private void CollectEquipment(
        StringName memberId,
        EquipmentState equipmentOverride,
        List<EffectiveTraitInstance> raw
    )
    {
        EquipmentState equipment = equipmentOverride ?? _gateway.GetEquipmentStateForTraitAggregation(memberId);
        if (equipment == null)
            return;

        foreach (StringName entrySlotId in equipment.GetEntrySlotIdsTyped())
        {
            EquipmentEntryState entry = equipment.GetEntry(entrySlotId);
            if (entry == null || entry.IsEmpty())
                continue;

            AppendEquipmentFixedTraits(raw, entry);
            EquipmentInstanceState instance = entry.GetEquipmentInstance();
            if (instance?.trait_instances == null)
                continue;

            foreach (TraitInstanceState traitInstance in instance.trait_instances)
                AppendInstanceTrait(raw, traitInstance, TraitSourceKind.EquipmentRoll);
        }
    }

    private void AppendEquipmentFixedTraits(
        List<EffectiveTraitInstance> raw,
        EquipmentEntryState entry
    )
    {
        ItemDefinition itemDefinition = _gateway.GetItemDefForTraitAggregation(entry.item_id);
        if (itemDefinition == null)
            return;

        AppendDefinitionTraits(
            raw,
            itemDefinition.GetTraitIdsTyped(),
            TraitSourceKind.EquipmentFixed,
            entry.instance_id
        );
    }

    private void AppendDefinitionTraits(
        List<EffectiveTraitInstance> raw,
        IEnumerable<StringName> traitIds,
        TraitSourceKind sourceKind,
        StringName sourceId
    )
    {
        if (traitIds == null || sourceId == "")
            return;

        foreach (StringName traitId in traitIds)
        {
            StringName normalizedTraitId = ProgressionDataUtils.to_string_name(traitId);
            if (
                !TryGetAllowedTraitDef(
                    normalizedTraitId,
                    sourceKind,
                    out TraitDefinition traitDefinition
                )
            )
                continue;

            raw.Add(
                new EffectiveTraitInstance
                {
                    TraitId = normalizedTraitId,
                    Definition = traitDefinition,
                    SourceKind = sourceKind,
                    SourceId = sourceId,
                    EffectiveInstanceKey = RawKey(sourceKind, sourceId, normalizedTraitId, null),
                    StackPolicy = traitDefinition.StackPolicyKind,
                    ChargeScope = traitDefinition.ChargeScopeKind,
                    ChargeResetTiming = traitDefinition.ChargeResetTimingKind,
                    Rank = 1,
                    Stacks = 1,
                    RollValues = new List<TraitRollValueState>(),
                }
            );
        }
    }

    private void AppendInstanceTrait(
        List<EffectiveTraitInstance> raw,
        TraitInstanceState sourceInstance,
        TraitSourceKind expectedSourceKind
    )
    {
        if (sourceInstance == null)
            return;

        TraitInstanceState instance = sourceInstance.DuplicateState();
        StringName traitId = ProgressionDataUtils.to_string_name(instance.trait_id);
        if (instance.SourceKind != expectedSourceKind)
        {
            GameLog.Error(
                $"Trait instance {instance.trait_instance_id} has source {instance.source_type}; expected {TraitContentRules.ToStringName(expectedSourceKind)}.",
                "trait.invalid_source",
                "progression"
            );
            return;
        }

        if (instance.trait_instance_id == "" || instance.source_id == "")
        {
            GameLog.Error(
                $"Trait instance {instance.trait_instance_id} for {traitId} is missing required ids.",
                "trait.invalid_instance",
                "progression"
            );
            return;
        }

        if (
            !TryGetAllowedTraitDef(
                traitId,
                expectedSourceKind,
                out TraitDefinition traitDefinition
            )
        )
            return;

        string validationError = instance.ValidateAgainstDef(traitDefinition);
        if (validationError.Length > 0)
        {
            GameLog.Error(validationError, "trait.validation_failed", "progression");
            return;
        }

        raw.Add(
            new EffectiveTraitInstance
            {
                TraitId = traitId,
                Definition = traitDefinition,
                TraitInstance = instance,
                SourceKind = expectedSourceKind,
                SourceId = instance.source_id,
                EffectiveInstanceKey = RawKey(
                    expectedSourceKind,
                    instance.source_id,
                    traitId,
                    instance
                ),
                StackPolicy = traitDefinition.StackPolicyKind,
                ChargeScope = traitDefinition.ChargeScopeKind,
                ChargeResetTiming = traitDefinition.ChargeResetTimingKind,
                Rank = Mathf.Max(instance.rank, 1),
                Stacks = Mathf.Max(instance.stacks, 1),
                RollValues = TraitInstanceState.NormalizeRollValues(instance.roll_values),
            }
        );
    }

    private bool TryGetAllowedTraitDef(
        StringName traitId,
        TraitSourceKind sourceKind,
        out TraitDefinition traitDefinition
    )
    {
        traitDefinition = null;
        TraitDefinition found = FindTraitDef(traitId);
        if (traitId == "" || found == null)
        {
            GameLog.Error(
                $"Missing trait definition for trait {traitId}.",
                "trait.missing_def",
                "progression"
            );
            return false;
        }

        if (!TraitContentRules.IsSourceKindAllowed(found, sourceKind))
        {
            GameLog.Error(
                $"Trait {traitId} does not allow source {TraitContentRules.ToStringName(sourceKind)}.",
                "trait.invalid_source",
                "progression"
            );
            return false;
        }

        traitDefinition = found;
        return true;
    }

    private static List<EffectiveTraitInstance> ApplyStackPolicies(
        List<EffectiveTraitInstance> raw
    )
    {
        List<EffectiveTraitInstance> result = new();
        List<List<EffectiveTraitInstance>> groups = new();
        List<StringName> order = new();

        foreach (EffectiveTraitInstance instance in raw)
        {
            if (instance == null || instance.TraitId == "")
                continue;
            int groupIndex = IndexOf(order, instance.TraitId);
            if (groupIndex < 0)
            {
                order.Add(instance.TraitId);
                groups.Add(new List<EffectiveTraitInstance>());
                groupIndex = groups.Count - 1;
            }
            groups[groupIndex].Add(instance);
        }

        for (int index = 0; index < order.Count; index++)
        {
            StringName traitId = order[index];
            List<EffectiveTraitInstance> group = groups[index];
            if (group.Count == 0)
                continue;

            TraitStackPolicyKind policy = group[0].StackPolicy;
            switch (policy)
            {
                case TraitStackPolicyKind.StackByInstance:
                    result.AddRange(group);
                    break;
                case TraitStackPolicyKind.HighestRoll:
                    result.Add(CollapseHighestRoll(group));
                    break;
                case TraitStackPolicyKind.Additive:
                    result.Add(CollapseAdditive(group));
                    break;
                case TraitStackPolicyKind.UniqueByTrait:
                default:
                    result.Add(CopyWithKey(group[0], traitId, 1));
                    break;
            }
        }

        return result;
    }

    private static EffectiveTraitInstance CollapseHighestRoll(List<EffectiveTraitInstance> group)
    {
        EffectiveTraitInstance selected = group[0];
        StringName compareKey = selected.Definition?.HighestRollCompareKey ?? "";
        int selectedValue = GetCompareRoll(selected, compareKey);

        for (int index = 1; index < group.Count; index++)
        {
            EffectiveTraitInstance candidate = group[index];
            int candidateValue = GetCompareRoll(candidate, compareKey);
            int valueCompare = candidateValue.CompareTo(selectedValue);
            int keyCompare = string.CompareOrdinal(
                candidate.EffectiveInstanceKey.ToString(),
                selected.EffectiveInstanceKey.ToString()
            );
            if (valueCompare > 0 || (valueCompare == 0 && keyCompare < 0))
            {
                selected = candidate;
                selectedValue = candidateValue;
            }
        }

        return CopyWithKey(selected, selected.TraitId, 1);
    }

    private static EffectiveTraitInstance CollapseAdditive(List<EffectiveTraitInstance> group)
    {
        int stacks = 0;
        foreach (EffectiveTraitInstance instance in group)
            stacks += Mathf.Max(instance?.Stacks ?? 0, 1);
        return CopyWithKey(group[0], group[0].TraitId, stacks);
    }

    private static int GetCompareRoll(EffectiveTraitInstance instance, StringName compareKey)
    {
        if (instance?.TraitInstance == null || compareKey == "")
            return int.MinValue;
        return instance.TraitInstance.GetIntRoll(compareKey, int.MinValue);
    }

    private static EffectiveTraitInstance CopyWithKey(
        EffectiveTraitInstance source,
        StringName effectiveInstanceKey,
        int stacks
    )
    {
        return new EffectiveTraitInstance
        {
            TraitId = source.TraitId,
            Definition = source.Definition,
            TraitInstance = source.TraitInstance,
            SourceKind = source.SourceKind,
            SourceId = source.SourceId,
            EffectiveInstanceKey = effectiveInstanceKey,
            StackPolicy = source.StackPolicy,
            ChargeScope = source.ChargeScope,
            ChargeResetTiming = source.ChargeResetTiming,
            Rank = Mathf.Max(source.Rank, 1),
            Stacks = Mathf.Max(stacks, 1),
            RollValues = TraitInstanceState.NormalizeRollValues(source.RollValues),
        };
    }

    private TraitDefinition FindTraitDef(StringName traitId)
    {
        StringName normalizedTraitId = ProgressionDataUtils.to_string_name(traitId);
        foreach (TraitDefinition traitDefinition in _traitDefinitions)
            if (
                traitDefinition != null
                && traitDefinition.TraitId == normalizedTraitId
            )
                return traitDefinition;
        return null;
    }

    private static int IndexOf(List<StringName> values, StringName expected)
    {
        for (int index = 0; index < values.Count; index++)
            if (values[index] == expected)
                return index;
        return -1;
    }

    private static StringName RawKey(
        TraitSourceKind sourceKind,
        StringName sourceId,
        StringName traitId,
        TraitInstanceState instance
    )
    {
        if (sourceKind == TraitSourceKind.Character || sourceKind == TraitSourceKind.EquipmentRoll)
            return instance?.trait_instance_id ?? "";

        StringName sourceType = TraitContentRules.ToStringName(sourceKind);
        if (sourceType == "" || sourceId == "" || traitId == "")
            return "";
        return new StringName($"{sourceType}::{sourceId}::{traitId}");
    }

    private static void AppendAttributeModifiers(
        List<AttributeModifierDefinition> result,
        EffectiveTraitInstance instance
    )
    {
        if (instance?.Definition?.AttributeModifiers == null)
            return;

        StringName sourceType = TraitContentRules.ToAttributeSourceType(instance.SourceKind);
        if (sourceType == "" || instance.EffectiveInstanceKey == "")
            return;

        foreach (
            AttributeModifierDefinition baseModifier in instance.Definition.AttributeModifiers
        )
        {
            if (baseModifier == null)
                continue;

            result.Add(
                new AttributeModifierDefinition(
                    baseModifier.AttributeId,
                    baseModifier.Mode,
                    baseModifier.GetValueForRank(instance.Rank)
                        * Mathf.Max(instance.Stacks, 1),
                    0,
                    sourceType,
                    instance.EffectiveInstanceKey
                )
            );
        }
    }
}
