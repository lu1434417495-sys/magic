using Godot;
using GArray = Godot.Collections.Array;

[GlobalClass]
public partial class GameRuntimeBattleSelectionState : RefCounted
{
    public Vector2I battle_selected_coord { get; set; } = new Vector2I(-1, -1);

    public StringName selected_skill_id { get; set; } = "";

    public StringName selected_skill_variant_id { get; set; } = "";

    public Godot.Collections.Array<Vector2I> queued_target_coords { get; set; } = new();

    public Godot.Collections.Array<StringName> queued_target_unit_ids { get; set; } = new();

    public StringName last_manual_unit_id { get; set; } = "";

    public void ClearTargets()
    {
        queued_target_coords.Clear();

        queued_target_unit_ids.Clear();
    }

    public void ClearSkillSelection(bool reset_last_manual = false)
    {
        selected_skill_id = "";

        selected_skill_variant_id = "";

        ClearTargets();
        if (reset_last_manual)
        {
            last_manual_unit_id = "";
        }
    }

    public void ResetForBattleEnd()
    {
        battle_selected_coord = new Vector2I(-1, -1);

        ClearSkillSelection(true);
    }
}
