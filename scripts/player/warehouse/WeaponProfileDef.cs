using Godot;

[GlobalClass]
public partial class WeaponProfileDef : Resource
{
    public const int ATTACK_RANGE_INHERIT = -1;

    public enum PropertyMergeMode
    {
        INHERIT = 0,
        REPLACE = 1,
        ADD = 2,
        REMOVE = 3,
    }

    public static int PROPERTY_MERGE_MODE_INHERIT() => (int)PropertyMergeMode.INHERIT;

    public static int PROPERTY_MERGE_MODE_REPLACE() => (int)PropertyMergeMode.REPLACE;

    public static int PROPERTY_MERGE_MODE_ADD() => (int)PropertyMergeMode.ADD;

    public static int PROPERTY_MERGE_MODE_REMOVE() => (int)PropertyMergeMode.REMOVE;

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
    public int attack_range { get; set; } = ATTACK_RANGE_INHERIT;

    [Export]
    public WeaponDamageDiceDef one_handed_dice { get; set; } = null;

    [Export]
    public WeaponDamageDiceDef two_handed_dice { get; set; } = null;

    [Export]
    public int properties_mode = (int)PropertyMergeMode.INHERIT;

    [Export]
    public Godot.Collections.Array<StringName> properties = new();

    public WeaponProfileDef merge_with_template(WeaponProfileDef template_profile)
    {
        return merge_profiles(template_profile, this);
    }

    public WeaponProfileDef duplicate_profile()
    {
        return merge_profiles(null, this);
    }

    public bool has_attack_range_override()
    {
        return attack_range != ATTACK_RANGE_INHERIT;
    }

    public Godot.Collections.Array<StringName> get_properties()
    {
        return _normalize_properties(properties);
    }

    public Godot.Collections.Array<StringName> GetProperties() => get_properties();

    public static WeaponProfileDef merge(
        WeaponProfileDef template_profile,
        WeaponProfileDef instance_profile
    )
    {
        return merge_profiles(template_profile, instance_profile);
    }

    public static WeaponProfileDef merge_profiles(
        WeaponProfileDef template_profile,
        WeaponProfileDef instance_profile
    )
    {
        if (template_profile == null && instance_profile == null)
        {
            return null;
        }

        var merged = new WeaponProfileDef();
        if (template_profile == null)
        {
            _copy_profile_fields(instance_profile, merged);
            merged.properties_mode = (int)PropertyMergeMode.REPLACE;
            return merged;
        }
        if (instance_profile == null)
        {
            _copy_profile_fields(template_profile, merged);
            merged.properties_mode = (int)PropertyMergeMode.REPLACE;
            return merged;
        }

        merged.weapon_type_id = _inherit_string_name(
            template_profile.weapon_type_id,
            instance_profile.weapon_type_id
        );
        merged.training_group = _inherit_string_name(
            template_profile.training_group,
            instance_profile.training_group
        );
        merged.range_type = _inherit_string_name(
            template_profile.range_type,
            instance_profile.range_type
        );
        merged.family = _inherit_string_name(template_profile.family, instance_profile.family);
        merged.damage_tag = _inherit_string_name(
            template_profile.damage_tag,
            instance_profile.damage_tag
        );
        merged.attack_range = instance_profile.has_attack_range_override()
            ? instance_profile.attack_range
            : template_profile.attack_range;
        merged.one_handed_dice = _inherit_dice(
            template_profile.one_handed_dice,
            instance_profile.one_handed_dice
        );
        merged.two_handed_dice = _inherit_dice(
            template_profile.two_handed_dice,
            instance_profile.two_handed_dice
        );
        merged.properties = _resolve_properties(
            template_profile.properties,
            instance_profile.properties,
            instance_profile.properties_mode
        );
        merged.properties_mode = (int)PropertyMergeMode.REPLACE;
        return merged;
    }

    public static int normalize_properties_mode(int mode)
    {
        if (mode < (int)PropertyMergeMode.INHERIT || mode > (int)PropertyMergeMode.REMOVE)
        {
            return (int)PropertyMergeMode.INHERIT;
        }
        return mode;
    }

    private static void _copy_profile_fields(WeaponProfileDef source, WeaponProfileDef target)
    {
        target.weapon_type_id = source.weapon_type_id;
        target.training_group = source.training_group;
        target.range_type = source.range_type;
        target.family = source.family;
        target.damage_tag = source.damage_tag;
        target.attack_range = source.attack_range;
        target.one_handed_dice = _duplicate_dice(source.one_handed_dice);
        target.two_handed_dice = _duplicate_dice(source.two_handed_dice);
        target.properties_mode = normalize_properties_mode(source.properties_mode);
        target.properties = _normalize_properties(source.properties);
    }

    private static StringName _inherit_string_name(
        StringName template_value,
        StringName instance_value
    )
    {
        return instance_value != new StringName("") ? instance_value : template_value;
    }

    private static WeaponDamageDiceDef _inherit_dice(
        WeaponDamageDiceDef template_dice,
        WeaponDamageDiceDef instance_dice
    )
    {
        return _duplicate_dice(instance_dice ?? template_dice);
    }

    private static WeaponDamageDiceDef _duplicate_dice(WeaponDamageDiceDef source)
    {
        return source?.duplicate_dice();
    }

    private static Godot.Collections.Array<StringName> _resolve_properties(
        Godot.Collections.Array<StringName> template_properties,
        Godot.Collections.Array<StringName> instance_properties,
        int mode
    )
    {
        switch (normalize_properties_mode(mode))
        {
            case (int)PropertyMergeMode.REPLACE:
                return _normalize_properties(instance_properties);
            case (int)PropertyMergeMode.ADD:
                return _add_properties(template_properties, instance_properties);
            case (int)PropertyMergeMode.REMOVE:
                return _remove_properties(template_properties, instance_properties);
            default:
                return _normalize_properties(template_properties);
        }
    }

    private static Godot.Collections.Array<StringName> _add_properties(
        Godot.Collections.Array<StringName> template_properties,
        Godot.Collections.Array<StringName> instance_properties
    )
    {
        var result = _normalize_properties(template_properties);
        var seen = new Godot.Collections.Dictionary<StringName, bool>();
        foreach (var value in result)
        {
            seen[value] = true;
        }
        foreach (var rawValue in instance_properties)
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

    private static Godot.Collections.Array<StringName> _remove_properties(
        Godot.Collections.Array<StringName> template_properties,
        Godot.Collections.Array<StringName> instance_properties
    )
    {
        var removeSet = new Godot.Collections.Dictionary<StringName, bool>();
        foreach (var rawValue in instancePropertiesOrEmpty(instance_properties))
        {
            var normalized = _to_string_name(rawValue);
            if (normalized != new StringName(""))
            {
                removeSet[normalized] = true;
            }
        }

        var result = new Godot.Collections.Array<StringName>();
        foreach (var rawValue in instancePropertiesOrEmpty(template_properties))
        {
            var normalized = _to_string_name(rawValue);
            if (normalized == new StringName("") || removeSet.ContainsKey(normalized))
            {
                continue;
            }
            result.Add(normalized);
        }
        return result;
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
