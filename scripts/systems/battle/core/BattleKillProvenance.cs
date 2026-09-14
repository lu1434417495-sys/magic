using System;
using Godot;

internal readonly struct BattleKillProvenance
{
    private BattleKillProvenance(
        BattleWeaponAttackOutcomeKind weaponAttackOutcomeKind,
        bool isAttack,
        bool includesWeaponDamage,
        StringName sourceEquipmentInstanceId,
        StringName sourceBindingId,
        StringName sourceActionId
    )
    {
        WeaponAttackOutcomeKind = weaponAttackOutcomeKind;
        IsAttack = isAttack;
        IncludesWeaponDamage = includesWeaponDamage;
        SourceEquipmentInstanceId =
            ProgressionDataUtils.to_string_name(sourceEquipmentInstanceId);
        SourceBindingId =
            ProgressionDataUtils.to_string_name(sourceBindingId);
        SourceActionId =
            ProgressionDataUtils.to_string_name(sourceActionId);
    }

    internal BattleWeaponAttackOutcomeKind WeaponAttackOutcomeKind { get; }
    internal bool IsAttack { get; }
    internal bool IncludesWeaponDamage { get; }
    internal StringName SourceEquipmentInstanceId { get; }
    internal StringName SourceBindingId { get; }
    internal StringName SourceActionId { get; }

    internal static BattleKillProvenance None =>
        new(
            BattleWeaponAttackOutcomeKind.Unknown,
            false,
            false,
            "",
            "",
            ""
        );

    internal static BattleKillProvenance ForWeaponAttack(
        BattleWeaponAttackOutcomeKind outcomeKind,
        StringName sourceEquipmentInstanceId,
        StringName sourceBindingId,
        StringName sourceActionId
    )
    {
        RequireConcreteOutcomeKind(outcomeKind);
        if (sourceActionId == new StringName(""))
            throw new ArgumentException("source action id is required");
        return new BattleKillProvenance(
            outcomeKind,
            true,
            true,
            sourceEquipmentInstanceId,
            sourceBindingId,
            sourceActionId
        );
    }

    internal static BattleKillProvenance FromWeaponAttackResult(
        BattleUnitState sourceUnit,
        AttackEffectResolutionResult result,
        BattleWeaponAttackOutcomeKind outcomeKind,
        StringName sourceActionId
    )
    {
        RequireConcreteOutcomeKind(outcomeKind);
        if (sourceUnit == null || !IncludesWeaponDamageResult(result))
            return None;
        if (sourceActionId == new StringName(""))
        {
            throw new ArgumentException(
                "source action id is required for a weapon-damage result",
                nameof(sourceActionId)
            );
        }
        return FromWeaponAttackResult(
            sourceUnit,
            result,
            outcomeKind,
            ForWeaponAttack(outcomeKind, "", "", sourceActionId)
        );
    }

    internal static BattleKillProvenance FromWeaponAttackResult(
        BattleUnitState sourceUnit,
        AttackEffectResolutionResult result,
        BattleWeaponAttackOutcomeKind outcomeKind,
        BattleKillProvenance fallback
    )
    {
        RequireConcreteOutcomeKind(outcomeKind);
        if (
            fallback.WeaponAttackOutcomeKind
                != BattleWeaponAttackOutcomeKind.Unknown
            && fallback.WeaponAttackOutcomeKind != outcomeKind
        )
        {
            throw new ArgumentException(
                "fallback outcome kind does not match",
                nameof(fallback)
            );
        }
        if (sourceUnit == null || !IncludesWeaponDamageResult(result))
            return None;

        AttackCheckInput attackCheck = result.AttackCheck;
        bool forcedCriticalApplied =
            result.CriticalHit && attackCheck.ForceCriticalOnHit;
        StringName equipmentInstanceId =
            forcedCriticalApplied
                ? attackCheck.ForcedCriticalSourceEquipmentInstanceId
                : fallback.SourceEquipmentInstanceId;
        if (equipmentInstanceId == new StringName(""))
        {
            equipmentInstanceId =
                sourceUnit
                    .GetEquipmentView()
                    ?.GetEquippedInstanceId("main_hand")
                ?? new StringName("");
        }
        StringName bindingId =
            forcedCriticalApplied
                ? attackCheck.ForcedCriticalSourceBindingId
                : fallback.SourceBindingId;
        StringName actionId =
            forcedCriticalApplied
            && attackCheck.ForcedCriticalSourceActionId
                != new StringName("")
                ? attackCheck.ForcedCriticalSourceActionId
                : fallback.SourceActionId;
        return ForWeaponAttack(
            outcomeKind,
            equipmentInstanceId,
            bindingId,
            actionId
        );
    }

    private static bool IncludesWeaponDamageResult(
        AttackEffectResolutionResult result
    )
    {
        foreach (
            DamageEventResult damageEvent
                in result.DamageEvents
                    ?? Array.Empty<DamageEventResult>()
        )
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

    private static void RequireConcreteOutcomeKind(
        BattleWeaponAttackOutcomeKind outcomeKind
    )
    {
        if (
            outcomeKind == BattleWeaponAttackOutcomeKind.Unknown
            || !Enum.IsDefined(outcomeKind)
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcomeKind),
                outcomeKind,
                null
            );
        }
    }
}
