using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

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
    }

    private sealed class ThreatProfile
    {
        public readonly List<ThreatSkillEntry> SkillEntries = new();
        public int Range;
        public int WeaponRange;
        public int WeaponDamage;
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
        if (scoreInput.skill_def is SkillDef skillDef)
        {
            if (skillDef.skill_id.ToString().StartsWith("mage_", StringComparison.Ordinal))
            {
                return true;
            }
            foreach (StringName tag in skillDef.tags)
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
            foreach (StringName skillId in actor.known_active_skill_ids)
            {
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

    private ThreatProjection GetCurrentActorThreatProjection(IBattleAiScoreContext context)
    {
        BattleUnitState actor = ContextUnitState(context);
        if (actor == null)
        {
            return EmptyThreatProjection();
        }
        return CollectActorThreatProjection(
            context,
            actor.coord,
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
        if (projectedCoord == new Vector2I(-1, -1) || projectedCoord == actor.coord)
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

    private static string BuildProjectionSuppressionKey(HashSet<StringName> suppressedThreatIds)
    {
        if (suppressedThreatIds == null || suppressedThreatIds.Count == 0)
        {
            return "-";
        }
        var parts = new List<string>();
        foreach (StringName unitId in suppressedThreatIds)
        {
            parts.Add(unitId.ToString());
        }
        parts.Sort(StringComparer.Ordinal);
        return string.Join(",", parts);
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
            return actor.coord;
        }
        return coord;
    }

    private static int ResolveActorSurvivalBudget(BattleUnitState actorUnit)
    {
        if (actorUnit == null)
        {
            return 1;
        }
        return Math.Max(actorUnit.current_hp, 1) + Math.Max(actorUnit.current_shield_hp, 0);
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
        var projection = new ThreatProjection();
        BattleState state = ContextState(context);
        BattleUnitState actor = ContextUnitState(context);
        BattleGridService gridService = ContextGridService(context);
        if (state == null || actor == null || gridService == null)
        {
            return projection;
        }
        Vector2I actorCoord =
            actorAnchorCoord != new Vector2I(-1, -1) ? actorAnchorCoord : actor.coord;
        foreach (BattleUnitState threatUnit in state.GetUnitsTyped())
        {
            if (threatUnit == null || !threatUnit.is_alive)
            {
                continue;
            }
            if (threatUnit.faction_id == actor.faction_id)
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
            int distanceToActor = DistanceFromAnchorToUnit(context, actorCoord, threatUnit);
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
        return projection;
    }

    private ThreatProfile GetUnitThreatProfile(IBattleAiScoreContext context, BattleUnitState threatUnit)
    {
        BattleUnitState actor = ContextUnitState(context);
        if (actor == null || threatUnit == null)
        {
            return new ThreatProfile();
        }
        return BuildUnitThreatProfile(context, threatUnit);
    }

    private ThreatProfile BuildUnitThreatProfile(IBattleAiScoreContext context, BattleUnitState threatUnit)
    {
        var profile = new ThreatProfile();
        BattleUnitState actor = ContextUnitState(context);
        if (actor == null || threatUnit == null)
        {
            return profile;
        }
        GDictionary skillDefs = ContextSkillDefs(context);
        foreach (StringName skillId in threatUnit.known_active_skill_ids)
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
            if (
                ProgressionDataUtils.to_string_name(skillDef.combat_profile.target_team_filter)
                == "ally"
            )
            {
                continue;
            }
            List<CombatEffectDef> effectDefs = CollectRoleThreatEffectDefs(
                threatUnit,
                skillDef
            );
            if (!IsDamageSkill(effectDefs) && !IsControlSkill(effectDefs))
            {
                continue;
            }
            int skillRange = BattleRangeService.GetEffectiveSkillThreatRange(
                threatUnit,
                skillDef
            );
            if (skillRange <= 0)
            {
                continue;
            }
            DamageEstimateResult damageEstimate = EstimateDamageForTargetResult(
                threatUnit,
                effectDefs,
                actor,
                normalizedSkillId
            );
            int damage = damageEstimate.IncomingBudgetDamage;
            profile.SkillEntries.Add(new ThreatSkillEntry { Range = skillRange, Damage = damage });
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
        GDictionary dice = threatUnit.weapon_uses_two_hands
            ? threatUnit.weapon_two_handed_dice
            : threatUnit.weapon_one_handed_dice;
        if (dice == null || dice.Count == 0)
        {
            return 0;
        }
        int diceCount = Math.Max(DictInt(dice, "dice_count", 0), 0);
        int diceSides = Math.Max(DictInt(dice, "dice_sides", 0), 0);
        if (diceCount <= 0 || diceSides <= 0)
        {
            return Math.Max(DictInt(dice, "flat_bonus", 0), 0);
        }
        int flatBonus = DictInt(dice, "flat_bonus", 0);
        return Math.Max(RoundToInt(diceCount * (diceSides + 1) / 2.0 + flatBonus), 0);
    }
}
