using Godot;

internal enum StageAdvancementTargetAxis
{
    Unknown,
    Full,
    Physical,
    Mental,
    Bloodline,
    Divine,
    Martial,
    Domain,
}

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
    internal StageAdvancementTargetAxis TargetAxisKind
    {
        get => ToTargetAxis(target_axis);
        set => target_axis = ToStringName(value);
    }

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

    internal static StageAdvancementTargetAxis ToTargetAxis(StringName targetAxis)
    {
        if (targetAxis == TargetAxisFull)
            return StageAdvancementTargetAxis.Full;
        if (targetAxis == TargetAxisPhysical)
            return StageAdvancementTargetAxis.Physical;
        if (targetAxis == TargetAxisMental)
            return StageAdvancementTargetAxis.Mental;
        if (targetAxis == TargetAxisBloodline)
            return StageAdvancementTargetAxis.Bloodline;
        if (targetAxis == TargetAxisDivine)
            return StageAdvancementTargetAxis.Divine;
        if (targetAxis == TargetAxisMartial)
            return StageAdvancementTargetAxis.Martial;
        if (targetAxis == TargetAxisDomain)
            return StageAdvancementTargetAxis.Domain;
        return StageAdvancementTargetAxis.Unknown;
    }

    internal static StringName ToStringName(StageAdvancementTargetAxis targetAxis) =>
        targetAxis switch
        {
            StageAdvancementTargetAxis.Full => TargetAxisFull,
            StageAdvancementTargetAxis.Physical => TargetAxisPhysical,
            StageAdvancementTargetAxis.Mental => TargetAxisMental,
            StageAdvancementTargetAxis.Bloodline => TargetAxisBloodline,
            StageAdvancementTargetAxis.Divine => TargetAxisDivine,
            StageAdvancementTargetAxis.Martial => TargetAxisMartial,
            StageAdvancementTargetAxis.Domain => TargetAxisDomain,
            _ => "",
        };

}
