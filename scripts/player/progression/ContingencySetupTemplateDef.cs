using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;

[GlobalClass]
public partial class ContingencySetupTemplateDef : Resource
{
    [Export]
    public StringName template_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export]
    public StringName source_skill_id { get; set; } = "";

    [Export]
    public int matrix_load { get; set; } = 1;

    [Export]
    public StringName release_mode { get; set; } = "";

    [Export]
    public GDictionary trigger { get; set; } = new();

    // Each entry follows the ContingencyStoredSpellEntryState schema, except that
    // cast_level is authored as max_cast_level and stamped per member at build time.
    [Export]
    public GDictionaryArray stored_spells { get; set; } = new();
}
