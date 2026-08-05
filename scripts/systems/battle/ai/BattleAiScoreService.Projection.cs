using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class BattleAiScoreService
{
    private sealed class ThreatProjection
    {
        public readonly List<StringName> UnitIds = new();
        public readonly Dictionary<StringName, int> ExpectedDamageByUnitId = new();
        public int ExpectedDamage;

        public int Count => UnitIds.Count;
    }

    private sealed class ThreatSkillEntry
    {
        public int Range;
        public int Damage;
        public int UnguardedDamage;
        public readonly List<int> UnguardedPhysicalDamageByInstance = new();
    }

    private sealed class ThreatProfile
    {
        public readonly List<ThreatSkillEntry> SkillEntries = new();
        public int Range;
        public int WeaponRange;
        public int WeaponDamage;
        public int UnguardedWeaponDamage;
        public readonly List<int> UnguardedWeaponPhysicalDamageByInstance = new();
        public bool GuardAwareWeaponInitialized;
    }

    private static bool ShouldPopulateSurvivalProjection(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context
    )
    {
        if (scoreInput == null)
        {
            return false;
        }
        if (scoreInput.score_bucket_id == "archer_survival")
        {
            return true;
        }
        BattleAiIntent intent = BattleAiActionIntent.ToKind(scoreInput.action_intent);
        if (
            intent == BattleAiIntent.Positioning
            || intent == BattleAiIntent.Survival
            || intent == BattleAiIntent.Escape
        )
        {
            return true;
        }
        SkillDefinition skillDefinition = ResolveScoreInputSkillDefinition(scoreInput, context);
        if (skillDefinition != null)
        {
            if (skillDefinition.SkillId.ToString().StartsWith("mage_", StringComparison.Ordinal))
            {
                return true;
            }
            foreach (StringName tag in skillDefinition.Tags)
            {
                if (ProgressionDataUtils.to_string_name(tag) == "mage")
                {
                    return true;
                }
            }
        }
        BattleUnitState actor = ContextUnitState(context);
        if (actor != null)
        {
            foreach (BattleAvailableSkillEntry entry in BuildActorAvailabilityEntries(context, actor))
            {
                StringName skillId = entry.EntryRef.SkillId;
                if (
                    skillId.ToString().StartsWith("mage_", StringComparison.Ordinal)
                    && scoreInput.action_kind == "ground_reposition_skill"
                )
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static IReadOnlyList<BattleAvailableSkillEntry> BuildActorAvailabilityEntries(
        IBattleAiScoreContext context,
        BattleUnitState actor
    )
    {
        BattleSkillAvailabilityService availabilityService = new(
            context?.skill_catalog,
            ContextSkillDefinitions(context)
        );
        BattleSkillAvailabilityView availabilityView = availabilityService.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = actor,
                Consumer = BattleSkillAvailabilityConsumer.AiScoring,
                IncludeKnownSkills = true,
                IncludeEquipmentSkills = false,
                IncludeScopedAutoCast = false,
            }
        );
        return availabilityView.SkillEntries;
    }

    private ThreatProjection GetCurrentActorThreatProjection(IBattleAiScoreContext context)
    {
        BattleUnitState actor = ContextUnitState(context);
        if (actor == null)
        {
            return EmptyThreatProjection();
        }
        return CollectActorThreatProjection(
            context,
            actor.GetAnchorCoord(),
            new HashSet<StringName>()
        );
    }

    private ThreatProjection GetProjectedActorThreatProjection(
        IBattleAiScoreContext context,
        Vector2I projectedCoord,
        HashSet<StringName> suppressedThreatIds,
        ThreatProjection preProjection
    )
    {
        BattleUnitState actor = ContextUnitState(context);
        if (actor == null)
        {
            return EmptyThreatProjection();
        }
        if (
            projectedCoord == new Vector2I(-1, -1)
            || projectedCoord == actor.GetAnchorCoord()
        )
        {
            return SubtractSuppressedThreatsFromProjection(preProjection, suppressedThreatIds);
        }
        return CollectActorThreatProjection(
            context,
            projectedCoord,
            suppressedThreatIds
        );
    }

    private static ThreatProjection SubtractSuppressedThreatsFromProjection(
        ThreatProjection preProjection,
        HashSet<StringName> suppressedThreatIds
    )
    {
        if (preProjection == null)
        {
            return EmptyThreatProjection();
        }
        if (suppressedThreatIds == null || suppressedThreatIds.Count == 0)
        {
            return preProjection;
        }
        var result = new ThreatProjection();
        foreach (StringName unitId in preProjection.UnitIds)
        {
            if (suppressedThreatIds.Contains(unitId))
            {
                continue;
            }
            result.UnitIds.Add(unitId);
            int damage = preProjection.ExpectedDamageByUnitId.TryGetValue(unitId, out int value)
                ? value
                : 0;
            result.ExpectedDamageByUnitId[unitId] = damage;
            result.ExpectedDamage += damage;
        }
        return result;
    }

    private static long BuildProjectionSuppressionSignature(HashSet<StringName> suppressedThreatIds)
    {
        if (suppressedThreatIds == null || suppressedThreatIds.Count == 0)
        {
            return 0;
        }
        var parts = new List<StringName>();
        foreach (StringName unitId in suppressedThreatIds)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(unitId);
            if (!IsEmpty(normalized))
            {
                parts.Add(normalized);
            }
        }
        parts.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        unchecked
        {
            long hash = 1469598103934665603;
            foreach (StringName unitId in parts)
            {
                hash = (hash ^ unitId.GetHashCode()) * 1099511628211;
            }
            return hash;
        }
    }

    private static ThreatProjection EmptyThreatProjection()
    {
        return new ThreatProjection();
    }

    private static Vector2I ResolveProjectedActorCoord(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        ScorePositionMetadata metadata
    )
    {
        Vector2I coord = ResolvePositionAnchorCoord(scoreInput, context, metadata);
        BattleUnitState actor = ContextUnitState(context);
        if (coord == new Vector2I(-1, -1) && actor != null)
        {
            return actor.GetAnchorCoord();
        }
        return coord;
    }

    private static int ResolveActorSurvivalBudget(BattleUnitState actorUnit)
    {
        if (actorUnit == null)
        {
            return 1;
        }
        return Math.Max(actorUnit.GetCurrentHp(), 1)
            + Math.Max(actorUnit.GetShieldStateTyped().CurrentHp, 0);
    }

    private static HashSet<StringName> BuildSuppressedThreatUnitIds(BattleAiScoreInput scoreInput)
    {
        var suppressedIds = new HashSet<StringName>();
        if (scoreInput == null)
        {
            return suppressedIds;
        }
        foreach (StringName targetId in scoreInput.estimated_lethal_target_ids)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(targetId);
            if (!IsEmpty(normalized))
            {
                suppressedIds.Add(normalized);
            }
        }
        foreach (StringName targetId in scoreInput.estimated_control_target_ids)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(targetId);
            if (!IsEmpty(normalized))
            {
                suppressedIds.Add(normalized);
            }
        }
        return suppressedIds;
    }

    private ThreatProjection CollectActorThreatProjection(
        IBattleAiScoreContext context,
        Vector2I actorAnchorCoord,
        HashSet<StringName> suppressedThreatIds
    )
    {
        BattleState state = ContextState(context);
        BattleUnitState actor = ContextUnitState(context);
        BattleGridService gridService = ContextGridService(context);
        if (state == null || actor == null || gridService == null)
        {
            return EmptyThreatProjection();
        }
        ThreatProjectionCacheKey cacheKey = _decisionScopeActive
            ? BuildThreatProjectionCacheKey(context, actorAnchorCoord, suppressedThreatIds)
            : default;
        if (
            _decisionScopeActive
            && _threatProjectionCache.TryGetValue(cacheKey, out ThreatProjection cachedProjection)
        )
        {
            return cachedProjection;
        }
        var projection = new ThreatProjection();
        Vector2I actorCoord =
            actorAnchorCoord != new Vector2I(-1, -1)
                ? actorAnchorCoord
                : actor.GetAnchorCoord();
        foreach (BattleUnitState threatUnit in GetHostileThreatUnitsForActor(context))
        {
            if (threatUnit == null)
            {
                continue;
            }
            if (suppressedThreatIds != null && suppressedThreatIds.Contains(threatUnit.unit_id))
            {
                continue;
            }
            ThreatProfile threatProfile = GetUnitThreatProfile(context, threatUnit);
            if (threatProfile.Range <= 0)
            {
                continue;
            }
            int distanceToActor = DistanceFromAnchorToUnitCached(context, actorCoord, threatUnit);
            if (distanceToActor < 0 || distanceToActor > threatProfile.Range)
            {
                continue;
            }
            projection.UnitIds.Add(threatUnit.unit_id);
            int threatDamage = EstimateThreatProfileDamageAtDistance(
                threatProfile,
                distanceToActor
            );
            projection.ExpectedDamageByUnitId[threatUnit.unit_id] = threatDamage;
            projection.ExpectedDamage += threatDamage;
        }
        projection.UnitIds.Sort(
            (left, right) => string.CompareOrdinal(left.ToString(), right.ToString())
        );
        if (_decisionScopeActive)
        {
            _threatProjectionCache[cacheKey] = projection;
        }
        return projection;
    }

    private IReadOnlyList<BattleUnitState> GetHostileThreatUnitsForActor(
        IBattleAiScoreContext context
    )
    {
        BattleState state = ContextState(context);
        BattleUnitState actor = ContextUnitState(context);
        if (state == null || actor == null)
        {
            return Array.Empty<BattleUnitState>();
        }
        StringName actorFactionId = ProgressionDataUtils.to_string_name(actor.faction_id);
        if (
            _decisionScopeActive
            && _hostileUnitsByActorFactionCache.TryGetValue(
                actorFactionId,
                out List<BattleUnitState> cachedUnits
            )
        )
        {
            return cachedUnits;
        }

        var units = new List<BattleUnitState>();
        foreach (BattleUnitState unitState in state.GetUnitsTyped())
        {
            if (
                unitState == null
                || !unitState.IsAlive()
                || unitState.faction_id == actorFactionId
            )
            {
                continue;
            }
            units.Add(unitState);
        }
        if (_decisionScopeActive)
        {
            _hostileUnitsByActorFactionCache[actorFactionId] = units;
        }
        return units;
    }

    private ThreatProfile GetUnitThreatProfile(IBattleAiScoreContext context, BattleUnitState threatUnit)
    {
        BattleUnitState actor = ContextUnitState(context);
        if (actor == null || threatUnit == null)
        {
            return new ThreatProfile();
        }
        StringName threatUnitId = ProgressionDataUtils.to_string_name(threatUnit.unit_id);
        if (
            _decisionScopeActive
            && !IsEmpty(threatUnitId)
            && _threatProfileCache.TryGetValue(threatUnitId, out ThreatProfile cachedProfile)
        )
        {
            return cachedProfile;
        }
        ThreatProfile profile = BuildUnitThreatProfile(context, threatUnit);
        if (_decisionScopeActive && !IsEmpty(threatUnitId))
        {
            _threatProfileCache[threatUnitId] = profile;
        }
        return profile;
    }

    private static ThreatProjectionCacheKey BuildThreatProjectionCacheKey(
        IBattleAiScoreContext context,
        Vector2I actorAnchorCoord,
        HashSet<StringName> suppressedThreatIds
    )
    {
        BattleUnitState actor = ContextUnitState(context);
        StringName actorId = actor != null ? ProgressionDataUtils.to_string_name(actor.unit_id) : "";
        Vector2I coord =
            actorAnchorCoord != new Vector2I(-1, -1)
                ? actorAnchorCoord
                : actor?.GetAnchorCoord() ?? new Vector2I(-1, -1);
        return new ThreatProjectionCacheKey(
            actorId,
            coord,
            BuildProjectionSuppressionSignature(suppressedThreatIds)
        );
    }

    private ThreatProfile BuildUnitThreatProfile(IBattleAiScoreContext context, BattleUnitState threatUnit)
    {
        var profile = new ThreatProfile();
        BattleUnitState actor = ContextUnitState(context);
        if (actor == null || threatUnit == null)
        {
            return profile;
        }
        BattleUnitState unguardedActor = BuildUnguardedThreatProjectionTarget(actor);
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            ContextSkillDefinitions(context);
        foreach (
            StringName skillId in threatUnit.GetKnownActiveSkillsViewTyped()
        )
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
            if (
                skillDefinition.CombatProfile.TargetFilterKind == BattleTargetFilter.Ally
                || skillDefinition.CombatProfile.TargetFilterKind == BattleTargetFilter.Self
            )
            {
                continue;
            }
            List<CombatEffectDefinition> effectDefinitions = CollectRoleThreatEffectDefinitions(
                threatUnit,
                skillDefinition,
                ContextSkillCatalog(context)
            );
            if (!IsDamageSkill(effectDefinitions) && !IsControlSkill(effectDefinitions))
            {
                continue;
            }
            int skillRange = BattleRangeService.GetEffectiveSkillThreatRange(
                threatUnit,
                skillDefinition,
                ContextSkillCatalog(context)
            );
            if (skillRange <= 0)
            {
                continue;
            }
            DamageEstimateResult damageEstimate = EstimateDamageForTargetResult(
                threatUnit,
                effectDefinitions,
                actor,
                normalizedSkillId
            );
            DamageEstimateResult unguardedDamageEstimate =
                ReferenceEquals(unguardedActor, actor)
                    ? damageEstimate
                    : EstimateDamageForTargetResult(
                        threatUnit,
                        effectDefinitions,
                        unguardedActor,
                        normalizedSkillId
                    );
            var threatEntry = new ThreatSkillEntry
            {
                Range = skillRange,
                Damage = damageEstimate.IncomingBudgetDamage,
                UnguardedDamage = unguardedDamageEstimate.IncomingBudgetDamage,
            };
            threatEntry.UnguardedPhysicalDamageByInstance.AddRange(
                CollectPhysicalDamageInstances(
                    threatUnit,
                    effectDefinitions,
                    unguardedDamageEstimate
                )
            );
            profile.SkillEntries.Add(threatEntry);
            profile.Range = Math.Max(profile.Range, skillRange);
        }
        profile.WeaponRange = BattleRangeService.GetWeaponAttackRange(threatUnit);
        profile.WeaponDamage = EstimateWeaponAverageDamage(threatUnit);
        if (profile.WeaponRange > 0)
        {
            profile.Range = Math.Max(profile.Range, profile.WeaponRange);
        }
        return profile;
    }

    private static int EstimateThreatProfileDamageAtDistance(
        ThreatProfile threatProfile,
        int distanceToActor
    )
    {
        int bestDamage = 0;
        if (threatProfile == null)
        {
            return 0;
        }
        foreach (ThreatSkillEntry entry in threatProfile.SkillEntries)
        {
            if (entry == null || (distanceToActor >= 0 && entry.Range < distanceToActor))
            {
                continue;
            }
            bestDamage = Math.Max(bestDamage, entry.Damage);
        }
        if (distanceToActor < 0 || threatProfile.WeaponRange >= distanceToActor)
        {
            bestDamage = Math.Max(bestDamage, threatProfile.WeaponDamage);
        }
        return bestDamage;
    }

    private static int EstimateWeaponAverageDamage(BattleUnitState threatUnit)
    {
        if (threatUnit == null)
        {
            return 0;
        }
        BattleWeaponProjectionValues weaponProjection =
            threatUnit.GetWeaponProjectionReadViewTyped().Values;
        BattleWeaponDiceValues dice = weaponProjection.ActiveDice;
        if (!dice.HasUsableDice)
        {
            return 0;
        }
        int diceCount = Math.Max(dice.DiceCount, 0);
        int diceSides = Math.Max(dice.DiceSides, 0);
        if (diceCount <= 0 || diceSides <= 0)
        {
            return Math.Max(dice.FlatBonus, 0);
        }
        int flatBonus = dice.FlatBonus;
        return Math.Max(RoundToInt(diceCount * (diceSides + 1) / 2.0 + flatBonus), 0);
    }
}
