using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class ContingencyAutomationDef : Resource
{
    [Export]
    public bool can_be_stored_in_contingency { get; set; }

    [Export]
    public int min_contingency_skill_level { get; set; } = 1;

    [Export]
    public StringName effect_category { get; set; } = "";

    [Export]
    public Godot.Collections.Array<StringName> tags { get; set; } = new();

    [Export]
    public int contingency_load_override { get; set; }

    [Export]
    public Godot.Collections.Array<StringName> allowed_target_resolvers { get; set; } = new();

    [Export]
    public bool requires_manual_targeting { get; set; }

    [Export]
    public GDictionary allowed_parameter_bindings { get; set; } = new();

    public bool AllowsTargetResolver(StringName resolver)
    {
        if (resolver == "")
            return false;
        foreach (StringName allowedResolver in allowed_target_resolvers)
            if (allowedResolver == resolver)
                return true;
        return false;
    }

    public bool AllowsParameterBinding(StringName bindingKey)
    {
        if (bindingKey == "" || allowed_parameter_bindings == null)
            return false;
        return allowed_parameter_bindings.ContainsKey(bindingKey)
            || allowed_parameter_bindings.ContainsKey(bindingKey.ToString());
    }
}
