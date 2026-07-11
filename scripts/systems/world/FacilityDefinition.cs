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

    internal static FacilityDefinition FromResource(FacilityConfig source, string path)
    {
        if (source == null)
            throw WorldDefinitionProjection.Invalid(path, "resource is null");
        return new FacilityDefinition(
            WorldDefinitionProjection.RequireString(
                source.facility_id,
                path + ".facility_id"
            ).Trim(),
            WorldDefinitionProjection.RequireString(
                source.display_name,
                path + ".display_name"
            ),
            WorldDefinitionProjection.RequireString(source.category, path + ".category"),
            source.min_settlement_tier,
            WorldDefinitionProjection.CopyStrings(
                source.AllowedSlotTagsProjectionBorrowed,
                path + ".allowed_slot_tags"
            ),
            WorldDefinitionProjection.ProjectResources<
                FacilityNpcConfig,
                FacilityNpcDefinition
            >(
                source.BoundServiceNpcsProjectionBorrowed,
                path + ".bound_service_npcs",
                FacilityNpcDefinition.FromResource
            ),
            WorldDefinitionProjection.RequireString(
                source.interaction_type,
                path + ".interaction_type"
            )
        );
    }
}
