using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class EquipmentRequirementDefinition
{
    public EquipmentRequirementDefinition(
        IReadOnlyList<string> requiredProfessionIds,
        int minBodySize,
        int maxBodySize,
        IReadOnlyList<EquipmentAttributeRequirementDefinition> attributeRequirements
    )
    {
        RequiredProfessionIds = FreezeValues(requiredProfessionIds, nameof(requiredProfessionIds));
        MinBodySize = minBodySize;
        MaxBodySize = maxBodySize;
        AttributeRequirements = FreezeValues(attributeRequirements, nameof(attributeRequirements));
    }

    public IReadOnlyList<string> RequiredProfessionIds { get; }
    public int MinBodySize { get; }
    public int MaxBodySize { get; }
    public IReadOnlyList<EquipmentAttributeRequirementDefinition> AttributeRequirements { get; }

    public EquipmentRequirementCheckResult CheckResult(PartyMemberState memberState)
    {
        var blockers = new List<string>();
        if (RequiredProfessionIds.Count > 0)
        {
            bool hasProfession = false;
            foreach (string rawId in RequiredProfessionIds)
            {
                StringName professionId = ProgressionDataUtils.to_string_name(rawId);
                if (memberState?.progression?.GetProfessionProgress(professionId) != null)
                {
                    hasProfession = true;
                    break;
                }
            }
            if (!hasProfession)
                blockers.Add("missing_profession");
        }

        if (MinBodySize > 0 && (memberState == null || memberState.body_size < MinBodySize))
            blockers.Add("body_size_too_small");
        if (MaxBodySize > 0 && (memberState == null || memberState.body_size > MaxBodySize))
            blockers.Add("body_size_too_large");

        foreach (EquipmentAttributeRequirementDefinition requirement in AttributeRequirements)
        {
            StringName attributeId = ProgressionDataUtils.to_string_name(requirement.AttributeId);
            if (attributeId == "" || requirement.MinValue <= 0)
                continue;
            int value =
                memberState
                    ?.progression
                    ?.unit_base_attributes
                    ?.GetAttributeValue(attributeId) ?? 0;
            if (value < requirement.MinValue)
                blockers.Add("attribute_too_low");
        }
        return new EquipmentRequirementCheckResult(blockers.Count == 0, blockers);
    }

    internal static EquipmentRequirementDefinition FromResource(EquipmentRequirement source)
    {
        if (source == null)
            return null;

        var attributeRequirements = new List<EquipmentAttributeRequirementDefinition>();
        string path = "equipment_requirement";
        int index = 0;
        foreach (
            EquipmentAttributeRequirementDef requirement in WarehouseDefinitionProjection.RequireCollection(
                source.AttributeRequirementsProjectionBorrowed,
                path + ".attribute_requirements"
            )
        )
        {
            string requirementPath = $"{path}.attribute_requirements[{index}]";
            if (requirement == null)
                throw WarehouseDefinitionProjection.Invalid(requirementPath, "resource is null");
            attributeRequirements.Add(
                EquipmentAttributeRequirementDefinition.FromResource(requirement)
            );
            index++;
        }

        return new EquipmentRequirementDefinition(
            new List<string>(
                WarehouseDefinitionProjection.RequireCollection(
                    source.RequiredProfessionIdsProjectionBorrowed,
                    path + ".required_profession_ids"
                )
            ),
            source.min_body_size,
            source.max_body_size,
            attributeRequirements
        );
    }

    internal static EquipmentRequirementDefinition CopyOf(EquipmentRequirementDefinition source)
    {
        if (source == null)
            return null;

        var attributes = new List<EquipmentAttributeRequirementDefinition>(
            source.AttributeRequirements.Count
        );
        foreach (EquipmentAttributeRequirementDefinition requirement in source.AttributeRequirements)
        {
            attributes.Add(
                new EquipmentAttributeRequirementDefinition(
                    requirement.AttributeId,
                    requirement.MinValue
                )
            );
        }
        return new EquipmentRequirementDefinition(
            source.RequiredProfessionIds,
            source.MinBodySize,
            source.MaxBodySize,
            attributes
        );
    }

    private static IReadOnlyList<T> FreezeValues<T>(IReadOnlyList<T> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var copied = new List<T>(values.Count);
        foreach (T value in values)
        {
            if (value is null)
                throw new ArgumentException("Definition lists must not contain null.", parameterName);
            copied.Add(value);
        }
        return new ReadOnlyCollection<T>(copied);
    }
}
