using System;

public sealed class WeightedFacilityDefinition
{
    public WeightedFacilityDefinition(string facilityTemplateId, int weight)
    {
        FacilityTemplateId = facilityTemplateId
            ?? throw new ArgumentNullException(nameof(facilityTemplateId));
        Weight = weight;
    }

    public string FacilityTemplateId { get; }
    public int Weight { get; }

    internal static WeightedFacilityDefinition FromResource(
        WeightedFacilityEntry source,
        string path
    )
    {
        if (source == null)
            throw WorldDefinitionProjection.Invalid(path, "resource is null");
        return new WeightedFacilityDefinition(
            WorldDefinitionProjection.RequireString(
                source.facility_id,
                path + ".facility_id"
            ).Trim(),
            source.weight
        );
    }
}
