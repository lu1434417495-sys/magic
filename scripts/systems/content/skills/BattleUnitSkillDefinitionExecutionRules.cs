using System;
using System.Collections.Generic;

/// <summary>
/// Shared definition-level capability rules for the formal unit-skill execution path.
/// Content validation and battle runtime both depend on this lower-layer predicate so
/// their accepted effect sets cannot drift apart.
/// </summary>
internal static class BattleUnitSkillDefinitionExecutionRules
{
    internal static bool HasRepeatAttackEffect(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition?.EffectKind
                is BattleEffectKind.RepeatAttackUntilFail
                    or BattleEffectKind.FixedRepeatAttack
            )
                return true;
        }
        return false;
    }

    internal static bool CanApplyUnitSkillResult(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition == null)
                continue;
            if (!IsUnitExecutionEffect(effectDefinition.EffectKind))
                return false;
        }
        return true;
    }

    internal static bool CanApplyUnitSkillOrRepeatResult(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    ) =>
        HasRepeatAttackEffect(effectDefinitions)
        || CanApplyUnitSkillResult(effectDefinitions);

    private static bool IsUnitExecutionEffect(BattleEffectKind effectKind) =>
        effectKind
            is BattleEffectKind.Damage
                or BattleEffectKind.EquipmentDurabilityDamage
                or BattleEffectKind.DispelMagic
                or BattleEffectKind.Heal
                or BattleEffectKind.HealFatal
                or BattleEffectKind.StaminaRestore
                or BattleEffectKind.Status
                or BattleEffectKind.ApplyStatus
                or BattleEffectKind.EraseStatus
                or BattleEffectKind.CleanseHarmful
                or BattleEffectKind.Execute
                or BattleEffectKind.GradedSaveExecute
                or BattleEffectKind.Shield
                or BattleEffectKind.LayeredBarrier
                or BattleEffectKind.BodySizeCategoryOverride
                or BattleEffectKind.ForcedMove
                or BattleEffectKind.SourceRetreat
                or BattleEffectKind.VaultBehindTarget
                or BattleEffectKind.ChainDamage
                or BattleEffectKind.OnKillGainResources;
}
