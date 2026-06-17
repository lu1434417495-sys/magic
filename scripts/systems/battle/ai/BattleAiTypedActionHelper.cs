using System;
using System.Collections.Generic;
using Godot;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

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
        GStringNameArray preferredSkillIds
    )
    {
        var results = new List<StringName>();
        BattleUnitState unitState = context?.unit_state;
        if (unitState == null)
            return results;

        var seen = new HashSet<StringName>();
        GStringNameArray sourceIds =
            preferredSkillIds != null && preferredSkillIds.Count > 0
                ? preferredSkillIds
                : unitState.known_active_skill_ids;
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

    public SkillDef GetSkillDef(BattleAiContext context, StringName skillId)
    {
        return context?.GetSkillDefTyped(skillId);
    }

    public BattleSkillCastBlockReasonKind GetSkillCastBlockReason(
        BattleAiContext context,
        SkillDef skillDef
    )
    {
        BattleUnitState unitState = context?.unit_state;
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (unitState == null || skillDef == null || combatProfile == null)
            return BattleSkillCastBlockReasonKind.InvalidSkillOrTarget;

        return context.skill_cast_block_reason_callback == null
            ? BattleSkillCastBlockReasonKind.SkillCastCheckUnbound
            : context.skill_cast_block_reason_callback.Invoke(unitState, skillDef);
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

    public List<CombatCastVariantDef> GetUnitCastVariants(BattleAiContext context, SkillDef skillDef)
    {
        var options = new List<CombatCastVariantDef>();
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (combatProfile == null)
            return options;
        if (combatProfile.cast_variants.Count == 0)
        {
            options.Add(null);
            return options;
        }

        BattleUnitState actor = context?.unit_state;
        int skillLevel = actor != null ? GetSkillLevel(actor, skillDef.skill_id) : 0;
        SkillEffectiveCombatProfile effectiveProfile =
            SkillEffectiveCombatProfileResolver.Resolve(
                context?.skill_catalog,
                skillDef,
                skillLevel
            );
        foreach (CombatCastVariantDef castVariant in effectiveProfile.UnlockedCastVariants)
        {
            if (
                castVariant != null
                && GetCastVariantTargetModeKind(skillDef, castVariant) == BattleTargetMode.Unit
            )
                options.Add(castVariant);
        }
        return options;
    }

    public Dictionary<string, object> BuildPositionMetadata(
        UseUnitSkillAction action,
        BattleAiContext context,
        BattleUnitState targetUnit,
        SkillDef skillDef
    )
    {
        Dictionary<string, object> metadata = ResolveDesiredDistanceContract(action, context, skillDef);
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

    public List<CombatEffectDef> CollectUnitSkillEffectDefs(
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        BattleUnitState activeUnit
    )
    {
        var effectDefs = new List<CombatEffectDef>();
        int skillLevel = activeUnit != null ? GetSkillLevel(activeUnit, skillDef?.skill_id ?? "") : 0;
        if (skillDef?.combat_profile != null)
            AddUnlockedEffectDefs(effectDefs, skillDef.combat_profile.effect_defs, skillLevel, activeUnit != null);
        if (castVariant != null)
            AddUnlockedEffectDefs(effectDefs, castVariant.effect_defs, skillLevel, activeUnit != null);
        return effectDefs;
    }

    public BattleTargetMode GetCastVariantTargetModeKind(
        SkillDef skillDef,
        CombatCastVariantDef castVariant
    )
    {
        if (castVariant == null)
            return BattleTargetMode.Unknown;
        return castVariant.TargetModeKind;
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
            skill_id = skillId,
            skill_variant_id = skillVariantId,
            target_unit_id = targetUnit.unit_id,
            target_coord = targetUnit.coord,
        };
    }

    public BattleAiDecision CreateDecision(
        UseUnitSkillAction action,
        BattleCommand command,
        string reasonText
    )
    {
        return new BattleAiDecision
        {
            command = command,
            action_id = action?.action_id ?? EmptyStringName,
            reason_text = reasonText,
            score_bucket_id = action?.score_bucket_id ?? EmptyStringName,
        };
    }

    public BattleAiDecision CreateScoredDecision(
        UseUnitSkillAction action,
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
            SkillDef skillDef = GetSkillDef(context, skillId);
            if (!IsHostileThreatSkill(skillDef))
                continue;
            if (!SkillHasTag(skillDef, "melee") && !SkillHasTag(skillDef, "weapon"))
                continue;
            int effectiveRange = BattleRangeService.GetEffectiveSkillRange(threatUnit, skillDef);
            if (effectiveRange <= 0 && SkillHasTag(skillDef, "melee"))
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
            SkillDef skillDef = GetSkillDef(context, skillId);
            if (!IsHostileThreatSkill(skillDef))
                continue;
            bestRange = Math.Max(
                bestRange,
                BattleRangeService.GetEffectiveSkillThreatRange(threatUnit, skillDef)
            );
        }
        if (bestRange < 0)
            bestRange = BattleRangeService.GetWeaponAttackRange(threatUnit);
        return bestRange;
    }

    private static Dictionary<string, object> ResolveDesiredDistanceContract(
        UseUnitSkillAction action,
        BattleAiContext context,
        SkillDef skillDef
    )
    {
        int configuredMin = action?.desired_min_distance ?? 0;
        int configuredMax = action?.desired_max_distance ?? 0;
        int effectiveAttackRange = ResolveEffectiveAttackRange(context, skillDef);
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

    private static int ResolveEffectiveAttackRange(BattleAiContext context, SkillDef skillDef)
    {
        BattleUnitState actor = context?.unit_state;
        if (actor == null || skillDef == null)
            return -1;
        return BattleRangeService.GetEffectiveSkillThreatRange(actor, skillDef);
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

    private static bool IsHostileThreatSkill(SkillDef skillDef)
    {
        CombatSkillDef combatProfile = skillDef?.combat_profile;
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
            SkillHasTag(skillDef, "output")
            || SkillHasTag(skillDef, "melee")
            || SkillHasTag(skillDef, "bow")
            || SkillHasTag(skillDef, "weapon")
        )
        {
            return true;
        }
        if (EffectListHasHostileThreat(combatProfile.effect_defs))
            return true;
        foreach (CombatCastVariantDef castVariant in combatProfile.cast_variants)
        {
            if (castVariant != null && EffectListHasHostileThreat(castVariant.effect_defs))
                return true;
        }
        return false;
    }

    private static bool EffectListHasHostileThreat(Godot.Collections.Array<CombatEffectDef> effectDefs)
    {
        foreach (CombatEffectDef effectDef in effectDefs)
        {
            if (effectDef == null)
                continue;
            if (
                effectDef.EffectKind == BattleEffectKind.Damage
                || effectDef.EffectKind == BattleEffectKind.ChainDamage
                || effectDef.EffectKind == BattleEffectKind.Execute
                || effectDef.EffectKind == BattleEffectKind.Charge
                || effectDef.EffectKind == BattleEffectKind.ForcedMove
                || effectDef.EffectKind == BattleEffectKind.PathStepAoe
                || effectDef.EffectKind == BattleEffectKind.Status
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool SkillHasTag(SkillDef skillDef, StringName expectedTag)
    {
        return skillDef != null && !IsEmpty(expectedTag) && skillDef.HasTag(expectedTag);
    }

    private static void AddUnlockedEffectDefs(
        List<CombatEffectDef> target,
        Godot.Collections.Array<CombatEffectDef> source,
        int skillLevel,
        bool shouldFilter
    )
    {
        foreach (CombatEffectDef effectDef in source)
        {
            if (IsEffectUnlockedForSkillLevel(effectDef, skillLevel, shouldFilter))
                target.Add(effectDef);
        }
    }

    private static bool IsEffectUnlockedForSkillLevel(
        CombatEffectDef effectDef,
        int skillLevel,
        bool shouldFilter
    )
    {
        if (effectDef == null)
            return false;
        if (!shouldFilter)
            return true;
        int minLevel = Math.Max(effectDef.min_skill_level, 0);
        int maxLevel = effectDef.max_skill_level;
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
