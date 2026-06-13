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
    private static readonly StringName FootprintSingle = "single";
    private static readonly StringName FootprintLine2 = "line2";
    private static readonly StringName FootprintSquare2 = "square2";
    private static readonly StringName FootprintUnordered = "unordered";

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
        get => ToFootprintPattern(footprint_pattern);
        set => footprint_pattern = ToStringName(value);
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
        if (value == FootprintSingle)
            return CombatCastFootprintPattern.Single;
        if (value == FootprintLine2)
            return CombatCastFootprintPattern.Line2;
        if (value == FootprintSquare2)
            return CombatCastFootprintPattern.Square2;
        if (value == FootprintUnordered)
            return CombatCastFootprintPattern.Unordered;
        return CombatCastFootprintPattern.Unknown;
    }

    internal static StringName ToStringName(CombatCastFootprintPattern pattern)
    {
        return pattern switch
        {
            CombatCastFootprintPattern.Single => FootprintSingle,
            CombatCastFootprintPattern.Line2 => FootprintLine2,
            CombatCastFootprintPattern.Square2 => FootprintSquare2,
            CombatCastFootprintPattern.Unordered => FootprintUnordered,
            _ => "",
        };
    }
}
