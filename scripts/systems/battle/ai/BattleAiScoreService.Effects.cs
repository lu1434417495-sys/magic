using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public partial class BattleAiScoreService
{
    private sealed class TargetEffectMetrics
    {
        public bool IsEmpty = true;
        public int Damage;
        public int PostSaveDamage;
        public int ShieldAbsorbed;
        public bool StableLethal;
        public int Healing;
        public int HarmfulControlCount;
        public int BeneficialControlCount;
        public int TerrainEffectCount;
        public int HeightDelta;
        public List<DamageSaveEstimate> SaveEstimates = new();
        public List<DamageEstimateBreakdown> DamageEstimates = new();
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
        public static ChainDamageParameters FromEffect(CombatEffectDef effectDef)
        {
            GDictionary parameters = GetEffectParams(effectDef);
            int baseRadius = Math.Max(DictInt(parameters, "base_chain_radius", 1), 0);
            return new ChainDamageParameters(
                baseRadius,
                DictStringName(parameters, "bonus_terrain_effect_id", ""),
                Math.Max(DictInt(parameters, "wet_chain_radius", baseRadius), baseRadius),
                effectDef?.prevent_repeat_target ?? true
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
        scoreInput.hit_payoff_score -= Math.Max(_scoreProfile.target_count_weight, 0);
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
        GArray reasons = ResolveMeteorHighPriorityReasons(
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
        scoreInput.high_priority_reasons[targetUnit.unit_id.ToString()] = reasons;
    }

    private GArray ResolveMeteorHighPriorityReasons(
        IBattleAiScoreContext context,
        BattleUnitState targetUnit,
        MeteorSwarmNumericSummary summary,
        int targetPriorityScore
    )
    {
        var reasons = new GArray();
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
            && threatMultiplier >= _scoreProfile.meteor_high_priority_threat_multiplier_bp
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
            centerDirectHpPercent >= _scoreProfile.meteor_high_priority_damage_hp_percent
            && roleSummary.HasHighThreatRole
        )
        {
            reasons.Add("center_direct_high_role_damage");
        }
        if (targetPriorityScore >= _scoreProfile.meteor_high_priority_target_priority_score)
        {
            reasons.Add("target_priority_score");
        }
        int threatRank = ResolveMeteorThreatRank(context, targetUnit);
        if (threatRank > 0 && threatRank <= Math.Max(_scoreProfile.meteor_top_threat_rank, 0))
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
        GDictionary skillDefs = ContextSkillDefs(context);
        foreach (StringName skillId in targetUnit.known_active_skill_ids)
        {
            StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (IsEmpty(normalizedSkillId))
            {
                continue;
            }
            SkillDef skillDef = GetSkillDef(skillDefs, normalizedSkillId);
            if (skillDef == null || skillDef.combat_profile == null)
            {
                continue;
            }
            List<CombatEffectDef> roleEffectDefs = CollectRoleThreatEffectDefs(
                targetUnit,
                skillDef
            );
            if (IsHealOrSupportSkill(skillDef, roleEffectDefs))
            {
                summary.HealSkillCount += 1;
            }
            if (IsControlSkill(roleEffectDefs))
            {
                summary.ControlSkillCount += 1;
            }
            if (IsDamageSkill(roleEffectDefs))
            {
                int effectiveRange = BattleRangeService.get_effective_skill_threat_range(
                    targetUnit,
                    skillDef
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
        return targetUnit.attribute_snapshot.get_value(BossTargetStatId) > 0
            || targetUnit.attribute_snapshot.get_value(FortuneMarkTargetStatId)
                > 0;
    }

    private int ResolveMeteorThreatRank(IBattleAiScoreContext context, BattleUnitState targetUnit)
    {
        AiTraceRecorder.enter("_resolve_meteor_threat_rank");
        int result = ResolveMeteorThreatRankImpl(context, targetUnit);
        AiTraceRecorder.exit("_resolve_meteor_threat_rank");
        return result;
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
        return ReadTargetNumericSummaries(meteorFacts.target_numeric_summary);
    }

    private static List<MeteorSwarmNumericSummary> ReadTargetNumericSummaries(
        IEnumerable<GDictionary> summaries
    )
    {
        var result = new List<MeteorSwarmNumericSummary>();
        foreach (GDictionary summary in summaries ?? System.Array.Empty<GDictionary>())
        {
            if (summary == null)
            {
                continue;
            }
            result.Add(MeteorSwarmNumericSummary.FromDictionary(summary));
        }
        return result;
    }

    private static GArray TargetNumericSummariesToArray(
        IEnumerable<MeteorSwarmNumericSummary> summaries
    )
    {
        var result = new GArray();
        foreach (
            MeteorSwarmNumericSummary summary in summaries
                ?? System.Array.Empty<MeteorSwarmNumericSummary>()
        )
        {
            if (summary != null)
            {
                result.Add(summary.ToDictionary());
            }
        }
        return result;
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
        if (_scoreProfile != null && _scoreProfile.meteor_friendly_fire_profile != "reckless")
        {
            if (
                summary.ExpectedDamageHpPercent
                >= _scoreProfile.meteor_friendly_fire_hard_expected_hp_percent
            )
            {
                return $"meteor_swarm_friendly_fire_expected_threshold:{targetLabel}";
            }
            if (
                summary.WorstCaseDamageHpPercent
                >= _scoreProfile.meteor_friendly_fire_hard_worst_case_hp_percent
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
            && targetUnit.attribute_snapshot.get_value("protected_ally") > 0;
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
        return DictStringName(scoreInput.special_profile_preview_facts, "profile_id", "")
            == MeteorSwarmProfileId;
    }

    private static int GetUnitMaxHp(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return 1;
        }
        if (unitState.attribute_snapshot != null)
        {
            int maxHp = unitState.attribute_snapshot.get_value("hp_max");
            if (maxHp > 0)
            {
                return maxHp;
            }
        }
        return Math.Max(unitState.current_hp, 1);
    }

    private void PopulateTargetEffectMetrics(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        BattleUnitState targetUnit,
        IReadOnlyList<CombatEffectDef> effectDefs,
        int hitCount = 1,
        bool isChainTarget = false
    )
    {
        AiTraceRecorder.enter("_populate_target_effect_metrics");
        PopulateTargetEffectMetricsImpl(
            scoreInput,
            context,
            targetUnit,
            effectDefs,
            hitCount,
            isChainTarget
        );
        AiTraceRecorder.exit("_populate_target_effect_metrics");
    }

    private void PopulateTargetEffectMetricsImpl(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        BattleUnitState targetUnit,
        IReadOnlyList<CombatEffectDef> effectDefs,
        int hitCount = 1,
        bool isChainTarget = false
    )
    {
        BattleUnitState actor = ContextUnitState(context);
        if (scoreInput == null || actor == null || targetUnit == null || hitCount <= 0)
        {
            return;
        }
        TargetEffectMetrics targetMetrics = BuildTargetEffectMetrics(
            context,
            scoreInput.skill_def as SkillDef,
            actor,
            targetUnit,
            effectDefs,
            hitCount
        );
        if (targetMetrics.IsEmpty)
        {
            return;
        }

        int estimatedDamage = targetMetrics.Damage;
        int estimatedPostSaveDamage = targetMetrics.PostSaveDamage;
        int estimatedShieldAbsorbed = targetMetrics.ShieldAbsorbed;
        bool stableLethal =
            targetMetrics.StableLethal || estimatedDamage >= Math.Max(targetUnit.current_hp, 1);
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
                beneficialControlCount
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
            stableLethal
        );
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
        bool stableLethal
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
        scoreInput.hit_payoff_score += estimatedDamage * _scoreProfile.damage_weight;
        scoreInput.hit_payoff_score -= estimatedHealing * _scoreProfile.heal_weight;
        scoreInput.hit_payoff_score += harmfulControlCount * _scoreProfile.status_weight;
        scoreInput.hit_payoff_score +=
            estimatedShieldAbsorbed * Math.Max(_scoreProfile.damage_weight / 4, 1);
        scoreInput.hit_payoff_score -= beneficialControlCount * _scoreProfile.status_weight;
        scoreInput.hit_payoff_score += estimatedTerrainEffectCount * _scoreProfile.terrain_weight;
        scoreInput.hit_payoff_score += estimatedHeightDelta * _scoreProfile.height_weight;
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
        int lethalBonus = stableLethal
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

    private void PopulateAllyTargetPayoff(
        BattleAiScoreInput scoreInput,
        BattleUnitState targetUnit,
        int estimatedDamage,
        int estimatedHealing,
        int harmfulControlCount,
        int beneficialControlCount
    )
    {
        bool hasAllyBenefit = estimatedHealing > 0 || beneficialControlCount > 0;
        if (hasAllyBenefit)
        {
            scoreInput.effective_target_count += 1;
            scoreInput.hit_payoff_score += estimatedHealing * _scoreProfile.heal_weight;
            scoreInput.hit_payoff_score += beneficialControlCount * _scoreProfile.status_weight;
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
            estimatedDamage * _scoreProfile.friendly_fire_damage_weight
            + _scoreProfile.friendly_fire_target_weight
            + harmfulControlCount * _scoreProfile.friendly_control_target_weight;
        if (estimatedDamage >= Math.Max(targetUnit.current_hp, 1))
        {
            scoreInput.estimated_friendly_lethal_target_count += 1;
            penalty += _scoreProfile.friendly_lethal_target_weight;
        }
        scoreInput.friendly_fire_penalty_score += penalty;
        scoreInput.hit_payoff_score -= penalty;
    }

    private TargetEffectMetrics BuildTargetEffectMetrics(
        IBattleAiScoreContext context,
        SkillDef skillDef,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        IReadOnlyList<CombatEffectDef> effectDefs,
        int hitCount = 1
    )
    {
        AiTraceRecorder.enter("_build_target_effect_metrics");
        TargetEffectMetrics result = BuildTargetEffectMetricsImpl(
            context,
            skillDef,
            sourceUnit,
            targetUnit,
            effectDefs,
            hitCount
        );
        AiTraceRecorder.exit("_build_target_effect_metrics");
        return result;
    }

    private TargetEffectMetrics BuildTargetEffectMetricsImpl(
        IBattleAiScoreContext context,
        SkillDef skillDef,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        IReadOnlyList<CombatEffectDef> effectDefs,
        int hitCount = 1
    )
    {
        var metrics = new TargetEffectMetrics();
        if (sourceUnit == null || targetUnit == null || hitCount <= 0)
        {
            return metrics;
        }
        var damageEffects = new List<CombatEffectDef>();
        foreach (CombatEffectDef effectDef in effectDefs ?? System.Array.Empty<CombatEffectDef>())
        {
            if (effectDef == null || effectDef.effect_type == ChainDamageEffectType)
            {
                continue;
            }
            StringName targetFilter = ResolveEffectTargetFilter(skillDef, effectDef);
            if (!IsUnitValidForEffect(sourceUnit, targetUnit, targetFilter))
            {
                continue;
            }
            metrics.IsEmpty = false;
            StringName effectType = ProgressionDataUtils.to_string_name(effectDef.effect_type);
            if (effectType == "damage")
            {
                damageEffects.Add(effectDef);
            }
            else if (effectType == "execute")
            {
                int burstDamage = Math.Max(DictInt(effectDef.@params, "burst_damage", 9999), 0);
                metrics.Damage += burstDamage * hitCount;
            }
            else if (effectType == "heal")
            {
                metrics.Healing += Math.Max(effectDef.power, 1) * hitCount;
            }
            else if (
                effectType == "status"
                || effectType == "apply_status"
                || effectType == "forced_move"
            )
            {
                if (IsBeneficialEffectFilter(targetFilter))
                {
                    metrics.BeneficialControlCount += hitCount;
                }
                else
                {
                    metrics.HarmfulControlCount += hitCount;
                }
            }
            else if (
                effectType == "shield"
                || effectType == "layered_barrier"
                || effectType == "stamina_restore"
                || effectType == "body_size_category_override"
            )
            {
                metrics.BeneficialControlCount += hitCount;
            }
            else if (effectType == "terrain" || effectType == "terrain_effect")
            {
                metrics.TerrainEffectCount += hitCount;
            }
            else if (effectType == "height" || effectType == "height_delta")
            {
                metrics.HeightDelta += Math.Abs(effectDef.height_delta) * hitCount;
            }
        }
        if (damageEffects.Count > 0)
        {
            DamageEstimateResult estimateResult = EstimateDamageForTargetResult(
                context,
                sourceUnit,
                RepeatEffects(damageEffects, hitCount),
                targetUnit,
                ResolveSkillId(skillDef)
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

    private static List<CombatEffectDef> RepeatEffects(
        IEnumerable<CombatEffectDef> effectDefs,
        int hitCount
    )
    {
        var repeated = new List<CombatEffectDef>();
        int safeHitCount = Math.Max(hitCount, 1);
        for (int i = 0; i < safeHitCount; i += 1)
        {
            foreach (CombatEffectDef effectDef in effectDefs ?? System.Array.Empty<CombatEffectDef>())
            {
                if (effectDef != null)
                {
                    repeated.Add(effectDef);
                }
            }
        }
        return repeated;
    }

    private static StringName ResolveEffectTargetFilter(
        SkillDef skillDef,
        CombatEffectDef effectDef
    )
    {
        StringName resolved = BattleTargetTeamRules.resolve_effect_target_filter(
            skillDef,
            effectDef
        );
        if (!IsEmpty(resolved))
        {
            return resolved;
        }
        StringName effectType = ProgressionDataUtils.to_string_name(
            effectDef != null ? effectDef.effect_type : ""
        );
        if (
            effectType == "heal"
            || effectType == "shield"
            || effectType == "layered_barrier"
            || effectType == "stamina_restore"
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
        return BattleTargetTeamRules.is_unit_valid_for_filter(
            sourceUnit,
            targetUnit,
            targetFilter,
            default
        );
    }

    private static bool IsBeneficialEffectFilter(StringName targetFilter)
    {
        return BattleTargetTeamRules.is_beneficial_filter(targetFilter);
    }

    private void PopulateChainDamageMetrics(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        IReadOnlyList<CombatEffectDef> effectDefs
    )
    {
        BattleState state = ContextState(context);
        BattleUnitState actor = ContextUnitState(context);
        if (scoreInput == null || state == null || actor == null)
        {
            return;
        }
        List<CombatEffectDef> chainEffects = CollectChainDamageEffectDefs(effectDefs);
        if (chainEffects.Count == 0)
        {
            return;
        }
        foreach (CombatEffectDef chainEffect in chainEffects)
        {
            List<CombatEffectDef> chainTargetEffects = BuildChainTargetEffectDefs(
                effectDefs,
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
                        scoreInput.skill_def as SkillDef,
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
                        true
                    );
                }
            }
        }
    }

    private static List<CombatEffectDef> CollectChainDamageEffectDefs(
        IEnumerable<CombatEffectDef> effectDefs
    )
    {
        var chainEffects = new List<CombatEffectDef>();
        foreach (CombatEffectDef effectDef in effectDefs ?? System.Array.Empty<CombatEffectDef>())
        {
            if (effectDef != null && effectDef.effect_type == ChainDamageEffectType)
            {
                chainEffects.Add(effectDef);
            }
        }
        return chainEffects;
    }

    private static List<CombatEffectDef> BuildChainTargetEffectDefs(
        IEnumerable<CombatEffectDef> effectDefs,
        CombatEffectDef chainEffect
    )
    {
        var chainTargetEffects = new List<CombatEffectDef>();
        foreach (CombatEffectDef effectDef in effectDefs ?? System.Array.Empty<CombatEffectDef>())
        {
            if (
                effectDef == null
                || effectDef == chainEffect
                || effectDef.effect_type == ChainDamageEffectType
            )
            {
                continue;
            }
            chainTargetEffects.Add(effectDef);
        }
        return chainTargetEffects;
    }

    private List<BattleUnitState> CollectChainDamageTargets(
        IBattleAiScoreContext context,
        BattleUnitState primaryTarget,
        SkillDef skillDef,
        CombatEffectDef chainEffect
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
        StringName targetFilter = ResolveEffectTargetFilter(skillDef, chainEffect);
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
        unitState.refresh_footprint();
        foreach (Vector2I occupiedCoord in unitState.occupied_coords)
        {
            BattleCellState cell = gridService.get_cell(state, occupiedCoord);
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

    private static GDictionary GetEffectParams(CombatEffectDef effectDef)
    {
        return effectDef?.@params ?? new GDictionary();
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
        primaryTarget.refresh_footprint();
        candidate.refresh_footprint();
        foreach (Vector2I primaryCoord in primaryTarget.occupied_coords)
        {
            foreach (Vector2I candidateCoord in candidate.occupied_coords)
            {
                if (gridService.get_distance(primaryCoord, candidateCoord) <= maxRadius)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static GVector2IArray GetLineCoords(Vector2I from, Vector2I to)
    {
        var coords = new GVector2IArray();
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
        sourceUnit.refresh_footprint();
        targetUnit.refresh_footprint();
        foreach (Vector2I sourceCoord in sourceUnit.occupied_coords)
        {
            BattleCellState sourceCell = gridService.get_cell(state, sourceCoord);
            if (sourceCell == null)
            {
                continue;
            }
            int sourceHeight = sourceCell.current_height;
            foreach (Vector2I targetCoord in targetUnit.occupied_coords)
            {
                foreach (Vector2I midCoord in GetLineCoords(sourceCoord, targetCoord))
                {
                    BattleCellState midCell = gridService.get_cell(state, midCoord);
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
