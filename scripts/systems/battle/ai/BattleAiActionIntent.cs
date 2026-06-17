using System.Collections.Generic;
using Godot;

internal enum BattleAiIntent
{
    Unknown,
    Offense,
    Control,
    Survival,
    Positioning,
    Escape,
    Wait,
}

internal static class BattleAiActionIntent
{
    private static readonly StringName IntentOffense = "offense";
    private static readonly StringName IntentControl = "control";
    private static readonly StringName IntentSurvival = "survival";
    private static readonly StringName IntentPositioning = "positioning";
    private static readonly StringName IntentEscape = "escape";
    private static readonly StringName IntentWait = "wait";

    internal static StringName Offense => IntentOffense;
    internal static StringName Control => IntentControl;
    internal static StringName Survival => IntentSurvival;
    internal static StringName Positioning => IntentPositioning;
    internal static StringName Escape => IntentEscape;
    internal static StringName Wait => IntentWait;

    internal static BattleAiIntent ToKind(StringName intent)
    {
        return intent.ToString() switch
        {
            "offense" => BattleAiIntent.Offense,
            "control" => BattleAiIntent.Control,
            "survival" => BattleAiIntent.Survival,
            "positioning" => BattleAiIntent.Positioning,
            "escape" => BattleAiIntent.Escape,
            "wait" => BattleAiIntent.Wait,
            _ => BattleAiIntent.Unknown,
        };
    }

    internal static StringName ToStringName(BattleAiIntent intent) =>
        intent switch
        {
            BattleAiIntent.Offense => IntentOffense,
            BattleAiIntent.Control => IntentControl,
            BattleAiIntent.Survival => IntentSurvival,
            BattleAiIntent.Positioning => IntentPositioning,
            BattleAiIntent.Escape => IntentEscape,
            BattleAiIntent.Wait => IntentWait,
            _ => "",
        };

    internal static bool IsValid(StringName intent) => ToKind(intent) != BattleAiIntent.Unknown;

    internal static StringName DefaultForActionKind(StringName actionKind)
    {
        return ProgressionDataUtils.to_string_name(actionKind).ToString() switch
        {
            "move" => IntentPositioning,
            "retreat" => IntentEscape,
            "wait" => IntentWait,
            "ground_reposition_skill" => IntentEscape,
            "skill" => IntentOffense,
            "unit_skill" => IntentOffense,
            "charge" => IntentOffense,
            _ => "",
        };
    }

    internal static StringName InferForSkill(
        SkillDef skillDef,
        IEnumerable<CombatEffectDef> effectDefs = null
    )
    {
        if (skillDef?.combat_profile is not CombatSkillDef combatProfile)
        {
            return IntentOffense;
        }

        bool hasControl = false;
        bool hasSurvival = BattleTargetTeamRules.IsBeneficialFilter(
            ProgressionDataUtils.to_string_name(combatProfile.target_team_filter)
        );

        foreach (CombatEffectDef effectDef in EnumerateRelevantEffects(combatProfile, effectDefs))
        {
            if (effectDef == null)
            {
                continue;
            }

            StringName effectFilter = ProgressionDataUtils.to_string_name(
                effectDef.effect_target_team_filter
            );
            bool beneficialEffect =
                BattleTargetTeamRules.IsBeneficialFilter(effectFilter)
                || (
                    effectFilter == ""
                    && BattleTargetTeamRules.IsBeneficialFilter(
                        ProgressionDataUtils.to_string_name(combatProfile.target_team_filter)
                    )
                );

            BattleEffectKind effectKind = effectDef.EffectKind;
            if (IsOffenseEffect(effectKind, beneficialEffect))
            {
                return IntentOffense;
            }
            if (IsControlEffect(effectKind, beneficialEffect))
            {
                hasControl = true;
                continue;
            }
            if (IsSurvivalEffect(effectKind, beneficialEffect))
            {
                hasSurvival = true;
            }
        }

        if (hasControl)
        {
            return IntentControl;
        }
        return hasSurvival ? IntentSurvival : IntentOffense;
    }

    internal static StringName DefaultFromSlotRole(StringName slotRole)
    {
        return EnemyAiGenerationSlotDef.ToSlotRole(slotRole) switch
        {
            EnemyAiGenerationSlotRole.Offense => IntentOffense,
            EnemyAiGenerationSlotRole.Control => IntentControl,
            EnemyAiGenerationSlotRole.Survival => IntentSurvival,
            EnemyAiGenerationSlotRole.Positioning => IntentPositioning,
            _ => "",
        };
    }

    private static IEnumerable<CombatEffectDef> EnumerateRelevantEffects(
        CombatSkillDef combatProfile,
        IEnumerable<CombatEffectDef> effectDefs
    )
    {
        bool yielded = false;
        if (effectDefs != null)
        {
            foreach (CombatEffectDef effectDef in effectDefs)
            {
                if (effectDef == null)
                {
                    continue;
                }
                yielded = true;
                yield return effectDef;
            }
        }
        if (yielded || combatProfile == null)
        {
            yield break;
        }
        foreach (CombatEffectDef effectDef in combatProfile.effect_defs)
        {
            if (effectDef != null)
            {
                yield return effectDef;
            }
        }
        foreach (Resource optionResource in combatProfile.cast_variants)
        {
            if (optionResource is not CombatCastVariantDef castVariant)
            {
                continue;
            }
            foreach (Resource effectResource in castVariant.effect_defs)
            {
                if (effectResource is CombatEffectDef effectDef)
                {
                    yield return effectDef;
                }
            }
        }
    }

    private static bool IsOffenseEffect(BattleEffectKind effectKind, bool beneficialEffect)
    {
        if (beneficialEffect)
        {
            return false;
        }
        return effectKind switch
        {
            BattleEffectKind.Damage
            or BattleEffectKind.ChainDamage
            or BattleEffectKind.Execute
            or BattleEffectKind.PathStepAoe
            or BattleEffectKind.RepeatAttackUntilFail
            or BattleEffectKind.Charge
            or BattleEffectKind.EquipmentDurabilityDamage => true,
            _ => false,
        };
    }

    private static bool IsControlEffect(BattleEffectKind effectKind, bool beneficialEffect)
    {
        if (beneficialEffect)
        {
            return false;
        }
        return effectKind switch
        {
            BattleEffectKind.ForcedMove
            or BattleEffectKind.Status
            or BattleEffectKind.ApplyStatus
            or BattleEffectKind.BodySizeCategoryOverride
            or BattleEffectKind.Terrain
            or BattleEffectKind.TerrainReplace
            or BattleEffectKind.TerrainReplaceTo
            or BattleEffectKind.Height
            or BattleEffectKind.HeightDelta
            or BattleEffectKind.TerrainEffect
            or BattleEffectKind.EdgeClear
            or BattleEffectKind.DispelMagic => true,
            _ => false,
        };
    }

    private static bool IsSurvivalEffect(BattleEffectKind effectKind, bool beneficialEffect)
    {
        if (beneficialEffect)
        {
            return true;
        }
        return effectKind switch
        {
            BattleEffectKind.Heal
            or BattleEffectKind.HealFatal
            or BattleEffectKind.StaminaRestore
            or BattleEffectKind.Shield
            or BattleEffectKind.LayeredBarrier
            or BattleEffectKind.CleanseHarmful
            or BattleEffectKind.EraseStatus => true,
            _ => false,
        };
    }
}
