using System;
using System.Collections.Generic;
using System.Globalization;

public sealed class FacilityDefinition
{
    public FacilityDefinition(
        string templateId,
        string displayName,
        string category,
        int minSettlementTier,
        IReadOnlyList<string> allowedSlotTags,
        IReadOnlyList<FacilityNpcDefinition> boundServiceNpcs,
        string interactionType
    )
    {
        TemplateId = templateId ?? throw new ArgumentNullException(nameof(templateId));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Category = category ?? throw new ArgumentNullException(nameof(category));
        MinSettlementTier = minSettlementTier;
        AllowedSlotTags = WorldDefinitionProjection.FreezeValues(
            allowedSlotTags,
            nameof(allowedSlotTags)
        );
        BoundServiceNpcs = WorldDefinitionProjection.FreezeValues(
            boundServiceNpcs,
            nameof(boundServiceNpcs)
        );
        InteractionType = interactionType
            ?? throw new ArgumentNullException(nameof(interactionType));
    }

    public string TemplateId { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public int MinSettlementTier { get; }
    public IReadOnlyList<string> AllowedSlotTags { get; }
    public IReadOnlyList<FacilityNpcDefinition> BoundServiceNpcs { get; }
    public string InteractionType { get; }

    public string GetPrimaryServiceName()
    {
        if (BoundServiceNpcs.Count == 0)
            return Capitalize(InteractionType);
        return Capitalize(BoundServiceNpcs[0].ServiceType);
    }

    private static string Capitalize(string value) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
            (value ?? string.Empty).Replace('_', ' ')
        );

}
