using Godot;

internal interface IBattleContingencyRuntimePort
{
    BattleState GetBattleState();

    BattleGridService GetGridService();

    SkillDefinition GetSkillDefinition(StringName skillId);

    UnitSkillGrantSourceType ResolveSourceSkillGrantSourceType(
        StringName memberId,
        StringName skillId
    );

    StringName AllocateSourceEventId(StringName prefix);

    bool ExecuteAutoCast(AutoCastRequest request, BattleEventBatch batch);

    void RefreshUnitOverlay(BattleUnitState unitState);
}
