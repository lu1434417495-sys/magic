using System.Collections.Generic;
using Godot;

public sealed partial class GameRuntimeFacade : IBattleHudContext
{
    BattleState IBattleHudContext.GetBattleState() => _battle_runtime?.GetState();

    IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
        IBattleHudContext.GetEquipmentAbilityBindings() =>
        _battle_runtime?.GetEquipmentAbilityBindingIndexTyped();

    int IBattleHudContext.GetBattleWorldStep() =>
        _battle_runtime?.GetBattleWorldStep() ?? GetWorldStep();

    BattlePreview IBattleHudContext.PreviewBattleCommand(BattleCommand command) =>
        PreviewBattleCommand(command);

    IReadOnlyDictionary<StringName, ItemDefinition> IBattleHudContext.GetItemDefinitions() =>
        GetItemDefsTyped();

    IReadOnlyDictionary<StringName, SkillDefinition> IBattleHudContext.GetSkillDefinitions() =>
        GetSkillDefinitionsTyped();

    ISkillCatalog IBattleHudContext.GetSkillCatalog() => GetSkillCatalogTyped();

    PartyMemberState IBattleHudContext.GetPartyMemberState(StringName memberId) =>
        _party_state?.GetMemberState(memberId);

    AttributeSnapshot IBattleHudContext.GetMemberAttributeSnapshotForEquipmentView(
        StringName memberId,
        EquipmentState equipmentView
    ) =>
        _character_management?.GetMemberAttributeSnapshotForEquipmentView(
            memberId,
            equipmentView
        );

    string IBattleHudContext.GetBattleSkillCastBlockMessage(
        BattleUnitState activeUnit,
        StringName skillId
    ) => GetBattleSkillCastBlockMessage(activeUnit, skillId);

    GearSetEvaluationSnapshot IBattleHudContext.EvaluateUnitGearSets(BattleUnitState unit) =>
        unit == null || unit.source_member_id == ""
            ? GearSetEvaluationSnapshot.Empty
            : _character_management?.EvaluateGearSets(
                unit.source_member_id,
                unit.GetEquipmentView()
            ) ?? GearSetEvaluationSnapshot.Empty;

    IReadOnlyDictionary<StringName, TraitDefinition> IBattleHudContext.GetTraitDefinitions() =>
        GetContentCatalogTyped()?.GetTraitDefsTyped()
        ?? new Dictionary<StringName, TraitDefinition>();
}
