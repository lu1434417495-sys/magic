using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleAiTypedActionHelper
{
    private static readonly StringName EmptyStringName = "";
    private static readonly StringName TargetFilterAny = "any";
    private static readonly StringName TargetFilterSelf = "self";
    private static readonly StringName TargetFilterAlly = "ally";
    private static readonly StringName TargetFilterEnemy = "enemy";
    private static readonly StringName SelectorNearestEnemy = "nearest_enemy";
    private static readonly StringName SelectorLowestHpEnemy = "lowest_hp_enemy";
    private static readonly StringName SelectorNearestAlly = "nearest_ally";
    private static readonly StringName SelectorLowestHpAlly = "lowest_hp_ally";
    private static readonly StringName SelectorSelf = "self";
    private static readonly StringName SelectorNearestRoleThreatEnemy = "nearest_role_threat_enemy";

    private const int HpBasisPointsDenominator = 10000;
    private const int RoleThreatMinEffectiveRange = 4;
    private const int RoleThreatDistanceWindow = 4;
    private const int RoleThreatMaxApproachDistance = 7;
    private const int RoleThreatMaxContactRange = 2;

    public List<StringName> ResolveKnownSkillIds(
        BattleAiContext context,
        IEnumerable<StringName> preferredSkillIds
    )
    {
        var results = new List<StringName>();
        BattleUnitState unitState = context?.unit_state;
        if (unitState == null)
            return results;

        var seen = new HashSet<StringName>();
        List<StringName> preferred = CopyStringNameList(preferredSkillIds);
        IEnumerable<StringName> sourceIds =
            preferred.Count > 0 ? preferred : unitState.known_active_skill_ids;
        foreach (StringName rawSkillId in sourceIds)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            if (IsEmpty(skillId) || seen.Contains(skillId))
                continue;
            seen.Add(skillId);
            if (unitState.known_active_skill_ids.Contains(skillId))
                results.Add(skillId);
        }
        return results;
    }

    public SkillDefinition GetSkillDefinition(BattleAiContext context, StringName skillId)
    {
        return context?.GetSkillDefinitionTyped(skillId);
    }

    private static List<StringName> CopyStringNameList(IEnumerable<StringName> values)
    {
        var result = new List<StringName>();
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            StringName normalizedValue = ProgressionDataUtils.to_string_name(value);
            if (!IsEmpty(normalizedValue))
            {
                result.Add(normalizedValue);
            }
        }
        return result;
    }

    public BattleSkillCastBlockReasonKind GetSkillCastBlockReason(
        BattleAiContext context,
        SkillDefinition skillDefinition
    )
    {
        BattleUnitState unitState = context?.unit_state;
        if (unitState == null || skillDefinition?.CombatProfile == null)
            return BattleSkillCastBlockReasonKind.InvalidSkillOrTarget;

        return context.skill_cast_block_reason_callback == null
            ? BattleSkillCastBlockReasonKind.SkillCastCheckUnbound
            : context.skill_cast_block_reason_callback.Invoke(unitState, skillDefinition);
    }

    public List<BattleUnitState> SortTargetUnits(
        BattleAiContext context,
        StringName targetFilter,
        StringName selector
    )
    {
        StringName effectiveFilter = targetFilter;
        BattleUnitState actor = context?.unit_state;
        if (
            actor != null
            && actor.ai_blackboard?.madness_target_any_team == true
            && selector != SelectorSelf
        )
        {
            effectiveFilter = TargetFilterAny;
        }
        else if (
            selector == SelectorNearestEnemy
            || selector == SelectorLowestHpEnemy
            || selector == SelectorNearestRoleThreatEnemy
        )
        {
            effectiveFilter = TargetFilterEnemy;
        }
        else if (selector == SelectorNearestAlly || selector == SelectorLowestHpAlly)
        {
            effectiveFilter = TargetFilterAlly;
        }
        else if (selector == SelectorSelf)
        {
            effectiveFilter = TargetFilterSelf;
        }

        List<BattleUnitState> units = CollectUnitsByFilter(context, effectiveFilter);
        BattleUnitState forcedTarget = context?.ResolveForcedTargetUnit(effectiveFilter);
        if (forcedTarget != null)
            return new List<BattleUnitState> { forcedTarget };
        if (selector == SelectorSelf)
            return units;

        int nearestDistance = ResolveNearestDistance(context, units);
        var sorted = new List<BattleUnitState>(units);
        sorted.Sort(
            (left, right) => CompareTargets(context, left, right, selector, nearestDistance)
        );
        return sorted;
    }

    public List<CombatCastVariantDefinition> GetUnitCastVariantDefinitions(
        BattleAiContext context,
        SkillDefinition skillDefinition
    )
    {
        var options = new List<CombatCastVariantDefinition>();
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
            return options;
        if (combatProfile.CastVariants.Count == 0)
        {
            options.Add(null);
            return options;
        }

        BattleUnitState actor = context?.unit_state;
        int skillLevel = actor != null ? GetSkillLevel(actor, skillDefinition.SkillId) : 0;
        SkillEffectiveCombatDefinition effectiveDefinition =
            SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
        foreach (CombatCastVariantDefinition castVariant in effectiveDefinition.UnlockedCastVariants)
        {
            if (
                castVariant != null
                && GetCastVariantTargetModeKind(combatProfile, castVariant) == BattleTargetMode.Unit
            )
                options.Add(castVariant);
        }
        return options;
    }

    public Dictionary<string, object> BuildPositionMetadata(
        BattleAiUnitSkillActionSpec action,
        BattleAiContext context,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition
    )
    {
        Dictionary<string, object> metadata = ResolveDesiredDistanceContract(
            action,
            context,
            skillDefinition
        );
        if (action.DistanceReferenceKind == EnemyAiDistanceReference.TargetUnit)
        {
            metadata["position_target_unit_id"] = targetUnit?.unit_id ?? EmptyStringName;
        }
        else if (action.DistanceReferenceKind == EnemyAiDistanceReference.EnemyFrontline)
        {
            BattleUnitState frontlineUnit = ResolveEnemyFrontlineUnit(context);
            if (frontlineUnit != null)
                metadata["position_target_unit_id"] = frontlineUnit.unit_id;
            else
                metadata["position_objective_kind"] = new StringName("none");
        }
        else
        {
            metadata["position_objective_kind"] = new StringName("none");
        }
        return metadata;
    }

    public List<CombatEffectDefinition> CollectUnitSkillEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        BattleUnitState activeUnit
    )
    {
        var effectDefinitions = new List<CombatEffectDefinition>();
        int skillLevel =
            activeUnit != null ? GetSkillLevel(activeUnit, skillDefinition?.SkillId ?? "") : 0;
        if (skillDefinition?.CombatProfile != null)
        {
            AddUnlockedEffectDefinitions(
                effectDefinitions,
                skillDefinition.CombatProfile.EffectDefinitions,
                skillLevel,
                activeUnit != null
            );
        }
        if (castVariant != null)
        {
            AddUnlockedEffectDefinitions(
                effectDefinitions,
                castVariant.EffectDefinitions,
                skillLevel,
                activeUnit != null
            );
        }
        return effectDefinitions;
    }

    public BattleTargetMode GetCastVariantTargetModeKind(
        CombatSkillDefinition combatProfile,
        CombatCastVariantDefinition castVariant
    )
    {
        if (castVariant == null)
            return BattleTargetMode.Unknown;
        BattleTargetMode targetMode = castVariant.TargetModeKind;
        return targetMode != BattleTargetMode.Unknown
            ? targetMode
            : combatProfile?.TargetModeKind ?? BattleTargetMode.Unknown;
    }

    public BattleCommand BuildUnitSkillCommand(
        BattleAiContext context,
        StringName skillId,
        BattleUnitState targetUnit,
        StringName skillVariantId
    )
    {
        BattleUnitState actor = context?.unit_state;
        if (actor == null || targetUnit == null)
            return null;
        return new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = actor.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(skillId),
            skill_id = skillId,
            skill_variant_id = skillVariantId,
            target_unit_id = targetUnit.unit_id,
            target_coord = targetUnit.coord,
        };
    }

    public BattleAiDecision CreateDecision(
        BattleAiUnitSkillActionSpec action,
        BattleCommand command,
        string reasonText
    )
    {
        return new BattleAiDecision
        {
            command = command,
            action_id = action?.ActionId ?? EmptyStringName,
            reason_text = reasonText,
            score_bucket_id = action?.ScoreBucketId ?? EmptyStringName,
        };
    }

    public BattleAiDecision CreateScoredDecision(
        BattleAiUnitSkillActionSpec action,
        BattleCommand command,
        BattleAiScoreInput scoreInput,
        string reasonText
    )
    {
        BattleAiDecision decision = CreateDecision(action, command, reasonText);
        decision.skill_score_input = scoreInput;
        decision.score_input = scoreInput;
        return decision;
    }

    private List<BattleUnitState> CollectUnitsByFilter(BattleAiContext context, StringName targetFilter)
    {
        var results = new List<BattleUnitState>();
        BattleState state = context?.state;
        BattleUnitState actor = context?.unit_state;
        if (state == null || actor == null)
            return results;
        foreach (BattleUnitState unit in state.GetUnitsTyped())
        {
            if (unit == null || !unit.is_alive)
                continue;
            if (!MatchesTargetFilter(context, unit, targetFilter))
                continue;
            results.Add(unit);
        }
        return results;
    }

    private static bool MatchesTargetFilter(
        BattleAiContext context,
        BattleUnitState targetUnit,
        StringName targetFilter
    )
    {
        BattleUnitState actor = context?.unit_state;
        if (actor == null || targetUnit == null)
            return false;
        return BattleTargetTeamRules.IsUnitValidForFilter(
            actor,
            targetUnit,
            targetFilter,
            new BattleTargetTeamRules.TargetFilterOptions(
                MadnessTargetAnyTeam: actor.ai_blackboard?.madness_target_any_team == true
            )
        );
    }

    private static int ResolveNearestDistance(
        BattleAiContext context,
        IReadOnlyList<BattleUnitState> units
    )
    {
        int nearestDistance = 999999;
        BattleUnitState actor = context?.unit_state;
        foreach (BattleUnitState unit in units ?? Array.Empty<BattleUnitState>())
        {
            if (unit == null)
                continue;
            nearestDistance = Math.Min(nearestDistance, DistanceBetweenUnits(actor, unit));
        }
        return nearestDistance;
    }

    private int CompareTargets(
        BattleAiContext context,
        BattleUnitState left,
        BattleUnitState right,
        StringName selector,
        int nearestDistance
    )
    {
        BattleUnitState actor = context?.unit_state;
        int leftHp = GetHpBasisPoints(left);
        int rightHp = GetHpBasisPoints(right);
        int leftDistance = DistanceBetweenUnits(actor, left);
        int rightDistance = DistanceBetweenUnits(actor, right);

        if (selector == SelectorNearestRoleThreatEnemy)
        {
            int leftThreat = GetRoleThreatSelectorScore(context, left, nearestDistance, leftDistance);
            int rightThreat = GetRoleThreatSelectorScore(context, right, nearestDistance, rightDistance);
            if (leftThreat != rightThreat)
                return rightThreat.CompareTo(leftThreat);
        }
        if (selector == SelectorLowestHpEnemy || selector == SelectorLowestHpAlly)
        {
            if (leftHp != rightHp)
                return leftHp.CompareTo(rightHp);
            return leftDistance.CompareTo(rightDistance);
        }
        if (leftDistance == rightDistance)
            return leftHp.CompareTo(rightHp);
        return leftDistance.CompareTo(rightDistance);
    }

    private int GetRoleThreatSelectorScore(
        BattleAiContext context,
        BattleUnitState unit,
        int nearestDistance,
        int distance
    )
    {
        if (unit == null)
            return 0;
        int threatRange = ResolveUnitEffectiveThreatRange(context, unit);
        bool isLocalRoleThreat =
            threatRange >= RoleThreatMinEffectiveRange
            && distance <= nearestDistance + RoleThreatDistanceWindow
            && distance <= RoleThreatMaxApproachDistance;
        if (isLocalRoleThreat)
            return 1000 + threatRange * 10;
        return ResolveUnitContactThreatRange(context, unit) > 0 ? 500 : 0;
    }

    private int ResolveUnitContactThreatRange(BattleAiContext context, BattleUnitState threatUnit)
    {
        if (context == null || threatUnit == null)
            return -1;
        int bestRange = -1;
        foreach (StringName rawSkillId in threatUnit.known_active_skill_ids)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            if (IsEmpty(skillId))
                continue;
            SkillDefinition skillDefinition = GetSkillDefinition(context, skillId);
            if (!IsHostileThreatSkill(skillDefinition))
                continue;
            if (!SkillHasTag(skillDefinition, "melee") && !SkillHasTag(skillDefinition, "weapon"))
                continue;
            int effectiveRange = BattleRangeService.GetEffectiveSkillRange(
                threatUnit,
                skillDefinition
            );
            if (effectiveRange <= 0 && SkillHasTag(skillDefinition, "melee"))
                effectiveRange = 1;
            if (effectiveRange > RoleThreatMaxContactRange)
                continue;
            bestRange = Math.Max(bestRange, effectiveRange);
        }

        int weaponRange = BattleRangeService.GetWeaponAttackRange(threatUnit);
        if (weaponRange > 0 && weaponRange <= RoleThreatMaxContactRange)
            bestRange = Math.Max(bestRange, weaponRange);
        return bestRange;
    }

    private int ResolveUnitEffectiveThreatRange(BattleAiContext context, BattleUnitState threatUnit)
    {
        if (context == null || threatUnit == null)
            return -1;
        int bestRange = -1;
        foreach (StringName rawSkillId in threatUnit.known_active_skill_ids)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            if (IsEmpty(skillId))
                continue;
            SkillDefinition skillDefinition = GetSkillDefinition(context, skillId);
            if (!IsHostileThreatSkill(skillDefinition))
                continue;
            bestRange = Math.Max(
                bestRange,
                BattleRangeService.GetEffectiveSkillThreatRange(threatUnit, skillDefinition)
            );
        }
        if (bestRange < 0)
            bestRange = BattleRangeService.GetWeaponAttackRange(threatUnit);
        return bestRange;
    }

    private static Dictionary<string, object> ResolveDesiredDistanceContract(
        BattleAiUnitSkillActionSpec action,
        BattleAiContext context,
        SkillDefinition skillDefinition
    )
    {
        int configuredMin = action?.DesiredMinDistance ?? 0;
        int configuredMax = action?.DesiredMaxDistance ?? 0;
        int effectiveAttackRange = ResolveEffectiveAttackRange(context, skillDefinition);
        int resolvedMax = configuredMax;
        if (effectiveAttackRange >= 0)
            resolvedMax = effectiveAttackRange;
        int resolvedMin = configuredMin;
        if (resolvedMax >= 0 && resolvedMin > resolvedMax)
            resolvedMin = resolvedMax;
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["desired_min_distance"] = resolvedMin,
            ["desired_max_distance"] = Math.Max(resolvedMax, resolvedMin),
            ["configured_desired_min_distance"] = configuredMin,
            ["configured_desired_max_distance"] = configuredMax,
            ["effective_attack_range"] = effectiveAttackRange,
        };
    }

    private static int ResolveEffectiveAttackRange(
        BattleAiContext context,
        SkillDefinition skillDefinition
    )
    {
        BattleUnitState actor = context?.unit_state;
        if (actor == null || skillDefinition == null)
            return -1;
        return BattleRangeService.GetEffectiveSkillThreatRange(actor, skillDefinition);
    }

    private BattleUnitState ResolveEnemyFrontlineUnit(BattleAiContext context)
    {
        List<BattleUnitState> targets = SortTargetUnits(
            context,
            TargetFilterEnemy,
            SelectorNearestEnemy
        );
        return targets.Count > 0 ? targets[0] : null;
    }

    private static bool IsHostileThreatSkill(SkillDefinition skillDefinition)
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
            return false;
        if (
            combatProfile.TargetFilterKind == BattleTargetFilter.Ally
            || combatProfile.TargetFilterKind == BattleTargetFilter.Self
        )
        {
            return false;
        }
        if (
            SkillHasTag(skillDefinition, "output")
            || SkillHasTag(skillDefinition, "melee")
            || SkillHasTag(skillDefinition, "bow")
            || SkillHasTag(skillDefinition, "weapon")
        )
        {
            return true;
        }
        if (EffectListHasHostileThreat(combatProfile.EffectDefinitions))
            return true;
        foreach (CombatCastVariantDefinition castVariant in combatProfile.CastVariants)
        {
            if (castVariant != null && EffectListHasHostileThreat(castVariant.EffectDefinitions))
                return true;
        }
        return false;
    }

    private static bool EffectListHasHostileThreat(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? System.Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition == null)
                continue;
            if (
                effectDefinition.EffectKind == BattleEffectKind.Damage
                || effectDefinition.EffectKind == BattleEffectKind.ChainDamage
                || effectDefinition.EffectKind == BattleEffectKind.Execute
                || effectDefinition.EffectKind == BattleEffectKind.Charge
                || effectDefinition.EffectKind == BattleEffectKind.ForcedMove
                || effectDefinition.EffectKind == BattleEffectKind.PathStepAoe
                || effectDefinition.EffectKind == BattleEffectKind.Status
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool SkillHasTag(SkillDefinition skillDefinition, StringName expectedTag)
    {
        return skillDefinition != null && !IsEmpty(expectedTag) && skillDefinition.HasTag(expectedTag);
    }

    private static void AddUnlockedEffectDefinitions(
        List<CombatEffectDefinition> target,
        IEnumerable<CombatEffectDefinition> source,
        int skillLevel,
        bool shouldFilter
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in source
                ?? System.Array.Empty<CombatEffectDefinition>()
        )
        {
            if (IsEffectUnlockedForSkillLevel(effectDefinition, skillLevel, shouldFilter))
                target.Add(effectDefinition);
        }
    }

    private static bool IsEffectUnlockedForSkillLevel(
        CombatEffectDefinition effectDefinition,
        int skillLevel,
        bool shouldFilter
    )
    {
        if (effectDefinition == null)
            return false;
        if (!shouldFilter)
            return true;
        int minLevel = Math.Max(effectDefinition.MinSkillLevel, 0);
        int maxLevel = effectDefinition.MaxSkillLevel;
        return skillLevel >= minLevel && (maxLevel < 0 || skillLevel <= maxLevel);
    }

    private static int GetHpBasisPoints(BattleUnitState unit)
    {
        if (unit?.attribute_snapshot == null)
            return HpBasisPointsDenominator;
        int hpMax = Math.Max(unit.attribute_snapshot.GetValue("hp_max"), 1);
        int currentHp = Math.Clamp(unit.current_hp, 0, hpMax);
        return Math.Clamp(
            (currentHp * HpBasisPointsDenominator) / hpMax,
            0,
            HpBasisPointsDenominator
        );
    }

    private static int DistanceBetweenUnits(BattleUnitState firstUnit, BattleUnitState secondUnit)
    {
        return BattleGridDistanceService.GetDistanceBetweenUnits(firstUnit, secondUnit);
    }

    private static int GetSkillLevel(BattleUnitState unitState, StringName skillId)
    {
        if (unitState == null || IsEmpty(skillId))
            return 0;
        int knownSkillLevel = unitState.GetKnownSkillLevelTyped(skillId);
        return knownSkillLevel > 0
            ? Math.Max(knownSkillLevel, 0)
            : unitState.known_active_skill_ids.Contains(skillId)
                ? 1
                : 0;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
