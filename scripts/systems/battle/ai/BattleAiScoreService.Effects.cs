using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class BattleAiScoreService
{
    private sealed class TargetEffectMetrics
    {
        public bool IsEmpty = true;
        public int Damage;
        public int PostSaveDamage;
        public int ShieldAbsorbed;
        public bool StableLethal;
        public bool IsExecute;
        public int KillProbabilityBasisPoints;
        public bool SoulFractureApplied;
        public int Healing;
        public int HarmfulControlCount;
        public int BeneficialControlCount;
        public int TerrainEffectCount;
        public int HeightDelta;
        public List<DamageSaveEstimate> SaveEstimates = new();
        public List<DamageEstimateBreakdown> DamageEstimates = new();

        public TargetEffectMetrics Clone()
        {
            return new TargetEffectMetrics
            {
                IsEmpty = IsEmpty,
                Damage = Damage,
                PostSaveDamage = PostSaveDamage,
                ShieldAbsorbed = ShieldAbsorbed,
                StableLethal = StableLethal,
                IsExecute = IsExecute,
                KillProbabilityBasisPoints = KillProbabilityBasisPoints,
                SoulFractureApplied = SoulFractureApplied,
                Healing = Healing,
                HarmfulControlCount = HarmfulControlCount,
                BeneficialControlCount = BeneficialControlCount,
                TerrainEffectCount = TerrainEffectCount,
                HeightDelta = HeightDelta,
                SaveEstimates = CloneSaveEstimates(SaveEstimates),
                DamageEstimates = CloneDamageEstimates(DamageEstimates),
            };
        }
    }

    private sealed class TargetRoleSummary
    {
        public int HealSkillCount;
        public int ControlSkillCount;
        public int BestRangedAttackRange;
        public int ThreatMultiplierBasisPoints = ThreatMultiplierBasisPointsDenominator;

        public bool HasHighThreatRole =>
            HealSkillCount > 0
            || ControlSkillCount > 0
            || BestRangedAttackRange >= MinRangedThreatRange;
    }

    private readonly record struct ChainDamageParameters(
        int BaseRadius,
        StringName BonusTerrainEffectId,
        int WetChainRadius,
        bool PreventRepeatTarget
    )
    {
        public static ChainDamageParameters FromEffect(CombatEffectDefinition effectDefinition)
        {
            int baseRadius = Math.Max(
                ReadIntParameter(effectDefinition, "base_chain_radius", 1),
                0
            );
            return new ChainDamageParameters(
                baseRadius,
                ReadStringNameParameter(effectDefinition, "bonus_terrain_effect_id"),
                Math.Max(
                    ReadIntParameter(effectDefinition, "wet_chain_radius", baseRadius),
                    baseRadius
                ),
                effectDefinition?.PreventRepeatTarget ?? true
            );
        }
    }

    private StringName ResolveMeteorUseCase(
        BattleAiScoreInput scoreInput,
        IReadOnlyList<MeteorSwarmNumericSummary> targetSummaries
    )
    {
        if (scoreInput == null)
        {
            return "";
        }
        scoreInput.low_value_penalty_reason = "";
        if (!string.IsNullOrEmpty(scoreInput.friendly_fire_reject_reason))
        {
            return "unsafe_friendly_fire";
        }
        if (HasMeteorDecapitationTarget(scoreInput, targetSummaries))
        {
            return "decapitation";
        }
        if (scoreInput.enemy_target_count >= 3)
        {
            return "cluster";
        }
        if (HasMeteorZoneDenial(scoreInput, targetSummaries))
        {
            return "zone_denial";
        }
        scoreInput.low_value_penalty_reason = "no_cluster_decapitation_or_zone_denial";
        scoreInput.hit_payoff_score -= Math.Max(_scoreProfile.TargetCountWeight, 0);
        return "impact";
    }

    private void RecordMeteorHighPriorityTarget(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        BattleUnitState targetUnit,
        MeteorSwarmNumericSummary summary,
        int targetPriorityScore
    )
    {
        if (scoreInput == null || targetUnit == null || !IsMeteorScoreInput(scoreInput))
        {
            return;
        }
        List<string> reasons = ResolveMeteorHighPriorityReasons(
            context,
            targetUnit,
            summary,
            targetPriorityScore
        );
        if (reasons.Count == 0)
        {
            return;
        }
        AppendUniqueStringName(scoreInput.high_priority_target_ids, targetUnit.unit_id);
        scoreInput.high_priority_reasons[targetUnit.unit_id] = reasons;
    }

    private List<string> ResolveMeteorHighPriorityReasons(
        IBattleAiScoreContext context,
        BattleUnitState targetUnit,
        MeteorSwarmNumericSummary summary,
        int targetPriorityScore
    )
    {
        var reasons = new List<string>();
        if (targetUnit == null)
        {
            return reasons;
        }
        if (IsMeteorEliteOrBossTarget(targetUnit))
        {
            reasons.Add("elite_or_boss");
        }
        TargetRoleSummary roleSummary = ResolveTargetRoleSummary(context, targetUnit);
        int threatMultiplier = roleSummary.ThreatMultiplierBasisPoints;
        if (
            roleSummary.HasHighThreatRole
            && threatMultiplier >= _scoreProfile.MeteorHighPriorityThreatMultiplierBp
        )
        {
            reasons.Add("role_threat_multiplier");
        }
        int centerDirectExpected = ResolveComponentExpectedDamage(
            summary.Components,
            "center_direct"
        );
        int maxHp = GetUnitMaxHp(targetUnit);
        int centerDirectHpPercent = RoundToInt(
            (double)centerDirectExpected * 100.0 / Math.Max(maxHp, 1)
        );
        if (
            centerDirectHpPercent >= _scoreProfile.MeteorHighPriorityDamageHpPercent
            && roleSummary.HasHighThreatRole
        )
        {
            reasons.Add("center_direct_high_role_damage");
        }
        if (targetPriorityScore >= _scoreProfile.MeteorHighPriorityTargetPriorityScore)
        {
            reasons.Add("target_priority_score");
        }
        int threatRank = ResolveMeteorThreatRank(context, targetUnit);
        if (threatRank > 0 && threatRank <= Math.Max(_scoreProfile.MeteorTopThreatRank, 0))
        {
            reasons.Add("top_threat_rank");
        }
        return reasons;
    }

    private static int ResolveComponentExpectedDamage(
        IEnumerable<MeteorSwarmComponentBreakdownEntry> components,
        StringName componentId
    )
    {
        foreach (
            MeteorSwarmComponentBreakdownEntry component in components
                ?? System.Array.Empty<MeteorSwarmComponentBreakdownEntry>()
        )
        {
            if (component.ComponentId == componentId)
            {
                return Math.Max(component.ExpectedDamage, 0);
            }
        }
        return 0;
    }

    private TargetRoleSummary ResolveTargetRoleSummary(
        IBattleAiScoreContext context,
        BattleUnitState targetUnit
    )
    {
        var summary = new TargetRoleSummary
        {
            ThreatMultiplierBasisPoints = ResolveTargetRoleThreatMultiplierBasisPoints(
                context,
                targetUnit
            ),
        };
        if (context == null || targetUnit == null)
        {
            return summary;
        }
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            ContextSkillDefinitions(context);
        foreach (StringName skillId in targetUnit.known_active_skill_ids)
        {
            StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (IsEmpty(normalizedSkillId))
            {
                continue;
            }
            SkillDefinition skillDefinition = GetSkillDefinition(
                skillDefinitions,
                normalizedSkillId
            );
            if (skillDefinition == null || skillDefinition.CombatProfile == null)
            {
                continue;
            }
            List<CombatEffectDefinition> roleEffectDefs = CollectRoleThreatEffectDefinitions(
                targetUnit,
                skillDefinition,
                ContextSkillCatalog(context)
            );
            if (IsHealOrSupportSkill(skillDefinition, roleEffectDefs))
            {
                summary.HealSkillCount += 1;
            }
            if (IsControlSkill(roleEffectDefs))
            {
                summary.ControlSkillCount += 1;
            }
            if (IsDamageSkill(roleEffectDefs))
            {
                int effectiveRange = BattleRangeService.GetEffectiveSkillThreatRange(
                    targetUnit,
                    skillDefinition
                );
                if (effectiveRange >= MinRangedThreatRange)
                {
                    summary.BestRangedAttackRange = Math.Max(
                        summary.BestRangedAttackRange,
                        effectiveRange
                    );
                }
            }
        }
        return summary;
    }

    private static bool IsMeteorEliteOrBossTarget(BattleUnitState targetUnit)
    {
        if (targetUnit == null || targetUnit.attribute_snapshot == null)
        {
            return false;
        }
        return targetUnit.attribute_snapshot.GetValue(BossTargetStatId) > 0
            || targetUnit.attribute_snapshot.GetValue(FortuneMarkTargetStatId)
                > 0;
    }

    private int ResolveMeteorThreatRank(IBattleAiScoreContext context, BattleUnitState targetUnit)
    {
        using BattleAiTraceSpan trace = new("_resolve_meteor_threat_rank");
        return ResolveMeteorThreatRankImpl(context, targetUnit);
    }

    private int ResolveMeteorThreatRankImpl(IBattleAiScoreContext context, BattleUnitState targetUnit)
    {
        BattleState state = ContextState(context);
        BattleUnitState actor = ContextUnitState(context);
        if (state == null || actor == null || targetUnit == null)
        {
            return 0;
        }
        var enemies = new List<BattleUnitState>();
        foreach (BattleUnitState unitState in state.GetUnitsTyped())
        {
            if (
                unitState == null
                || !unitState.is_alive
                || unitState.faction_id == actor.faction_id
            )
            {
                continue;
            }
            enemies.Add(unitState);
        }
        enemies.Sort(
            (left, right) =>
            {
                int leftMultiplier = ResolveTargetRoleThreatMultiplierBasisPoints(context, left);
                int rightMultiplier = ResolveTargetRoleThreatMultiplierBasisPoints(context, right);
                if (leftMultiplier != rightMultiplier)
                {
                    return rightMultiplier.CompareTo(leftMultiplier);
                }
                int leftBoss = IsMeteorEliteOrBossTarget(left) ? 1 : 0;
                int rightBoss = IsMeteorEliteOrBossTarget(right) ? 1 : 0;
                if (leftBoss != rightBoss)
                {
                    return rightBoss.CompareTo(leftBoss);
                }
                return string.CompareOrdinal(left.unit_id.ToString(), right.unit_id.ToString());
            }
        );
        for (int index = 0; index < enemies.Count; index += 1)
        {
            if (enemies[index] != null && enemies[index].unit_id == targetUnit.unit_id)
            {
                return index + 1;
            }
        }
        return 0;
    }

    private static bool HasMeteorDecapitationTarget(
        BattleAiScoreInput scoreInput,
        IReadOnlyList<MeteorSwarmNumericSummary> targetSummaries
    )
    {
        if (
            scoreInput == null
            || targetSummaries == null
            || targetSummaries.Count == 0
            || scoreInput.high_priority_target_ids.Count == 0
        )
        {
            return false;
        }
        foreach (MeteorSwarmNumericSummary summary in targetSummaries)
        {
            if (!summary.HasCenterDirect)
            {
                continue;
            }
            if (scoreInput.high_priority_target_ids.Contains(summary.TargetUnitId))
            {
                return true;
            }
        }
        return false;
    }

    private static List<MeteorSwarmNumericSummary> ReadTargetNumericSummaries(
        BattleSpecialProfilePreviewFacts facts
    )
    {
        if (facts is not MeteorSwarmPreviewFacts meteorFacts)
        {
            return new List<MeteorSwarmNumericSummary>();
        }
        if (meteorFacts.target_numeric_summaries.Count > 0)
        {
            return new List<MeteorSwarmNumericSummary>(meteorFacts.target_numeric_summaries);
        }
        return new List<MeteorSwarmNumericSummary>();
    }

    private static bool HasMeteorZoneDenial(
        BattleAiScoreInput scoreInput,
        IReadOnlyList<MeteorSwarmNumericSummary> targetSummaries
    )
    {
        if (scoreInput == null || scoreInput.estimated_terrain_effect_count <= 0)
        {
            return false;
        }
        return scoreInput.enemy_target_count > 0
            || targetSummaries == null
            || targetSummaries.Count == 0;
    }

    private string ResolveMeteorFriendlyFireRejectReason(
        BattleUnitState targetUnit,
        MeteorSwarmNumericSummary summary,
        int estimatedDamage,
        int worstCaseDamage,
        int statusCount
    )
    {
        if (targetUnit == null)
        {
            return "";
        }
        string targetLabel = targetUnit.unit_id.ToString();
        if (
            IsMeteorProtectedAlly(targetUnit)
            && MeteorSummaryHasAnyProtectedAllyConsequence(
                summary,
                estimatedDamage,
                worstCaseDamage,
                statusCount
            )
        )
        {
            return $"meteor_swarm_protected_ally:{targetLabel}";
        }
        int lethalProbability = summary.LethalProbabilityPercent;
        if (lethalProbability > 0 || worstCaseDamage >= Math.Max(targetUnit.current_hp, 1))
        {
            return $"meteor_swarm_friendly_fire_lethal:{targetLabel}";
        }
        if (
            _scoreProfile != null
            && _scoreProfile.MeteorFriendlyFireProfileKind
                != BattleAiMeteorFriendlyFireProfile.Reckless
        )
        {
            if (
                summary.ExpectedDamageHpPercent
                >= _scoreProfile.MeteorFriendlyFireHardExpectedHpPercent
            )
            {
                return $"meteor_swarm_friendly_fire_expected_threshold:{targetLabel}";
            }
            if (
                summary.WorstCaseDamageHpPercent
                >= _scoreProfile.MeteorFriendlyFireHardWorstCaseHpPercent
            )
            {
                return $"meteor_swarm_friendly_fire_worst_threshold:{targetLabel}";
            }
        }
        if (summary.HardReject)
        {
            return $"meteor_swarm_friendly_fire_hard_reject:{targetLabel}";
        }
        return "";
    }

    private static bool IsMeteorProtectedAlly(BattleUnitState targetUnit)
    {
        if (targetUnit == null)
        {
            return false;
        }
        if (
            targetUnit.ai_blackboard?.meteor_protected_ally == true
            || targetUnit.ai_blackboard?.protected_ally == true
        )
        {
            return true;
        }
        return targetUnit.attribute_snapshot != null
            && targetUnit.attribute_snapshot.GetValue("protected_ally") > 0;
    }

    private static bool MeteorSummaryHasAnyProtectedAllyConsequence(
        MeteorSwarmNumericSummary summary,
        int estimatedDamage,
        int worstCaseDamage,
        int statusCount
    )
    {
        if (estimatedDamage > 0 || worstCaseDamage > 0 || statusCount > 0)
        {
            return true;
        }
        if (summary.ApPenalty > 0)
        {
            return true;
        }
        return summary.HostileTerrain?.HasProtectedAllyConsequence == true;
    }

    private static bool IsMeteorScoreInput(BattleAiScoreInput scoreInput)
    {
        if (scoreInput == null)
        {
            return false;
        }
        return scoreInput.special_profile_preview_facts?.profile_id == MeteorSwarmProfileId;
    }

    private static int GetUnitMaxHp(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return 1;
        }
        if (unitState.attribute_snapshot != null)
        {
            int maxHp = unitState.attribute_snapshot.GetValue("hp_max");
            if (maxHp > 0)
            {
                return maxHp;
            }
        }
        return Math.Max(unitState.current_hp, 1);
    }

    private void PopulateEnemyTargetPayoff(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        BattleUnitState targetUnit,
        int estimatedDamage,
        int estimatedHealing,
        int harmfulControlCount,
        int beneficialControlCount,
        int estimatedTerrainEffectCount,
        int estimatedHeightDelta,
        int estimatedShieldAbsorbed,
        bool stableLethal,
        bool isExecute = false,
        int executeKillProbabilityBasisPoints = 0
    )
    {
        bool hasBeneficialEnemyEffect =
            estimatedDamage > 0
            || harmfulControlCount > 0
            || estimatedShieldAbsorbed > 0
            || estimatedTerrainEffectCount > 0
            || estimatedHeightDelta > 0;
        if (hasBeneficialEnemyEffect)
        {
            scoreInput.effective_target_count += 1;
        }
        scoreInput.hit_payoff_score += estimatedDamage * _scoreProfile.DamageWeight;
        scoreInput.hit_payoff_score -= estimatedHealing * _scoreProfile.HealWeight;
        scoreInput.hit_payoff_score += harmfulControlCount * _scoreProfile.StatusWeight;
        scoreInput.hit_payoff_score +=
            estimatedShieldAbsorbed * Math.Max(_scoreProfile.ShieldAbsorbedWeight, 0);
        scoreInput.hit_payoff_score -= beneficialControlCount * _scoreProfile.StatusWeight;
        scoreInput.hit_payoff_score += estimatedTerrainEffectCount * _scoreProfile.TerrainWeight;
        scoreInput.hit_payoff_score += estimatedHeightDelta * _scoreProfile.HeightWeight;
        int targetPriorityBonus = ResolveTargetRoleThreatBonus(
            context,
            targetUnit,
            estimatedDamage,
            harmfulControlCount,
            estimatedTerrainEffectCount,
            estimatedHeightDelta
        );
        scoreInput.target_priority_score += targetPriorityBonus;
        scoreInput.hit_payoff_score += targetPriorityBonus;
        int lethalBonus = isExecute
            ? ResolveExecuteLethalBonusFromBasisPoints(
                scoreInput,
                context,
                targetUnit,
                executeKillProbabilityBasisPoints
            )
            : stableLethal
                ? ResolveLethalTargetBonus(scoreInput, context, targetUnit, estimatedDamage)
                : 0;
        scoreInput.hit_payoff_score += lethalBonus;
        scoreInput.target_priority_score += lethalBonus;
        if (harmfulControlCount > 0)
        {
            AppendUniqueStringName(scoreInput.estimated_control_target_ids, targetUnit.unit_id);
            if (IsPriorityThreatTarget(context, targetUnit))
            {
                AppendUniqueStringName(
                    scoreInput.estimated_control_threat_target_ids,
                    targetUnit.unit_id
                );
            }
        }
    }

    private void PopulateTargetEffectMetrics(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        BattleUnitState targetUnit,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        int hitCount = 1,
        bool isChainTarget = false,
        SkillDefinition skillDefinition = null
    )
    {
        using BattleAiTraceSpan trace = new("_populate_target_effect_metrics");
        PopulateTargetEffectMetricsImpl(
            scoreInput,
            context,
            targetUnit,
            effectDefinitions,
            hitCount,
            isChainTarget,
            skillDefinition
        );
    }

    private void PopulateTargetEffectMetricsImpl(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        BattleUnitState targetUnit,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        int hitCount = 1,
        bool isChainTarget = false,
        SkillDefinition skillDefinition = null
    )
    {
        BattleUnitState actor = ContextUnitState(context);
        if (scoreInput == null || actor == null || targetUnit == null || hitCount <= 0)
        {
            return;
        }
        skillDefinition ??= ResolveScoreInputSkillDefinition(scoreInput, context);
        TargetEffectMetrics targetMetrics = BuildTargetEffectMetrics(
            context,
            skillDefinition,
            actor,
            targetUnit,
            effectDefinitions,
            hitCount
        );
        if (targetMetrics.IsEmpty)
        {
            return;
        }

        int estimatedDamage = targetMetrics.Damage;
        int estimatedPostSaveDamage = targetMetrics.PostSaveDamage;
        int estimatedShieldAbsorbed = targetMetrics.ShieldAbsorbed;
        bool stableLethal = targetMetrics.IsExecute
            ? targetMetrics.KillProbabilityBasisPoints >= 10000
            : targetMetrics.StableLethal || estimatedDamage >= Math.Max(targetUnit.current_hp, 1);
        int estimatedHealing = targetMetrics.Healing;
        int harmfulControlCount = targetMetrics.HarmfulControlCount;
        int beneficialControlCount = targetMetrics.BeneficialControlCount;
        int estimatedTerrainEffectCount = targetMetrics.TerrainEffectCount;
        int estimatedHeightDelta = targetMetrics.HeightDelta;
        bool isAlly = targetUnit.faction_id == actor.faction_id;
        AppendSaveEstimatesForTarget(scoreInput, targetUnit, targetMetrics.SaveEstimates);
        AppendDamageEstimatesForTarget(scoreInput, targetUnit, targetMetrics.DamageEstimates);

        scoreInput.estimated_damage += estimatedDamage;
        scoreInput.estimated_post_save_damage += estimatedPostSaveDamage;
        scoreInput.estimated_shield_absorbed += estimatedShieldAbsorbed;
        scoreInput.estimated_healing += estimatedHealing;
        scoreInput.estimated_status_count += harmfulControlCount + beneficialControlCount;
        scoreInput.estimated_control_count += harmfulControlCount + beneficialControlCount;
        scoreInput.estimated_terrain_effect_count += estimatedTerrainEffectCount;
        scoreInput.estimated_height_delta += estimatedHeightDelta;
        if (targetMetrics.IsExecute)
        {
            scoreInput.execute_kill_probability_basis_points = Math.Max(
                scoreInput.execute_kill_probability_basis_points,
                targetMetrics.KillProbabilityBasisPoints
            );
            scoreInput.execute_soul_fracture_applied =
                scoreInput.execute_soul_fracture_applied || targetMetrics.SoulFractureApplied;
        }

        if (isChainTarget)
        {
            scoreInput.estimated_chain_target_count += 1;
            if (isAlly)
            {
                scoreInput.estimated_chain_ally_target_count += 1;
            }
            else
            {
                scoreInput.estimated_chain_enemy_target_count += 1;
            }
        }

        if (isAlly)
        {
            scoreInput.ally_target_count += 1;
            scoreInput.estimated_ally_damage += estimatedDamage;
            scoreInput.estimated_ally_healing += estimatedHealing;
            PopulateAllyTargetPayoff(
                scoreInput,
                targetUnit,
                estimatedDamage,
                estimatedHealing,
                harmfulControlCount,
                beneficialControlCount,
                targetMetrics.IsExecute,
                targetMetrics.KillProbabilityBasisPoints
            );
            return;
        }

        scoreInput.enemy_target_count += 1;
        scoreInput.estimated_enemy_damage += estimatedDamage;
        scoreInput.estimated_enemy_healing += estimatedHealing;
        PopulateEnemyTargetPayoff(
            scoreInput,
            context,
            targetUnit,
            estimatedDamage,
            estimatedHealing,
            harmfulControlCount,
            beneficialControlCount,
            estimatedTerrainEffectCount,
            estimatedHeightDelta,
            estimatedShieldAbsorbed,
            stableLethal,
            targetMetrics.IsExecute,
            targetMetrics.KillProbabilityBasisPoints
        );
    }

    private void PopulateAllyTargetPayoff(
        BattleAiScoreInput scoreInput,
        BattleUnitState targetUnit,
        int estimatedDamage,
        int estimatedHealing,
        int harmfulControlCount,
        int beneficialControlCount,
        bool isExecute = false,
        int executeKillProbabilityBasisPoints = 0
    )
    {
        bool hasAllyBenefit = estimatedHealing > 0 || beneficialControlCount > 0;
        if (hasAllyBenefit)
        {
            scoreInput.effective_target_count += 1;
            scoreInput.hit_payoff_score += estimatedHealing * _scoreProfile.HealWeight;
            scoreInput.hit_payoff_score += beneficialControlCount * _scoreProfile.StatusWeight;
        }
        if (estimatedDamage <= 0 && harmfulControlCount <= 0)
        {
            return;
        }
        scoreInput.estimated_friendly_fire_target_count += 1;
        scoreInput.estimated_friendly_fire_damage += estimatedDamage;
        if (harmfulControlCount > 0)
        {
            scoreInput.estimated_friendly_control_target_count += 1;
        }
        int penalty =
            estimatedDamage * _scoreProfile.FriendlyFireDamageWeight
            + _scoreProfile.FriendlyFireTargetWeight
            + harmfulControlCount * _scoreProfile.FriendlyControlTargetWeight;
        bool isFriendlyLethal =
            isExecute
                ? Mathf.Clamp(executeKillProbabilityBasisPoints, 0, 10000) > 0
                : estimatedDamage >= Math.Max(targetUnit.current_hp, 1);
        if (isFriendlyLethal)
        {
            scoreInput.estimated_friendly_lethal_target_count += 1;
            penalty += _scoreProfile.FriendlyLethalTargetWeight;
        }
        scoreInput.friendly_fire_penalty_score += penalty;
        scoreInput.hit_payoff_score -= penalty;
    }

    private TargetEffectMetrics BuildTargetEffectMetrics(
        IBattleAiScoreContext context,
        SkillDefinition skillDefinition,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        int hitCount = 1
    )
    {
        using BattleAiTraceSpan trace = new("_build_target_effect_metrics");
        TargetEffectMetrics result;
        if (_decisionScopeActive)
        {
            TargetEffectMetricsCacheKey cacheKey = BuildTargetEffectMetricsCacheKey(
                skillDefinition,
                sourceUnit,
                targetUnit,
                effectDefinitions,
                hitCount
            );
            if (_targetEffectMetricsCache.TryGetValue(cacheKey, out TargetEffectMetrics cached))
            {
                return cached.Clone();
            }
            result = BuildTargetEffectMetricsImpl(
                context,
                skillDefinition,
                sourceUnit,
                targetUnit,
                effectDefinitions,
                hitCount
            );
            _targetEffectMetricsCache[cacheKey] = result.Clone();
        }
        else
        {
            result = BuildTargetEffectMetricsImpl(
                context,
                skillDefinition,
                sourceUnit,
                targetUnit,
                effectDefinitions,
                hitCount
            );
        }
        return result;
    }

    private static TargetEffectMetricsCacheKey BuildTargetEffectMetricsCacheKey(
        SkillDefinition skillDefinition,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        int hitCount
    )
    {
        return new TargetEffectMetricsCacheKey(
            ResolveSkillId(skillDefinition),
            ProgressionDataUtils.to_string_name(sourceUnit?.unit_id ?? ""),
            ProgressionDataUtils.to_string_name(targetUnit?.unit_id ?? ""),
            Math.Max(hitCount, 1),
            BuildCombatEffectSignature(effectDefinitions),
            BuildUnitEffectSignature(sourceUnit),
            BuildUnitEffectSignature(targetUnit)
        );
    }

    private static int BuildCombatEffectSignature(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        unchecked
        {
            int hash = 17;
            int count = 0;
            foreach (
                CombatEffectDefinition effectDefinition in effectDefinitions
                    ?? System.Array.Empty<CombatEffectDefinition>()
            )
            {
                count += 1;
                hash = hash * 31 + (int)(effectDefinition?.EffectKind ?? BattleEffectKind.Unknown);
                hash = hash * 31 + ProgressionDataUtils.to_string_name(effectDefinition?.EffectType ?? "").GetHashCode();
                hash = hash * 31 + ProgressionDataUtils.to_string_name(effectDefinition?.EffectTargetTeamFilter ?? "").GetHashCode();
                hash = hash * 31 + ProgressionDataUtils.to_string_name(effectDefinition?.StatusId ?? "").GetHashCode();
                hash = hash * 31 + ProgressionDataUtils.to_string_name(effectDefinition?.SaveFailureStatusId ?? "").GetHashCode();
                hash = hash * 31 + ProgressionDataUtils.to_string_name(effectDefinition?.TerrainEffectId ?? "").GetHashCode();
                hash = hash * 31 + ProgressionDataUtils.to_string_name(effectDefinition?.TerrainReplaceTo ?? "").GetHashCode();
                hash = hash * 31 + ProgressionDataUtils.to_string_name(effectDefinition?.DamageTag ?? "").GetHashCode();
                hash = hash * 31 + ProgressionDataUtils.to_string_name(effectDefinition?.DamageCategory ?? "").GetHashCode();
                hash = hash * 31 + ProgressionDataUtils.to_string_name(effectDefinition?.DrBypassTag ?? "").GetHashCode();
                hash = hash * 31 + ProgressionDataUtils.to_string_name(effectDefinition?.SaveAbility ?? "").GetHashCode();
                hash = hash * 31 + ProgressionDataUtils.to_string_name(effectDefinition?.SaveTag ?? "").GetHashCode();
                hash = hash * 31 + (effectDefinition?.Power ?? 0);
                hash = hash * 31 + (effectDefinition?.DiceCount ?? 0);
                hash = hash * 31 + (effectDefinition?.DiceSides ?? 0);
                hash = hash * 31 + (effectDefinition?.DiceBonus ?? 0);
                hash = hash * 31 + (effectDefinition?.DamageRatioPercent ?? 0);
                hash = hash * 31 + (effectDefinition?.HeightDelta ?? 0);
                hash = hash * 31 + (effectDefinition?.SaveDc ?? 0);
                hash = hash * 31 + (effectDefinition?.SavePartialOnSuccess == true ? 1 : 0);
                hash = hash * 31 + BuildStringNameListSignature(effectDefinition?.EffectTags);
            }
            return hash * 31 + count;
        }
    }

    private static int BuildUnitEffectSignature(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return 0;
        }
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + ProgressionDataUtils.to_string_name(unitState.unit_id).GetHashCode();
            hash = hash * 31 + unitState.current_hp;
            hash = hash * 31 + unitState.current_shield_hp;
            hash = hash * 31 + unitState.current_stamina;
            hash = hash * 31 + unitState.current_mp;
            foreach (StringName statusId in unitState.GetSortedStatusEffectIdsTyped())
            {
                BattleStatusEffectState status = unitState.GetStatusEffect(statusId);
                hash = hash * 31 + ProgressionDataUtils.to_string_name(statusId).GetHashCode();
                hash = hash * 31 + (status?.power ?? 0);
                hash = hash * 31 + (status?.stacks ?? 0);
                hash = hash * 31 + (status?.range_bonus ?? 0);
                hash = hash * 31 + (status?.death_prevention_priority ?? 0);
                hash = hash * 31 + (status?.save_bonus ?? 0);
                hash = hash * 31 + (status?.control_save_bonus ?? 0);
                hash = hash * 31 + BuildStringNameListSignature(status?.save_advantage_tags);
                hash = hash * 31 + BuildStringNameListSignature(status?.save_disadvantage_tags);
                hash = hash * 31 + BuildStringNameListSignature(status?.save_immunity_tags);
                hash = hash * 31 + BuildStringNameListSignature(status?.save_tags);
            }
            hash = hash * 31 + BuildStringNameArraySignature(unitState.save_advantage_tags);
            return hash;
        }
    }

    private static int BuildStringNameArraySignature(GStringNameArray values)
    {
        unchecked
        {
            int hash = 17;
            if (values == null)
            {
                return hash;
            }
            foreach (StringName value in values)
            {
                hash = hash * 31 + ProgressionDataUtils.to_string_name(value).GetHashCode();
            }
            return hash;
        }
    }

    private static int BuildStringNameListSignature(IReadOnlyList<StringName> values)
    {
        unchecked
        {
            int hash = 17;
            if (values == null)
            {
                return hash;
            }
            foreach (StringName value in values)
            {
                hash = hash * 31 + ProgressionDataUtils.to_string_name(value).GetHashCode();
            }
            return hash;
        }
    }

    private TargetEffectMetrics BuildTargetEffectMetricsImpl(
        IBattleAiScoreContext context,
        SkillDefinition skillDefinition,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        int hitCount = 1
    )
    {
        var metrics = new TargetEffectMetrics();
        if (sourceUnit == null || targetUnit == null || hitCount <= 0)
        {
            return metrics;
        }
        var damageEffects = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? System.Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition == null
                || effectDefinition.EffectKind == BattleEffectKind.ChainDamage
            )
            {
                continue;
            }
            StringName targetFilter = ResolveEffectTargetFilter(skillDefinition, effectDefinition);
            if (!IsUnitValidForEffect(sourceUnit, targetUnit, targetFilter))
            {
                continue;
            }
            BattleEffectKind effectKind = effectDefinition.EffectKind;
            if (effectKind == BattleEffectKind.Damage)
            {
                metrics.IsEmpty = false;
                damageEffects.Add(effectDefinition);
            }
            else if (effectKind == BattleEffectKind.Execute)
            {
                TargetEffectMetrics executeMetrics = EstimateExecuteForTargetResult(
                    skillDefinition,
                    sourceUnit,
                    targetUnit,
                    effectDefinition,
                    hitCount
                );
                if (!executeMetrics.IsEmpty)
                {
                    return executeMetrics;
                }
            }
            else if (effectKind == BattleEffectKind.GradedSaveExecute)
            {
                TargetEffectMetrics executeMetrics = EstimateGradedSaveExecuteForTargetResult(
                    skillDefinition,
                    sourceUnit,
                    targetUnit,
                    effectDefinition,
                    hitCount
                );
                if (!executeMetrics.IsEmpty)
                {
                    return executeMetrics;
                }
            }
            else if (effectKind == BattleEffectKind.Heal)
            {
                metrics.IsEmpty = false;
                metrics.Healing += EstimateRecoveryAmount(effectDefinition, sourceUnit) * hitCount;
            }
            else if (
                effectKind == BattleEffectKind.Status
                || effectKind == BattleEffectKind.ApplyStatus
                || effectKind == BattleEffectKind.ForcedMove
            )
            {
                if (IsBeneficialEffectFilter(targetFilter))
                {
                    metrics.IsEmpty = false;
                    metrics.BeneficialControlCount += hitCount;
                }
                else
                {
                    metrics.IsEmpty = false;
                    metrics.HarmfulControlCount += hitCount;
                }
            }
            else if (
                effectKind == BattleEffectKind.Shield
                || effectKind == BattleEffectKind.LayeredBarrier
                || effectKind == BattleEffectKind.StaminaRestore
                || effectKind == BattleEffectKind.BodySizeCategoryOverride
            )
            {
                metrics.IsEmpty = false;
                metrics.BeneficialControlCount += hitCount;
            }
            else if (
                effectKind == BattleEffectKind.Terrain
                || effectKind == BattleEffectKind.TerrainEffect
            )
            {
                metrics.IsEmpty = false;
                metrics.TerrainEffectCount += hitCount;
            }
            else if (
                effectKind == BattleEffectKind.Height
                || effectKind == BattleEffectKind.HeightDelta
            )
            {
                metrics.IsEmpty = false;
                metrics.HeightDelta += Math.Abs(effectDefinition.HeightDelta) * hitCount;
            }
        }
        if (damageEffects.Count > 0)
        {
            DamageEstimateResult estimateResult = EstimateDamageForTargetResult(
                sourceUnit,
                RepeatEffectDefinitions(damageEffects, hitCount),
                targetUnit,
                ResolveSkillId(skillDefinition)
            );
            int damage = estimateResult.Damage;
            metrics.Damage += damage;
            metrics.PostSaveDamage += estimateResult.PostSaveDamage;
            metrics.ShieldAbsorbed += estimateResult.ShieldAbsorbed;
            metrics.StableLethal = metrics.StableLethal || estimateResult.StableLethal;
            metrics.SaveEstimates.AddRange(ScaleSaveEstimates(estimateResult.SaveEstimates, 1));
            metrics.DamageEstimates = CloneDamageEstimates(estimateResult.DamageEstimates);
        }
        return metrics;
    }

    private TargetEffectMetrics EstimateExecuteForTargetResult(
        SkillDefinition skillDefinition,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        int hitCount
    )
    {
        var empty = new TargetEffectMetrics { IsEmpty = true, IsExecute = true };
        if (
            skillDefinition == null
            || sourceUnit == null
            || targetUnit == null
            || effectDefinition == null
        )
        {
            return empty;
        }
        StringName skillId = ResolveSkillId(skillDefinition);
        BattleExecutionRuleParams parameters = BattleExecutionRuleParams.FromEffect(
            effectDefinition,
            skillId
        );
        BattleExecutePlan plan = BattleExecutionRules.BuildExecutePlan(
            sourceUnit,
            targetUnit,
            parameters
        );
        if (!plan.CanExecute)
        {
            return empty;
        }

        DamageSaveEstimate saveEstimate = BuildDamageSaveEstimate(
            sourceUnit,
            targetUnit,
            effectDefinition,
            plan.FatalDamage,
            skillId
        );
        int saveFailureBps = saveEstimate?.HasSave == true
            ? Mathf.Clamp(saveEstimate.SaveFailureProbabilityBasisPoints, 0, 10000)
            : 10000;
        int protectionPenaltyBps = EstimateDeathProtectionPenaltyBasisPoints(targetUnit);
        int killBps = Mathf.Clamp(saveFailureBps - protectionPenaltyBps, 0, 10000);
        int expectedDamage = plan.FatalDamage * saveFailureBps / 10000;
        int scaledHitCount = Math.Max(hitCount, 1);
        var saveEstimates = new List<DamageSaveEstimate>();
        if (saveEstimate != null)
        {
            saveEstimates.Add(saveEstimate.Scaled(scaledHitCount));
        }

        return new TargetEffectMetrics
        {
            IsEmpty = false,
            IsExecute = true,
            Damage = expectedDamage * scaledHitCount,
            PostSaveDamage = expectedDamage * scaledHitCount,
            StableLethal = killBps >= 10000,
            KillProbabilityBasisPoints = killBps,
            SoulFractureApplied = plan.SoulFractureParams.HasValue,
            HarmfulControlCount = plan.SoulFractureParams.HasValue ? 1 : 0,
            SaveEstimates = saveEstimates,
            DamageEstimates = new List<DamageEstimateBreakdown>
            {
                new()
                {
                    HpDamage = expectedDamage * scaledHitCount,
                    Damage = expectedDamage * scaledHitCount,
                    PostSaveDamage = expectedDamage * scaledHitCount,
                    IncomingBudgetDamage = expectedDamage * scaledHitCount,
                    ShieldAbsorbed = 0,
                    StableLethal = killBps >= 10000,
                    LethalProbabilityBasisPoints = killBps,
                    SaveEstimates = saveEstimates,
                },
            },
        };
    }

    private TargetEffectMetrics EstimateGradedSaveExecuteForTargetResult(
        SkillDefinition skillDefinition,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        int hitCount
    )
    {
        var empty = new TargetEffectMetrics { IsEmpty = true, IsExecute = true };
        if (
            skillDefinition == null
            || sourceUnit == null
            || targetUnit == null
            || effectDefinition == null
        )
        {
            return empty;
        }
        if (
            !PhantasmalKillExecutionRules.TryReadPhantasmalKillProfile(
                effectDefinition,
                out PhantasmalKillExecutionProfile profile,
                out _
            )
        )
        {
            return empty;
        }

        StringName skillId = ResolveSkillId(skillDefinition);
        BattleSaveContext saveContext = BattleSaveContext.ForSkill(skillId);
        BattleGradedSaveGradeDistribution distribution =
            PhantasmalKillExecutionRules.EstimateGradeDistribution(
                sourceUnit,
                targetUnit,
                effectDefinition,
                saveContext
            );
        if (distribution.ImmuneBasisPoints >= 10000)
        {
            return empty;
        }

        int scaledHitCount = Math.Max(hitCount, 1);
        int failureDamage = PhantasmalKillExecutionRules.EstimateAverageDiceDamage(
            profile.FailureDamageDiceCount,
            profile.FailureDamageDiceSides
        );
        int criticalFailureDamage = PhantasmalKillExecutionRules.EstimateAverageDiceDamage(
            profile.CriticalFailureDamageDiceCount,
            profile.CriticalFailureDamageDiceSides
        );
        int expectedDamage = RoundToInt(
            (
                failureDamage * (double)distribution.FailureBasisPoints
                + criticalFailureDamage * (double)distribution.CriticalFailureBasisPoints
            ) / 10000.0
        );
        int targetMaxHp = GetUnitMaxHp(targetUnit);
        int failureExecuteThreshold =
            PhantasmalKillExecutionRules.ResolveFailureExecuteThreshold(profile, targetMaxHp);
        int criticalFailureExecuteThreshold =
            PhantasmalKillExecutionRules.ResolveCriticalFailureExecuteThreshold(
                profile,
                targetMaxHp
            );
        int killBasisPoints = 0;
        if (targetUnit.current_hp <= failureExecuteThreshold)
        {
            killBasisPoints += distribution.FailureBasisPoints;
        }
        if (targetUnit.current_hp <= criticalFailureExecuteThreshold)
        {
            killBasisPoints += distribution.CriticalFailureBasisPoints;
        }
        killBasisPoints = Mathf.Clamp(killBasisPoints, 0, 10000);

        int controlCount = RoundToInt(
            (
                distribution.SuccessBasisPoints
                + 2.0 * distribution.FailureBasisPoints
                + 2.0 * distribution.CriticalFailureBasisPoints
            ) / 10000.0
        );
        DamageSaveEstimate saveEstimate = BuildGradedSaveExecuteEstimate(
            sourceUnit,
            targetUnit,
            effectDefinition,
            skillId,
            failureDamage,
            criticalFailureDamage,
            expectedDamage,
            distribution
        );
        var saveEstimates = new List<DamageSaveEstimate>();
        if (saveEstimate != null)
        {
            saveEstimates.Add(saveEstimate.Scaled(scaledHitCount));
        }

        int scaledDamage = expectedDamage * scaledHitCount;
        return new TargetEffectMetrics
        {
            IsEmpty = false,
            IsExecute = true,
            Damage = scaledDamage,
            PostSaveDamage = scaledDamage,
            StableLethal = killBasisPoints >= 10000,
            KillProbabilityBasisPoints = killBasisPoints,
            HarmfulControlCount = controlCount * scaledHitCount,
            SaveEstimates = saveEstimates,
            DamageEstimates = new List<DamageEstimateBreakdown>
            {
                new()
                {
                    HpDamage = scaledDamage,
                    Damage = scaledDamage,
                    PostSaveDamage = scaledDamage,
                    IncomingBudgetDamage = scaledDamage,
                    ShieldAbsorbed = 0,
                    StableLethal = killBasisPoints >= 10000,
                    LethalProbabilityBasisPoints = killBasisPoints,
                    SaveEstimates = CloneSaveEstimates(saveEstimates),
                },
            },
        };
    }

    private static DamageSaveEstimate BuildGradedSaveExecuteEstimate(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        StringName skillId,
        int failureDamage,
        int criticalFailureDamage,
        int expectedDamage,
        BattleGradedSaveGradeDistribution distribution
    )
    {
        BattleSaveProbabilityResult probability =
            BattleSaveResolver.EstimateSaveSuccessProbabilityResult(
                sourceUnit,
                targetUnit,
                effectDefinition,
                BattleSaveContext.ForSkill(skillId)
            );
        if (!probability.HasSave)
        {
            return null;
        }
        int successBps = Mathf.Clamp(
            distribution.CriticalSuccessBasisPoints + distribution.SuccessBasisPoints,
            0,
            10000
        );
        int failureBps = Mathf.Clamp(
            distribution.FailureBasisPoints + distribution.CriticalFailureBasisPoints,
            0,
            10000
        );
        return new DamageSaveEstimate
        {
            HasSave = true,
            DamageBeforeSave = Math.Max(criticalFailureDamage, failureDamage),
            DamageAfterSaveEstimate = Math.Max(expectedDamage, 0),
            DamageOnSaveFailure = Math.Max(failureDamage, 0),
            DamageOnSaveSuccess = 0,
            SavePartialOnSuccess = false,
            SaveSuccessProbabilityBasisPoints = successBps,
            SaveSuccessRatePercent = RoundToInt(successBps / 100.0),
            SaveFailureProbabilityBasisPoints = failureBps,
            Dc = probability.Dc,
            Ability = probability.Ability.ToString(),
            SaveTag = probability.SaveTag.ToString(),
            AdvantageState = probability.AdvantageState.ToString(),
            AbilityValue = probability.AbilityValue,
            AbilityModifier = probability.AbilityModifier,
            Bonus = probability.Bonus,
            Immune = probability.Immune,
            HitCount = 1,
        };
    }

    private static int EstimateDeathProtectionPenaltyBasisPoints(BattleUnitState targetUnit)
    {
        if (targetUnit == null)
        {
            return 0;
        }
        DeathResolutionContext executeContext =
            BattleDeathResolutionRules.PowerWordKillExecuteContext();
        foreach (StringName statusId in targetUnit.GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState status = targetUnit.GetStatusEffect(statusId);
            int priority = Math.Max(status?.death_prevention_priority ?? 0, 0);
            if (
                priority > 0
                && BattleDeathResolutionRules.CanDeathPreventionBlock(executeContext, priority)
            )
            {
                return 10000;
            }
        }
        return 0;
    }

    private static int EstimateRecoveryAmount(
        CombatEffectDefinition effectDefinition,
        BattleUnitState sourceUnit
    )
    {
        if (effectDefinition == null)
        {
            return 0;
        }
        if (HasAttributeScaledDiceConfig(effectDefinition))
        {
            int diceCount = Math.Max(effectDefinition.DiceCount, 1);
            int diceSides = EstimateAttributeScaledDiceSides(effectDefinition, sourceUnit);
            return Math.Max((int)Math.Round(diceCount * (diceSides + 1) / 2.0), 1);
        }
        int amount = Math.Max(effectDefinition.Power, 0);
        if (effectDefinition.DiceCount > 0 && effectDefinition.DiceSides > 0)
        {
            amount += (int)Math.Round(
                Math.Max(effectDefinition.DiceCount, 0)
                    * (Math.Max(effectDefinition.DiceSides, 0) + 1)
                    / 2.0
            );
        }
        return Math.Max(amount, 1);
    }

    private static bool HasAttributeScaledDiceConfig(CombatEffectDefinition effectDefinition)
    {
        return effectDefinition != null
            && effectDefinition.DiceCount > 0
            && effectDefinition.DiceSidesBase > 0;
    }

    private static int EstimateAttributeScaledDiceSides(
        CombatEffectDefinition effectDefinition,
        BattleUnitState sourceUnit
    )
    {
        int conMod = GetBaseAttributeModifier(sourceUnit, UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution));
        int willMod = GetBaseAttributeModifier(sourceUnit, UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower));
        int baseSides = Math.Max(effectDefinition?.DiceSidesBase ?? 0, 0);
        int conModSides = Math.Max(effectDefinition?.DiceSidesPerConstitutionMod ?? 0, 0);
        int willModSides = Math.Max(effectDefinition?.DiceSidesPerWillpowerMod ?? 0, 0);
        long diceSidesRaw =
            (long)baseSides + (long)conMod * conModSides + (long)willMod * willModSides;
        return (int)Math.Clamp(diceSidesRaw, 4L, int.MaxValue);
    }

    private static int GetBaseAttributeModifier(BattleUnitState unitState, StringName attributeId)
    {
        if (unitState?.attribute_snapshot == null || attributeId == "")
        {
            return 0;
        }
        StringName modifierId = AttributeSnapshot.GetBaseAttributeModifierId(attributeId);
        return modifierId == "" ? 0 : unitState.attribute_snapshot.GetValue(modifierId);
    }

    private static List<CombatEffectDefinition> RepeatEffectDefinitions(
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        int hitCount
    )
    {
        var repeated = new List<CombatEffectDefinition>();
        int safeHitCount = Math.Max(hitCount, 1);
        for (int i = 0; i < safeHitCount; i += 1)
        {
            foreach (
                CombatEffectDefinition effectDefinition in effectDefinitions
                    ?? System.Array.Empty<CombatEffectDefinition>()
            )
            {
                if (effectDefinition != null)
                {
                    repeated.Add(effectDefinition);
                }
            }
        }
        return repeated;
    }

    private static StringName ResolveEffectTargetFilter(
        SkillDefinition skillDefinition,
        CombatEffectDefinition effectDefinition
    )
    {
        StringName resolved =
            effectDefinition != null && !IsEmpty(effectDefinition.EffectTargetTeamFilter)
                ? effectDefinition.EffectTargetTeamFilter
                : skillDefinition?.CombatProfile?.TargetTeamFilter ?? new StringName("");
        if (!IsEmpty(resolved))
        {
            return resolved;
        }
        BattleEffectKind effectKind = effectDefinition?.EffectKind ?? BattleEffectKind.Unknown;
        if (
            effectKind == BattleEffectKind.Heal
            || effectKind == BattleEffectKind.Shield
            || effectKind == BattleEffectKind.LayeredBarrier
            || effectKind == BattleEffectKind.StaminaRestore
        )
        {
            return "ally";
        }
        return "enemy";
    }

    private static bool IsUnitValidForEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        StringName targetFilter
    )
    {
        return BattleTargetTeamRules.IsUnitValidForFilter(
            sourceUnit,
            targetUnit,
            targetFilter,
            default
        );
    }

    private static bool IsBeneficialEffectFilter(StringName targetFilter)
    {
        return BattleTargetTeamRules.IsBeneficialFilter(targetFilter);
    }

    private void PopulateChainDamageMetrics(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        BattleState state = ContextState(context);
        BattleUnitState actor = ContextUnitState(context);
        if (scoreInput == null || state == null || actor == null)
        {
            return;
        }
        List<CombatEffectDefinition> chainEffects = CollectChainDamageEffectDefinitions(
            effectDefinitions
        );
        if (chainEffects.Count == 0)
        {
            return;
        }
        foreach (CombatEffectDefinition chainEffect in chainEffects)
        {
            List<CombatEffectDefinition> chainTargetEffects = BuildChainTargetEffectDefinitions(
                effectDefinitions,
                chainEffect
            );
            if (chainTargetEffects.Count == 0)
            {
                continue;
            }
            foreach (StringName primaryTargetId in scoreInput.target_unit_ids)
            {
                BattleUnitState primaryTarget = GetUnit(state, primaryTargetId);
                if (primaryTarget == null)
                {
                    continue;
                }
                foreach (
                    BattleUnitState chainTarget in CollectChainDamageTargets(
                        context,
                        primaryTarget,
                        skillDefinition ?? ResolveScoreInputSkillDefinition(scoreInput, context),
                        chainEffect
                    )
                )
                {
                    PopulateTargetEffectMetrics(
                        scoreInput,
                        context,
                        chainTarget,
                        chainTargetEffects,
                        1,
                        true,
                        skillDefinition: skillDefinition
                    );
                }
            }
        }
    }

    private static List<CombatEffectDefinition> CollectChainDamageEffectDefinitions(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        var chainEffects = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? System.Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition != null
                && effectDefinition.EffectKind == BattleEffectKind.ChainDamage
            )
            {
                chainEffects.Add(effectDefinition);
            }
        }
        return chainEffects;
    }

    private static List<CombatEffectDefinition> BuildChainTargetEffectDefinitions(
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        CombatEffectDefinition chainEffect
    )
    {
        var chainTargetEffects = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? System.Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition == null
                || effectDefinition == chainEffect
                || effectDefinition.EffectKind == BattleEffectKind.ChainDamage
            )
            {
                continue;
            }
            chainTargetEffects.Add(effectDefinition);
        }
        return chainTargetEffects;
    }

    private List<BattleUnitState> CollectChainDamageTargets(
        IBattleAiScoreContext context,
        BattleUnitState primaryTarget,
        SkillDefinition skillDefinition,
        CombatEffectDefinition chainEffect
    )
    {
        var targets = new List<BattleUnitState>();
        BattleState state = ContextState(context);
        BattleUnitState actor = ContextUnitState(context);
        if (state == null || actor == null || primaryTarget == null || chainEffect == null)
        {
            return targets;
        }
        ChainDamageParameters chainParameters = ChainDamageParameters.FromEffect(chainEffect);
        int maxRadius = ResolveChainDamageRadius(context, primaryTarget, chainParameters);
        if (maxRadius <= 0)
        {
            return targets;
        }
        StringName targetFilter = ResolveEffectTargetFilter(skillDefinition, chainEffect);
        var visited = new HashSet<StringName> { primaryTarget.unit_id };
        var queue = new Queue<BattleUnitState>();
        queue.Enqueue(primaryTarget);
        while (queue.Count > 0)
        {
            BattleUnitState current = queue.Dequeue();
            foreach (BattleUnitState candidate in state.GetUnitsTyped())
            {
                if (candidate == null || !candidate.is_alive)
                {
                    continue;
                }
                if (chainParameters.PreventRepeatTarget && visited.Contains(candidate.unit_id))
                {
                    continue;
                }
                if (!IsUnitValidForEffect(actor, candidate, targetFilter))
                {
                    continue;
                }
                if (!IsWithinChainRadius(context, primaryTarget, candidate, maxRadius))
                {
                    continue;
                }
                if (!IsChainPathClear(context, current, candidate))
                {
                    continue;
                }
                visited.Add(candidate.unit_id);
                targets.Add(candidate);
                queue.Enqueue(candidate);
            }
        }
        targets.Sort(
            (left, right) =>
            {
                int leftDistance = DistanceBetweenUnits(context, primaryTarget, left);
                int rightDistance = DistanceBetweenUnits(context, primaryTarget, right);
                if (leftDistance != rightDistance)
                {
                    return leftDistance.CompareTo(rightDistance);
                }
                if (left.coord.Y != right.coord.Y)
                {
                    return left.coord.Y.CompareTo(right.coord.Y);
                }
                if (left.coord.X != right.coord.X)
                {
                    return left.coord.X.CompareTo(right.coord.X);
                }
                return string.CompareOrdinal(left.unit_id.ToString(), right.unit_id.ToString());
            }
        );
        return targets;
    }

    private int ResolveChainDamageRadius(
        IBattleAiScoreContext context,
        BattleUnitState primaryTarget,
        ChainDamageParameters chainParameters
    )
    {
        if (
            !IsEmpty(chainParameters.BonusTerrainEffectId)
            && UnitStandsOnTerrainEffect(
                context,
                primaryTarget,
                chainParameters.BonusTerrainEffectId
            )
        )
        {
            return chainParameters.WetChainRadius;
        }
        return chainParameters.BaseRadius;
    }

    private static bool UnitStandsOnTerrainEffect(
        IBattleAiScoreContext context,
        BattleUnitState unitState,
        StringName terrainEffectId
    )
    {
        BattleState state = ContextState(context);
        BattleGridService gridService = ContextGridService(context);
        if (state == null || gridService == null || unitState == null || IsEmpty(terrainEffectId))
        {
            return false;
        }
        unitState.RefreshFootprint();
        foreach (Vector2I occupiedCoord in unitState.occupied_coords)
        {
            BattleCellState cell = gridService.GetCellState(state, occupiedCoord);
            if (cell == null)
            {
                continue;
            }
            if (cell.terrain_effect_ids.Contains(terrainEffectId))
            {
                return true;
            }
            foreach (BattleTerrainEffectState effectState in cell.timed_terrain_effects)
            {
                if (effectState != null && effectState.effect_id == terrainEffectId)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool IsWithinChainRadius(
        IBattleAiScoreContext context,
        BattleUnitState primaryTarget,
        BattleUnitState candidate,
        int maxRadius
    )
    {
        BattleGridService gridService = ContextGridService(context);
        if (gridService == null || primaryTarget == null || candidate == null || maxRadius <= 0)
        {
            return false;
        }
        primaryTarget.RefreshFootprint();
        candidate.RefreshFootprint();
        foreach (Vector2I primaryCoord in primaryTarget.occupied_coords)
        {
            foreach (Vector2I candidateCoord in candidate.occupied_coords)
            {
                if (gridService.GetDistance(primaryCoord, candidateCoord) <= maxRadius)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static List<Vector2I> GetLineCoords(Vector2I from, Vector2I to)
    {
        var coords = new List<Vector2I>();
        int dx = Math.Abs(to.X - from.X);
        int dy = Math.Abs(to.Y - from.Y);
        int sx = from.X < to.X ? 1 : -1;
        int sy = from.Y < to.Y ? 1 : -1;
        int err = dx - dy;
        int x = from.X;
        int y = from.Y;
        while (x != to.X || y != to.Y)
        {
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
            if (x == to.X && y == to.Y)
            {
                break;
            }
            coords.Add(new Vector2I(x, y));
        }
        return coords;
    }

    private static bool IsChainPathClear(
        IBattleAiScoreContext context,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit
    )
    {
        BattleState state = ContextState(context);
        BattleGridService gridService = ContextGridService(context);
        if (state == null || gridService == null || sourceUnit == null || targetUnit == null)
        {
            return false;
        }
        sourceUnit.RefreshFootprint();
        targetUnit.RefreshFootprint();
        foreach (Vector2I sourceCoord in sourceUnit.occupied_coords)
        {
            BattleCellState sourceCell = gridService.GetCellState(state, sourceCoord);
            if (sourceCell == null)
            {
                continue;
            }
            int sourceHeight = sourceCell.current_height;
            foreach (Vector2I targetCoord in targetUnit.occupied_coords)
            {
                foreach (Vector2I midCoord in GetLineCoords(sourceCoord, targetCoord))
                {
                    BattleCellState midCell = gridService.GetCellState(state, midCoord);
                    if (midCell == null)
                    {
                        continue;
                    }
                    if (Math.Abs(midCell.current_height - sourceHeight) > 1)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }
}
