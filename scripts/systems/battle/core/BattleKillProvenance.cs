using Godot;

internal readonly struct BattleKillProvenance
{
    internal BattleKillProvenance(
        bool isAttack,
        bool includesWeaponDamage,
        StringName sourceEquipmentInstanceId,
        StringName sourceBindingId,
        StringName sourceActionId
    )
    {
        IsAttack = isAttack;
        IncludesWeaponDamage = includesWeaponDamage;
        SourceEquipmentInstanceId = ProgressionDataUtils.to_string_name(sourceEquipmentInstanceId);
        SourceBindingId = ProgressionDataUtils.to_string_name(sourceBindingId);
        SourceActionId = ProgressionDataUtils.to_string_name(sourceActionId);
    }

    internal bool IsAttack { get; }
    internal bool IncludesWeaponDamage { get; }
    internal StringName SourceEquipmentInstanceId { get; }
    internal StringName SourceBindingId { get; }
    internal StringName SourceActionId { get; }

    internal static BattleKillProvenance None => new(false, false, "", "", "");

    internal static BattleKillProvenance ForEquipmentAttack(
        StringName sourceEquipmentInstanceId,
        StringName sourceBindingId,
        StringName sourceActionId
    ) => new(
        true,
        true,
        sourceEquipmentInstanceId,
        sourceBindingId,
        sourceActionId
    );
}
