using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiUnitSkillCandidateEvaluator : RefCounted
{
    private static readonly StringName EmptyStringName = "";
    private static readonly StringName TargetModeUnit = "unit";
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
    private static readonly Vector2I InvalidCoord = new(-1, -1);

    private const int HpBasisPointsDenominator = 10000;
    private const int RoleThreatMinEffectiveRange = 4;
    private const int RoleThreatDistanceWindow = 4;
    private const int RoleThreatMaxApproachDistance = 7;
    private const int RoleThreatMaxContactRange = 2;

    private readonly BattleAiTypedActionHelper _helper = new();
    private readonly BattleAiDecisionEngine _scoreOrdering = new();
    private Resource _battleCommandScript;
    private Resource _battleAiDecisionScript;

    private sealed class UnitInfo
    {
        public GodotObject Source;
        public StringName UnitId = EmptyStringName;
        public StringName FactionId = EmptyStringName;
        public string DisplayName = "";
        public Vector2I Coord = InvalidCoord;
        public Vector2I FootprintSize = Vector2I.One;
        public readonly List<Vector2I> OccupiedCoords = new();
        public bool IsAlive;
        public int CurrentHp;
        public int CurrentShieldHp;
        public int CurrentAp;
        public int CurrentMp;
        public int CurrentStamina;
        public int CurrentAura;
        public int WeaponAttackRange;
        public GDictionary AiBlackboard = new();
        public GDictionary KnownSkillLevelMap = new();
        public GDictionary Cooldowns = new();
        public readonly List<StringName> KnownActiveSkillIds = new();
        public readonly HashSet<StringName> KnownActiveSkillSet = new();
        public readonly HashSet<StringName> UnlockedCombatResourceIds = new();
    }

    private sealed class EvaluationCache
    {
        public readonly GodotObject Context;
        public readonly GodotObject State;
        public readonly UnitInfo Actor;
        public readonly GDictionary SkillDefs;
        private List<UnitInfo> _allUnits;
        private readonly Dictionary<string, List<UnitInfo>> _sortedTargets = new();

        public EvaluationCache(GodotObject context, GodotObject state, UnitInfo actor, GDictionary skillDefs)
        {
            Context = context;
            State = state;
            Actor = actor;
            SkillDefs = skillDefs;
        }

        public List<UnitInfo> AllUnits
        {
            get
            {
                if (_allUnits != null)
                {
                    return _allUnits;
                }
                _allUnits = new List<UnitInfo>();
                GDictionary units = GetDictionary(State, "units");
                foreach (Variant unitValue in units.Values)
                {
                    GodotObject unitObject = unitValue.AsGodotObject();
                    if (unitObject != null)
                    {
                        _allUnits.Add(BuildUnitInfo(unitObject));
                    }
                }
                return _allUnits;
            }
        }

        public bool TryGetSortedTargets(string key, out List<UnitInfo> units)
        {
            return _sortedTargets.TryGetValue(key, out units);
        }

        public void StoreSortedTargets(string key, List<UnitInfo> units)
        {
            _sortedTargets[key] = new List<UnitInfo>(units);
        }
    }

    public GodotObject evaluate(GodotObject action, GodotObject context)
    {
        if (action == null || context == null)
        {
            return null;
        }

        StringName distanceReference = GetStringName(action, "distance_reference");
        int desiredMinDistance = GetInt(action, "desired_min_distance", -1);
        int desiredMaxDistance = GetInt(action, "desired_max_distance", -1);
        if (!HasExplicitDistanceContract(distanceReference, desiredMinDistance, desiredMaxDistance))
        {
            return null;
        }

        GodotObject actorObject = GetObject(context, "unit_state");
        GodotObject state = GetObject(context, "state");
        if (actorObject == null || state == null)
        {
            return null;
        }

        UnitInfo actor = BuildUnitInfo(actorObject);
        GDictionary skillDefs = GetDictionary(context, "skill_defs");
        var cache = new EvaluationCache(context, state, actor, skillDefs);
        bool traceEnabled = GetBool(context, "trace_enabled");
        GDictionary actionTrace = BeginActionTrace(action, context, traceEnabled, new GDictionary
        {
            ["action_kind"] = "unit_skill",
            ["target_selector"] = GetStringName(action, "target_selector").ToString(),
            ["minimum_effective_target_count"] = GetInt(action, "minimum_effective_target_count"),
            ["maximum_friendly_fire_target_count"] = GetInt(action, "maximum_friendly_fire_target_count"),
            ["allow_friendly_lethal"] = GetBool(action, "allow_friendly_lethal"),
            ["distance_reference"] = distanceReference.ToString(),
            ["desired_min_distance"] = desiredMinDistance,
            ["desired_max_distance"] = desiredMaxDistance,
        });

        GodotObject bestDecision = null;
        GodotObject bestScoreInput = null;
        GodotObject fallbackDecision = null;
        GArray knownSkillIds = _helper.ResolveKnownSkillIds(context, GetArray(action, "skill_ids"));

        foreach (Variant skillIdValue in knownSkillIds)
        {
            StringName skillId = ToStringName(skillIdValue);
            TraceCountIncrement(actionTrace, "skill_considered_count", 1);
            GodotObject skillDef = _helper.GetSkillDef(context, skillId);
            GodotObject combatProfile = GetObject(skillDef, "combat_profile");
            if (skillDef == null || combatProfile == null)
            {
                TraceAddBlockReason(actionTrace, "missing_skill_def");
                continue;
            }
            if (GetStringName(combatProfile, "target_mode") != TargetModeUnit)
            {
                TraceAddBlockReason(actionTrace, "non_unit_skill");
                continue;
            }

            string blockReason = _helper.GetSkillCastBlockReason(context, skillDef);
            if (!string.IsNullOrEmpty(blockReason))
            {
                TraceAddBlockReason(actionTrace, blockReason);
                continue;
            }

            GArray targets = _helper.SortTargetUnits(context, GetStringName(combatProfile, "target_team_filter"), GetStringName(action, "target_selector"));
            if (targets.Count == 0)
            {
                TraceAddBlockReason(actionTrace, "no_valid_targets");
                continue;
            }

            GArray castVariants = _helper.GetUnitCastVariants(context, skillDef);
            if (castVariants.Count == 0)
            {
                TraceAddBlockReason(actionTrace, "no_unlocked_unit_variants");
                continue;
            }

            foreach (Variant castVariantValue in castVariants)
            {
                GodotObject castVariant = castVariantValue.AsGodotObject();
                StringName variantId = castVariant != null ? GetStringName(castVariant, "variant_id") : EmptyStringName;
                string variantLabel = FormatSkillVariantLabel(skillDef, castVariant);

                foreach (Variant targetValue in targets)
                {
                    GodotObject target = targetValue.AsGodotObject();
                    if (target == null)
                    {
                        continue;
                    }
                    TraceCountIncrement(actionTrace, "evaluation_count", 1);
                    GodotObject command = _helper.BuildUnitSkillCommand(context, skillId, target, variantId);
                    GodotObject preview = context.Call("preview_command", command).AsGodotObject();
                    if (preview == null || !GetBool(preview, "allowed"))
                    {
                        TraceCountIncrement(actionTrace, "preview_reject_count", 1);
                        continue;
                    }

                    GDictionary positionMetadata = _helper.BuildPositionMetadata(action, context, target, skillDef);
                    positionMetadata["action_label"] = variantLabel;
                    GArray effectDefs = _helper.CollectUnitSkillEffectDefs(skillDef, castVariant, actorObject);
                    GodotObject scoreInput = BuildSkillScoreInput(action, context, skillDef, command, preview, effectDefs, positionMetadata);
                    GDictionary candidateExtra = BuildCandidateExtra(skillId, variantId, skillDef, castVariant, target);

                    if (scoreInput == null)
                    {
                        fallbackDecision ??= _helper.CreateDecision(
                            action,
                            command,
                            $"{actor.DisplayName} 选择对 {GetString(target, "display_name")} 使用 {variantLabel}。");
                        OfferCandidate(actionTrace, traceEnabled, variantLabel, target, command, null, candidateExtra);
                        continue;
                    }

                    if (GetInt(scoreInput, "effective_target_count") < GetInt(action, "minimum_effective_target_count"))
                    {
                        TraceAddBlockReason(actionTrace, "minimum_effective_target_count");
                        continue;
                    }
                    if (!PassesFriendlyFireLimits(action, scoreInput))
                    {
                        TraceAddBlockReason(actionTrace, "friendly_fire_limit");
                        continue;
                    }

                    OfferCandidate(actionTrace, traceEnabled, variantLabel, target, command, scoreInput, candidateExtra);
                    if (!_scoreOrdering.is_better_score_input(scoreInput, bestScoreInput))
                    {
                        continue;
                    }

                    bestScoreInput = scoreInput;
                    bestDecision = _helper.CreateScoredDecision(
                        action,
                        command,
                        scoreInput,
                        $"{actor.DisplayName} 选择对 {GetString(target, "display_name")} 使用 {variantLabel}（评分 {GetInt(scoreInput, "total_score")}）。");
                }
            }
        }

        GodotObject resolvedDecision = bestDecision ?? fallbackDecision;
        FinalizeActionTrace(context, actionTrace, resolvedDecision, traceEnabled);
        return resolvedDecision;
    }

    private static bool HasExplicitDistanceContract(StringName distanceReference, int desiredMinDistance, int desiredMaxDistance)
    {
        return desiredMinDistance >= 0
            && desiredMaxDistance >= desiredMinDistance
            && (distanceReference == DistanceRefTargetUnit || distanceReference == DistanceRefEnemyFrontline);
    }

    private List<StringName> ResolveKnownSkillIds(GodotObject action, UnitInfo actor)
    {
        var results = new List<StringName>();
        var seen = new HashSet<StringName>();
        GArray preferredSkillIds = GetArray(action, "skill_ids");
        IEnumerable<StringName> sourceIds = preferredSkillIds.Count > 0
            ? BuildStringNameList(Variant.From(preferredSkillIds))
            : actor.KnownActiveSkillIds;
        foreach (StringName skillId in sourceIds)
        {
            if (IsEmpty(skillId) || seen.Contains(skillId))
            {
                continue;
            }
            seen.Add(skillId);
            if (actor.KnownActiveSkillSet.Contains(skillId))
            {
                results.Add(skillId);
            }
        }
        return results;
    }

    private string GetSkillCastBlockReason(UnitInfo actor, GodotObject skillDef, GodotObject combatProfile)
    {
        if (actor == null || skillDef == null || combatProfile == null)
        {
            return "技能或目标无效。";
        }

        StringName skillId = GetStringName(skillDef, "skill_id");
        int skillLevel = GetUnitSkillLevel(actor, skillId);
        GDictionary costs = GetEffectiveResourceCosts(combatProfile, skillLevel);
        int cooldown = GetInt(actor.Cooldowns, skillId.ToString(), GetInt(actor.Cooldowns, skillId, 0));
        if (cooldown > 0)
        {
            return $"{GetString(skillDef, "display_name")} 仍在冷却中（{cooldown}）。";
        }

        string lockedReason = GetLockedCombatResourceBlockReason(actor, costs);
        if (!string.IsNullOrEmpty(lockedReason))
        {
            return lockedReason;
        }
        if (actor.CurrentAp < GetInt(costs, "ap_cost", GetInt(combatProfile, "ap_cost")))
        {
            return "AP不足，无法施放该技能。";
        }
        if (actor.CurrentMp < GetInt(costs, "mp_cost", GetInt(combatProfile, "mp_cost")))
        {
            return "法力不足，无法施放该技能。";
        }
        if (actor.CurrentStamina < GetInt(costs, "stamina_cost", GetInt(combatProfile, "stamina_cost")))
        {
            return "体力不足，无法施放该技能。";
        }
        if (actor.CurrentAura < GetInt(costs, "aura_cost", GetInt(combatProfile, "aura_cost")))
        {
            return "斗气不足，无法施放该技能。";
        }
        return "";
    }

    private static string GetLockedCombatResourceBlockReason(UnitInfo actor, GDictionary costs)
    {
        if (actor == null)
        {
            return "技能施放者无效。";
        }
        if (GetInt(costs, "mp_cost") > 0 && !actor.UnlockedCombatResourceIds.Contains(new StringName("mp")))
        {
            return "法力尚未解锁，无法施放该技能。";
        }
        if (GetInt(costs, "stamina_cost") > 0 && !actor.UnlockedCombatResourceIds.Contains(new StringName("stamina")))
        {
            return "体力尚未解锁，无法施放该技能。";
        }
        if (GetInt(costs, "aura_cost") > 0 && !actor.UnlockedCombatResourceIds.Contains(new StringName("aura")))
        {
            return "斗气尚未解锁，无法施放该技能。";
        }
        return "";
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

    private List<UnitInfo> SortTargetUnits(
        EvaluationCache cache,
        StringName targetFilter,
        StringName selector)
    {
        UnitInfo actor = cache.Actor;
        StringName effectiveFilter = targetFilter;
        bool madnessTargetAnyTeam = GetBool(actor.AiBlackboard, "madness_target_any_team");
        if (madnessTargetAnyTeam && selector != SelectorSelf)
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

        string cacheKey = $"{effectiveFilter}:{selector}";
        if (cache.TryGetSortedTargets(cacheKey, out List<UnitInfo> cachedTargets))
        {
            return new List<UnitInfo>(cachedTargets);
        }

        GodotObject forcedTarget = ResolveForcedTargetUnit(cache.Context, effectiveFilter);
        if (forcedTarget != null)
        {
            var forcedTargets = new List<UnitInfo> { BuildUnitInfo(forcedTarget) };
            cache.StoreSortedTargets(cacheKey, forcedTargets);
            return forcedTargets;
        }

        List<UnitInfo> units = CollectUnitsByFilter(cache, effectiveFilter);
        if (selector == SelectorSelf)
        {
            cache.StoreSortedTargets(cacheKey, units);
            return units;
        }

        int nearestDistance = ResolveNearestDistance(actor, units);
        units.Sort((left, right) =>
        {
            int leftHp = GetHpBasisPoints(left);
            int rightHp = GetHpBasisPoints(right);
            int leftDistance = DistanceBetweenUnits(actor, left);
            int rightDistance = DistanceBetweenUnits(actor, right);

            if (selector == SelectorNearestRoleThreatEnemy)
            {
                int leftThreat = GetRoleThreatSelectorScore(cache.SkillDefs, left, nearestDistance, leftDistance);
                int rightThreat = GetRoleThreatSelectorScore(cache.SkillDefs, right, nearestDistance, rightDistance);
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
        });
        cache.StoreSortedTargets(cacheKey, units);
        return units;
    }

    private static GodotObject ResolveForcedTargetUnit(GodotObject context, StringName targetFilter)
    {
        if (context == null || targetFilter != TargetFilterEnemy || !context.HasMethod("resolve_forced_target_unit"))
        {
            return null;
        }
        return context.Call("resolve_forced_target_unit", targetFilter).AsGodotObject();
    }

    private static List<UnitInfo> CollectUnitsByFilter(EvaluationCache cache, StringName targetFilter)
    {
        var results = new List<UnitInfo>();
        foreach (UnitInfo unit in cache.AllUnits)
        {
            if (!MatchesTargetFilter(cache.Actor, unit, targetFilter))
            {
                continue;
            }
            results.Add(unit);
        }
        return results;
    }

    private static bool MatchesTargetFilter(UnitInfo actor, UnitInfo target, StringName targetFilter)
    {
        if (target == null || !target.IsAlive)
        {
            return false;
        }
        if (actor != null && GetBool(actor.AiBlackboard, "madness_target_any_team") && IsMadnessTargetFilter(targetFilter))
        {
            return target.UnitId != actor.UnitId;
        }
        if (targetFilter == TargetFilterAny)
        {
            return true;
        }
        if (targetFilter == TargetFilterSelf)
        {
            return actor != null && target.UnitId == actor.UnitId;
        }
        if (targetFilter == TargetFilterAlly)
        {
            return actor != null && target.FactionId == actor.FactionId;
        }
        if (targetFilter == TargetFilterEnemy)
        {
            return actor != null && target.FactionId != actor.FactionId;
        }
        return false;
    }

    private static bool IsMadnessTargetFilter(StringName targetFilter)
    {
        return targetFilter == TargetFilterAlly || targetFilter == TargetFilterEnemy || targetFilter == TargetFilterAny;
    }

    private static int ResolveNearestDistance(UnitInfo actor, List<UnitInfo> units)
    {
        int nearestDistance = 999999;
        foreach (UnitInfo unit in units)
        {
            nearestDistance = Math.Min(nearestDistance, DistanceBetweenUnits(actor, unit));
        }
        return nearestDistance;
    }

    private static int GetRoleThreatSelectorScore(GDictionary skillDefs, UnitInfo unit, int nearestDistance, int distance)
    {
        if (unit == null)
        {
            return 0;
        }
        int threatRange = ResolveUnitEffectiveThreatRange(skillDefs, unit);
        bool isLocalRoleThreat = threatRange >= RoleThreatMinEffectiveRange
            && distance <= nearestDistance + RoleThreatDistanceWindow
            && distance <= RoleThreatMaxApproachDistance;
        if (isLocalRoleThreat)
        {
            return 1000 + threatRange * 10;
        }
        return ResolveUnitContactThreatRange(skillDefs, unit) > 0 ? 500 : 0;
    }

    private static int ResolveUnitContactThreatRange(GDictionary skillDefs, UnitInfo unit)
    {
        if (unit == null)
        {
            return -1;
        }
        int bestRange = -1;
        foreach (StringName skillId in unit.KnownActiveSkillIds)
        {
            GodotObject skillDef = GetSkillDef(skillDefs, skillId);
            if (!IsHostileThreatSkill(skillDef))
            {
                continue;
            }
            if (!SkillHasTag(skillDef, "melee") && !SkillHasTag(skillDef, "weapon"))
            {
                continue;
            }
            int effectiveRange = ResolveEffectiveSkillRange(unit, skillDef, false);
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
        if (unit.WeaponAttackRange > 0 && unit.WeaponAttackRange <= RoleThreatMaxContactRange)
        {
            bestRange = Math.Max(bestRange, unit.WeaponAttackRange);
        }
        return bestRange;
    }

    private static int ResolveUnitEffectiveThreatRange(GDictionary skillDefs, UnitInfo unit)
    {
        if (unit == null)
        {
            return -1;
        }
        int bestRange = -1;
        foreach (StringName skillId in unit.KnownActiveSkillIds)
        {
            GodotObject skillDef = GetSkillDef(skillDefs, skillId);
            if (!IsHostileThreatSkill(skillDef))
            {
                continue;
            }
            bestRange = Math.Max(bestRange, ResolveEffectiveSkillRange(unit, skillDef, true));
        }
        if (bestRange < 0)
        {
            bestRange = unit.WeaponAttackRange;
        }
        return bestRange;
    }

    private static bool IsHostileThreatSkill(GodotObject skillDef)
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
        if (SkillHasTag(skillDef, "output") || SkillHasTag(skillDef, "melee") || SkillHasTag(skillDef, "bow") || SkillHasTag(skillDef, "weapon"))
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

    private static int ResolveEffectiveSkillRange(UnitInfo unit, GodotObject skillDef, bool includeGroundReach)
    {
        GodotObject combatProfile = GetObject(skillDef, "combat_profile");
        if (unit == null || skillDef == null || combatProfile == null)
        {
            return 0;
        }
        int skillLevel = GetUnitSkillLevel(unit, GetStringName(skillDef, "skill_id"));
        int configuredRange = Math.Max(combatProfile.Call("get_effective_range_value", skillLevel).AsInt32(), 0);
        int range = configuredRange;
        if (IsGroundRelocationSkill(skillDef))
        {
            return Math.Max(range, 0);
        }
        if (RequiresCurrentMeleeWeapon(skillDef) || SkillHasTag(skillDef, "melee") || SkillHasTag(skillDef, "bow") || SkillHasTag(skillDef, "weapon"))
        {
            if (unit.WeaponAttackRange > 0)
            {
                range = unit.WeaponAttackRange;
            }
            else if (SkillHasTag(skillDef, "melee"))
            {
                range = 1;
            }
        }
        if (includeGroundReach && GetStringName(combatProfile, "target_mode") == new StringName("ground") && !IsGroundRelocationSkill(skillDef))
        {
            StringName areaPattern = combatProfile.Call("get_effective_area_pattern", skillLevel).AsStringName();
            if (!IsEmpty(areaPattern) && areaPattern != new StringName("single") && areaPattern != new StringName("self"))
            {
                range += Math.Max(combatProfile.Call("get_effective_area_value", skillLevel).AsInt32(), 0);
            }
        }
        return Math.Max(range, 0);
    }

    private static bool RequiresCurrentMeleeWeapon(GodotObject skillDef)
    {
        GodotObject combatProfile = GetObject(skillDef, "combat_profile");
        if (skillDef == null || combatProfile == null)
        {
            return false;
        }
        if (GetArray(combatProfile, "required_weapon_families").Count > 0)
        {
            return true;
        }
        foreach (Variant effectValue in GetArray(combatProfile, "effect_defs"))
        {
            if (EffectRequiresWeapon(effectValue.AsGodotObject()))
            {
                return true;
            }
        }
        foreach (Variant variantValue in GetArray(combatProfile, "cast_variants"))
        {
            GodotObject variant = variantValue.AsGodotObject();
            if (variant == null)
            {
                continue;
            }
            foreach (Variant effectValue in GetArray(variant, "effect_defs"))
            {
                if (EffectRequiresWeapon(effectValue.AsGodotObject()))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool EffectRequiresWeapon(GodotObject effectDef)
    {
        return effectDef != null && GetBool(GetDictionary(effectDef, "params"), "requires_weapon");
    }

    private static bool IsGroundRelocationSkill(GodotObject skillDef)
    {
        GodotObject combatProfile = GetObject(skillDef, "combat_profile");
        if (skillDef == null || combatProfile == null || GetStringName(combatProfile, "target_mode") != new StringName("ground"))
        {
            return false;
        }
        foreach (Variant effectValue in GetArray(combatProfile, "effect_defs"))
        {
            if (IsGroundRelocationEffect(effectValue.AsGodotObject()))
            {
                return true;
            }
        }
        foreach (Variant variantValue in GetArray(combatProfile, "cast_variants"))
        {
            GodotObject variant = variantValue.AsGodotObject();
            if (variant == null)
            {
                continue;
            }
            foreach (Variant effectValue in GetArray(variant, "effect_defs"))
            {
                if (IsGroundRelocationEffect(effectValue.AsGodotObject()))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool IsGroundRelocationEffect(GodotObject effectDef)
    {
        if (effectDef == null || GetStringName(effectDef, "effect_type") != new StringName("forced_move"))
        {
            return false;
        }
        StringName mode = GetStringName(effectDef, "forced_move_mode");
        return mode == new StringName("jump") || mode == new StringName("blink");
    }

    private static bool SkillHasTag(GodotObject skillDef, StringName expectedTag)
    {
        if (skillDef == null || IsEmpty(expectedTag))
        {
            return false;
        }
        foreach (StringName tag in BuildStringNameList(skillDef.Get("tags")))
        {
            if (tag == expectedTag)
            {
                return true;
            }
        }
        return false;
    }

    private static int GetHpBasisPoints(UnitInfo unit)
    {
        if (unit == null)
        {
            return HpBasisPointsDenominator;
        }
        int hpMax = unit.CurrentHp;
        GodotObject snapshot = GetObject(unit.Source, "attribute_snapshot");
        if (snapshot != null && snapshot.HasMethod("get_value"))
        {
            hpMax = Math.Max(snapshot.Call("get_value", new StringName("hp_max")).AsInt32(), 1);
        }
        int currentHp = Math.Clamp(unit.CurrentHp, 0, Math.Max(hpMax, 1));
        return Math.Clamp((currentHp * HpBasisPointsDenominator) / Math.Max(hpMax, 1), 0, HpBasisPointsDenominator);
    }

    private static List<GodotObject> GetUnitCastVariants(UnitInfo actor, GodotObject skillDef, GodotObject combatProfile)
    {
        var variants = new List<GodotObject>();
        if (skillDef == null || combatProfile == null)
        {
            return variants;
        }
        if (GetArray(combatProfile, "cast_variants").Count == 0)
        {
            variants.Add(null);
            return variants;
        }

        int skillLevel = GetUnitSkillLevel(actor, GetStringName(skillDef, "skill_id"));
        foreach (Variant variantValue in GetUnlockedCastVariants(combatProfile, skillLevel))
        {
            GodotObject castVariant = variantValue.AsGodotObject();
            if (castVariant == null)
            {
                continue;
            }
            if (GetCastVariantTargetMode(skillDef, combatProfile, castVariant) == TargetModeUnit)
            {
                variants.Add(castVariant);
            }
        }
        return variants;
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

    private static GArray CollectUnitSkillEffectDefs(UnitInfo actor, GodotObject skillDef, GodotObject combatProfile, GodotObject castVariant)
    {
        var effectDefs = new GArray();
        int skillLevel = GetRulesUnitSkillLevel(actor, GetStringName(skillDef, "skill_id"));
        if (skillDef != null && combatProfile != null)
        {
            AddUnlockedEffects(effectDefs, GetArray(combatProfile, "effect_defs"), skillLevel, actor != null);
        }
        if (castVariant != null)
        {
            AddUnlockedEffects(effectDefs, GetArray(castVariant, "effect_defs"), skillLevel, actor != null);
        }
        return effectDefs;
    }

    private static int GetRulesUnitSkillLevel(UnitInfo unit, StringName skillId)
    {
        if (unit == null || IsEmpty(skillId))
        {
            return 0;
        }
        if (unit.KnownSkillLevelMap.ContainsKey(skillId))
        {
            return Math.Max(unit.KnownSkillLevelMap[skillId].AsInt32(), 0);
        }
        string skillText = skillId.ToString();
        return unit.KnownSkillLevelMap.ContainsKey(skillText)
            ? Math.Max(unit.KnownSkillLevelMap[skillText].AsInt32(), 0)
            : 0;
    }

    private static void AddUnlockedEffects(GArray results, GArray rawEffects, int skillLevel, bool shouldFilter)
    {
        foreach (Variant effectValue in rawEffects)
        {
            GodotObject effect = effectValue.AsGodotObject();
            if (effect == null)
            {
                continue;
            }
            if (shouldFilter)
            {
                int minLevel = Math.Max(GetInt(effect, "min_skill_level"), 0);
                int maxLevel = GetInt(effect, "max_skill_level", -1);
                if (skillLevel < minLevel || (maxLevel >= 0 && skillLevel > maxLevel))
                {
                    continue;
                }
            }
            results.Add(effect);
        }
    }

    private static StringName GetCastVariantTargetMode(GodotObject skillDef, GodotObject combatProfile, GodotObject castVariant)
    {
        if (castVariant == null)
        {
            return EmptyStringName;
        }
        StringName variantMode = GetStringName(castVariant, "target_mode");
        if (!IsEmpty(variantMode))
        {
            return variantMode;
        }
        return combatProfile != null ? GetStringName(combatProfile, "target_mode") : EmptyStringName;
    }

    private static string FormatSkillVariantLabel(GodotObject skillDef, GodotObject castVariant)
    {
        if (skillDef == null)
        {
            return "";
        }
        string skillName = GetString(skillDef, "display_name");
        string variantName = castVariant != null ? GetString(castVariant, "display_name") : "";
        return string.IsNullOrEmpty(variantName) ? skillName : $"{skillName}·{variantName}";
    }

    private GDictionary BuildPositionMetadata(
        EvaluationCache cache,
        UnitInfo target,
        GodotObject skillDef,
        GodotObject combatProfile,
        StringName distanceReference,
        int configuredMin,
        int configuredMax)
    {
        GDictionary metadata = ResolveDesiredDistanceContract(cache.Actor, skillDef, combatProfile, configuredMin, configuredMax);
        if (distanceReference == DistanceRefTargetUnit)
        {
            metadata["position_target_unit"] = target.Source;
        }
        else if (distanceReference == DistanceRefEnemyFrontline)
        {
            List<UnitInfo> targets = SortTargetUnits(cache, TargetFilterEnemy, SelectorNearestEnemy);
            if (targets.Count > 0)
            {
                metadata["position_target_unit"] = targets[0].Source;
            }
            else
            {
                metadata["position_objective_kind"] = "none";
            }
        }
        else
        {
            metadata["position_objective_kind"] = "none";
        }
        return metadata;
    }

    private static GDictionary ResolveDesiredDistanceContract(UnitInfo actor, GodotObject skillDef, GodotObject combatProfile, int configuredMin, int configuredMax)
    {
        int effectiveAttackRange = skillDef != null ? ResolveEffectiveSkillRange(actor, skillDef, true) : -1;
        int resolvedMax = effectiveAttackRange >= 0 ? effectiveAttackRange : configuredMax;
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

    private GodotObject BuildSkillScoreInput(
        GodotObject action,
        GodotObject context,
        GodotObject skillDef,
        GodotObject command,
        GodotObject preview,
        GArray effectDefs,
        GDictionary metadata)
    {
        if (context == null)
        {
            return null;
        }

        GDictionary scoringMetadata = metadata?.Duplicate(true) ?? new GDictionary();
        scoringMetadata["score_bucket_id"] = GetStringName(action, "score_bucket_id");
        scoringMetadata["action_kind"] = ToStringName(GetVariant(scoringMetadata, "action_kind"), new StringName("skill"));
        scoringMetadata["action_label"] = GetString(scoringMetadata, "action_label", GetString(skillDef, "display_name", GetStringName(action, "action_id").ToString()));
        if (context.HasMethod("merge_current_action_metadata"))
        {
            scoringMetadata = context.Call("merge_current_action_metadata", scoringMetadata).AsGodotDictionary();
        }
        scoringMetadata["score_bucket_id"] = ToStringName(GetVariant(scoringMetadata, "score_bucket_id"), GetStringName(action, "score_bucket_id"));
        return context.Call("build_skill_score_input", skillDef, command, preview, effectDefs, scoringMetadata).AsGodotObject();
    }

    private GodotObject BuildUnitSkillCommand(UnitInfo actor, StringName skillId, UnitInfo target, StringName variantId)
    {
        if (actor == null || target == null)
        {
            return null;
        }
        GodotObject command = NewFromScript(ref _battleCommandScript, "res://scripts/systems/battle/core/battle_command.gd");
        if (command == null)
        {
            return null;
        }
        command.Set("command_type", new StringName("skill"));
        command.Set("unit_id", actor.UnitId);
        command.Set("skill_id", skillId);
        command.Set("skill_variant_id", variantId);
        command.Set("target_unit_id", target.UnitId);
        command.Set("target_coord", target.Coord);
        return command;
    }

    private GodotObject CreateDecision(GodotObject action, GodotObject command, string reasonText)
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

    private static GodotObject NewFromScript(ref Resource script, string path)
    {
        script ??= ResourceLoader.Load<Resource>(path);
        return script?.Call("new").AsGodotObject();
    }

    private GodotObject CreateScoredDecision(GodotObject action, GodotObject command, GodotObject scoreInput, string reasonText)
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

    private GDictionary BuildCandidateExtra(StringName skillId, StringName variantId, GodotObject skillDef, GodotObject castVariant, GodotObject target)
    {
        return new GDictionary
        {
            ["skill_id"] = skillId.ToString(),
            ["skill_variant_id"] = variantId.ToString(),
            ["skill_variant_target_mode"] = _helper.GetCastVariantTargetMode(skillDef, castVariant).ToString(),
            ["target_unit_id"] = GetStringName(target, "unit_id").ToString(),
        };
    }

    private static bool PassesFriendlyFireLimits(GodotObject action, GodotObject scoreInput)
    {
        if (scoreInput == null)
        {
            return false;
        }
        if (GetInt(scoreInput, "estimated_friendly_fire_target_count") > GetInt(action, "maximum_friendly_fire_target_count"))
        {
            return false;
        }
        if (!GetBool(action, "allow_friendly_lethal") && GetInt(scoreInput, "estimated_friendly_lethal_target_count") > 0)
        {
            return false;
        }
        return true;
    }

    private static GDictionary BeginActionTrace(GodotObject action, GodotObject context, bool traceEnabled, GDictionary metadata)
    {
        GDictionary traceMetadata = metadata?.Duplicate(true) ?? new GDictionary();
        if (context != null && context.HasMethod("merge_current_action_metadata"))
        {
            traceMetadata = context.Call("merge_current_action_metadata", traceMetadata).AsGodotDictionary();
        }
        StringName scoreBucketId = ToStringName(GetVariant(traceMetadata, "score_bucket_id"), GetStringName(action, "score_bucket_id"));
        StringName actionId = GetStringName(action, "action_id");
        StringName traceId = context != null && context.HasMethod("next_action_trace_id")
            ? context.Call("next_action_trace_id", actionId).AsStringName()
            : actionId;
        return new GDictionary
        {
            ["trace_id"] = traceId,
            ["action_id"] = actionId.ToString(),
            ["score_bucket_id"] = scoreBucketId.ToString(),
            ["metadata"] = traceMetadata,
            ["evaluation_count"] = 0,
            ["blocked_count"] = 0,
            ["preview_reject_count"] = 0,
            ["candidate_count"] = 0,
            ["block_reasons"] = new GDictionary(),
            ["top_candidates"] = traceEnabled ? new GArray() : new GArray(),
            ["chosen"] = false,
        };
    }

    private static void TraceCountIncrement(GDictionary actionTrace, string key, int amount = 1)
    {
        if (actionTrace == null || actionTrace.Count == 0 || string.IsNullOrEmpty(key))
        {
            return;
        }
        actionTrace[key] = GetInt(actionTrace, key) + amount;
    }

    private static void TraceAddBlockReason(GDictionary actionTrace, string reasonKey)
    {
        if (actionTrace == null || actionTrace.Count == 0 || string.IsNullOrEmpty(reasonKey))
        {
            return;
        }
        TraceCountIncrement(actionTrace, "blocked_count", 1);
        GDictionary blockReasons = GetDictionary(actionTrace, "block_reasons");
        blockReasons[reasonKey] = GetInt(blockReasons, reasonKey) + 1;
        actionTrace["block_reasons"] = blockReasons;
    }

    private static void OfferCandidate(
        GDictionary actionTrace,
        bool traceEnabled,
        string variantLabel,
        GodotObject target,
        GodotObject command,
        GodotObject scoreInput,
        GDictionary candidateExtra)
    {
        if (actionTrace == null || actionTrace.Count == 0)
        {
            return;
        }
        TraceCountIncrement(actionTrace, "candidate_count", 1);
        if (!traceEnabled)
        {
            return;
        }

        GDictionary candidateSummary = BuildCandidateSummary(
            $"{variantLabel}->{GetString(target, "display_name")}",
            command,
            scoreInput,
            candidateExtra);
        GArray topCandidates = GetArray(actionTrace, "top_candidates");
        topCandidates.Add(candidateSummary.Duplicate(true));
        var sorted = new List<GDictionary>();
        foreach (Variant candidateValue in topCandidates)
        {
            if (candidateValue.VariantType == Variant.Type.Dictionary)
            {
                sorted.Add(candidateValue.AsGodotDictionary());
            }
        }
        sorted.Sort((left, right) => GetInt(right, "total_score", -999999).CompareTo(GetInt(left, "total_score", -999999)));
        var trimmed = new GArray();
        for (int i = 0; i < Math.Min(sorted.Count, 5); i++)
        {
            trimmed.Add(sorted[i]);
        }
        actionTrace["top_candidates"] = trimmed;
    }

    private static GDictionary BuildCandidateSummary(string label, GodotObject command, GodotObject scoreInput, GDictionary extra)
    {
        var summary = new GDictionary
        {
            ["label"] = label,
            ["command"] = BuildCommandSummary(command),
            ["total_score"] = scoreInput != null ? GetInt(scoreInput, "total_score") : GetInt(extra, "total_score"),
            ["score_input"] = scoreInput != null && scoreInput.HasMethod("to_dict") ? scoreInput.Call("to_dict").AsGodotDictionary() : new GDictionary(),
        };
        if (extra != null)
        {
            foreach (Variant key in extra.Keys)
            {
                summary[key] = extra[key];
            }
        }
        return summary;
    }

    private static GDictionary BuildCommandSummary(GodotObject command)
    {
        if (command == null)
        {
            return new GDictionary();
        }
        return new GDictionary
        {
            ["command_type"] = GetStringName(command, "command_type").ToString(),
            ["unit_id"] = GetStringName(command, "unit_id").ToString(),
            ["skill_id"] = GetStringName(command, "skill_id").ToString(),
            ["skill_variant_id"] = GetStringName(command, "skill_variant_id").ToString(),
            ["target_unit_id"] = GetStringName(command, "target_unit_id").ToString(),
            ["target_unit_ids"] = GetArray(command, "target_unit_ids").Duplicate(),
            ["target_coord"] = GetVector2I(command, "target_coord", InvalidCoord),
            ["target_coords"] = GetArray(command, "target_coords").Duplicate(),
        };
    }

    private static void FinalizeActionTrace(GodotObject context, GDictionary actionTrace, GodotObject bestDecision, bool traceEnabled)
    {
        if (actionTrace == null || actionTrace.Count == 0)
        {
            return;
        }
        StringName traceId = GetStringName(actionTrace, "trace_id");
        if (bestDecision != null)
        {
            bestDecision.Set("action_trace_id", traceId);
        }
        if (!traceEnabled)
        {
            return;
        }

        if (bestDecision != null)
        {
            actionTrace["best_reason_text"] = GetString(bestDecision, "reason_text");
            actionTrace["best_command"] = BuildCommandSummary(GetObject(bestDecision, "command"));
            GodotObject scoreInput = GetObject(bestDecision, "score_input") ?? GetObject(bestDecision, "skill_score_input");
            actionTrace["best_score_input"] = scoreInput != null && scoreInput.HasMethod("to_dict")
                ? scoreInput.Call("to_dict").AsGodotDictionary()
                : new GDictionary();
        }
        if (context != null && context.HasMethod("record_action_trace"))
        {
            context.Call("record_action_trace", actionTrace);
        }
    }

    private static UnitInfo BuildUnitInfo(GodotObject unitObject)
    {
        var unit = new UnitInfo
        {
            Source = unitObject,
            UnitId = GetStringName(unitObject, "unit_id"),
            FactionId = GetStringName(unitObject, "faction_id"),
            DisplayName = GetString(unitObject, "display_name"),
            Coord = GetVector2I(unitObject, "coord", InvalidCoord),
            FootprintSize = NormalizeFootprint(GetVector2I(unitObject, "footprint_size", Vector2I.One)),
            IsAlive = GetBool(unitObject, "is_alive"),
            CurrentHp = GetInt(unitObject, "current_hp"),
            CurrentShieldHp = GetInt(unitObject, "current_shield_hp"),
            CurrentAp = GetInt(unitObject, "current_ap"),
            CurrentMp = GetInt(unitObject, "current_mp"),
            CurrentStamina = GetInt(unitObject, "current_stamina"),
            CurrentAura = GetInt(unitObject, "current_aura"),
            WeaponAttackRange = GetInt(unitObject, "weapon_attack_range"),
            AiBlackboard = GetDictionary(unitObject, "ai_blackboard"),
            KnownSkillLevelMap = GetDictionary(unitObject, "known_skill_level_map"),
            Cooldowns = GetDictionary(unitObject, "cooldowns"),
        };
        unit.KnownActiveSkillIds.AddRange(BuildStringNameList(unitObject.Get("known_active_skill_ids")));
        foreach (StringName skillId in unit.KnownActiveSkillIds)
        {
            unit.KnownActiveSkillSet.Add(skillId);
        }
        foreach (StringName resourceId in BuildStringNameList(unitObject.Get("unlocked_combat_resource_ids")))
        {
            unit.UnlockedCombatResourceIds.Add(resourceId);
        }
        unit.OccupiedCoords.AddRange(BuildVector2IList(unitObject.Get("occupied_coords")));
        if (unit.OccupiedCoords.Count == 0)
        {
            unit.OccupiedCoords.AddRange(GetFootprintCoords(unit.Coord, unit.FootprintSize));
        }
        return unit;
    }

    private static Vector2I NormalizeFootprint(Vector2I footprint)
    {
        return new Vector2I(Math.Max(footprint.X, 1), Math.Max(footprint.Y, 1));
    }

    private static int DistanceBetweenUnits(UnitInfo first, UnitInfo second)
    {
        if (first == null || second == null)
        {
            return 999999;
        }
        int bestDistance = int.MaxValue;
        foreach (Vector2I firstCoord in first.OccupiedCoords)
        {
            foreach (Vector2I secondCoord in second.OccupiedCoords)
            {
                bestDistance = Math.Min(bestDistance, Math.Abs(firstCoord.X - secondCoord.X) + Math.Abs(firstCoord.Y - secondCoord.Y));
            }
        }
        return bestDistance == int.MaxValue ? 999999 : bestDistance;
    }

    private static List<Vector2I> GetFootprintCoords(Vector2I anchorCoord, Vector2I footprint)
    {
        var coords = new List<Vector2I>();
        Vector2I normalized = NormalizeFootprint(footprint);
        for (int y = 0; y < normalized.Y; y++)
        {
            for (int x = 0; x < normalized.X; x++)
            {
                coords.Add(anchorCoord + new Vector2I(x, y));
            }
        }
        return coords;
    }

    private static int GetUnitSkillLevel(UnitInfo unit, StringName skillId)
    {
        if (unit == null || IsEmpty(skillId))
        {
            return 0;
        }
        if (unit.KnownSkillLevelMap.ContainsKey(skillId))
        {
            return unit.KnownSkillLevelMap[skillId].AsInt32();
        }
        string skillText = skillId.ToString();
        if (unit.KnownSkillLevelMap.ContainsKey(skillText))
        {
            return unit.KnownSkillLevelMap[skillText].AsInt32();
        }
        return unit.KnownActiveSkillSet.Contains(skillId) ? 1 : 0;
    }

    private static GodotObject GetSkillDef(GDictionary skillDefs, StringName skillId)
    {
        if (skillDefs == null || IsEmpty(skillId))
        {
            return null;
        }
        if (skillDefs.ContainsKey(skillId))
        {
            return skillDefs[skillId].AsGodotObject();
        }
        string skillText = skillId.ToString();
        return skillDefs.ContainsKey(skillText) ? skillDefs[skillText].AsGodotObject() : null;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
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

    private static GArray GetArray(GDictionary dictionary, string key)
    {
        Variant value = GetVariant(dictionary, key);
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
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

    private static GDictionary GetDictionary(GDictionary dictionary, string key)
    {
        Variant value = GetVariant(dictionary, key);
        return value.VariantType == Variant.Type.Dictionary ? value.AsGodotDictionary() : new GDictionary();
    }

    private static Variant GetVariant(GDictionary dictionary, string key)
    {
        if (dictionary == null)
        {
            return default;
        }
        if (dictionary.ContainsKey(key))
        {
            return dictionary[key];
        }
        StringName stringNameKey = new(key);
        return dictionary.ContainsKey(stringNameKey) ? dictionary[stringNameKey] : default;
    }

    private static Variant GetVariant(GDictionary dictionary, StringName key)
    {
        if (dictionary == null)
        {
            return default;
        }
        if (dictionary.ContainsKey(key))
        {
            return dictionary[key];
        }
        string stringKey = key.ToString();
        return dictionary.ContainsKey(stringKey) ? dictionary[stringKey] : default;
    }

    private static StringName GetStringName(GodotObject obj, string propertyName, StringName fallback = default)
    {
        if (obj == null)
        {
            return fallback ?? EmptyStringName;
        }
        return ToStringName(obj.Get(propertyName), fallback ?? EmptyStringName);
    }

    private static StringName GetStringName(GDictionary dictionary, string key, StringName fallback = default)
    {
        return ToStringName(GetVariant(dictionary, key), fallback ?? EmptyStringName);
    }

    private static StringName GetStringName(GDictionary dictionary, StringName key, StringName fallback = default)
    {
        return ToStringName(GetVariant(dictionary, key), fallback ?? EmptyStringName);
    }

    private static StringName ToStringName(Variant value, StringName fallback = default)
    {
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

    private static string GetString(GDictionary dictionary, string key, string fallback = "")
    {
        Variant value = GetVariant(dictionary, key);
        return value.VariantType == Variant.Type.Nil ? fallback : value.ToString();
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
        Variant value = GetVariant(dictionary, key);
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
    }

    private static int GetInt(GDictionary dictionary, StringName key, int fallback = 0)
    {
        Variant value = GetVariant(dictionary, key);
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
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

    private static bool GetBool(GDictionary dictionary, string key, bool fallback = false)
    {
        Variant value = GetVariant(dictionary, key);
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsBool();
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

    private static List<StringName> BuildStringNameList(Variant value)
    {
        var result = new List<StringName>();
        if (value.VariantType != Variant.Type.Array)
        {
            return result;
        }
        foreach (Variant item in value.AsGodotArray())
        {
            result.Add(ToStringName(item));
        }
        return result;
    }

    private static List<Vector2I> BuildVector2IList(Variant value)
    {
        var result = new List<Vector2I>();
        if (value.VariantType != Variant.Type.Array)
        {
            return result;
        }
        foreach (Variant item in value.AsGodotArray())
        {
            if (item.VariantType == Variant.Type.Vector2I)
            {
                result.Add(item.AsVector2I());
            }
        }
        return result;
    }
}
