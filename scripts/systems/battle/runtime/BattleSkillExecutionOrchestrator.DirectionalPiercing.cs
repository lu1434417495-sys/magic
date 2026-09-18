using static BattleSkillTargetPlanRules;
using System;
using System.Collections.Generic;
using Godot;

internal sealed partial class BattleSkillExecutionOrchestrator
{

    private bool _handle_directional_piercing_skill_command(
        BattleUnitState activeUnit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleEventBatch batch
    )
    {
        BattleGroundSkillValidationResult validation =
            Runtime?.ValidateGroundSkillCommandResultTyped(
                activeUnit,
                skillDefinition,
                castVariantDefinition,
                command
            ) ?? BattleGroundSkillValidationResult.Denied("地面技能目标无效。" );
        if (!validation.Allowed || validation.TargetCoords.Count != 1)
        {
            batch?.AddLogLine(
                string.IsNullOrEmpty(validation.Message)
                    ? "贯穿方向无效。"
                    : validation.Message
            );
            return false;
        }

        BattleDirectionalPiercingPlan plan = BattleDirectionalPiercingRules.BuildPlan(
            Runtime?._state,
            Runtime?.GetGridService(),
            activeUnit,
            skillDefinition,
            validation.TargetCoords[0],
            BattleRangeService.GetEffectiveSkillRange(activeUnit, skillDefinition)
        );
        if (!plan.Allowed)
        {
            batch?.AddLogLine(plan.Message);
            return false;
        }
        if (!_consume_skill_costs(activeUnit, skillDefinition, castVariantDefinition, batch))
            return false;

        CombatSkillResourceCosts costs = _get_effective_skill_resource_costs(
            activeUnit,
            skillDefinition
        );
        _record_action_issued(
            activeUnit,
            BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            costs.ApCost
        );
        _append_changed_unit_id(batch, activeUnit.unit_id);
        if (ResolveSpellReactionsAfterCost(activeUnit, skillDefinition, batch).Interrupted)
            return false;

        IReadOnlyList<CombatEffectDefinition> baseEffects =
            Runtime?.CollectGroundUnitEffectDefinitionsTyped(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            ) ?? Array.Empty<CombatEffectDefinition>();
        int skillLevel = _get_unit_skill_level(activeUnit, skillDefinition.SkillId);
        CombatDirectionalPiercingDefinition profile =
            skillDefinition.CombatProfile.DirectionalPiercing;
        int baseDamagePercent = profile.GetBaseDamagePercent(skillLevel);
        using BattleLogicalAttackScope logicalAttack =
            BeginLogicalAttackForEffects(activeUnit, baseEffects);
        try
        {
            int successfulHitCount = 0;

            foreach (BattleUnitState targetUnit in plan.Targets)
            {
                if (targetUnit == null || !targetUnit.IsAlive())
                    continue;
                int decayPercent = BattleDirectionalPiercingRules
                    .GetDamagePercentAfterSuccessfulHits(profile, successfulHitCount);
                double combinedMultiplier = baseDamagePercent / 100.0 * decayPercent / 100.0;
                IReadOnlyList<CombatEffectDefinition> targetEffects =
                    BattleDirectionalPiercingRules.BuildDirectionalPiercingEffects(baseEffects, combinedMultiplier);
                bool attackSucceeded = false;
                batch?.AddLogLine(
                    $"{targetUnit.display_name} 承受 {baseDamagePercent}%×{decayPercent}% 的贯穿武器伤害。"
                );
                _apply_unit_skill_result(
                    activeUnit,
                    targetUnit,
                    skillDefinition,
                    castVariantDefinition,
                    targetEffects,
                    batch,
                    logicalAttack.Context,
                    resolution_sink: result => attackSucceeded = result.AttackSuccess,
                    force_weapon_attack_resolution: true
                );
                if (attackSucceeded)
                    successfulHitCount++;
            }
            batch?.AddLogLine(
                $"{activeUnit.display_name} 的{_format_skill_variant_label(skillDefinition, castVariantDefinition)}沿 {FormatDirection(plan.Direction)} 贯穿，完成 {plan.Targets.Count} 次独立武器攻击。"
            );
            logicalAttack.Complete();
            return true;
        }
        catch
        {
            Runtime?.AbortActiveReactionBoundary();
            throw;
        }
    }

}
