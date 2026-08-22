using Godot;

// add_damage_dice 的 damage_type_mode closed domain：explicit 要求显式 damage_type，
// inherit_primary 禁止显式 damage tag 并由 per-main-direct-effect query 继承当前主
// effect 的 canonical damage tag。fail-closed：未识别字符串映射到 Unknown。
internal enum EquipmentAbilityDamageTypeModeKind
{
    Unknown = 0,
    Explicit,
    InheritPrimary,
}

internal static class EquipmentAbilityDamageTypeModeContentRules
{
    internal static readonly StringName Explicit = "explicit";
    internal static readonly StringName InheritPrimary = "inherit_primary";

    internal static EquipmentAbilityDamageTypeModeKind ToKind(StringName value)
    {
        string normalized = ProgressionDataUtils.to_string_name(value).ToString();
        return normalized switch
        {
            "" => EquipmentAbilityDamageTypeModeKind.Explicit,
            "explicit" => EquipmentAbilityDamageTypeModeKind.Explicit,
            "inherit_primary" => EquipmentAbilityDamageTypeModeKind.InheritPrimary,
            _ => EquipmentAbilityDamageTypeModeKind.Unknown,
        };
    }

    internal static StringName ToStringName(EquipmentAbilityDamageTypeModeKind kind)
    {
        return kind switch
        {
            EquipmentAbilityDamageTypeModeKind.Explicit => Explicit,
            EquipmentAbilityDamageTypeModeKind.InheritPrimary => InheritPrimary,
            _ => new StringName(""),
        };
    }

    internal static bool IsValid(EquipmentAbilityDamageTypeModeKind kind)
    {
        return kind != EquipmentAbilityDamageTypeModeKind.Unknown;
    }
}
