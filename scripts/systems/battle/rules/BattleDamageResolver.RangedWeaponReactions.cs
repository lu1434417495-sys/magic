using System;
using System.Collections.Generic;
using Godot;

public partial class BattleDamageResolver
{
    private IBattleRangedWeaponAttackReactionSink _rangedWeaponAttackReactionSink;

    internal void SetRangedWeaponAttackReactionSink(
        IBattleRangedWeaponAttackReactionSink sink
    )
    {
        _rangedWeaponAttackReactionSink = sink;
    }

    private static BattleRangedWeaponAttackSnapshot CaptureRangedWeaponAttackSnapshot(
        BattleUnitState sourceUnit,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        if (sourceUnit == null)
            return BattleRangedWeaponAttackSnapshot.Empty;

        CombatEffectDefinition mainWeaponDamage = null;
        foreach (
            CombatEffectDefinition effectDefinition in
                effectDefinitions ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition?.EffectKind == BattleEffectKind.Damage
                && effectDefinition.AddWeaponDice
            )
            {
                mainWeaponDamage = effectDefinition;
                break;
            }
        }
        if (mainWeaponDamage == null)
            return BattleRangedWeaponAttackSnapshot.Empty;

        BattleWeaponProjectionValues weapon =
            sourceUnit.GetWeaponProjectionReadViewTyped().Values;
        BattleWeaponDiceValues dice = weapon.ActiveDice;
        if (!dice.HasUsableDice)
            return BattleRangedWeaponAttackSnapshot.Empty;

        return new BattleRangedWeaponAttackSnapshot(
            weapon.Family,
            weapon.RangeType,
            Math.Max(dice.DiceCount, 0),
            Math.Max(dice.DiceSides, 0),
            Math.Max(mainWeaponDamage.WeaponDiceMultiplier, 1)
        );
    }

    private void ResolveRangedWeaponAttackReaction(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleRangedWeaponAttackSnapshot weaponSnapshot,
        AttackResolutionMetadata attackMetadata,
        AttackContext attackContext
    )
    {
        if (!weaponSnapshot.IsUsable || _rangedWeaponAttackReactionSink == null)
            return;
        _rangedWeaponAttackReactionSink.ResolveRangedWeaponAttackReaction(
            new BattleRangedWeaponAttackReactionContext
            {
                Attacker = sourceUnit,
                Defender = targetUnit,
                BattleState = attackContext?.BattleState,
                TriggeringSkillId = attackMetadata?.SkillId ?? new StringName(""),
                TriggeringAttackSucceeded = attackMetadata?.AttackSuccess == true,
                WeaponSnapshot = weaponSnapshot,
                Batch = attackContext?.EventBatch,
            }
        );
    }
}
