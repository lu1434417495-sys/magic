using System.Collections.Generic;

internal sealed class EquipmentAbilityTriggerSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values => EquipmentAbilityClosedVocabulary.TriggerValues;
}

internal sealed class EquipmentAbilityTimingSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values => EquipmentAbilityClosedVocabulary.TimingValues;
}

internal sealed class EquipmentAbilityConditionGroupModeSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values => EquipmentAbilityClosedVocabulary.ConditionGroupModeValues;
}

internal sealed class EquipmentAbilityFactQueryKindSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values => EquipmentAbilityClosedVocabulary.FactQueryKindValues;
}

internal sealed class EquipmentAbilityFactIdSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values => EquipmentAbilityClosedVocabulary.FactIdValues;
}

internal sealed class EquipmentAbilityFactSubjectSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values => EquipmentAbilityClosedVocabulary.FactSubjectValues;
}

internal sealed class EquipmentAbilityFactAggregationSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values => EquipmentAbilityClosedVocabulary.FactAggregationValues;
}

internal sealed class EquipmentAbilityFactValueKindSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values => EquipmentAbilityClosedVocabulary.FactValueKindValues;
}
