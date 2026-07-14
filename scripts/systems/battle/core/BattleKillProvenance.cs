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

    internal static BattleKillProvenance FromWeaponAttackResult(
        BattleUnitState sourceUnit,
        AttackEffectResolutionResult result,
        StringName sourceActionId
    )
    {
        return FromWeaponAttackResult(
            sourceUnit,
            result,
            ForEquipmentAttack("", "", sourceActionId)
        );
    }

    internal static BattleKillProvenance FromWeaponAttackResult(
        BattleUnitState sourceUnit,
        AttackEffectResolutionResult result,
        BattleKillProvenance fallback
    )
    {
        if (sourceUnit == null || !IncludesWeaponDamageResult(result))
            return None;
        AttackCheckInput attackCheck = result.AttackCheck;
        bool forcedCriticalApplied = result.CriticalHit && attackCheck.ForceCriticalOnHit;
        StringName equipmentInstanceId = forcedCriticalApplied
            ? attackCheck.ForcedCriticalSourceEquipmentInstanceId
            : fallback.SourceEquipmentInstanceId;
        if (equipmentInstanceId == "")
        {
            equipmentInstanceId =
                sourceUnit.GetEquipmentView()?.GetEquippedInstanceId("main_hand")
                ?? new StringName("");
        }
        return ForEquipmentAttack(
            equipmentInstanceId,
            forcedCriticalApplied
                ? attackCheck.ForcedCriticalSourceBindingId
                : fallback.SourceBindingId,
            forcedCriticalApplied && attackCheck.ForcedCriticalSourceActionId != ""
                ? attackCheck.ForcedCriticalSourceActionId
                : fallback.SourceActionId
        );
    }

    private static bool IncludesWeaponDamageResult(AttackEffectResolutionResult result)
    {
        foreach (DamageEventResult damageEvent in result.DamageEvents ?? System.Array.Empty<DamageEventResult>())
        {
            if (
                damageEvent.AddWeaponDice
                && damageEvent.WeaponDamageDice.Count > 0
                && damageEvent.WeaponDamageDice.Sides > 0
            )
            {
                return true;
            }
        }
        return false;
    }
}
