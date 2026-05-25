using Godot;

[GlobalClass]
public partial class AttributeSnapshot : RefCounted
{
    private static readonly StringName StrengthModifier = "strength_modifier";
    private static readonly StringName AgilityModifier = "agility_modifier";
    private static readonly StringName ConstitutionModifier = "constitution_modifier";
    private static readonly StringName PerceptionModifier = "perception_modifier";
    private static readonly StringName IntelligenceModifier = "intelligence_modifier";
    private static readonly StringName WillpowerModifier = "willpower_modifier";
    private static readonly StringName BaseAttackBonus = "base_attack_bonus";
    private static readonly StringName SpellProficiencyBonus = "spell_proficiency_bonus";
    private static readonly StringName BabProgressionFull = "full";
    private static readonly StringName BabProgressionThreeQuarter = "three_quarter";
    private static readonly StringName BabProgressionHalf = "half";
    private const int BabRateFull = 4;
    private const int BabRateThreeQuarter = 3;
    private const int BabRateHalf = 2;
    private const int BabDenominator = 8;

    private Godot.Collections.Dictionary _values = new();

    public static StringName STRENGTH_MODIFIER() => StrengthModifier;
    public static StringName AGILITY_MODIFIER() => AgilityModifier;
    public static StringName CONSTITUTION_MODIFIER() => ConstitutionModifier;
    public static StringName PERCEPTION_MODIFIER() => PerceptionModifier;
    public static StringName INTELLIGENCE_MODIFIER() => IntelligenceModifier;
    public static StringName WILLPOWER_MODIFIER() => WillpowerModifier;
    public static StringName BASE_ATTACK_BONUS() => BaseAttackBonus;
    public static StringName SPELL_PROFICIENCY_BONUS() => SpellProficiencyBonus;
    public static StringName BAB_PROGRESSION_FULL() => BabProgressionFull;
    public static StringName BAB_PROGRESSION_THREE_QUARTER() => BabProgressionThreeQuarter;
    public static StringName BAB_PROGRESSION_HALF() => BabProgressionHalf;
    public static int BAB_RATE_FULL() => BabRateFull;
    public static int BAB_RATE_THREE_QUARTER() => BabRateThreeQuarter;
    public static int BAB_RATE_HALF() => BabRateHalf;
    public static int BAB_DENOMINATOR() => BabDenominator;

    public void set_value(StringName attribute_id, int value)
    {
        _values[attribute_id] = value;
        StringName modifierId = get_base_attribute_modifier_id(attribute_id);
        if (modifierId != new StringName(""))
        {
            _values[modifierId] = calculate_score_modifier(value);
        }
    }

    public int get_value(StringName attribute_id) => _values.ContainsKey(attribute_id) ? _values[attribute_id].AsInt32() : 0;
    public bool has_value(StringName attribute_id) => _values.ContainsKey(attribute_id);

    public Godot.Collections.Dictionary get_all_values()
    {
        var result = new Godot.Collections.Dictionary();
        foreach (Variant key in _values.Keys)
        {
            result[key] = _values[key];
        }
        return result;
    }

    public Godot.Collections.Dictionary to_dict() => ProgressionDataUtils.string_name_int_map_to_string_dict(_values);

    public static StringName get_base_attribute_modifier_id(StringName attribute_id)
    {
        if (attribute_id == UnitBaseAttributes.STRENGTH()) return StrengthModifier;
        if (attribute_id == UnitBaseAttributes.AGILITY()) return AgilityModifier;
        if (attribute_id == UnitBaseAttributes.CONSTITUTION()) return ConstitutionModifier;
        if (attribute_id == UnitBaseAttributes.PERCEPTION()) return PerceptionModifier;
        if (attribute_id == UnitBaseAttributes.INTELLIGENCE()) return IntelligenceModifier;
        if (attribute_id == UnitBaseAttributes.WILLPOWER()) return WillpowerModifier;
        return "";
    }

    public static int calculate_score_modifier(int score) => Mathf.FloorToInt((score - 10) / 2.0f);

    public static int calculate_base_attack_bonus(Godot.Collections.Array active_profession_pairs)
    {
        int numerator = 0;
        foreach (Variant pair in active_profession_pairs)
        {
            if (pair.VariantType != Variant.Type.Array) continue;
            Godot.Collections.Array values = pair.AsGodotArray();
            if (values.Count < 2) continue;
            int rank = values[0].AsInt32();
            if (rank <= 0) continue;
            numerator += rank * get_bab_rate_for_progression(values[1]);
        }
        return numerator / BabDenominator;
    }

    public static int calculate_spell_proficiency_bonus(int character_level)
    {
        int effectiveLevel = Mathf.Max(character_level, 1);
        return Mathf.Clamp(2 + (effectiveLevel - 1) / 4, 2, 6);
    }

    public static int get_bab_rate_for_progression(Variant progression)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(progression);
        if (normalized == BabProgressionFull) return BabRateFull;
        if (normalized == BabProgressionThreeQuarter) return BabRateThreeQuarter;
        return BabRateHalf;
    }
}
