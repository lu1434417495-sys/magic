using Godot;

[GlobalClass]
public partial class StageAdvancementModifier : Resource
{
    private static readonly StringName TargetAxisFull = "full";
    private static readonly StringName TargetAxisPhysical = "physical";
    private static readonly StringName TargetAxisMental = "mental";
    private static readonly StringName TargetAxisBloodline = "bloodline";
    private static readonly StringName TargetAxisDivine = "divine";
    private static readonly StringName TargetAxisMartial = "martial";
    private static readonly StringName TargetAxisDomain = "domain";

    [Export]
    public StringName modifier_id = "";

    [Export]
    public string display_name = "";

    [Export]
    public StringName target_axis = TargetAxisFull;

    [Export]
    public int stage_offset = 1;

    [Export]
    public StringName max_stage_id = "";

    [Export]
    public Godot.Collections.Array<StringName> applies_to_race_ids = new();

    [Export]
    public Godot.Collections.Array<StringName> applies_to_subrace_ids = new();

    [Export]
    public Godot.Collections.Array<StringName> applies_to_bloodline_ids = new();

    [Export]
    public Godot.Collections.Array<StringName> applies_to_ascension_ids = new();

    [Export]
    public bool grants_attributes = true;

    [Export]
    public bool grants_traits;

    [Export]
    public bool grants_body_size_change;

    public static StringName TARGET_AXIS_FULL() => TargetAxisFull;

    public static StringName TARGET_AXIS_PHYSICAL() => TargetAxisPhysical;

    public static StringName TARGET_AXIS_MENTAL() => TargetAxisMental;

    public static StringName TARGET_AXIS_BLOODLINE() => TargetAxisBloodline;

    public static StringName TARGET_AXIS_DIVINE() => TargetAxisDivine;

    public static StringName TARGET_AXIS_MARTIAL() => TargetAxisMartial;

    public static StringName TARGET_AXIS_DOMAIN() => TargetAxisDomain;

    public static Godot.Collections.Array<StringName> VALID_TARGET_AXES()
    {
        return new Godot.Collections.Array<StringName>
        {
            TargetAxisFull,
            TargetAxisPhysical,
            TargetAxisMental,
            TargetAxisBloodline,
            TargetAxisDivine,
            TargetAxisMartial,
            TargetAxisDomain,
        };
    }
}
