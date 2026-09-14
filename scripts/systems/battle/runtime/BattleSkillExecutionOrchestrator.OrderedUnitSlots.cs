using System;
using System.Collections.Generic;
using Godot;

internal sealed partial class BattleSkillExecutionOrchestrator
{
    internal bool _handle_ordered_unit_target_slots_skill_command(
        BattleUnitState activeUnit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch
    )
    {
        BattleUnitSkillValidationResult validation = _validate_unit_skill_targets_result(
            activeUnit,
            command,
            skillDefinition,
            castVariantDefinition
        );
        if (!validation.Allowed || validation.TargetUnits.Count == 0)
            return false;

        IReadOnlyList<CombatEffectDefinition> resolvedEffectDefinitions =
            effectDefinitions
            ?? CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            );
        if (!CanApplyUnitSkillOrRepeatResultFromDefinitions(resolvedEffectDefinitions))
            return false;

        int targetSlotCount = validation.TargetUnits.Count;
        if (
            !_consume_skill_costs(
                activeUnit,
                skillDefinition,
                castVariantDefinition,
                batch,
                targetSlotCount
            )
        )
        {
            return false;
        }
        CombatSkillResourceCosts costs = _get_effective_skill_resource_costs(
            activeUnit,
            skillDefinition,
            targetSlotCount
        );
        _record_action_issued(
            activeUnit,
            BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            costs.ApCost
        );
        _append_changed_unit_id(batch, activeUnit.unit_id);

        if (ResolveSpellReactionsAfterCost(activeUnit, skillDefinition, batch).Interrupted)
            return false;

        BattleSpellControlResult spellControlContext =
            _resolve_unit_spell_control_after_cost_result(
                activeUnit,
                skillDefinition,
                batch
            );
        if (spellControlContext.SkipEffects)
            return true;

        using BattleLogicalAttackScope logicalAttack =
            BeginLogicalAttackForEffects(activeUnit, resolvedEffectDefinitions);
        try
        {
            bool applied = ApplyOrderedUnitTargetSlots(
                activeUnit,
                validation.TargetUnits,
                skillDefinition,
                castVariantDefinition,
                resolvedEffectDefinitions,
                batch,
                logicalAttack.Context,
                spellControlContext,
                BattleForcedMoveContext.FromDestination(
                    command.forced_move_destination_coord
                ),
                collapseActiveSkillMastery: true
            );
            logicalAttack.Complete();
            return applied || targetSlotCount > 0;
        }
        catch
        {
            Runtime?.AbortActiveReactionBoundary();
            throw;
        }
    }

    private bool ApplyOrderedUnitTargetSlots(
        BattleUnitState activeUnit,
        IReadOnlyList<BattleUnitState> orderedTargetSlots,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch,
        BattleAttackActionContext actionContext,
        BattleSpellControlResult spellControlContext,
        BattleForcedMoveContext forcedMoveContext = default,
        bool collapseActiveSkillMastery = false
    )
    {
        bool applied = false;
        BattleRepeatAttackResolver repeatAttackResolver = Runtime?._repeat_attack_resolver;
        foreach (
            BattleUnitState targetUnit in orderedTargetSlots
                ?? Array.Empty<BattleUnitState>()
        )
        {
            if (targetUnit == null || !targetUnit.IsAlive())
            {
                batch?.AddLogLine("后续攻击的既定目标已失效，该次攻击消散。");
                continue;
            }

            IReadOnlyDictionary<CombatEffectDefinition, IReadOnlyList<BattleUnitState>>
                targetPlan = BuildUnitEffectTargetPlan(
                    activeUnit,
                    skillDefinition,
                    effectDefinitions,
                    new[] { targetUnit }
                );
            IReadOnlyList<CombatEffectDefinition> targetEffects =
                CollectPlannedEffectsForTarget(
                    effectDefinitions,
                    targetPlan,
                    targetUnit.unit_id
                );
            if (targetEffects.Count == 0)
                continue;

            CombatEffectDefinition repeatAttackEffect =
                repeatAttackResolver?.get_repeat_attack_effect_def(targetEffects);
            if (repeatAttackEffect != null)
            {
                if (
                    repeatAttackResolver != null
                    && repeatAttackResolver.ApplyRepeatAttackSkillResult(
                        activeUnit,
                        targetUnit,
                        skillDefinition,
                        targetEffects,
                        repeatAttackEffect,
                        batch,
                        actionContext,
                        castVariantDefinition
                    )
                )
                {
                    applied = true;
                }
                continue;
            }

            if (
                _apply_unit_skill_result(
                    activeUnit,
                    targetUnit,
                    skillDefinition,
                    castVariantDefinition,
                    targetEffects,
                    batch,
                    actionContext,
                    spellControlContext,
                    forced_move_context: forcedMoveContext
                )
            )
            {
                applied = true;
            }
        }
        if (collapseActiveSkillMastery)
            Runtime?._skill_mastery_service.CollapseTargetResultsToSingleGrant();
        return applied;
    }
}
