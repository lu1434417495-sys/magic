using System;
using System.Collections.Generic;
using Godot;
using static GdInterop;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiScoreProjectionService : RefCounted
{
    private static readonly StringName EmptyStringName = "";
    private static readonly Vector2I InvalidCoord = new(-1, -1);
    private const int ThreatMultiplierBasisPointsDenominator = 10000;

    private sealed class UnitInfo
    {
        public GodotObject Source;
        public StringName UnitId = EmptyStringName;
        public StringName FactionId = EmptyStringName;
        public Vector2I Coord = InvalidCoord;
        public Vector2I FootprintSize = Vector2I.One;
        public readonly List<Vector2I> OccupiedCoords = new();
        public bool IsAlive;
        public int CurrentHp;
        public int CurrentShieldHp;
        public int WeaponAttackRange;
        public bool WeaponUsesTwoHands;
        public WeaponDice WeaponOneHandedDice = WeaponDice.Empty;
        public WeaponDice WeaponTwoHandedDice = WeaponDice.Empty;
        public readonly List<StringName> KnownActiveSkillIds = new();
        public readonly Dictionary<StringName, int> KnownSkillLevelMap = new();
    }

    private readonly record struct WeaponDice(int DiceCount, int DiceSides, int FlatBonus)
    {
        public static WeaponDice Empty => new(0, 0, 0);

        public int AverageDamage()
        {
            if (DiceCount <= 0 || DiceSides <= 0)
            {
                return Math.Max(FlatBonus, 0);
            }
            return Math.Max((int)Math.Round(DiceCount * (DiceSides + 1) / 2.0 + FlatBonus), 0);
        }
    }

    private readonly struct ThreatSkillEntry
    {
        public readonly int Range;
        public readonly int Damage;

        public ThreatSkillEntry(int range, int damage)
        {
            Range = range;
            Damage = damage;
        }
    }

    private readonly struct EffectInfo
    {
        public readonly BattleEffectKind EffectKind;
        public readonly StringName StatusId;
        public readonly StringName SaveFailureStatusId;
        public readonly int Power;
        public readonly int DamageRatioPercent;
        public readonly bool RequiresWeapon;
        public readonly BattleForcedMoveMode ForcedMoveMode;

        public EffectInfo(
            BattleEffectKind effectKind,
            StringName statusId,
            StringName saveFailureStatusId,
            int power,
            int damageRatioPercent,
            bool requiresWeapon,
            BattleForcedMoveMode forcedMoveMode)
        {
            EffectKind = effectKind;
            StatusId = statusId;
            SaveFailureStatusId = saveFailureStatusId;
            Power = power;
            DamageRatioPercent = damageRatioPercent;
            RequiresWeapon = requiresWeapon;
            ForcedMoveMode = forcedMoveMode;
        }
    }

    private sealed class ThreatProfile
    {
        public int Range;
        public int WeaponRange;
        public int WeaponDamage;
        public readonly List<ThreatSkillEntry> SkillEntries = new();
    }

    private readonly struct Projection
    {
        public readonly List<StringName> UnitIds;
        public readonly Dictionary<StringName, int> DamageByUnitId;
        public readonly int ExpectedDamage;

        public Projection(List<StringName> unitIds, Dictionary<StringName, int> damageByUnitId, int expectedDamage)
        {
            UnitIds = unitIds;
            DamageByUnitId = damageByUnitId;
            ExpectedDamage = expectedDamage;
        }
    }

    private sealed class PositionMetadata
    {
        public int DesiredMinDistance;
        public int DesiredMaxDistance;
        public int CurrentDistance;
        public int SafeDistance;
        public BattlePositionObjectiveKind ExplicitObjectiveKind;
        public Vector2I AnchorCoord;
        public GodotObject TargetUnit;
    }

    private readonly record struct PositionScoreProfile(
        int BaseScore,
        int DistanceStep,
        int UndershootPenalty,
        int OvershootPenalty
    );

    public bool populate_position_and_threat(GodotObject score_input, GodotObject context, GDictionary metadata, GodotObject score_profile)
    {
        if (score_input == null || context == null || score_profile == null)
        {
            return false;
        }

        metadata ??= new GDictionary();
        GodotObject actorObject = context.Get("unit_state").AsGodotObject();
        GodotObject state = context.Get("state").AsGodotObject();
        if (actorObject == null)
        {
            return false;
        }

        UnitInfo actor = BuildUnitInfo(actorObject);
        PositionMetadata positionMetadata = BuildPositionMetadata(metadata);
        PositionScoreProfile positionProfile = BuildPositionScoreProfile(score_profile);
        PopulatePosition(score_input, actor, positionMetadata, positionProfile);
        if (state != null && ShouldPopulateSurvivalProjection(score_input, context))
        {
            PopulateThreatProjection(score_input, context, state, actor, positionMetadata);
        }
        return true;
    }

    private void PopulatePosition(GodotObject scoreInput, UnitInfo actor, PositionMetadata metadata, PositionScoreProfile scoreProfile)
    {
        scoreInput.Set("desired_min_distance", metadata.DesiredMinDistance);
        scoreInput.Set("desired_max_distance", metadata.DesiredMaxDistance);
        scoreInput.Set("position_current_distance", metadata.CurrentDistance);
        scoreInput.Set("position_safe_distance", metadata.SafeDistance);

        if (metadata.ExplicitObjectiveKind == BattlePositionObjectiveKind.None)
        {
            scoreInput.Set("position_objective_kind", BattleTypedNames.PositionNone);
            scoreInput.Set("position_anchor_coord", actor.Coord);
            scoreInput.Set("distance_to_primary_coord", -1);
            scoreInput.Set("position_objective_score", 0);
            return;
        }

        Vector2I anchorCoord = ResolvePositionAnchorCoord(scoreInput, actor, metadata);
        int distanceToPrimary;
        int currentDistanceToTarget = -1;
        BattlePositionObjectiveKind objectiveKind;
        if (metadata.TargetUnit != null)
        {
            UnitInfo target = BuildUnitInfo(metadata.TargetUnit);
            objectiveKind = metadata.ExplicitObjectiveKind != BattlePositionObjectiveKind.Unknown ? metadata.ExplicitObjectiveKind : BattlePositionObjectiveKind.DistanceBand;
            distanceToPrimary = DistanceFromAnchorToUnit(anchorCoord, actor.FootprintSize, target);
            if (objectiveKind == BattlePositionObjectiveKind.DistanceBandProgress)
            {
                currentDistanceToTarget = DistanceFromAnchorToUnit(actor.Coord, actor.FootprintSize, target);
            }
        }
        else
        {
            objectiveKind = metadata.ExplicitObjectiveKind != BattlePositionObjectiveKind.Unknown ? metadata.ExplicitObjectiveKind : BattlePositionObjectiveKind.CastDistance;
            Vector2I primaryCoord = GetVector2I(scoreInput, "primary_coord", InvalidCoord);
            distanceToPrimary = primaryCoord != InvalidCoord ? DistanceFromUnitToCoord(actor, primaryCoord) : -1;
        }

        int positionScore = BuildPositionObjectiveScore(
            objectiveKind,
            distanceToPrimary,
            metadata.DesiredMinDistance,
            metadata.DesiredMaxDistance,
            currentDistanceToTarget,
            scoreProfile);
        scoreInput.Set("position_objective_kind", BattleTypedNames.ToStringName(objectiveKind));
        scoreInput.Set("position_anchor_coord", anchorCoord);
        scoreInput.Set("distance_to_primary_coord", distanceToPrimary);
        scoreInput.Set("position_objective_score", positionScore);
    }

    private void PopulateThreatProjection(GodotObject scoreInput, GodotObject context, GodotObject state, UnitInfo actor, PositionMetadata metadata)
    {
        int actorHpBudget = Math.Max(actor.CurrentHp, 1) + Math.Max(actor.CurrentShieldHp, 0);
        Vector2I projectedCoord = ResolvePositionAnchorCoord(scoreInput, actor, metadata);
        if (projectedCoord == InvalidCoord)
        {
            projectedCoord = actor.Coord;
        }

        HashSet<StringName> suppressedThreatIds = BuildSuppressedThreatIds(scoreInput);
        Projection preProjection = GetCurrentActorThreatProjection(context, state, actor);
        Projection postProjection = projectedCoord == actor.Coord || projectedCoord == InvalidCoord
            ? SubtractSuppressedThreats(preProjection, suppressedThreatIds)
            : GetProjectedActorThreatProjection(context, state, actor, projectedCoord, suppressedThreatIds);

        scoreInput.Set("has_post_action_threat_projection", true);
        scoreInput.Set("projected_actor_coord", projectedCoord);
        scoreInput.Set("pre_action_threat_unit_ids", ToStringNameArray(preProjection.UnitIds));
        scoreInput.Set("pre_action_threat_count", preProjection.UnitIds.Count);
        scoreInput.Set("pre_action_threat_expected_damage", preProjection.ExpectedDamage);
        scoreInput.Set("pre_action_survival_margin", actorHpBudget - preProjection.ExpectedDamage);
        scoreInput.Set("pre_action_is_lethal_survival_risk", preProjection.UnitIds.Count > 0 && preProjection.ExpectedDamage >= actorHpBudget);
        scoreInput.Set("post_action_remaining_threat_unit_ids", ToStringNameArray(postProjection.UnitIds));
        scoreInput.Set("post_action_remaining_threat_count", postProjection.UnitIds.Count);
        scoreInput.Set("post_action_remaining_threat_expected_damage", postProjection.ExpectedDamage);
        scoreInput.Set("post_action_survival_margin", actorHpBudget - postProjection.ExpectedDamage);
        scoreInput.Set("post_action_is_lethal_survival_risk", postProjection.UnitIds.Count > 0 && postProjection.ExpectedDamage >= actorHpBudget);
    }

    private Projection GetCurrentActorThreatProjection(GodotObject context, GodotObject state, UnitInfo actor)
    {
        GDictionary cache = GetDictionary(context, "score_projection_cache");
        string cacheKey = $"cs_current_actor_threat_projection:{actor.UnitId}";
        if (TryProjectionFromCache(cache, cacheKey, out Projection cached))
        {
            return cached;
        }
        Projection projection = CollectActorThreatProjection(context, state, actor, actor.Coord, new HashSet<StringName>());
        cache[cacheKey] = ProjectionToDictionary(projection);
        return projection;
    }

    private Projection GetProjectedActorThreatProjection(
        GodotObject context,
        GodotObject state,
        UnitInfo actor,
        Vector2I projectedCoord,
        HashSet<StringName> suppressedThreatIds)
    {
        GDictionary cache = GetDictionary(context, "score_projection_cache");
        string cacheKey = $"cs_actor_threat_projection:{actor.UnitId}:{projectedCoord}:{BuildSuppressionKey(suppressedThreatIds)}";
        if (TryProjectionFromCache(cache, cacheKey, out Projection cached))
        {
            return cached;
        }
        Projection projection = CollectActorThreatProjection(context, state, actor, projectedCoord, suppressedThreatIds);
        cache[cacheKey] = ProjectionToDictionary(projection);
        return projection;
    }

    private Projection CollectActorThreatProjection(
        GodotObject context,
        GodotObject state,
        UnitInfo actor,
        Vector2I actorAnchorCoord,
        HashSet<StringName> suppressedThreatIds)
    {
        var threatUnitIds = new List<StringName>();
        var damageByUnitId = new Dictionary<StringName, int>();
        int expectedDamage = 0;
        GDictionary units = GetDictionary(state, "units");
        foreach (Variant unitVariant in units.Values)
        {
            GodotObject threatObject = unitVariant.AsGodotObject();
            if (threatObject == null)
            {
                continue;
            }
            UnitInfo threatUnit = BuildUnitInfo(threatObject);
            if (!threatUnit.IsAlive || threatUnit.FactionId == actor.FactionId || suppressedThreatIds.Contains(threatUnit.UnitId))
            {
                continue;
            }
            ThreatProfile threatProfile = GetUnitThreatProfile(context, actor, threatUnit);
            if (threatProfile.Range <= 0)
            {
                continue;
            }
            int distanceToActor = DistanceFromAnchorToUnit(actorAnchorCoord, actor.FootprintSize, threatUnit);
            if (distanceToActor < 0 || distanceToActor > threatProfile.Range)
            {
                continue;
            }
            int threatDamage = EstimateThreatProfileDamageAtDistance(threatProfile, distanceToActor);
            threatUnitIds.Add(threatUnit.UnitId);
            damageByUnitId[threatUnit.UnitId] = threatDamage;
            expectedDamage += threatDamage;
        }
        threatUnitIds.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        return new Projection(threatUnitIds, damageByUnitId, expectedDamage);
    }

    private ThreatProfile GetUnitThreatProfile(GodotObject context, UnitInfo actor, UnitInfo threatUnit)
    {
        GDictionary cache = GetDictionary(context, "score_projection_cache");
        string cacheKey = $"cs_threat_profile:{threatUnit.UnitId}->{actor.UnitId}";
        Variant cachedValue = GetVariant(cache, cacheKey);
        if (cachedValue.VariantType == Variant.Type.Dictionary)
        {
            return ThreatProfileFromDictionary(cachedValue.AsGodotDictionary());
        }

        ThreatProfile profile = BuildUnitThreatProfile(context, actor, threatUnit);
        cache[cacheKey] = ThreatProfileToDictionary(profile);
        return profile;
    }

    private ThreatProfile BuildUnitThreatProfile(GodotObject context, UnitInfo actor, UnitInfo threatUnit)
    {
        var profile = new ThreatProfile();
        GDictionary skillDefs = GetDictionary(context, "skill_defs");
        foreach (StringName skillId in threatUnit.KnownActiveSkillIds)
        {
            GodotObject skillDef = GetSkillDef(skillDefs, skillId);
            GodotObject combatProfile = skillDef?.Get("combat_profile").AsGodotObject();
            if (skillDef == null || combatProfile == null)
            {
                continue;
            }
            if (BattleTypedNames.ToTargetFilter(GetStringName(combatProfile, "target_team_filter")) == BattleTargetFilter.Ally)
            {
                continue;
            }
            int skillLevel = GetUnitSkillLevel(threatUnit, skillId);
            List<EffectInfo> effects = CollectRoleThreatEffects(skillDef, combatProfile, skillLevel);
            if (!IsDamageSkill(effects) && !IsControlSkill(effects))
            {
                continue;
            }
            int skillRange = ResolveEffectiveSkillThreatRange(threatUnit, skillDef, combatProfile, skillLevel, effects);
            if (skillRange <= 0)
            {
                continue;
            }
            int damage = EstimateDamageForTarget(effects, actor);
            profile.SkillEntries.Add(new ThreatSkillEntry(skillRange, damage));
            profile.Range = Math.Max(profile.Range, skillRange);
        }
        profile.WeaponRange = Math.Max(threatUnit.WeaponAttackRange, 0);
        profile.WeaponDamage = EstimateWeaponAverageDamage(threatUnit);
        if (profile.WeaponRange > 0)
        {
            profile.Range = Math.Max(profile.Range, profile.WeaponRange);
        }
        return profile;
    }

    private static List<EffectInfo> CollectRoleThreatEffects(GodotObject skillDef, GodotObject combatProfile, int skillLevel)
    {
        var effects = new List<EffectInfo>();
        AddUnlockedEffects(effects, GetArray(combatProfile, "effect_defs"), skillLevel);
        Variant variantsValue = combatProfile.Call("get_unlocked_cast_variants", skillLevel);
        if (variantsValue.VariantType == Variant.Type.Array)
        {
            foreach (Variant variantValue in variantsValue.AsGodotArray())
            {
                GodotObject castVariant = variantValue.AsGodotObject();
                if (castVariant != null)
                {
                    AddUnlockedEffects(effects, GetArray(castVariant, "effect_defs"), skillLevel);
                }
            }
        }
        return effects;
    }

    private static void AddUnlockedEffects(List<EffectInfo> effects, GArray rawEffects, int skillLevel)
    {
        foreach (Variant effectValue in rawEffects)
        {
            GodotObject effect = effectValue.AsGodotObject();
            if (effect == null)
            {
                continue;
            }
            int minSkillLevel = Math.Max(GetInt(effect, "min_skill_level"), 0);
            int maxSkillLevel = GetInt(effect, "max_skill_level", -1);
            if (skillLevel < minSkillLevel)
            {
                continue;
            }
            if (maxSkillLevel >= 0 && skillLevel > maxSkillLevel)
            {
                continue;
            }
            effects.Add(BuildEffectInfo(effect));
        }
    }

    private static EffectInfo BuildEffectInfo(GodotObject effect)
    {
        GDictionary parameters = GetDictionary(effect, "params");
        return new EffectInfo(
            BattleTypedNames.ToEffectKind(GetStringName(effect, "effect_type")),
            GetStringName(effect, "status_id"),
            GetStringName(effect, "save_failure_status_id"),
            GetInt(effect, "power"),
            GetInt(effect, "damage_ratio_percent", 100),
            GetBool(parameters, "requires_weapon"),
            BattleTypedNames.ToForcedMoveMode(GetStringName(effect, "forced_move_mode"))
        );
    }

    private static bool IsDamageSkill(List<EffectInfo> effects)
    {
        foreach (EffectInfo effect in effects)
        {
            if (effect.EffectKind is BattleEffectKind.Damage or BattleEffectKind.Execute)
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsControlSkill(List<EffectInfo> effects)
    {
        foreach (EffectInfo effect in effects)
        {
            if (effect.EffectKind is BattleEffectKind.Status or BattleEffectKind.ApplyStatus or BattleEffectKind.ForcedMove)
            {
                return true;
            }
            if (effect.StatusId != EmptyStringName || effect.SaveFailureStatusId != EmptyStringName)
            {
                return true;
            }
        }
        return false;
    }

    private static int EstimateDamageForTarget(List<EffectInfo> effects, UnitInfo target)
    {
        int total = 0;
        foreach (EffectInfo effect in effects)
        {
            if (effect.EffectKind != BattleEffectKind.Damage)
            {
                continue;
            }
            int baseDamage = Math.Max(effect.Power, 0);
            int ratioPercent = Math.Max(effect.DamageRatioPercent, 0);
            int adjusted = Math.Max((int)Math.Round(baseDamage * (ratioPercent / 100.0)), 0);
            total += adjusted;
        }
        return total;
    }

    private static int ResolveEffectiveSkillThreatRange(UnitInfo unit, GodotObject skillDef, GodotObject combatProfile, int skillLevel, List<EffectInfo> effects)
    {
        int configuredRange = Math.Max(combatProfile.Call("get_effective_range_value", skillLevel).AsInt32(), 0);
        int range = configuredRange;
        if (RequiresCurrentMeleeWeapon(combatProfile, effects) || SkillHasTag(skillDef, "weapon") || SkillHasTag(skillDef, "bow"))
        {
            range = unit.WeaponAttackRange > 0 ? unit.WeaponAttackRange : range;
        }
        if (SkillHasTag(skillDef, "melee") && unit.WeaponAttackRange <= 0)
        {
            range = 1;
        }
        if (BattleTypedNames.ToTargetMode(GetStringName(combatProfile, "target_mode")) == BattleTargetMode.Ground && !IsGroundRelocationSkill(combatProfile))
        {
            StringName areaPattern = combatProfile.Call("get_effective_area_pattern", skillLevel).AsStringName();
            BattleAreaPattern parsedAreaPattern = BattleTypedNames.ToAreaPattern(areaPattern);
            int areaValue = combatProfile.Call("get_effective_area_value", skillLevel).AsInt32();
            range += BattleTypedNames.GetAreaPatternThreatReachBonus(parsedAreaPattern, areaValue);
        }
        return Math.Max(range, 0);
    }

    private static bool RequiresCurrentMeleeWeapon(GodotObject combatProfile, List<EffectInfo> effects)
    {
        if (GetArray(combatProfile, "required_weapon_families").Count > 0)
        {
            return true;
        }
        foreach (EffectInfo effect in effects)
        {
            if (effect.RequiresWeapon)
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsGroundRelocationSkill(GodotObject combatProfile)
    {
        foreach (Variant effectValue in GetArray(combatProfile, "effect_defs"))
        {
            GodotObject effect = effectValue.AsGodotObject();
            if (effect != null && IsGroundRelocationEffect(BuildEffectInfo(effect)))
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
                GodotObject effect = effectValue.AsGodotObject();
                if (effect != null && IsGroundRelocationEffect(BuildEffectInfo(effect)))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool IsGroundRelocationEffect(EffectInfo effect)
    {
        if (effect.EffectKind != BattleEffectKind.ForcedMove)
        {
            return false;
        }
        return effect.ForcedMoveMode is BattleForcedMoveMode.Jump or BattleForcedMoveMode.Blink;
    }

    private static bool SkillHasTag(GodotObject skillDef, string expectedTag)
    {
        StringName expected = new(expectedTag);
        foreach (Variant tagValue in GetArray(skillDef, "tags"))
        {
            if (tagValue.AsStringName() == expected)
            {
                return true;
            }
        }
        return false;
    }

    private static int EstimateThreatProfileDamageAtDistance(ThreatProfile profile, int distanceToActor)
    {
        int bestDamage = 0;
        foreach (ThreatSkillEntry entry in profile.SkillEntries)
        {
            if (distanceToActor >= 0 && entry.Range < distanceToActor)
            {
                continue;
            }
            bestDamage = Math.Max(bestDamage, entry.Damage);
        }
        if (distanceToActor < 0 || profile.WeaponRange >= distanceToActor)
        {
            bestDamage = Math.Max(bestDamage, profile.WeaponDamage);
        }
        return bestDamage;
    }

    private static int EstimateWeaponAverageDamage(UnitInfo unit)
    {
        WeaponDice dice = unit.WeaponUsesTwoHands ? unit.WeaponTwoHandedDice : unit.WeaponOneHandedDice;
        return dice.AverageDamage();
    }

    private static bool ShouldPopulateSurvivalProjection(GodotObject scoreInput, GodotObject context)
    {
        string bucketText = GetStringName(scoreInput, "score_bucket_id").ToString();
        if (bucketText == "archer_survival" || bucketText.EndsWith("_survival", StringComparison.Ordinal))
        {
            return true;
        }
        string intent = GetStringName(scoreInput, "action_intent").ToString();
        if (intent == "survival" || intent == "escape")
        {
            return true;
        }
        GodotObject skillDef = scoreInput.Get("skill_def").AsGodotObject();
        if (skillDef != null)
        {
            if (GetStringName(skillDef, "skill_id").ToString().StartsWith("mage_", StringComparison.Ordinal))
            {
                return true;
            }
            if (SkillHasTag(skillDef, "mage"))
            {
                return true;
            }
        }
        GodotObject actor = context.Get("unit_state").AsGodotObject();
        if (actor != null && GetStringName(scoreInput, "action_kind") == new StringName("ground_reposition_skill"))
        {
            foreach (StringName skillId in BuildStringNameList(actor.Get("known_active_skill_ids")))
            {
                if (skillId.ToString().StartsWith("mage_", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static UnitInfo BuildUnitInfo(GodotObject unitObject)
    {
        var unit = new UnitInfo
        {
            Source = unitObject,
            UnitId = GetStringName(unitObject, "unit_id"),
            FactionId = GetStringName(unitObject, "faction_id"),
            Coord = GetVector2I(unitObject, "coord", InvalidCoord),
            FootprintSize = NormalizeFootprint(GetVector2I(unitObject, "footprint_size", Vector2I.One)),
            IsAlive = GetBool(unitObject, "is_alive"),
            CurrentHp = GetInt(unitObject, "current_hp"),
            CurrentShieldHp = GetInt(unitObject, "current_shield_hp"),
            WeaponAttackRange = GetInt(unitObject, "weapon_attack_range"),
            WeaponUsesTwoHands = GetBool(unitObject, "weapon_uses_two_hands"),
            WeaponOneHandedDice = BuildWeaponDice(GetDictionary(unitObject, "weapon_one_handed_dice")),
            WeaponTwoHandedDice = BuildWeaponDice(GetDictionary(unitObject, "weapon_two_handed_dice")),
        };
        foreach ((StringName skillId, int skillLevel) in BuildSkillLevelMap(GetDictionary(unitObject, "known_skill_level_map")))
        {
            unit.KnownSkillLevelMap[skillId] = skillLevel;
        }
        unit.KnownActiveSkillIds.AddRange(BuildStringNameList(unitObject.Get("known_active_skill_ids")));
        unit.OccupiedCoords.AddRange(BuildVector2IList(unitObject.Get("occupied_coords")));
        if (unit.OccupiedCoords.Count == 0)
        {
            unit.OccupiedCoords.AddRange(GetFootprintCoords(unit.Coord, unit.FootprintSize));
        }
        return unit;
    }

    private static WeaponDice BuildWeaponDice(GDictionary dice)
    {
        if (dice == null || dice.Count == 0)
        {
            return WeaponDice.Empty;
        }
        return new WeaponDice(
            Math.Max(GetInt(dice, "dice_count"), 0),
            Math.Max(GetInt(dice, "dice_sides"), 0),
            GetInt(dice, "flat_bonus")
        );
    }

    private static Dictionary<StringName, int> BuildSkillLevelMap(GDictionary rawLevels)
    {
        var levels = new Dictionary<StringName, int>();
        if (rawLevels == null || rawLevels.Count == 0)
        {
            return levels;
        }
        foreach (Variant keyValue in rawLevels.Keys)
        {
            StringName skillId = keyValue.VariantType == Variant.Type.StringName
                ? keyValue.AsStringName()
                : new StringName(keyValue.ToString());
            if (skillId == EmptyStringName)
            {
                continue;
            }
            levels[skillId] = Math.Max(rawLevels[keyValue].AsInt32(), 0);
        }
        return levels;
    }

    private static Vector2I NormalizeFootprint(Vector2I footprint)
    {
        return new Vector2I(Math.Max(footprint.X, 1), Math.Max(footprint.Y, 1));
    }

    private static PositionMetadata BuildPositionMetadata(GDictionary metadata)
    {
        int desiredMinDistance = GetInt(metadata, "desired_min_distance", -1);
        int desiredMaxDistanceRaw = GetInt(metadata, "desired_max_distance", desiredMinDistance);
        int desiredMaxDistance = desiredMinDistance >= 0 && desiredMaxDistanceRaw >= 0
            ? Math.Max(desiredMaxDistanceRaw, desiredMinDistance)
            : -1;
        return new PositionMetadata
        {
            DesiredMinDistance = desiredMinDistance,
            DesiredMaxDistance = desiredMaxDistance,
            CurrentDistance = GetInt(metadata, "position_current_distance", -1),
            SafeDistance = GetInt(metadata, "position_safe_distance", -1),
            ExplicitObjectiveKind = BattleTypedNames.ToPositionObjectiveKind(GetStringName(metadata, "position_objective_kind")),
            AnchorCoord = GetVector2I(metadata, "position_anchor_coord", InvalidCoord),
            TargetUnit = GetObject(metadata, "position_target_unit"),
        };
    }

    private static PositionScoreProfile BuildPositionScoreProfile(GodotObject scoreProfile)
    {
        return new PositionScoreProfile(
            GetInt(scoreProfile, "position_base_score"),
            GetInt(scoreProfile, "position_distance_step"),
            GetInt(scoreProfile, "position_undershoot_penalty"),
            GetInt(scoreProfile, "position_overshoot_penalty")
        );
    }

    private static Vector2I ResolvePositionAnchorCoord(GodotObject scoreInput, UnitInfo actor, PositionMetadata metadata)
    {
        if (metadata.AnchorCoord != InvalidCoord)
        {
            return metadata.AnchorCoord;
        }
        GodotObject preview = scoreInput.Get("preview").AsGodotObject();
        if (preview != null)
        {
            Vector2I resolvedAnchor = GetVector2I(preview, "resolved_anchor_coord", InvalidCoord);
            if (resolvedAnchor != InvalidCoord)
            {
                return resolvedAnchor;
            }
        }
        return actor.Coord;
    }

    private static int BuildPositionObjectiveScore(
        BattlePositionObjectiveKind objectiveKind,
        int distance,
        int desiredMin,
        int desiredMax,
        int currentDistance,
        PositionScoreProfile scoreProfile)
    {
        if (distance < 0 || desiredMin < 0 || desiredMax < 0)
        {
            return 0;
        }
        if (objectiveKind == BattlePositionObjectiveKind.DistanceBandProgress)
        {
            return BuildDistanceBandProgressScore(distance, desiredMin, desiredMax, currentDistance, scoreProfile);
        }
        if (objectiveKind == BattlePositionObjectiveKind.DistanceFloor)
        {
            if (distance < desiredMin)
            {
                return -((desiredMin - distance) * scoreProfile.UndershootPenalty);
            }
            return scoreProfile.BaseScore + (distance - desiredMin) * scoreProfile.DistanceStep;
        }
        if (distance >= desiredMin && distance <= desiredMax)
        {
            return Math.Max(scoreProfile.BaseScore - distance * scoreProfile.DistanceStep, 0);
        }
        if (distance < desiredMin)
        {
            return -((desiredMin - distance) * scoreProfile.UndershootPenalty);
        }
        return -((distance - desiredMax) * scoreProfile.OvershootPenalty);
    }

    private static int BuildDistanceBandProgressScore(int distance, int desiredMin, int desiredMax, int currentDistance, PositionScoreProfile scoreProfile)
    {
        int candidateGap = BuildDistanceGap(distance, desiredMin, desiredMax);
        if (candidateGap < 0)
        {
            return 0;
        }
        int currentGap = BuildDistanceGap(currentDistance, desiredMin, desiredMax);
        if (currentGap <= 0)
        {
            return BuildDistanceBandAbsoluteScore(distance, desiredMin, desiredMax, scoreProfile);
        }
        if (candidateGap < currentGap)
        {
            int progressSteps = currentGap - candidateGap;
            return scoreProfile.BaseScore + progressSteps * scoreProfile.DistanceStep;
        }
        if (candidateGap == currentGap)
        {
            return -scoreProfile.DistanceStep;
        }
        return -((candidateGap - currentGap) * scoreProfile.OvershootPenalty);
    }

    private static int BuildDistanceBandAbsoluteScore(int distance, int desiredMin, int desiredMax, PositionScoreProfile scoreProfile)
    {
        if (distance >= desiredMin && distance <= desiredMax)
        {
            return Math.Max(scoreProfile.BaseScore - distance * scoreProfile.DistanceStep, 0);
        }
        if (distance < desiredMin)
        {
            return -((desiredMin - distance) * scoreProfile.UndershootPenalty);
        }
        return -((distance - desiredMax) * scoreProfile.OvershootPenalty);
    }

    private static int BuildDistanceGap(int distance, int desiredMin, int desiredMax)
    {
        if (distance < 0 || desiredMin < 0 || desiredMax < 0)
        {
            return -1;
        }
        if (distance < desiredMin)
        {
            return desiredMin - distance;
        }
        return distance > desiredMax ? distance - desiredMax : 0;
    }

    private static HashSet<StringName> BuildSuppressedThreatIds(GodotObject scoreInput)
    {
        var suppressed = new HashSet<StringName>();
        AddSuppressedIds(suppressed, scoreInput.Get("estimated_lethal_target_ids"));
        AddSuppressedIds(suppressed, scoreInput.Get("estimated_control_target_ids"));
        return suppressed;
    }

    private static void AddSuppressedIds(HashSet<StringName> suppressed, Variant idsValue)
    {
        foreach (StringName unitId in BuildStringNameList(idsValue))
        {
            if (unitId != EmptyStringName)
            {
                suppressed.Add(unitId);
            }
        }
    }

    private static Projection SubtractSuppressedThreats(Projection projection, HashSet<StringName> suppressed)
    {
        if (suppressed.Count == 0)
        {
            return projection;
        }
        var ids = new List<StringName>();
        var damage = new Dictionary<StringName, int>();
        int expectedDamage = 0;
        foreach (StringName unitId in projection.UnitIds)
        {
            if (suppressed.Contains(unitId))
            {
                continue;
            }
            int unitDamage = projection.DamageByUnitId.TryGetValue(unitId, out int value) ? value : 0;
            ids.Add(unitId);
            damage[unitId] = unitDamage;
            expectedDamage += unitDamage;
        }
        return new Projection(ids, damage, expectedDamage);
    }

    private static string BuildSuppressionKey(HashSet<StringName> suppressed)
    {
        if (suppressed.Count == 0)
        {
            return "-";
        }
        var parts = new List<string>();
        foreach (StringName unitId in suppressed)
        {
            parts.Add(unitId.ToString());
        }
        parts.Sort(StringComparer.Ordinal);
        return string.Join(",", parts);
    }

    private static int GetUnitSkillLevel(UnitInfo unit, StringName skillId)
    {
        if (unit.KnownSkillLevelMap.TryGetValue(skillId, out int skillLevel))
        {
            return skillLevel;
        }
        return unit.KnownActiveSkillIds.Contains(skillId) ? 1 : 0;
    }

    private static GodotObject GetSkillDef(GDictionary skillDefs, StringName skillId)
    {
        if (skillDefs.ContainsKey(skillId))
        {
            return skillDefs[skillId].AsGodotObject();
        }
        string stringKey = skillId.ToString();
        return skillDefs.ContainsKey(stringKey) ? skillDefs[stringKey].AsGodotObject() : null;
    }

    private static int DistanceFromUnitToCoord(UnitInfo unit, Vector2I targetCoord)
    {
        int bestDistance = int.MaxValue;
        foreach (Vector2I occupiedCoord in unit.OccupiedCoords)
        {
            bestDistance = Math.Min(bestDistance, ManhattanDistance(occupiedCoord, targetCoord));
        }
        return bestDistance == int.MaxValue ? -1 : bestDistance;
    }

    private static int DistanceFromAnchorToUnit(Vector2I anchorCoord, Vector2I actorFootprint, UnitInfo target)
    {
        if (anchorCoord == InvalidCoord)
        {
            return -1;
        }
        int bestDistance = int.MaxValue;
        foreach (Vector2I sourceCoord in GetFootprintCoords(anchorCoord, actorFootprint))
        {
            foreach (Vector2I targetCoord in target.OccupiedCoords)
            {
                bestDistance = Math.Min(bestDistance, ManhattanDistance(sourceCoord, targetCoord));
            }
        }
        return bestDistance == int.MaxValue ? -1 : bestDistance;
    }

    private static int ManhattanDistance(Vector2I left, Vector2I right)
    {
        return Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
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

    private static GDictionary ProjectionToDictionary(Projection projection)
    {
        var damageByUnitId = new GDictionary();
        foreach ((StringName unitId, int damage) in projection.DamageByUnitId)
        {
            damageByUnitId[unitId] = damage;
        }
        return new GDictionary
        {
            ["unit_ids"] = ToStringNameArray(projection.UnitIds),
            ["expected_damage"] = projection.ExpectedDamage,
            ["expected_damage_by_unit_id"] = damageByUnitId,
        };
    }

    private static bool TryProjectionFromCache(GDictionary cache, string key, out Projection projection)
    {
        projection = default;
        Variant value = GetVariant(cache, key);
        if (value.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }
        GDictionary dictionary = value.AsGodotDictionary();
        var ids = BuildStringNameList(GetVariant(dictionary, "unit_ids"));
        GDictionary rawDamage = GetDictionary(dictionary, "expected_damage_by_unit_id");
        var damage = new Dictionary<StringName, int>();
        foreach (Variant keyValue in rawDamage.Keys)
        {
            StringName unitId = keyValue.AsStringName();
            damage[unitId] = rawDamage[keyValue].AsInt32();
        }
        projection = new Projection(ids, damage, GetInt(dictionary, "expected_damage"));
        return true;
    }

    private static GDictionary ThreatProfileToDictionary(ThreatProfile profile)
    {
        var skillEntries = new GArray();
        foreach (ThreatSkillEntry entry in profile.SkillEntries)
        {
            skillEntries.Add(new GDictionary
            {
                ["range"] = entry.Range,
                ["damage"] = entry.Damage,
            });
        }
        return new GDictionary
        {
            ["range"] = profile.Range,
            ["weapon_range"] = profile.WeaponRange,
            ["weapon_damage"] = profile.WeaponDamage,
            ["skill_entries"] = skillEntries,
        };
    }

    private static ThreatProfile ThreatProfileFromDictionary(GDictionary dictionary)
    {
        var profile = new ThreatProfile
        {
            Range = GetInt(dictionary, "range"),
            WeaponRange = GetInt(dictionary, "weapon_range"),
            WeaponDamage = GetInt(dictionary, "weapon_damage"),
        };
        foreach (Variant entryValue in GetArray(dictionary, "skill_entries"))
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }
            GDictionary entry = entryValue.AsGodotDictionary();
            profile.SkillEntries.Add(new ThreatSkillEntry(GetInt(entry, "range"), GetInt(entry, "damage")));
        }
        return profile;
    }

    private static Godot.Collections.Array<StringName> ToStringNameArray(List<StringName> values)
    {
        var array = new Godot.Collections.Array<StringName>();
        foreach (StringName value in values)
        {
            array.Add(value);
        }
        return array;
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
            result.Add(item.AsStringName());
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
            result.Add(item.AsVector2I());
        }
        return result;
    }

    private static Variant GetVariant(GDictionary source, string key)
    {
        return TryGet(source, key, out Variant value) ? value : default;
    }
}
