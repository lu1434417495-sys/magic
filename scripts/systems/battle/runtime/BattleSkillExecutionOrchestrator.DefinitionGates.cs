using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// Partial slice of BattleSkillExecutionOrchestrator — skill-definition predicate gates (repeat/chain/applicability checks).
// Pure physical split: same class, no behavior change. See BattleSkillExecutionOrchestrator.cs.
internal sealed partial class BattleSkillExecutionOrchestrator
{

    internal static int GetUnitMaxHp(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return 0;
        }
        int snapshotMaxHp =
            unitState.attribute_snapshot?.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax)) ?? 0;
        return Math.Max(snapshotMaxHp, unitState.GetCurrentHp());
    }

    private static bool HasRepeatAttackEffectDefinition(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    ) => BattleUnitSkillDefinitionExecutionRules.HasRepeatAttackEffect(effectDefinitions);

    private static bool CanApplyUnitSkillResultFromDefinitions(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    ) => BattleUnitSkillDefinitionExecutionRules.CanApplyUnitSkillResult(effectDefinitions);

    private static bool CanApplyUnitSkillOrRepeatResultFromDefinitions(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        return BattleUnitSkillDefinitionExecutionRules.CanApplyUnitSkillOrRepeatResult(
            effectDefinitions
        );
    }

    private static bool CanHandleUnitSkillCommandFromDefinitions(
        SkillDefinition skillDefinition,
        BattleSkillResolutionPolicy policy
    )
    {
        return skillDefinition?.CombatProfile != null
            && skillDefinition.CombatProfile.SpecialResolutionProfileId != new StringName("meteor_swarm")
            && policy?.RoutesToUnitTargeting == true
            && CanApplyUnitSkillOrRepeatResultFromDefinitions(policy.EffectDefinitions);
    }

    private bool CanApplyPendingUnitCastFromDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleUnitState activeUnit
    )
    {
        return skillDefinition?.CombatProfile != null
            && skillDefinition.CombatProfile.SpecialResolutionProfileId != new StringName("meteor_swarm")
            && !IsRandomChainSkill(skillDefinition)
            && CanApplyUnitSkillOrRepeatResultFromDefinitions(
                CollectUnitSkillEffectDefinitions(skillDefinition, castVariantDefinition, activeUnit)
            );
    }

    private static bool IsRandomChainSkill(SkillDefinition skillDefinition)
    {
        return skillDefinition?.CombatProfile?.TargetSelectionModeKind
            == BattleTargetSelectionMode.RandomChain;
    }

    internal AttackPreviewData _build_unit_skill_hit_preview(
        BattleUnitReadView active_unit,
        IReadOnlyList<BattleUnitReadView> target_units,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        if (
            !active_unit.IsValid
            || skillDefinition == null
            || target_units == null
            || target_units.Count != 1
        )
        {
            return null;
        }
        BattleUnitReadView targetUnit = target_units[0];
        if (!targetUnit.IsValid)
        {
            return null;
        }
        List<CombatEffectDefinition> effectDefinitions = CollectUnitSkillEffectDefinitions(
            skillDefinition,
            castVariantDefinition,
            active_unit
        );
        BattleRepeatAttackResolver repeatAttackResolver = Runtime?._repeat_attack_resolver;
        CombatEffectDefinition repeatAttackEffect =
            repeatAttackResolver?.get_repeat_attack_effect_def(effectDefinitions);
        BattleAttackCheckPolicyService attackPolicy = Runtime?.GetAttackCheckPolicyService();
        BattleSkillResolutionRules skillResolutionRules = Runtime?._skill_resolution_rules;
        if (attackPolicy == null || skillResolutionRules == null)
        {
            return null;
        }
        if (repeatAttackEffect == null)
        {
            if (
                !skillResolutionRules.ShouldResolveUnitSkillAsFateAttack(
                    active_unit,
                    targetUnit,
                    skillDefinition,
                    effectDefinitions
                )
            )
            {
                return null;
            }
            BattleAttackCheckPolicyContext attackContext = attackPolicy.BuildSkillDefinitionAttackContext(
                Runtime?._state,
                active_unit,
                targetUnit,
                skillDefinition,
                new StringName("skill_attack_preview"),
                new StringName("hud_preview"),
                skillResolutionRules.IsForceHitNoCritSkill(skillDefinition, active_unit)
            );
            return attackPolicy.BuildAttackPreview(attackContext);
        }
        List<BattleRepeatAttackStageSpec> stageSpecs =
            BattleRepeatAttackResolver.BuildStageSpecsFromRepeatAttackEffect(
            active_unit,
            skillDefinition,
            repeatAttackEffect,
            -1,
            true
        );
        BattleAttackCheckPolicyContext repeatContext = attackPolicy.BuildRepeatAttackStageContext(
            Runtime?._state,
            active_unit,
            targetUnit,
            skillDefinition,
            default,
            new StringName("repeat_attack_preview"),
            new StringName("hud_preview")
        );
        return attackPolicy.BuildRepeatAttackPreview(repeatContext, stageSpecs);
    }

    internal void _append_damage_preview_line(BattlePreview preview)
    {
        if (preview == null || preview.DamagePreviewTyped == null)
        {
            return;
        }
        string damagePreviewText = preview.DamagePreviewTyped.Value.SummaryText;
        if (string.IsNullOrEmpty(damagePreviewText))
        {
            return;
        }
        preview.AddLogLine(damagePreviewText);
    }
}
