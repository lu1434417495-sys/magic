using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiTypedActionHelper : RefCounted
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
    private static readonly StringName DistanceRefTargetUnit = "target_unit";
    private static readonly StringName DistanceRefEnemyFrontline = "enemy_frontline";
    private static readonly StringName TargetModeUnit = "unit";

    private const int HpBasisPointsDenominator = 10000;
    private const int RoleThreatMinEffectiveRange = 4;
    private const int RoleThreatDistanceWindow = 4;
    private const int RoleThreatMaxApproachDistance = 7;
    private const int RoleThreatMaxContactRange = 2;
    private Resource _battleCommandScript;
    private Resource _battleAiDecisionScript;
    private readonly BattleSkillResolutionRules _skillResolutionRules = new();

    public GArray ResolveKnownSkillIds(GodotObject context, GArray preferredSkillIds)
    {
        var results = new GArray();
        GodotObject unitState = GetObject(context, "unit_state");
        if (context == null || unitState == null)
        {
            return results;
        }

        var seen = new HashSet<StringName>();
        GArray knownActiveSkillIds = GetArray(unitState, "known_active_skill_ids");
        GArray sourceIds = preferredSkillIds != null && preferredSkillIds.Count > 0
            ? preferredSkillIds
            : knownActiveSkillIds;
        foreach (Variant rawSkillId in sourceIds)
        {
            StringName skillId = new(rawSkillId.ToString());
            if (IsEmpty(skillId) || seen.Contains(skillId))
            {
                continue;
            }
            seen.Add(skillId);
            if (ArrayHasStringName(knownActiveSkillIds, skillId))
            {
                results.Add(skillId);
            }
        }
        return results;
    }

    public GodotObject GetSkillDef(GodotObject context, StringName skillId)
    {
        if (context == null || IsEmpty(skillId))
        {
            return null;
        }
        GDictionary skillDefs = GetDictionary(context, "skill_defs");
        return skillDefs.ContainsKey(skillId) ? skillDefs[skillId].AsGodotObject() : null;
    }

    public string GetSkillCastBlockReason(GodotObject context, GodotObject skillDef)
    {
        GodotObject unitState = GetObject(context, "unit_state");
        GodotObject combatProfile = GetObject(skillDef, "combat_profile");
        if (context == null || unitState == null || skillDef == null || combatProfile == null)
        {
            return "技能或目标无效。";
        }

        StringName skillId = GetStringName(skillDef, "skill_id");
        int skillLevel = GetSkillLevel(unitState, skillId);
        GDictionary costs = GetEffectiveResourceCosts(combatProfile, skillLevel);
        int cooldown = GetInt(GetDictionary(unitState, "cooldowns"), skillId, 0);
        if (cooldown > 0)
        {
            return $"{GetString(skillDef, "display_name")} 仍在冷却中（{cooldown}）。";
        }

        string lockedReason = GetLockedCombatResourceBlockReason(unitState, costs);
        if (!string.IsNullOrEmpty(lockedReason))
        {
            return lockedReason;
        }
        if (GetInt(unitState, "current_ap") < GetInt(costs, "ap_cost", GetInt(combatProfile, "ap_cost")))
        {
            return "AP不足，无法施放该技能。";
        }
        if (GetInt(unitState, "current_mp") < GetInt(costs, "mp_cost", GetInt(combatProfile, "mp_cost")))
        {
            return "法力不足，无法施放该技能。";
        }
        if (GetInt(unitState, "current_stamina") < GetInt(costs, "stamina_cost", GetInt(combatProfile, "stamina_cost")))
        {
            return "体力不足，无法施放该技能。";
        }
        if (GetInt(unitState, "current_aura") < GetInt(costs, "aura_cost", GetInt(combatProfile, "aura_cost")))
        {
            return "斗气不足，无法施放该技能。";
        }
        return "";
    }

    public GArray SortTargetUnits(GodotObject context, StringName targetFilter, StringName selector)
    {
        StringName effectiveFilter = targetFilter;
        GodotObject actor = GetObject(context, "unit_state");
        if (context != null
            && actor != null
            && GetBool(GetDictionary(actor, "ai_blackboard"), "madness_target_any_team")
            && selector != SelectorSelf)
        {
            effectiveFilter = TargetFilterAny;
        }
        else if (selector == SelectorNearestEnemy || selector == SelectorLowestHpEnemy || selector == SelectorNearestRoleThreatEnemy)
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

        GArray units = CollectUnitsByFilter(context, effectiveFilter);
        GodotObject forcedTarget = ResolveForcedTargetUnit(context, effectiveFilter);
        if (forcedTarget != null)
        {
            return new GArray { forcedTarget };
        }
        if (selector == SelectorSelf)
        {
            return units;
        }

        int nearestDistance = ResolveNearestDistance(context, units);
        var sorted = new List<GodotObject>();
        foreach (Variant unitValue in units)
        {
            GodotObject unit = unitValue.AsGodotObject();
            if (unit != null)
            {
                sorted.Add(unit);
            }
        }
        sorted.Sort((left, right) => CompareTargets(context, left, right, selector, nearestDistance));

        var result = new GArray();
        foreach (GodotObject unit in sorted)
        {
            result.Add(unit);
        }
        return result;
    }

    public GArray GetUnitCastVariants(GodotObject context, GodotObject skillDef)
    {
        var variants = new GArray();
        GodotObject combatProfile = GetObject(skillDef, "combat_profile");
        if (skillDef == null || combatProfile == null)
        {
            return variants;
        }
        if (GetArray(combatProfile, "cast_variants").Count == 0)
        {
            variants.Add(Variant.From<GodotObject>(null));
            return variants;
        }

        GodotObject actor = GetObject(context, "unit_state");
        int skillLevel = actor != null ? GetSkillLevel(actor, GetStringName(skillDef, "skill_id")) : 0;
        foreach (Variant variantValue in GetUnlockedCastVariants(combatProfile, skillLevel))
        {
            GodotObject castVariant = variantValue.AsGodotObject();
            if (castVariant == null)
            {
                continue;
            }
            if (GetCastVariantTargetMode(skillDef, castVariant) == TargetModeUnit)
            {
                variants.Add(castVariant);
            }
        }
        return variants;
    }

    public GDictionary BuildPositionMetadata(
        GodotObject action,
        GodotObject context,
        GodotObject targetUnit,
        GodotObject skillDef)
    {
        GDictionary metadata = ResolveDesiredDistanceContract(action, context, skillDef);
        StringName distanceReference = GetStringName(action, "distance_reference");
        if (distanceReference == DistanceRefTargetUnit)
        {
            metadata["position_target_unit"] = targetUnit;
        }
        else if (distanceReference == DistanceRefEnemyFrontline)
        {
            GodotObject frontlineUnit = ResolveEnemyFrontlineUnit(context);
            if (frontlineUnit != null)
            {
                metadata["position_target_unit"] = frontlineUnit;
            }
            else
            {
                metadata["position_objective_kind"] = new StringName("none");
            }
        }
        else
        {
            metadata["position_objective_kind"] = new StringName("none");
        }
        return metadata;
    }

    public GArray CollectUnitSkillEffectDefs(GodotObject skillDef, GodotObject castVariant, GodotObject activeUnit)
    {
        return _skillResolutionRules.collect_unit_skill_effect_defs(skillDef, castVariant, activeUnit);
    }

    public StringName GetCastVariantTargetMode(GodotObject skillDef, GodotObject castVariant)
    {
        return _skillResolutionRules.get_cast_variant_target_mode(skillDef, castVariant);
    }

    public GodotObject BuildUnitSkillCommand(GodotObject context, StringName skillId, GodotObject targetUnit, StringName skillVariantId)
    {
        GodotObject actor = GetObject(context, "unit_state");
        if (context == null || actor == null || targetUnit == null)
        {
            return null;
        }
        GodotObject command = NewFromScript(ref _battleCommandScript, "res://scripts/systems/battle/core/battle_command.gd");
        if (command == null)
        {
            return null;
        }
        command.Set("command_type", new StringName("skill"));
        command.Set("unit_id", GetStringName(actor, "unit_id"));
        command.Set("skill_id", skillId);
        command.Set("skill_variant_id", skillVariantId);
        command.Set("target_unit_id", GetStringName(targetUnit, "unit_id"));
        command.Set("target_coord", GetVector2I(targetUnit, "coord"));
        return command;
    }

    public GodotObject CreateDecision(GodotObject action, GodotObject command, string reasonText)
    {
        GodotObject decision = NewFromScript(ref _battleAiDecisionScript, "res://scripts/systems/battle/ai/battle_ai_decision.gd");
        if (decision == null)
        {
            return null;
        }
        decision.Set("command", command);
        decision.Set("action_id", GetStringName(action, "action_id"));
        decision.Set("reason_text", reasonText);
        decision.Set("score_bucket_id", GetStringName(action, "score_bucket_id"));
        return decision;
    }

    public GodotObject CreateScoredDecision(GodotObject action, GodotObject command, GodotObject scoreInput, string reasonText)
    {
        GodotObject decision = CreateDecision(action, command, reasonText);
        if (decision == null)
        {
            return null;
        }
        decision.Set("skill_score_input", scoreInput);
        decision.Set("score_input", scoreInput);
        return decision;
    }

    private GArray CollectUnitsByFilter(GodotObject context, StringName targetFilter)
    {
        var results = new GArray();
        GodotObject state = GetObject(context, "state");
        GodotObject actor = GetObject(context, "unit_state");
        if (context == null || state == null || actor == null)
        {
            return results;
        }
        GDictionary units = GetDictionary(state, "units");
        foreach (Variant unitId in units.Keys)
        {
            GodotObject unit = units[unitId].AsGodotObject();
            if (unit == null || !GetBool(unit, "is_alive"))
            {
                continue;
            }
            if (!MatchesTargetFilter(context, unit, targetFilter))
            {
                continue;
            }
            results.Add(unit);
        }
        return results;
    }

    private bool MatchesTargetFilter(GodotObject context, GodotObject targetUnit, StringName targetFilter)
    {
        GodotObject actor = GetObject(context, "unit_state");
        var options = new GDictionary
        {
            ["madness_target_any_team"] = GetBool(GetDictionary(actor, "ai_blackboard"), "madness_target_any_team"),
            ["madness_target_filters"] = new GArray { TargetFilterAlly, TargetFilterEnemy, TargetFilterAny },
        };
        return BattleTargetTeamRules.is_unit_valid_for_filter(actor as BattleUnitState, targetUnit as BattleUnitState, targetFilter, options);
    }

    private GodotObject ResolveForcedTargetUnit(GodotObject context, StringName targetFilter)
    {
        if (context == null || !context.HasMethod("resolve_forced_target_unit"))
        {
            return null;
        }
        return context.Call("resolve_forced_target_unit", targetFilter).AsGodotObject();
    }

    private int ResolveNearestDistance(GodotObject context, GArray units)
    {
        int nearestDistance = 999999;
        GodotObject actor = GetObject(context, "unit_state");
        foreach (Variant unitValue in units)
        {
            GodotObject unit = unitValue.AsGodotObject();
            if (unit == null)
            {
                continue;
            }
            nearestDistance = Math.Min(nearestDistance, DistanceBetweenUnits(context, actor, unit));
        }
        return nearestDistance;
    }

    private int CompareTargets(GodotObject context, GodotObject left, GodotObject right, StringName selector, int nearestDistance)
    {
        GodotObject actor = GetObject(context, "unit_state");
        int leftHp = GetHpBasisPoints(left);
        int rightHp = GetHpBasisPoints(right);
        int leftDistance = DistanceBetweenUnits(context, actor, left);
        int rightDistance = DistanceBetweenUnits(context, actor, right);

        if (selector == SelectorNearestRoleThreatEnemy)
        {
            int leftThreat = GetRoleThreatSelectorScore(context, left, nearestDistance, leftDistance);
            int rightThreat = GetRoleThreatSelectorScore(context, right, nearestDistance, rightDistance);
            if (leftThreat != rightThreat)
            {
                return rightThreat.CompareTo(leftThreat);
            }
        }
        if (selector == SelectorLowestHpEnemy || selector == SelectorLowestHpAlly)
        {
            if (leftHp != rightHp)
            {
                return leftHp.CompareTo(rightHp);
            }
            return leftDistance.CompareTo(rightDistance);
        }
        if (leftDistance == rightDistance)
        {
            return leftHp.CompareTo(rightHp);
        }
        return leftDistance.CompareTo(rightDistance);
    }

    private int GetRoleThreatSelectorScore(GodotObject context, GodotObject unit, int nearestDistance, int distance)
    {
        if (unit == null)
        {
            return 0;
        }
        int threatRange = ResolveUnitEffectiveThreatRange(context, unit);
        bool isLocalRoleThreat = threatRange >= RoleThreatMinEffectiveRange
            && distance <= nearestDistance + RoleThreatDistanceWindow
            && distance <= RoleThreatMaxApproachDistance;
        if (isLocalRoleThreat)
        {
            return 1000 + threatRange * 10;
        }
        return ResolveUnitContactThreatRange(context, unit) > 0 ? 500 : 0;
    }

    private int ResolveUnitContactThreatRange(GodotObject context, GodotObject threatUnit)
    {
        if (context == null || threatUnit == null)
        {
            return -1;
        }
        int bestRange = -1;
        foreach (Variant rawSkillId in GetArray(threatUnit, "known_active_skill_ids"))
        {
            StringName skillId = new(rawSkillId.ToString());
            if (IsEmpty(skillId))
            {
                continue;
            }
            GodotObject skillDef = GetSkillDef(context, skillId);
            if (!IsHostileThreatSkill(skillDef))
            {
                continue;
            }
            if (!SkillHasTag(skillDef, "melee") && !SkillHasTag(skillDef, "weapon"))
            {
                continue;
            }
            int effectiveRange = BattleRangeService.get_effective_skill_range(threatUnit, skillDef);
            if (effectiveRange <= 0 && SkillHasTag(skillDef, "melee"))
            {
                effectiveRange = 1;
            }
            if (effectiveRange > RoleThreatMaxContactRange)
            {
                continue;
            }
            bestRange = Math.Max(bestRange, effectiveRange);
        }

        int weaponRange = BattleRangeService.get_weapon_attack_range(threatUnit);
        if (weaponRange > 0 && weaponRange <= RoleThreatMaxContactRange)
        {
            bestRange = Math.Max(bestRange, weaponRange);
        }
        return bestRange;
    }

    private int ResolveUnitEffectiveThreatRange(GodotObject context, GodotObject threatUnit)
    {
        if (context == null || threatUnit == null)
        {
            return -1;
        }
        int bestRange = -1;
        foreach (Variant rawSkillId in GetArray(threatUnit, "known_active_skill_ids"))
        {
            StringName skillId = new(rawSkillId.ToString());
            if (IsEmpty(skillId))
            {
                continue;
            }
            GodotObject skillDef = GetSkillDef(context, skillId);
            if (!IsHostileThreatSkill(skillDef))
            {
                continue;
            }
            bestRange = Math.Max(bestRange, BattleRangeService.get_effective_skill_threat_range(threatUnit, skillDef));
        }
        if (bestRange < 0)
        {
            bestRange = BattleRangeService.get_weapon_attack_range(threatUnit);
        }
        return bestRange;
    }

    private GDictionary ResolveDesiredDistanceContract(GodotObject action, GodotObject context, GodotObject skillDef)
    {
        int configuredMin = GetInt(action, "desired_min_distance");
        int configuredMax = GetInt(action, "desired_max_distance");
        int effectiveAttackRange = ResolveEffectiveAttackRange(context, skillDef);
        int resolvedMax = configuredMax;
        if (effectiveAttackRange >= 0)
        {
            resolvedMax = effectiveAttackRange;
        }
        int resolvedMin = configuredMin;
        if (resolvedMax >= 0 && resolvedMin > resolvedMax)
        {
            resolvedMin = resolvedMax;
        }
        return new GDictionary
        {
            ["desired_min_distance"] = resolvedMin,
            ["desired_max_distance"] = Math.Max(resolvedMax, resolvedMin),
            ["configured_desired_min_distance"] = configuredMin,
            ["configured_desired_max_distance"] = configuredMax,
            ["effective_attack_range"] = effectiveAttackRange,
        };
    }

    private int ResolveEffectiveAttackRange(GodotObject context, GodotObject skillDef)
    {
        GodotObject actor = GetObject(context, "unit_state");
        if (context == null || actor == null)
        {
            return -1;
        }
        if (skillDef != null)
        {
            return BattleRangeService.get_effective_skill_threat_range(actor, skillDef);
        }
        return -1;
    }

    private GodotObject ResolveEnemyFrontlineUnit(GodotObject context)
    {
        GArray targets = SortTargetUnits(context, TargetFilterEnemy, SelectorNearestEnemy);
        return targets.Count > 0 ? targets[0].AsGodotObject() : null;
    }

    private bool IsHostileThreatSkill(GodotObject skillDef)
    {
        GodotObject combatProfile = GetObject(skillDef, "combat_profile");
        if (skillDef == null || combatProfile == null)
        {
            return false;
        }
        StringName targetFilter = GetStringName(combatProfile, "target_team_filter");
        if (targetFilter == TargetFilterAlly || targetFilter == TargetFilterSelf)
        {
            return false;
        }
        if (SkillHasTag(skillDef, "output")
            || SkillHasTag(skillDef, "melee")
            || SkillHasTag(skillDef, "bow")
            || SkillHasTag(skillDef, "weapon"))
        {
            return true;
        }
        if (EffectListHasHostileThreat(GetArray(combatProfile, "effect_defs")))
        {
            return true;
        }
        foreach (Variant variantValue in GetArray(combatProfile, "cast_variants"))
        {
            GodotObject castVariant = variantValue.AsGodotObject();
            if (castVariant != null && EffectListHasHostileThreat(GetArray(castVariant, "effect_defs")))
            {
                return true;
            }
        }
        return false;
    }

    private static bool EffectListHasHostileThreat(GArray effectDefs)
    {
        foreach (Variant effectValue in effectDefs)
        {
            GodotObject effectDef = effectValue.AsGodotObject();
            if (effectDef == null)
            {
                continue;
            }
            StringName effectType = GetStringName(effectDef, "effect_type");
            if (effectType == new StringName("damage")
                || effectType == new StringName("chain_damage")
                || effectType == new StringName("charge")
                || effectType == new StringName("forced_move")
                || effectType == new StringName("path_step_aoe")
                || effectType == new StringName("status"))
            {
                return true;
            }
        }
        return false;
    }

    private static bool SkillHasTag(GodotObject skillDef, StringName expectedTag)
    {
        if (skillDef == null || IsEmpty(expectedTag))
        {
            return false;
        }
        foreach (Variant tag in GetArray(skillDef, "tags"))
        {
            if (new StringName(tag.ToString()) == expectedTag)
            {
                return true;
            }
        }
        return false;
    }

    private static GDictionary GetEffectiveResourceCosts(GodotObject combatProfile, int skillLevel)
    {
        if (combatProfile == null || !combatProfile.HasMethod("get_effective_resource_costs"))
        {
            return new GDictionary();
        }
        Variant costs = combatProfile.Call("get_effective_resource_costs", skillLevel);
        return costs.VariantType == Variant.Type.Dictionary ? costs.AsGodotDictionary() : new GDictionary();
    }

    private static GArray GetUnlockedCastVariants(GodotObject combatProfile, int skillLevel)
    {
        if (combatProfile == null || !combatProfile.HasMethod("get_unlocked_cast_variants"))
        {
            return new GArray();
        }
        Variant variants = combatProfile.Call("get_unlocked_cast_variants", skillLevel);
        return variants.VariantType == Variant.Type.Array ? variants.AsGodotArray() : new GArray();
    }

    private static int GetHpBasisPoints(GodotObject unit)
    {
        if (unit == null)
        {
            return HpBasisPointsDenominator;
        }
        GodotObject snapshot = GetObject(unit, "attribute_snapshot");
        if (snapshot == null)
        {
            return HpBasisPointsDenominator;
        }
        int hpMax = Math.Max(GetInt(GetDictionary(snapshot, "_values"), "hp_max", 0), 1);
        int currentHp = Math.Clamp(GetInt(unit, "current_hp"), 0, hpMax);
        return Math.Clamp((currentHp * HpBasisPointsDenominator) / hpMax, 0, HpBasisPointsDenominator);
    }

    private static int DistanceBetweenUnits(GodotObject context, GodotObject firstUnit, GodotObject secondUnit)
    {
        return BattleGridDistanceService.get_distance_between_units(firstUnit as BattleUnitState, secondUnit as BattleUnitState);
    }

    private static int GetSkillLevel(GodotObject unitState, StringName skillId)
    {
        if (unitState == null || IsEmpty(skillId))
        {
            return 0;
        }
        GDictionary knownSkillLevelMap = GetDictionary(unitState, "known_skill_level_map");
        if (knownSkillLevelMap.ContainsKey(skillId))
        {
            return knownSkillLevelMap[skillId].AsInt32();
        }
        return ArrayHasStringName(GetArray(unitState, "known_active_skill_ids"), skillId) ? 1 : 0;
    }

    private static string GetLockedCombatResourceBlockReason(GodotObject unitState, GDictionary costs)
    {
        if (unitState == null)
        {
            return "技能施放者无效。";
        }
        if (GetInt(costs, "mp_cost") > 0 && !HasCombatResourceUnlocked(unitState, "mp"))
        {
            return "法力尚未解锁，无法施放该技能。";
        }
        if (GetInt(costs, "stamina_cost") > 0 && !HasCombatResourceUnlocked(unitState, "stamina"))
        {
            return "体力尚未解锁，无法施放该技能。";
        }
        if (GetInt(costs, "aura_cost") > 0 && !HasCombatResourceUnlocked(unitState, "aura"))
        {
            return "斗气尚未解锁，无法施放该技能。";
        }
        return "";
    }

    private static bool HasCombatResourceUnlocked(GodotObject unitState, StringName resourceId)
    {
        return ArrayHasStringName(GetArray(unitState, "unlocked_combat_resource_ids"), resourceId);
    }

    private static GodotObject NewFromScript(ref Resource script, string path)
    {
        script ??= ResourceLoader.Load<Resource>(path);
        return script?.Call("new").AsGodotObject();
    }

    private static bool ArrayHasStringName(GArray values, StringName expected)
    {
        foreach (Variant value in values)
        {
            if (new StringName(value.ToString()) == expected)
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private static GodotObject GetObject(GodotObject obj, string propertyName)
    {
        if (obj == null)
        {
            return null;
        }
        Variant value = obj.Get(propertyName);
        return value.VariantType == Variant.Type.Nil ? null : value.AsGodotObject();
    }

    private static GDictionary GetDictionary(GodotObject obj, string propertyName)
    {
        if (obj == null)
        {
            return new GDictionary();
        }
        Variant value = obj.Get(propertyName);
        return value.VariantType == Variant.Type.Dictionary ? value.AsGodotDictionary() : new GDictionary();
    }

    private static GArray GetArray(GodotObject obj, string propertyName)
    {
        if (obj == null)
        {
            return new GArray();
        }
        Variant value = obj.Get(propertyName);
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static int GetInt(GodotObject obj, string propertyName, int fallback = 0)
    {
        if (obj == null)
        {
            return fallback;
        }
        Variant value = obj.Get(propertyName);
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
    }

    private static int GetInt(GDictionary dictionary, string key, int fallback = 0)
    {
        if (dictionary == null || !TryGetDictionaryValue(dictionary, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
    }

    private static int GetInt(GDictionary dictionary, StringName key, int fallback = 0)
    {
        if (dictionary == null || !TryGetDictionaryValue(dictionary, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
    }

    private static bool GetBool(GDictionary dictionary, string key, bool fallback = false)
    {
        if (dictionary == null || !TryGetDictionaryValue(dictionary, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsBool();
    }

    private static bool GetBool(GodotObject obj, string propertyName, bool fallback = false)
    {
        if (obj == null)
        {
            return fallback;
        }
        Variant value = obj.Get(propertyName);
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsBool();
    }

    private static StringName GetStringName(GodotObject obj, string propertyName, StringName fallback = default)
    {
        if (obj == null)
        {
            return fallback ?? EmptyStringName;
        }
        Variant value = obj.Get(propertyName);
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => fallback ?? EmptyStringName,
        };
    }

    private static string GetString(GodotObject obj, string propertyName, string fallback = "")
    {
        if (obj == null)
        {
            return fallback;
        }
        Variant value = obj.Get(propertyName);
        return value.VariantType == Variant.Type.Nil ? fallback : value.ToString();
    }

    private static Vector2I GetVector2I(GodotObject obj, string propertyName, Vector2I fallback = default)
    {
        if (obj == null)
        {
            return fallback;
        }
        Variant value = obj.Get(propertyName);
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsVector2I();
    }

    private static bool TryGetDictionaryValue(GDictionary dictionary, string key, out Variant value)
    {
        if (dictionary != null)
        {
            if (dictionary.ContainsKey(key))
            {
                value = dictionary[key];
                return true;
            }
            StringName stringNameKey = new(key);
            if (dictionary.ContainsKey(stringNameKey))
            {
                value = dictionary[stringNameKey];
                return true;
            }
        }
        value = default;
        return false;
    }

    private static bool TryGetDictionaryValue(GDictionary dictionary, StringName key, out Variant value)
    {
        if (dictionary != null)
        {
            if (dictionary.ContainsKey(key))
            {
                value = dictionary[key];
                return true;
            }
            string stringKey = key.ToString();
            if (dictionary.ContainsKey(stringKey))
            {
                value = dictionary[stringKey];
                return true;
            }
        }
        value = default;
        return false;
    }
}
