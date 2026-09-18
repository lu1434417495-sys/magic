using static BattleSkillTargetPlanRules;
using System;
using System.Collections.Generic;
using Godot;

internal sealed partial class BattleSkillExecutionOrchestrator
{

    private bool _handle_line_through_attack_skill_command(
        BattleUnitState activeUnit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch
    )
    {
        BattleUnitState primaryTarget = ResolveSingleCommandTarget(command);
        BattleLineThroughAttackPlan plan = BattleLineThroughAttackRules.BuildPlan(
            Runtime?._state,
            Runtime?.GetGridService(),
            activeUnit,
            primaryTarget,
            skillDefinition,
            Runtime?._movement_service?.IsMovementBlocked(activeUnit) == true
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
        CombatLineThroughAttackDefinition profile =
            skillDefinition.CombatProfile.LineThroughAttack;
        int skillLevel = _get_unit_skill_level(activeUnit, skillDefinition.SkillId);
        using BattleLogicalAttackScope logicalAttack =
            BeginLogicalAttackForEffects(activeUnit, baseEffects);
        try
        {
            int successfulIntermediateHits = 0;

            foreach (BattleUnitState intermediateTarget in plan.IntermediateTargets)
            {
                if (intermediateTarget?.IsAlive() != true)
                    continue;
                bool attackSucceeded = false;
                IReadOnlyList<CombatEffectDefinition> intermediateEffects =
                    BuildLineThroughAttackEffects(
                        baseEffects,
                        profile.IntermediateWeaponDiceMultiplier
                    );
                batch?.AddLogLine(
                    $"{intermediateTarget.display_name} 承受 {profile.IntermediateWeaponDiceMultiplier}W 途中武器攻击（攻击检定无额外修正）。"
                );
                _apply_unit_skill_result(
                    activeUnit,
                    intermediateTarget,
                    skillDefinition,
                    castVariantDefinition,
                    intermediateEffects,
                    batch,
                    logicalAttack.Context,
                    flat_attack_bonus: 0,
                    resolution_sink: result => attackSucceeded = result.AttackSuccess,
                    force_weapon_attack_resolution: true,
                    record_skill_mastery: false
                );
                if (attackSucceeded)
                    successfulIntermediateHits++;
                if (!activeUnit.IsAlive())
                {
                    batch?.AddLogLine("施术者在途中攻击结算中倒下，终点主攻击与位移取消。");
                    logicalAttack.Complete();
                    return true;
                }
            }

            if (plan.PrimaryTarget?.IsAlive() != true)
            {
                batch?.AddLogLine("终点目标在途中攻击结算中倒下，终点主攻击与位移取消。");
                logicalAttack.Complete();
                return true;
            }

            int primaryWeaponDice = profile.GetPrimaryWeaponDiceMultiplier(
                skillLevel,
                successfulIntermediateHits
            );
            int primaryAttackBonus = profile.GetPrimaryAttackRollBonus(
                skillLevel,
                successfulIntermediateHits
            );
            bool primaryAttackSucceeded = false;
            IReadOnlyList<CombatEffectDefinition> primaryEffects =
                BuildLineThroughAttackEffects(baseEffects, primaryWeaponDice);
            batch?.AddLogLine(
                $"{plan.PrimaryTarget.display_name} 承受终点主攻击：{primaryWeaponDice}W，攻击检定{FormatSignedBonus(primaryAttackBonus)}；途中成功命中 {successfulIntermediateHits} 次。"
            );
            _apply_unit_skill_result(
                activeUnit,
                plan.PrimaryTarget,
                skillDefinition,
                castVariantDefinition,
                primaryEffects,
                batch,
                logicalAttack.Context,
                flat_attack_bonus: primaryAttackBonus,
                resolution_sink: result => primaryAttackSucceeded = result.AttackSuccess,
                force_weapon_attack_resolution: true
            );
            if (primaryAttackSucceeded)
            {
                Runtime?._movement_service?.ExecuteLineThroughAttackLanding(
                    activeUnit,
                    plan,
                    skillDefinition,
                    batch
                );
            }
            else
            {
                batch?.AddLogLine("终点主攻击未命中，敌后位移不发生。");
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

    private BattleUnitState ResolveSingleCommandTarget(BattleCommand command)
    {
        StringName targetUnitId = command?.target_unit_id ?? new StringName("");
        if (targetUnitId == "" && command?.TargetUnitIdsTyped?.Count == 1)
            targetUnitId = command.TargetUnitIdsTyped[0];
        return Runtime?._state?.GetAliveUnit(targetUnitId);
    }

}
