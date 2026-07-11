using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class WeaponProfileDef : Resource
{
    private const int AttackRangeInherit = -1;

    public enum PropertyMergeMode
    {
        INHERIT = 0,
        REPLACE = 1,
        ADD = 2,
        REMOVE = 3,
    }

    [Export]
    public StringName weapon_type_id { get; set; } = new("");

    [Export]
    public StringName training_group { get; set; } = new("");

    [Export]
    public StringName range_type { get; set; } = new("");

    [Export]
    public StringName family { get; set; } = new("");

    [Export]
    public StringName damage_tag { get; set; } = new("");

    [Export]
    public int attack_range { get; set; } = AttackRangeInherit;

    [Export]
    public WeaponDamageDiceDef one_handed_dice { get; set; } = null;

    [Export]
    public WeaponDamageDiceDef two_handed_dice { get; set; } = null;

    [Export]
    public int properties_mode = (int)PropertyMergeMode.INHERIT;

    [Export]
    public Godot.Collections.Array<StringName> properties = new();

    internal WeaponDamageDiceDef OneHandedDiceProjectionBorrowed => one_handed_dice;
    internal WeaponDamageDiceDef TwoHandedDiceProjectionBorrowed => two_handed_dice;
    internal Godot.Collections.Array<StringName> PropertiesProjectionBorrowed => properties;

    internal WeaponProfileDefinition ToDefinition() =>
        WeaponProfileDefinition.FromResource(this);

    public bool HasAttackRangeOverride()
    {
        return attack_range != AttackRangeInherit;
    }

    public List<StringName> GetPropertiesTyped()
    {
        return new List<StringName>(_normalize_properties(properties));
    }

    public static int NormalizePropertiesMode(int mode)
    {
        if (!IsValidPropertiesMode(mode))
        {
            return (int)PropertyMergeMode.INHERIT;
        }
        return mode;
    }

    public static bool IsValidPropertiesMode(int mode)
    {
        return mode >= (int)PropertyMergeMode.INHERIT
            && mode <= (int)PropertyMergeMode.REMOVE;
    }

    private static Godot.Collections.Array<StringName> _normalize_properties(
        Godot.Collections.Array<StringName> raw_properties
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        var seen = new Godot.Collections.Dictionary<StringName, bool>();
        foreach (var rawValue in instancePropertiesOrEmpty(raw_properties))
        {
            var normalized = _to_string_name(rawValue);
            if (normalized == new StringName("") || seen.ContainsKey(normalized))
            {
                continue;
            }
            seen[normalized] = true;
            result.Add(normalized);
        }
        return result;
    }

    private static Godot.Collections.Array<StringName> instancePropertiesOrEmpty(
        Godot.Collections.Array<StringName> values
    )
    {
        return values ?? new Godot.Collections.Array<StringName>();
    }

    private static StringName _to_string_name(StringName value)
    {
        return value;
    }
}
