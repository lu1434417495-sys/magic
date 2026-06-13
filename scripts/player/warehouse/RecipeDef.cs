using Godot;
using Godot.Collections;

[GlobalClass]
public partial class RecipeDef : Resource
{
    [Export]
    public StringName recipe_id = "";

    [Export]
    public string display_name = "";

    [Export(PropertyHint.MultilineText)]
    public string description = "";

    [Export]
    public Array<StringName> input_item_ids = new();

    [Export]
    public int[] input_item_quantities = System.Array.Empty<int>();

    [Export]
    public StringName output_item_id = "";

    [Export(PropertyHint.Range, "1,9999,1")]
    public int output_quantity = 1;

    [Export]
    public Array<StringName> required_facility_tags = new();

    [Export]
    public string failure_reason = "";
}
