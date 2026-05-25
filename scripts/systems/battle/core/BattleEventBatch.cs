using Godot;
using GArray = Godot.Collections.Array;

[GlobalClass]
public partial class BattleEventBatch : RefCounted
{
    public bool phase_changed { get; set; }
    public bool battle_ended { get; set; }
    public Godot.Collections.Array<StringName> changed_unit_ids { get; set; } = new();
    public Godot.Collections.Array<Vector2I> changed_coords { get; set; } = new();
    public Godot.Collections.Array<string> log_lines { get; set; } = new();
    public GArray report_entries { get; set; } = new();
    public GArray progression_deltas { get; set; } = new();
    public bool modal_requested { get; set; }

    public void clear()
    {
        phase_changed = false;
        battle_ended = false;
        changed_unit_ids.Clear();
        changed_coords.Clear();
        log_lines.Clear();
        report_entries.Clear();
        progression_deltas.Clear();
        modal_requested = false;
    }
}
