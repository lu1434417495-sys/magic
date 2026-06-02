using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BarrierOutcomeDef : Resource
{
    [Export]
    public StringName outcome_type = "";

    [Export]
    public int amount = 0;

    [Export]
    public StringName damage_tag = "";

    [Export]
    public bool half_on_success = false;

    [Export]
    public int success_amount = 0;

    [Export]
    public StringName success_damage_tag = "";

    [Export]
    public int fatal_damage = 99999;

    [Export]
    public StringName status_id = "";

    [Export]
    public StringName save_ability = "";

    [Export]
    public StringName save_tag = "";

    [Export]
    public int save_dc = 0;

    public Dictionary ToRuntimeDict(int defaultSaveDc = 0)
    {
        int resolvedSaveDc = save_dc;
        if (resolvedSaveDc <= 0)
            resolvedSaveDc = Mathf.Max(defaultSaveDc, 0);
        return new Dictionary()
        {
            { "outcome_type", (string)outcome_type },
            { "amount", amount },
            { "damage_tag", (string)damage_tag },
            { "half_on_success", half_on_success },
            { "success_amount", success_amount },
            { "success_damage_tag", (string)success_damage_tag },
            { "fatal_damage", Mathf.Max(fatal_damage, 1) },
            { "status_id", (string)status_id },
            { "save_ability", (string)save_ability },
            { "save_tag", (string)save_tag },
            { "save_dc", resolvedSaveDc },
        };
    }
}
