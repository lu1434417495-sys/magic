using System.Collections.Generic;
using Godot;

// Read-only/preview capabilities needed to build HUD snapshots. Presentation code
// does not borrow the runtime/session composition roots.
internal interface IBattleHudContext
{
    BattleState GetBattleState();
    IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
        GetEquipmentAbilityBindings();
    int GetBattleWorldStep();
    BattlePreview PreviewBattleCommand(BattleCommand command);
    IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefinitions();
    IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitions();
    ISkillCatalog GetSkillCatalog();
    PartyMemberState GetPartyMemberState(StringName memberId);
    AttributeSnapshot GetMemberAttributeSnapshotForEquipmentView(
        StringName memberId,
        EquipmentState equipmentView
    );
    string GetBattleSkillCastBlockMessage(BattleUnitState activeUnit, StringName skillId);
}

internal sealed class BattleHudSessionContext : IBattleHudContext
{
    private readonly GameSession _session;

    internal BattleHudSessionContext(GameSession session)
    {
        _session = session;
    }

    public BattleState GetBattleState() => null;

    public IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
        GetEquipmentAbilityBindings() => null;

    public int GetBattleWorldStep() => -1;

    public BattlePreview PreviewBattleCommand(BattleCommand command) => null;

    public IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefinitions() =>
        _session?.GetItemDefsTyped()
        ?? new Dictionary<StringName, ItemDefinition>();

    public IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitions() =>
        _session?.GetContentCatalogTyped()?.GetSkillDefinitionsTyped()
        ?? new Dictionary<StringName, SkillDefinition>();

    public ISkillCatalog GetSkillCatalog() =>
        _session?.GetContentCatalogTyped()?.GetSkillCatalogTyped();

    public PartyMemberState GetPartyMemberState(StringName memberId) =>
        _session?.GetPartyMemberState(memberId);

    public AttributeSnapshot GetMemberAttributeSnapshotForEquipmentView(
        StringName memberId,
        EquipmentState equipmentView
    ) => null;

    public string GetBattleSkillCastBlockMessage(
        BattleUnitState activeUnit,
        StringName skillId
    ) => "";
}
