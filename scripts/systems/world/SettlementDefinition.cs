using System;
using System.Collections.Generic;
using Godot;

public enum SettlementTierKind
{
    Unknown = -1,
    Village = 0,
    Town = 1,
    City = 2,
    Capital = 3,
    WorldStronghold = 4,
    Metropolis = 5,
}

public sealed class SettlementDefinition
{
    public SettlementDefinition(
        string templateId,
        string displayName,
        int tier,
        IReadOnlyList<FacilitySlotDefinition> facilitySlots,
        IReadOnlyList<string> guaranteedFacilityIds,
        IReadOnlyList<WeightedFacilityDefinition> optionalFacilityPool,
        int maxOptionalFacilities
    )
    {
        TemplateId = templateId ?? throw new ArgumentNullException(nameof(templateId));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Tier = tier;
        FacilitySlots = WorldDefinitionProjection.FreezeValues(
            facilitySlots,
            nameof(facilitySlots)
        );
        GuaranteedFacilityIds = WorldDefinitionProjection.FreezeValues(
            guaranteedFacilityIds,
            nameof(guaranteedFacilityIds)
        );
        OptionalFacilityPool = WorldDefinitionProjection.FreezeValues(
            optionalFacilityPool,
            nameof(optionalFacilityPool)
        );
        MaxOptionalFacilities = maxOptionalFacilities;
    }

    public string TemplateId { get; }
    public string DisplayName { get; }
    public int Tier { get; }
    public SettlementTierKind TierKind => ToTierKind(Tier);
    public IReadOnlyList<FacilitySlotDefinition> FacilitySlots { get; }
    public IReadOnlyList<string> GuaranteedFacilityIds { get; }
    public IReadOnlyList<WeightedFacilityDefinition> OptionalFacilityPool { get; }
    public int MaxOptionalFacilities { get; }
    public Vector2I FootprintSize => GetFootprintSize();
    public string TierName => GetTierName();

    public Vector2I GetFootprintSize() =>
        Tier switch
        {
            (int)SettlementTierKind.Village => Vector2I.One,
            (int)SettlementTierKind.Town => new Vector2I(2, 2),
            (int)SettlementTierKind.City => new Vector2I(2, 2),
            (int)SettlementTierKind.Capital => new Vector2I(3, 3),
            (int)SettlementTierKind.WorldStronghold => new Vector2I(4, 4),
            (int)SettlementTierKind.Metropolis => new Vector2I(5, 5),
            _ => Vector2I.One,
        };

    public string GetTierName() =>
        Tier switch
        {
            (int)SettlementTierKind.Village => "村",
            (int)SettlementTierKind.Town => "镇",
            (int)SettlementTierKind.City => "城市",
            (int)SettlementTierKind.Capital => "主城",
            (int)SettlementTierKind.WorldStronghold => "世界据点",
            (int)SettlementTierKind.Metropolis => "都会",
            _ => "未知",
        };

    internal static SettlementDefinition FromResource(SettlementConfig source, string path)
    {
        if (source == null)
            throw WorldDefinitionProjection.Invalid(path, "resource is null");
        return new SettlementDefinition(
            WorldDefinitionProjection.RequireString(
                source.settlement_id,
                path + ".settlement_id"
            ).Trim(),
            WorldDefinitionProjection.RequireString(
                source.display_name,
                path + ".display_name"
            ),
            source.tier,
            WorldDefinitionProjection.ProjectResources<
                FacilitySlotConfig,
                FacilitySlotDefinition
            >(
                source.FacilitySlotsProjectionBorrowed,
                path + ".facility_slots",
                FacilitySlotDefinition.FromResource
            ),
            WorldDefinitionProjection.CopyStrings(
                source.GuaranteedFacilityIdsProjectionBorrowed,
                path + ".guaranteed_facility_ids",
                trim: true
            ),
            WorldDefinitionProjection.ProjectResources<
                WeightedFacilityEntry,
                WeightedFacilityDefinition
            >(
                source.OptionalFacilityPoolProjectionBorrowed,
                path + ".optional_facility_pool",
                WeightedFacilityDefinition.FromResource
            ),
            source.max_optional_facilities
        );
    }

    internal static SettlementTierKind ToTierKind(int tier) =>
        tier >= (int)SettlementTierKind.Village && tier <= (int)SettlementTierKind.Metropolis
            ? (SettlementTierKind)tier
            : SettlementTierKind.Unknown;
}
