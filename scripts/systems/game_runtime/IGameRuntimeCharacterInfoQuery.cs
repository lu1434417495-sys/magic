using Godot;
using GDictionary = Godot.Collections.Dictionary;

// Narrow read-only capability used by the character-info projection builder.
// Definitions are borrowed from the current content snapshot; callers must not retain them
// across runtime rebinds. Identity summaries are detached projection payloads.
internal interface IGameRuntimeCharacterInfoQuery
{
    string FormatCoord(Vector2I coord);

    string GetSkillDisplayName(StringName skillId);

    bool HasPartyMember(StringName memberId);

    bool TryGetItemDefinition(StringName itemId, out ItemDefinition itemDefinition);

    bool TryGetTraitDefinition(StringName traitId, out TraitDefinition traitDefinition);

    GearSetEvaluationSnapshot EvaluateGearSets(
        StringName memberId,
        EquipmentState equipmentStateOverride
    ) => GearSetEvaluationSnapshot.Empty;

    // Granted-action visibility for the gear-set summary (remaining uses and disabled
    // reason). When an override view is given it must be the same view the evaluation
    // ran on, e.g. the battle-local unit equipment view during battle.
    System.Collections.Generic.IReadOnlyList<GearSetGrantedActionSummary> BuildGearSetGrantedActionSummaries(
        StringName memberId,
        EquipmentState equipmentStateOverride,
        GearSetEvaluationSnapshot evaluation
    ) => System.Array.Empty<GearSetGrantedActionSummary>();

    GDictionary GetIdentitySummary(StringName memberId);
}
