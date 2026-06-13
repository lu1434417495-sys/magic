using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal enum UnitBaseAttributeKind
{
    Unknown = 0,
    Strength,
    Agility,
    Constitution,
    Perception,
    Intelligence,
    Willpower,
    ActionThreshold,
    HiddenLuckAtBirth,
    FaithLuckBonus,
}

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

    internal const int EffectiveLuckMin = -6;
    internal const int EffectiveLuckMax = 7;
    internal const int DropLuckMax = 5;
    internal const int CombatLuckScoreMax = 4;
    private static readonly StringName[] BaseAttributeIds =
    {
        StrengthId,
        AgilityId,
        ConstitutionId,
        PerceptionId,
        IntelligenceId,
        WillpowerId,
    };

    public int strength;
    public int agility;
    public int constitution;
    public int perception;
    public int intelligence;
    public int willpower;
    public GDictionary custom_stats = new();

    public static IReadOnlyList<StringName> GetBaseAttributeIdsTyped() => BaseAttributeIds;

    public static bool IsBaseAttributeId(StringName attributeId) =>
        ToAttributeKind(attributeId) is
            UnitBaseAttributeKind.Strength
            or UnitBaseAttributeKind.Agility
            or UnitBaseAttributeKind.Constitution
            or UnitBaseAttributeKind.Perception
            or UnitBaseAttributeKind.Intelligence
            or UnitBaseAttributeKind.Willpower;

    internal static StringName ToStringName(UnitBaseAttributeKind kind)
    {
        return kind switch
        {
            UnitBaseAttributeKind.Strength => StrengthId,
            UnitBaseAttributeKind.Agility => AgilityId,
            UnitBaseAttributeKind.Constitution => ConstitutionId,
            UnitBaseAttributeKind.Perception => PerceptionId,
            UnitBaseAttributeKind.Intelligence => IntelligenceId,
            UnitBaseAttributeKind.Willpower => WillpowerId,
            UnitBaseAttributeKind.ActionThreshold => ActionThresholdId,
            UnitBaseAttributeKind.HiddenLuckAtBirth => HiddenLuckAtBirthId,
            UnitBaseAttributeKind.FaithLuckBonus => FaithLuckBonusId,
            _ => new StringName(""),
        };
    }

    internal static UnitBaseAttributeKind ToAttributeKind(StringName attributeId)
    {
        if (attributeId == StrengthId)
            return UnitBaseAttributeKind.Strength;
        if (attributeId == AgilityId)
            return UnitBaseAttributeKind.Agility;
        if (attributeId == ConstitutionId)
            return UnitBaseAttributeKind.Constitution;
        if (attributeId == PerceptionId)
            return UnitBaseAttributeKind.Perception;
        if (attributeId == IntelligenceId)
            return UnitBaseAttributeKind.Intelligence;
        if (attributeId == WillpowerId)
            return UnitBaseAttributeKind.Willpower;
        if (attributeId == ActionThresholdId)
            return UnitBaseAttributeKind.ActionThreshold;
        if (attributeId == HiddenLuckAtBirthId)
            return UnitBaseAttributeKind.HiddenLuckAtBirth;
        if (attributeId == FaithLuckBonusId)
            return UnitBaseAttributeKind.FaithLuckBonus;
        return UnitBaseAttributeKind.Unknown;
    }

    public int GetAttributeValue(StringName attribute_id)
    {
        switch (ToAttributeKind(attribute_id))
        {
            case UnitBaseAttributeKind.Strength:
                return strength;
            case UnitBaseAttributeKind.Agility:
                return agility;
            case UnitBaseAttributeKind.Constitution:
                return constitution;
            case UnitBaseAttributeKind.Perception:
                return perception;
            case UnitBaseAttributeKind.Intelligence:
                return intelligence;
            case UnitBaseAttributeKind.Willpower:
                return willpower;
        }
        return custom_stats.ContainsKey(attribute_id) ? custom_stats[attribute_id].AsInt32() : 0;
    }

    public void SetAttributeValue(StringName attribute_id, int value)
    {
        switch (ToAttributeKind(attribute_id))
        {
            case UnitBaseAttributeKind.Strength:
                strength = value;
                return;
            case UnitBaseAttributeKind.Agility:
                agility = value;
                return;
            case UnitBaseAttributeKind.Constitution:
                constitution = value;
                return;
            case UnitBaseAttributeKind.Perception:
                perception = value;
                return;
            case UnitBaseAttributeKind.Intelligence:
                intelligence = value;
                return;
            case UnitBaseAttributeKind.Willpower:
                willpower = value;
                return;
        }
        custom_stats[attribute_id] = value;
    }

    public int GetHiddenLuckAtBirth() =>
        GetAttributeValue(ToStringName(UnitBaseAttributeKind.HiddenLuckAtBirth));

    public int GetFaithLuckBonus() =>
        GetAttributeValue(ToStringName(UnitBaseAttributeKind.FaithLuckBonus));

    public int GetEffectiveLuck() =>
        Mathf.Clamp(
            GetHiddenLuckAtBirth() + GetFaithLuckBonus(),
            EffectiveLuckMin,
            EffectiveLuckMax
        );

    public int GetCombatLuckScore() =>
        Mathf.Min(
            CombatLuckScoreMax,
            Mathf.Max(0, GetHiddenLuckAtBirth())
                + Mathf.FloorToInt(Mathf.Max(0, GetFaithLuckBonus()) / 2.0f)
        );

    public int GetDropLuck() => Mathf.Clamp(GetEffectiveLuck(), EffectiveLuckMin, DropLuckMax);

    public UnitBaseAttributes DuplicateState()
    {
        return new UnitBaseAttributes
        {
            strength = strength,
            agility = agility,
            constitution = constitution,
            perception = perception,
            intelligence = intelligence,
            willpower = willpower,
            custom_stats = custom_stats?.Duplicate(true) ?? new GDictionary(),
        };
    }

    public GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["strength"] = strength,
            ["agility"] = agility,
            ["constitution"] = constitution,
            ["perception"] = perception,
            ["intelligence"] = intelligence,
            ["willpower"] = willpower,
            ["custom_stats"] = ProgressionDataUtils.string_name_int_map_to_string_dict(
                custom_stats
            ),
        };
    }

    public static UnitBaseAttributes FromDictionary(GDictionary data)
    {
        if (
            !HasExactFields(
                data,
                new Godot.Collections.Array<string>
                {
                    "strength",
                    "agility",
                    "constitution",
                    "perception",
                    "intelligence",
                    "willpower",
                    "custom_stats",
                }
            )
        )
        {
            return null;
        }

        var customStatsValue = data["custom_stats"];
        if (customStatsValue.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }

        foreach (StringName attributeId in GetBaseAttributeIdsTyped())
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

    private static bool HasExactFields(
        GDictionary data,
        Godot.Collections.Array<string> expectedFields
    )
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
        foreach (var rawKey in values.Keys)
        {
            if (
                rawKey.VariantType != Variant.Type.String
                && rawKey.VariantType != Variant.Type.StringName
            )
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
