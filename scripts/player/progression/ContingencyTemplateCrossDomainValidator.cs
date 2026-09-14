using System;
using System.Collections.Generic;
using Godot;

internal static class ContingencyTemplateCrossDomainValidator
{
    internal static IReadOnlyList<string> Validate(
        IReadOnlyDictionary<StringName, ContingencySetupTemplateDefinition> templates,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(itemDefinitions);

        var orderedTemplates = new List<
            KeyValuePair<StringName, ContingencySetupTemplateDefinition>
        >(templates);
        orderedTemplates.Sort(
            static (left, right) => StringComparer.Ordinal.Compare(
                left.Key.ToString(),
                right.Key.ToString()
            )
        );

        var errors = new List<string>();
        foreach (
            KeyValuePair<StringName, ContingencySetupTemplateDefinition> entry in orderedTemplates
        )
        {
            ContingencySetupTemplateDefinition template = entry.Value;
            StringName templateId = template != null && template.TemplateId != ""
                ? template.TemplateId
                : entry.Key;
            if (template == null)
            {
                errors.Add($"Contingency template {templateId} definition is missing.");
                continue;
            }

            IReadOnlyList<ContingencyMaterialCostDefinition> costs =
                template.ChargeMaterialCosts;
            if (costs == null)
            {
                errors.Add(
                    $"Contingency template {templateId} charge_material_costs definition is missing."
                );
                continue;
            }
            for (int index = 0; index < costs.Count; index++)
            {
                ContingencyMaterialCostDefinition cost = costs[index];
                StringName itemId = cost?.ItemId ?? default;
                if (
                    cost != null
                    && itemId != ""
                    && itemDefinitions.TryGetValue(itemId, out ItemDefinition itemDefinition)
                    && itemDefinition != null
                )
                {
                    continue;
                }
                errors.Add(
                    $"Contingency template {templateId} charge_material_costs[{index}].item_id references missing item {itemId}."
                );
            }
        }
        return errors;
    }
}
