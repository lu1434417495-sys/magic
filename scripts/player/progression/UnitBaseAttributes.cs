using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class UnitBaseAttributes : RefCounted
{
    private static readonly StringName StrengthId = "strength";
    private static readonly StringName AgilityId = "agility";
    private static readonly StringName ConstitutionId = "constitution";
    private static readonly StringName PerceptionId = "perception";
    private static readonly StringName IntelligenceId = "intelligence";
    private static readonly StringName WillpowerId = "willpower";
    private static readonly StringName ActionThresholdId = "action_threshold";
    private static readonly StringName HiddenLuckAtBirthId = "hidden_luck_at_birth";
    private static readonly StringName FaithLuckBonusId = "faith_luck_bonus";

    private const int EffectiveLuckMin = -6;
    private const int EffectiveLuckMax = 7;
    private const int DropLuckMax = 5;
    private const int CombatLuckScoreMax = 4;

    public int strength;
    public int agility;
    public int constitution;
    public int perception;
    public int intelligence;
    public int willpower;
    public GDictionary custom_stats = new();

    public static StringName STRENGTH() => StrengthId;
    public static StringName AGILITY() => AgilityId;
    public static StringName CONSTITUTION() => ConstitutionId;
    public static StringName PERCEPTION() => PerceptionId;
    public static StringName INTELLIGENCE() => IntelligenceId;
    public static StringName WILLPOWER() => WillpowerId;
    public static StringName ACTION_THRESHOLD() => ActionThresholdId;
    public static StringName HIDDEN_LUCK_AT_BIRTH() => HiddenLuckAtBirthId;
    public static StringName FAITH_LUCK_BONUS() => FaithLuckBonusId;
    public static int EFFECTIVE_LUCK_MIN() => EffectiveLuckMin;
    public static int EFFECTIVE_LUCK_MAX() => EffectiveLuckMax;
    public static int DROP_LUCK_MAX() => DropLuckMax;
    public static int COMBAT_LUCK_SCORE_MAX() => CombatLuckScoreMax;

    public static Godot.Collections.Array<StringName> BASE_ATTRIBUTE_IDS()
    {
        return new Godot.Collections.Array<StringName>
        {
            StrengthId,
            AgilityId,
            ConstitutionId,
            PerceptionId,
            IntelligenceId,
            WillpowerId,
        };
    }

    public int get_attribute_value(StringName attribute_id)
    {
        if (attribute_id == StrengthId) return strength;
        if (attribute_id == AgilityId) return agility;
        if (attribute_id == ConstitutionId) return constitution;
        if (attribute_id == PerceptionId) return perception;
        if (attribute_id == IntelligenceId) return intelligence;
        if (attribute_id == WillpowerId) return willpower;
        return custom_stats.ContainsKey(attribute_id) ? custom_stats[attribute_id].AsInt32() : 0;
    }

    public void set_attribute_value(StringName attribute_id, int value)
    {
        if (attribute_id == StrengthId) strength = value;
        else if (attribute_id == AgilityId) agility = value;
        else if (attribute_id == ConstitutionId) constitution = value;
        else if (attribute_id == PerceptionId) perception = value;
        else if (attribute_id == IntelligenceId) intelligence = value;
        else if (attribute_id == WillpowerId) willpower = value;
        else custom_stats[attribute_id] = value;
    }

    public static Godot.Collections.Array<StringName> get_all_base_attribute_ids() => BASE_ATTRIBUTE_IDS();

    public int get_hidden_luck_at_birth() => get_attribute_value(HiddenLuckAtBirthId);
    public int get_faith_luck_bonus() => get_attribute_value(FaithLuckBonusId);
    public int get_effective_luck() => Mathf.Clamp(get_hidden_luck_at_birth() + get_faith_luck_bonus(), EffectiveLuckMin, EffectiveLuckMax);
    public int get_combat_luck_score() => Mathf.Min(CombatLuckScoreMax, Mathf.Max(0, get_hidden_luck_at_birth()) + Mathf.FloorToInt(Mathf.Max(0, get_faith_luck_bonus()) / 2.0f));
    public int get_drop_luck() => Mathf.Clamp(get_effective_luck(), EffectiveLuckMin, DropLuckMax);

    public GDictionary to_dict()
    {
        return new GDictionary
        {
            ["strength"] = strength,
            ["agility"] = agility,
            ["constitution"] = constitution,
            ["perception"] = perception,
            ["intelligence"] = intelligence,
            ["willpower"] = willpower,
            ["custom_stats"] = ProgressionDataUtils.string_name_int_map_to_string_dict(custom_stats),
        };
    }

    public static UnitBaseAttributes from_dict(GDictionary data)
    {
        if (!HasExactFields(data, new Godot.Collections.Array<string> { "strength", "agility", "constitution", "perception", "intelligence", "willpower", "custom_stats" }))
        {
            return null;
        }

        Variant customStatsValue = data["custom_stats"];
        if (customStatsValue.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }

        foreach (StringName attributeId in BASE_ATTRIBUTE_IDS())
        {
            if (data[attributeId.ToString()].VariantType != Variant.Type.Int)
            {
                return null;
            }
        }

        GDictionary parsedCustomStats = ParseIntMap(customStatsValue.AsGodotDictionary());
        if (parsedCustomStats == null)
        {
            return null;
        }

        return new UnitBaseAttributes
        {
            strength = data["strength"].AsInt32(),
            agility = data["agility"].AsInt32(),
            constitution = data["constitution"].AsInt32(),
            perception = data["perception"].AsInt32(),
            intelligence = data["intelligence"].AsInt32(),
            willpower = data["willpower"].AsInt32(),
            custom_stats = parsedCustomStats,
        };
    }

    private static bool HasExactFields(GDictionary data, Godot.Collections.Array<string> expectedFields)
    {
        if (data.Count != expectedFields.Count)
        {
            return false;
        }
        foreach (string fieldName in expectedFields)
        {
            if (!data.ContainsKey(fieldName))
            {
                return false;
            }
        }
        return true;
    }

    private static GDictionary ParseIntMap(GDictionary values)
    {
        var parsedValues = new GDictionary();
        var seenKeys = new GDictionary();
        foreach (Variant rawKey in values.Keys)
        {
            if (rawKey.VariantType != Variant.Type.String && rawKey.VariantType != Variant.Type.StringName)
            {
                return null;
            }

            StringName parsedKey = ProgressionDataUtils.to_string_name(rawKey);
            if (parsedKey == new StringName("") || seenKeys.ContainsKey(parsedKey))
            {
                return null;
            }
            if (values[rawKey].VariantType != Variant.Type.Int)
            {
                return null;
            }

            seenKeys[parsedKey] = true;
            parsedValues[parsedKey] = values[rawKey].AsInt32();
        }
        return parsedValues;
    }
}
