using Godot;

internal enum CombatCastFootprintPattern
{
    Unknown = 0,
    Single,
    Line2,
    Square2,
    Unordered,
}

[GlobalClass]
public partial class CombatCastVariantDef : Resource
{
    [Export]
    public StringName variant_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export(PropertyHint.MultilineText)]
    public string description { get; set; } = "";

    [Export]
    public int min_skill_level { get; set; }

    [Export]
    public StringName target_mode { get; set; } = "ground";
    internal BattleTargetMode TargetModeKind
    {
        get => BattleTypedNames.ToTargetMode(target_mode);
        set => target_mode = BattleTypedNames.ToStringName(value);
    }

    [Export]
    public StringName footprint_pattern { get; set; } = "single";
    internal CombatCastFootprintPattern FootprintPatternKind
    {
        get => CombatSkillTargetingContentRules.ToFootprintPattern(footprint_pattern);
        set => footprint_pattern = CombatSkillTargetingContentRules.ToFootprintPatternId(value);
    }

    [Export]
    public int required_coord_count { get; set; } = 1;

    [Export]
    public Godot.Collections.Array<StringName> allowed_base_terrains { get; set; } = new();

    [Export]
    public Godot.Collections.Array<CombatEffectDef> effect_defs { get; set; } = new();

    [Export]
    public Godot.Collections.Dictionary @params { get; set; } = new();

    internal static CombatCastFootprintPattern ToFootprintPattern(StringName value)
    {
        return CombatSkillTargetingContentRules.ToFootprintPattern(value);
    }

    internal static StringName ToStringName(CombatCastFootprintPattern pattern)
    {
        return CombatSkillTargetingContentRules.ToFootprintPatternId(pattern);
    }
}
