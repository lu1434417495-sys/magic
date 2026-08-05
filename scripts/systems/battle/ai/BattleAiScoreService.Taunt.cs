using System;
using System.Collections.Generic;
using Godot;

public partial class BattleAiScoreService
{
    private static bool IsTauntProtectionStatus(StringName statusId) =>
        ProgressionDataUtils.to_string_name(statusId)
        == BattleStatusSemanticTable.STATUS_TAUNTED;

    private void PopulateTauntAllyDamageRelief(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        CombatEffectDefinition tauntEffect = ResolveTauntEffect(effectDefinitions);
        BattleState state = ContextState(context);
        BattleUnitState actor = ContextUnitState(context);
        if (
            scoreInput == null
            || tauntEffect == null
            || state == null
            || actor == null
        )
        {
            return;
        }

        int durationTu = Math.Max(tauntEffect.DurationTu, 0);
        if (durationTu <= 0)
        {
            return;
        }

        var scoredTargetIds = new HashSet<StringName>();
        foreach (StringName targetId in scoreInput.target_unit_ids)
        {
            BattleUnitState threatUnit = GetUnit(state, targetId);
            int readyInTu =
                threatUnit != null
                    ? ResolveThreatReadyInTu(state, threatUnit)
                    : int.MaxValue;
            if (
                threatUnit == null
                || !threatUnit.IsAlive()
                || !scoredTargetIds.Add(threatUnit.unit_id)
                || !IsUnitValidForEffect(
                    actor,
                    threatUnit,
                    ResolveEffectTargetFilter(null, tauntEffect),
                    tauntEffect
                )
                || !BattleCognitionRules.MeetsMinimum(
                    threatUnit,
                    BattleCognitionKind.Sapient
                )
                || readyInTu >= durationTu
            )
            {
                continue;
            }

            int relief = EstimateTauntAllyDamageRelief(
                context,
                actor,
                threatUnit,
                readyInTu
            );
            if (relief <= 0)
            {
                continue;
            }
            scoreInput.estimated_taunt_ally_damage_relief += relief;
            scoreInput.enemy_target_count += 1;
            scoreInput.effective_target_count += 1;
        }

        scoreInput.hit_payoff_score +=
            scoreInput.estimated_taunt_ally_damage_relief
            * Math.Max(_scoreProfile.DamageWeight, 0);
    }

    private int EstimateTauntAllyDamageRelief(
        IBattleAiScoreContext context,
        BattleUnitState actor,
        BattleUnitState threatUnit,
        int readyInTu
    )
    {
        BattleState state = ContextState(context);
        if (state == null || actor == null || threatUnit == null)
        {
            return 0;
        }

        int bestProtectedAllyRelief = 0;
        BattleUnitState projectedThreat =
            BuildThreatActivationProjection(threatUnit, readyInTu);
        using var hitResolver = new BattleHitResolver();
        foreach (BattleUnitState allyUnit in state.GetUnitsTyped())
        {
            if (
                allyUnit == null
                || !allyUnit.IsAlive()
                || allyUnit.unit_id == actor.unit_id
                || allyUnit.faction_id != actor.faction_id
                || state.IsAttackDisadvantage(threatUnit, allyUnit)
            )
            {
                continue;
            }
            bestProtectedAllyRelief = Math.Max(
                bestProtectedAllyRelief,
                EstimateBestAttackDisadvantageRelief(
                    context,
                    threatUnit,
                    projectedThreat,
                    allyUnit,
                    hitResolver,
                    readyInTu
                )
            );
        }
        return bestProtectedAllyRelief;
    }

    private int EstimateBestAttackDisadvantageRelief(
        IBattleAiScoreContext context,
        BattleUnitState threatUnit,
        BattleUnitState projectedThreat,
        BattleUnitState protectedAlly,
        BattleHitResolver hitResolver,
        int readyInTu
    )
    {
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            ContextSkillDefinitions(context);
        int distance = DistanceBetweenUnits(
            context,
            threatUnit,
            protectedAlly
        );
        int bestRelief = 0;
        using var resolutionRules = new BattleSkillResolutionRules();
        foreach (
            StringName skillId in threatUnit.GetKnownActiveSkillsViewTyped()
        )
        {
            StringName normalizedSkillId =
                ProgressionDataUtils.to_string_name(skillId);
            SkillDefinition skillDefinition = GetSkillDefinition(
                skillDefinitions,
                normalizedSkillId
            );
            if (
                skillDefinition == null
                || skillDefinition.CombatProfile == null
                || skillDefinition.CombatProfile.TargetFilterKind
                    == BattleTargetFilter.Ally
                || skillDefinition.CombatProfile.TargetFilterKind
                    == BattleTargetFilter.Self
            )
            {
                continue;
            }

            if (
                !CanThreatUseSkillAtActivation(
                    context,
                    threatUnit,
                    projectedThreat,
                    skillDefinition,
                    readyInTu
                )
            )
            {
                continue;
            }

            int range = BattleRangeService.GetEffectiveSkillThreatRange(
                threatUnit,
                skillDefinition,
                ContextSkillCatalog(context)
            );
            if (range <= 0 || distance < 0 || distance > range)
            {
                continue;
            }

            List<CombatEffectDefinition> damageEffects =
                CollectRoleThreatEffectDefinitions(
                    threatUnit,
                    skillDefinition,
                    ContextSkillCatalog(context)
                );
            if (!IsDamageSkill(damageEffects))
            {
                continue;
            }
            if (
                resolutionRules.IsForceHitNoCritSkill(
                    skillDefinition,
                    threatUnit
                )
                || !resolutionRules.ShouldResolveUnitSkillAsFateAttack(
                    threatUnit,
                    protectedAlly,
                    skillDefinition,
                    damageEffects
                )
            )
            {
                continue;
            }
            DamageEstimateResult damageEstimate =
                EstimateDamageForTargetResult(
                    threatUnit,
                    damageEffects,
                    protectedAlly,
                    normalizedSkillId
                );
            int damage = Math.Max(
                damageEstimate?.IncomingBudgetDamage ?? 0,
                0
            );
            if (damage <= 0)
            {
                continue;
            }

            AttackCheckInput attackCheck =
                hitResolver.BuildSkillAttackCheck(
                    threatUnit,
                    protectedAlly,
                    skillDefinition
                );
            if (attackCheck.Invalid)
            {
                continue;
            }
            bestRelief = Math.Max(
                bestRelief,
                EstimateDisadvantageDamageRelief(
                    damage,
                    attackCheck.HitRatePercent
                )
            );
        }

        BattleWeaponProjectionValues weapon =
            threatUnit.GetWeaponProjectionReadViewTyped().Values;
        BattleWeaponDiceValues weaponDice = weapon.ActiveDice;
        int weaponRange = BattleRangeService.GetWeaponAttackRange(
            threatUnit
        );
        if (
            weaponDice.HasUsableDice
            && weaponRange > 0
            && distance >= 0
            && distance <= weaponRange
        )
        {
            CombatEffectDefinition weaponEffect =
                BattleRuntimeEffectDefinitions.Damage(
                    weapon.PhysicalDamageTag,
                    Math.Max(weaponDice.DiceCount, 0),
                    Math.Max(weaponDice.DiceSides, 0),
                    weaponDice.FlatBonus
                );
            DamageEstimateResult weaponDamageEstimate =
                EstimateDamageForTargetResult(
                    threatUnit,
                    new[] { weaponEffect },
                    protectedAlly
                );
            SkillDefinition basicAttack = GetSkillDefinition(
                skillDefinitions,
                new StringName("basic_attack")
            );
            if (
                basicAttack != null
                && !CanThreatUseSkillAtActivation(
                    context,
                    threatUnit,
                    projectedThreat,
                    basicAttack,
                    readyInTu
                )
            )
            {
                return bestRelief;
            }
            AttackCheckInput weaponAttackCheck =
                hitResolver.BuildSkillDefinitionAttackCheck(
                    threatUnit,
                    protectedAlly,
                    basicAttack,
                    0,
                    0
                );
            if (!weaponAttackCheck.Invalid)
            {
                bestRelief = Math.Max(
                    bestRelief,
                    EstimateDisadvantageDamageRelief(
                        Math.Max(
                            weaponDamageEstimate?.IncomingBudgetDamage
                                ?? 0,
                            0
                        ),
                        weaponAttackCheck.HitRatePercent
                    )
                );
            }
        }
        return bestRelief;
    }

    private static bool CanThreatUseSkillAtActivation(
        IBattleAiScoreContext context,
        BattleUnitState threatUnit,
        BattleUnitState projectedUnit,
        SkillDefinition skillDefinition,
        int readyInTu
    )
    {
        if (
            threatUnit == null
            || projectedUnit == null
            || skillDefinition?.CombatProfile == null
        )
        {
            return false;
        }

        int elapsedTu = Math.Max(readyInTu, 0);
        projectedUnit.SetCooldownTyped(
            skillDefinition.SkillId,
            Math.Max(
                threatUnit.GetCooldownTyped(skillDefinition.SkillId)
                    - elapsedTu,
                0
            )
        );
        if (
            context?.skill_cast_block_reason_callback != null
        )
        {
            return !BattleSkillCastBlockReasonKinds.IsBlocked(
                context.skill_cast_block_reason_callback.Invoke(
                    projectedUnit,
                    skillDefinition
                )
            );
        }

        int skillLevel = projectedUnit.GetKnownSkillLevelTyped(
            skillDefinition.SkillId
        );
        CombatSkillResourceCosts costs =
            skillDefinition.CombatProfile
                .GetEffectiveResourceCostValues(skillLevel);
        return projectedUnit.GetCooldownTyped(skillDefinition.SkillId)
                <= 0
            && projectedUnit.GetCurrentAp() >= costs.ApCost
            && projectedUnit.GetCurrentMp() >= costs.MpCost
            && projectedUnit.GetCurrentStamina()
                >= costs.StaminaCost
            && projectedUnit.GetCurrentAura() >= costs.AuraCost;
    }

    private static BattleUnitState BuildThreatActivationProjection(
        BattleUnitState threatUnit,
        int readyInTu
    )
    {
        if (threatUnit == null)
        {
            return null;
        }

        BattleUnitState projectedUnit = threatUnit.clone();
        int elapsedTu = Math.Max(readyInTu, 0);
        projectedUnit.SetCurrentAp(
            Math.Max(
                projectedUnit.attribute_snapshot?.GetValue(
                    AttributeService.ToStringName(
                        AttributeIdKind.ActionPoints
                    )
                ) ?? 0,
                1
            )
        );

        int staminaMax =
            BattleStaminaRecoveryRules.ResolveSnapshotStaminaMax(
                projectedUnit
            );
        if (staminaMax > 0)
        {
            projectedUnit.ApplyStaminaRecoveryTyped(
                BattleStaminaRecoveryRules.ResolveTickCount(elapsedTu),
                staminaMax,
                BattleStaminaRecoveryRules.ResolveProgressGainPerTick(
                    projectedUnit,
                    projectedUnit.IsRestingTyped()
                ),
                BattleStaminaRecoveryRules.ProgressDenominator
            );
        }
        if (elapsedTu > 0)
        {
            new BattleRuntimeSkillTurnResolver()
                .AdvanceUnitStatusDurations(projectedUnit, elapsedTu);
        }
        return projectedUnit;
    }

    private static int EstimateDisadvantageDamageRelief(
        int onHitDamage,
        int normalHitRatePercent
    )
    {
        int hitRate = Math.Clamp(normalHitRatePercent, 0, 100);
        int reliefBasisPoints = hitRate * 100 - hitRate * hitRate;
        return RoundToInt(
            Math.Max(onHitDamage, 0) * (double)reliefBasisPoints / 10000.0
        );
    }

    private static CombatEffectDefinition ResolveTauntEffect(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        CombatEffectDefinition strongest = null;
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition == null
                || (
                    effectDefinition.EffectKind != BattleEffectKind.Status
                    && effectDefinition.EffectKind
                        != BattleEffectKind.ApplyStatus
                )
                || !IsTauntProtectionStatus(effectDefinition.StatusId)
            )
            {
                continue;
            }
            if (
                strongest == null
                || effectDefinition.DurationTu > strongest.DurationTu
            )
            {
                strongest = effectDefinition;
            }
        }
        return strongest;
    }
}
