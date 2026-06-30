using System.Collections.Generic;
using Godot;

public sealed class GameRuntimeBattleSelectionState
{
    public Vector2I battle_selected_coord { get; set; } = new Vector2I(-1, -1);

    public StringName selected_skill_entry_id { get; set; } = "";

    public StringName selected_skill_id { get; set; } = "";

    public StringName selected_skill_variant_id { get; set; } = "";

    public List<Vector2I> queued_target_coords { get; } = new();

    public List<StringName> queued_target_unit_ids { get; } = new();

    public StringName last_manual_unit_id { get; set; } = "";

    public void SetTargetCoords(IEnumerable<Vector2I> targetCoords)
    {
        queued_target_coords.Clear();
        if (targetCoords == null)
        {
            return;
        }
        foreach (Vector2I targetCoord in targetCoords)
        {
            queued_target_coords.Add(targetCoord);
        }
    }

    public void SetTargetUnitIds(IEnumerable<StringName> targetUnitIds)
    {
        queued_target_unit_ids.Clear();
        if (targetUnitIds == null)
        {
            return;
        }
        foreach (StringName targetUnitId in targetUnitIds)
        {
            queued_target_unit_ids.Add(targetUnitId);
        }
    }

    public void ClearTargets()
    {
        queued_target_coords.Clear();

        queued_target_unit_ids.Clear();
    }

    public void ClearSkillSelection(bool reset_last_manual = false)
    {
        selected_skill_entry_id = "";

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
