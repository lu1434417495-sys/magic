using static BattleSkillTargetPlanRules;
using System;
using System.Collections.Generic;
using Godot;

internal sealed partial class BattleSkillExecutionOrchestrator
{

    private bool _handle_sequential_line_hit_skill_command(
        BattleUnitState activeUnit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch
    )
    {
        BattleUnitState primaryTarget = ResolveSingleCommandTarget(command);
        BattleSequentialLineHitPlan plan = BattleSequentialLineHitRules.BuildPlan(
            Runtime?._state,
            Runtime?.GetGridService(),
            activeUnit,
            primaryTarget,
            skillDefinition
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
            effectDefinitions
            ?? CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            );
        int skillLevel = _get_unit_skill_level(activeUnit, skillDefinition.SkillId);
        using BattleLogicalAttackScope logicalAttack =
            BeginLogicalAttackForEffects(activeUnit, baseEffects);
        try
        {
            int followUpPenalty = skillDefinition.CombatProfile.SequentialLineHit
                .GetFollowUpAttackPenalty(skillLevel);
            for (int targetIndex = 0; targetIndex < plan.Targets.Count; targetIndex++)
            {
                BattleUnitState target = plan.Targets[targetIndex];
                if (target?.IsAlive() != true)
                {
                    batch?.AddLogLine("下一个目标已经失效，力场长矛停止续行。");
                    break;
                }
                int stageAttackPenalty = -targetIndex * followUpPenalty;
                bool attackSucceeded = false;
                batch?.AddLogLine(
                    $"{target.display_name} 承受第 {targetIndex + 1} 段力场攻击（阶段攻击检定{FormatSignedBonus(stageAttackPenalty)}）。"
                );
                _apply_unit_skill_result(
                    activeUnit,
                    target,
                    skillDefinition,
                    castVariantDefinition,
                    baseEffects,
                    batch,
                    logicalAttack.Context,
                    flat_attack_bonus: stageAttackPenalty,
                    resolution_sink: result => attackSucceeded = result.AttackSuccess
                );
                if (!attackSucceeded)
                {
                    batch?.AddLogLine("本段攻击未命中，力场长矛立即消散。");
                    break;
                }
                if (!activeUnit.IsAlive())
                {
                    batch?.AddLogLine("施术者已倒下，力场长矛停止续行。");
                    break;
                }
            }
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
