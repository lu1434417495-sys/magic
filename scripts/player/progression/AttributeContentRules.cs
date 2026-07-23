using System;
using System.Collections.Generic;
using Godot;

internal enum ArmorClassComponentKind
{
    Unknown = 0,
    ArmorBonus,
    ShieldBonus,
    DodgeBonus,
    DeflectionBonus,
    NaturalArmorBonus,
}

internal static class AttributeContentRules
{
    internal static readonly StringName ArmorAcBonus = "armor_ac_bonus";
    internal static readonly StringName ShieldAcBonus = "shield_ac_bonus";
    internal static readonly StringName DodgeBonus = "dodge_bonus";
    internal static readonly StringName DeflectionBonus = "deflection_bonus";
    internal static readonly StringName NaturalArmorAcBonus = "natural_armor_ac_bonus";

    internal static IReadOnlyList<StringName> ArmorClassComponentAttributeIds { get; } =
        Array.AsReadOnly(
            new[]
            {
                ArmorAcBonus,
                ShieldAcBonus,
                DodgeBonus,
                DeflectionBonus,
                NaturalArmorAcBonus,
            }
        );

    internal static bool IsArmorClassComponentAttributeId(StringName attributeId) =>
        ToArmorClassComponentKind(attributeId) != ArmorClassComponentKind.Unknown;

    internal static ArmorClassComponentKind ToArmorClassComponentKind(StringName attributeId)
    {
        if (attributeId == ArmorAcBonus)
            return ArmorClassComponentKind.ArmorBonus;
        if (attributeId == ShieldAcBonus)
            return ArmorClassComponentKind.ShieldBonus;
        if (attributeId == DodgeBonus)
            return ArmorClassComponentKind.DodgeBonus;
        if (attributeId == DeflectionBonus)
            return ArmorClassComponentKind.DeflectionBonus;
        if (attributeId == NaturalArmorAcBonus)
            return ArmorClassComponentKind.NaturalArmorBonus;
        return ArmorClassComponentKind.Unknown;
    }

    internal static StringName ToStringName(ArmorClassComponentKind kind)
    {
        return kind switch
        {
            ArmorClassComponentKind.ArmorBonus => ArmorAcBonus,
            ArmorClassComponentKind.ShieldBonus => ShieldAcBonus,
            ArmorClassComponentKind.DodgeBonus => DodgeBonus,
            ArmorClassComponentKind.DeflectionBonus => DeflectionBonus,
            ArmorClassComponentKind.NaturalArmorBonus => NaturalArmorAcBonus,
            _ => new StringName(""),
        };
    }
}
