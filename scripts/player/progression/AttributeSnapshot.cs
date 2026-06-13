using System.Collections.Generic;
using Godot;

internal enum AttributeSnapshotIdKind
{
    Unknown = 0,
    StrengthModifier,
    AgilityModifier,
    ConstitutionModifier,
    PerceptionModifier,
    IntelligenceModifier,
    WillpowerModifier,
    BaseAttackBonus,
    SpellProficiencyBonus,
}

[GlobalClass]
public partial class AttributeSnapshot : RefCounted
{
    internal readonly record struct BaseAttackProgressionPair(
        int Rank,
        ProfessionBaseAttackProgression Progression
    );

    private static readonly StringName StrengthModifier = "strength_modifier";
    private static readonly StringName AgilityModifier = "agility_modifier";
    private static readonly StringName ConstitutionModifier = "constitution_modifier";
    private static readonly StringName PerceptionModifier = "perception_modifier";
    private static readonly StringName IntelligenceModifier = "intelligence_modifier";
    private static readonly StringName WillpowerModifier = "willpower_modifier";
    private static readonly StringName BaseAttackBonus = "base_attack_bonus";
    private static readonly StringName SpellProficiencyBonus = "spell_proficiency_bonus";
    private const int BabRateFull = 4;
    private const int BabRateThreeQuarter = 3;
    private const int BabRateHalf = 2;
    private const int BabDenominator = 8;

    private System.Collections.Generic.Dictionary<StringName, int> _values = new();

    internal static StringName ToStringName(AttributeSnapshotIdKind kind)
    {
        return kind switch
        {
            AttributeSnapshotIdKind.StrengthModifier => StrengthModifier,
            AttributeSnapshotIdKind.AgilityModifier => AgilityModifier,
            AttributeSnapshotIdKind.ConstitutionModifier => ConstitutionModifier,
            AttributeSnapshotIdKind.PerceptionModifier => PerceptionModifier,
            AttributeSnapshotIdKind.IntelligenceModifier => IntelligenceModifier,
            AttributeSnapshotIdKind.WillpowerModifier => WillpowerModifier,
            AttributeSnapshotIdKind.BaseAttackBonus => BaseAttackBonus,
            AttributeSnapshotIdKind.SpellProficiencyBonus => SpellProficiencyBonus,
            _ => new StringName(""),
        };
    }

    public void SetValue(StringName attribute_id, int value)
    {
        _values[attribute_id] = value;
        StringName modifierId = GetBaseAttributeModifierId(attribute_id);
        if (modifierId != new StringName(""))
        {
            _values[modifierId] = CalculateScoreModifier(value);
        }
    }

    public int GetValue(StringName attribute_id) =>
        _values.TryGetValue(attribute_id, out int v) ? v : 0;

    public bool HasValue(StringName attribute_id) => _values.ContainsKey(attribute_id);

    internal Dictionary<StringName, int> GetAllValuesTyped()
    {
        return new Dictionary<StringName, int>(_values);
    }

    public Godot.Collections.Dictionary ToDictionary() =>
        ProgressionDataUtils.string_name_int_map_to_string_dict(_values);

    public static StringName GetBaseAttributeModifierId(StringName attribute_id)
    {
        if (attribute_id == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength))
            return StrengthModifier;
        if (attribute_id == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility))
            return AgilityModifier;
        if (attribute_id == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution))
            return ConstitutionModifier;
        if (attribute_id == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Perception))
            return PerceptionModifier;
        if (attribute_id == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence))
            return IntelligenceModifier;
        if (attribute_id == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower))
            return WillpowerModifier;
        return "";
    }

    public static int CalculateScoreModifier(int score) => Mathf.FloorToInt((score - 10) / 2.0f);

    internal static int CalculateBaseAttackBonus(
        IEnumerable<BaseAttackProgressionPair> activeProfessionPairs
    )
    {
        int numerator = 0;
        foreach (BaseAttackProgressionPair pair in activeProfessionPairs)
        {
            int rank = pair.Rank;
            if (rank <= 0)
                continue;
            numerator += rank * GetBabRateForProgression(pair.Progression);
        }
        return numerator / BabDenominator;
    }

    public static int CalculateSpellProficiencyBonus(int character_level)
    {
        int effectiveLevel = Mathf.Max(character_level, 1);
        return Mathf.Clamp(2 + (effectiveLevel - 1) / 4, 2, 6);
    }

    internal static int GetBabRateForProgression(ProfessionBaseAttackProgression progression)
    {
        return progression switch
        {
            ProfessionBaseAttackProgression.Full => BabRateFull,
            ProfessionBaseAttackProgression.ThreeQuarter => BabRateThreeQuarter,
            ProfessionBaseAttackProgression.Half => BabRateHalf,
            _ => 0,
        };
    }
}
