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

    private static readonly StringName[] ResourceAttributeIds =
    {
        "hp_max",
        "character_hp_max_percent_bonus",
        "mp_max",
        "stamina_max",
        "stamina_recovery_percent_bonus",
        "aura_max",
        "action_points",
        "action_threshold",
    };

    private static readonly StringName[] CombatAttributeIds =
    {
        "armor_class",
        ArmorAcBonus,
        ShieldAcBonus,
        DodgeBonus,
        DeflectionBonus,
        "armor_max_dex_bonus",
    };

    private static HashSet<StringName> _recognizedAttributeIdCache;

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

    // 可消费 canonical 属性 id 的唯一 closed-domain owner：基础属性、基础属性调整值、
    // 资源属性与战斗属性。identity registries 与 GearSetContentRegistry 共用本判定，
    // 不各自维护第二份白名单。
    internal static bool IsRecognizedAttributeId(StringName attributeId)
    {
        if (attributeId == "")
            return false;
        return RecognizedAttributeIdSet().Contains(attributeId);
    }

    private static HashSet<StringName> RecognizedAttributeIdSet()
    {
        if (_recognizedAttributeIdCache != null)
            return _recognizedAttributeIdCache;

        var recognized = new HashSet<StringName>();
        foreach (StringName attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
        {
            recognized.Add(attributeId);
            StringName modifierId = AttributeSnapshot.GetBaseAttributeModifierId(attributeId);
            if (modifierId != "")
                recognized.Add(modifierId);
        }
        foreach (StringName attributeId in ResourceAttributeIds)
            recognized.Add(attributeId);
        foreach (StringName attributeId in CombatAttributeIds)
            recognized.Add(attributeId);

        _recognizedAttributeIdCache = recognized;
        return recognized;
    }

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
