using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleExecutionRules : RefCounted
{
    private static readonly StringName HpMax = "hp_max";
    private static readonly StringName BossTargetStatId = "boss_target";
    private static readonly StringName FortuneMarkTargetStatId = "fortune_mark_target";
    private static readonly StringName BranchInvalidTarget = "invalid_target";
    private static readonly StringName BranchLowHpExecute = "low_hp_execute";
    private static readonly StringName SoulFractureStatusId = "soul_fracture";

    private readonly record struct SoulFractureParams(
        bool HasValue,
        int DurationTu,
        int HealMultiplierPercent,
        int ShieldGainMultiplierPercent
    )
    {
        public static SoulFractureParams Empty => new(false, 0, 100, 100);

        public GDictionary ToDictionary()
        {
            if (!HasValue)
            {
                return new GDictionary();
            }
            return new GDictionary
            {
                ["status_id"] = SoulFractureStatusId,
                ["duration_tu"] = DurationTu,
                ["heal_multiplier_percent"] = HealMultiplierPercent,
                ["shield_gain_multiplier_percent"] = ShieldGainMultiplierPercent,
            };
        }
    }

    private readonly record struct ExecutePlan(
        StringName Branch,
        int CurrentHp,
        int MaxHp,
        int Threshold,
        int FatalDamage,
        bool BypassShield,
        SoulFractureParams SoulFractureParams
    )
    {
        public GDictionary ToDictionary()
        {
            return new GDictionary
            {
                ["branch"] = Branch,
                ["current_hp"] = CurrentHp,
                ["max_hp"] = MaxHp,
                ["threshold"] = Threshold,
                ["fatal_damage"] = FatalDamage,
                ["bypass_shield"] = BypassShield,
                ["soul_fracture_params"] = SoulFractureParams.ToDictionary(),
            };
        }
    }

    public static StringName BOSS_TARGET_STAT_ID() => BossTargetStatId;

    public static StringName FORTUNE_MARK_TARGET_STAT_ID() => FortuneMarkTargetStatId;

    public static StringName BRANCH_INVALID_TARGET() => BranchInvalidTarget;

    public static StringName BRANCH_LOW_HP_EXECUTE() => BranchLowHpExecute;

    public static int resolve_threshold(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GDictionary @params
    )
    {
        GDictionary normalizedParams = @params ?? new GDictionary();
        int baseValue = Mathf.Max(ReadInt(normalizedParams, "threshold_base_value", 0), 0);
        int anchor = Mathf.Max(ReadInt(normalizedParams, "threshold_level_anchor", 17), 0);
        int bonusPer = Mathf.Max(
            ReadInt(normalizedParams, "threshold_level_bonus_per_delta", 5),
            0
        );
        StringName abilityId = ReadStringName(
            normalizedParams,
            "threshold_ability_mod",
            "intelligence_modifier"
        );
        int abilityMultiplier = Mathf.Max(
            ReadInt(normalizedParams, "threshold_ability_mod_multiplier", 5),
            0
        );
        int maxHpRatio = Mathf.Max(
            ReadInt(normalizedParams, "threshold_max_hp_ratio_percent", 20),
            0
        );
        int capRatio = Mathf.Max(
            ReadInt(normalizedParams, "threshold_cap_max_hp_ratio_percent", 50),
            0
        );

        int skillLevel = 0;
        StringName skillId = ReadStringName(normalizedParams, "skill_id");
        if (!IsEmpty(skillId) && source_unit != null)
        {
            skillLevel = ReadInt(source_unit.known_skill_level_map, skillId, 0);
        }
        int levelBonus = Mathf.Max(skillLevel - anchor, 0) * bonusPer;

        int abilityMod = 0;
        if (!IsEmpty(abilityId) && source_unit != null)
        {
            abilityMod = GetAttributeValue(source_unit, abilityId);
        }

        int targetMaxHp = Mathf.Max(GetAttributeValue(target_unit, HpMax), 0);
        int hpFloor = Mathf.Max(targetMaxHp * maxHpRatio / 100, 0);
        int rawThreshold =
            Mathf.Max(baseValue, hpFloor) + levelBonus + abilityMod * abilityMultiplier;
        int cap = Mathf.Max(targetMaxHp * capRatio / 100, 0);
        return cap > 0 ? Mathf.Min(rawThreshold, cap) : rawThreshold;
    }

    public static GDictionary build_execute_plan(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GDictionary @params
    )
    {
        GDictionary normalizedParams = @params ?? new GDictionary();
        int maxHp = Mathf.Max(GetAttributeValue(target_unit, HpMax), 0);
        int currentHp = target_unit != null ? Mathf.Max(target_unit.current_hp, 0) : 0;
        int threshold = resolve_threshold(source_unit, target_unit, normalizedParams);

        if (target_unit != null && currentHp <= threshold)
        {
            return new ExecutePlan(
                BranchLowHpExecute,
                currentHp,
                maxHp,
                threshold,
                currentHp,
                true,
                BuildSoulFractureParams(normalizedParams)
            ).ToDictionary();
        }
        return new ExecutePlan(
            BranchInvalidTarget,
            currentHp,
            maxHp,
            threshold,
            0,
            false,
            SoulFractureParams.Empty
        ).ToDictionary();
    }

    public static bool is_boss_target(BattleUnitState target_unit)
    {
        if (target_unit == null || target_unit.attribute_snapshot == null)
        {
            return false;
        }
        return GetAttributeValue(target_unit, BossTargetStatId) > 0
            || GetAttributeValue(target_unit, FortuneMarkTargetStatId) > 1;
    }

    public static bool is_elite_or_boss_target(BattleUnitState target_unit)
    {
        if (target_unit == null || target_unit.attribute_snapshot == null)
        {
            return false;
        }
        return GetAttributeValue(target_unit, BossTargetStatId) > 0
            || GetAttributeValue(target_unit, FortuneMarkTargetStatId) > 0;
    }

    public static int resolve_non_lethal_damage(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GDictionary @params,
        bool is_boss = false
    )
    {
        GDictionary normalizedParams = @params ?? new GDictionary();
        if (is_boss)
        {
            int ratio = Mathf.Max(
                ReadInt(
                    normalizedParams,
                    "boss_non_lethal_damage_max_hp_ratio_percent",
                    12
                ),
                0
            );
            int floorVal = Mathf.Max(
                ReadInt(normalizedParams, "boss_non_lethal_damage_floor", 25),
                1
            );
            int targetMaxHp = Mathf.Max(GetAttributeValue(target_unit, HpMax), 0);
            return Mathf.Max(targetMaxHp * ratio / 100, floorVal);
        }

        int nonLethalRatio = Mathf.Max(
            ReadInt(normalizedParams, "non_lethal_damage_ratio_percent", 30),
            0
        );
        int threshold = resolve_threshold(source_unit, target_unit, normalizedParams);
        return Mathf.Max(threshold * nonLethalRatio / 100, 1);
    }

    private static SoulFractureParams BuildSoulFractureParams(GDictionary @params)
    {
        int durationTu = ReadInt(@params, "soul_fracture_duration_tu");
        if (durationTu <= 0)
        {
            return SoulFractureParams.Empty;
        }
        return new SoulFractureParams(
            true,
            durationTu,
            ReadInt(@params, "heal_multiplier_percent", 100),
            ReadInt(@params, "shield_gain_multiplier_percent", 100)
        );
    }

    private static int GetAttributeValue(BattleUnitState unit, StringName attributeId)
    {
        AttributeSnapshot attributeSnapshot = unit?.attribute_snapshot;
        if (attributeSnapshot == null)
        {
            return 0;
        }
        return attributeSnapshot.get_value(attributeId);
    }

    private static bool IsEmpty(StringName value) => value == default || value == "";

    private static int ReadInt(GDictionary source, object key, int fallback = 0)
    {
        if (!TryReadValue(source, key, out dynamic value))
        {
            return fallback;
        }
        try
        {
            return value.AsInt32();
        }
        catch
        {
            return int.TryParse(value.ToString(), out int parsed) ? parsed : fallback;
        }
    }

    private static StringName ReadStringName(
        GDictionary source,
        object key,
        StringName fallback = default
    )
    {
        if (!TryReadValue(source, key, out dynamic value))
        {
            return fallback;
        }
        StringName parsed = ProgressionDataUtils.to_string_name(value);
        return IsEmpty(parsed) ? fallback : parsed;
    }

    private static bool TryReadValue(GDictionary source, object key, out dynamic value)
    {
        if (source == null)
        {
            value = default;
            return false;
        }
        try
        {
            dynamic dynamicKey = key;
            if (source.ContainsKey(dynamicKey))
            {
                value = source[dynamicKey];
                return true;
            }
        }
        catch
        {
        }
        if (key is StringName stringNameKey)
        {
            string keyText = stringNameKey.ToString();
            if (source.ContainsKey(keyText))
            {
                value = source[keyText];
                return true;
            }
        }
        else if (key is string stringKey)
        {
            var stringName = new StringName(stringKey);
            if (source.ContainsKey(stringName))
            {
                value = source[stringName];
                return true;
            }
        }
        value = default;
        return false;
    }
}
