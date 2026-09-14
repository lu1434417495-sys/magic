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
}
