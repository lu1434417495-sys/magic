using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public readonly record struct BattleCommonSkillTargetResult(
    StringName TargetUnitId,
    int Damage,
    int Healing,
    bool Defeated
);

[GlobalClass]
public partial class BattleCommonSkillOutcome : RefCounted
{
    public StringName source_unit_id { get; set; } = "";
    public StringName skill_id { get; set; } = "";
    public int total_damage { get; set; }
    public int total_healing { get; set; }
    public Godot.Collections.Array<StringName> defeated_unit_ids { get; set; } = new();
    public Godot.Collections.Array<StringName> changed_unit_ids { get; set; } = new();
    public Godot.Collections.Array<Vector2I> changed_coords { get; set; } = new();
    public Godot.Collections.Array<string> log_lines { get; set; } = new();
    public Godot.Collections.Array<GDictionary> report_entries { get; set; } = new();
    public List<BattleCommonSkillTargetResult> target_results { get; } = new();
    public Dictionary<StringName, List<StringName>> status_effect_ids_by_unit_id { get; } = new();

    public void add_changed_unit_id(StringName unit_id)
    {
        if (IsEmpty(unit_id) || changed_unit_ids.Contains(unit_id))
        {
            return;
        }
        changed_unit_ids.Add(unit_id);
    }

    public void add_changed_coord(Vector2I coord)
    {
        if (changed_coords.Contains(coord))
        {
            return;
        }
        changed_coords.Add(coord);
    }

    public void add_defeated_unit_id(StringName unit_id)
    {
        if (IsEmpty(unit_id) || defeated_unit_ids.Contains(unit_id))
        {
            return;
        }
        defeated_unit_ids.Add(unit_id);
    }

    public void add_target_result(StringName target_unit_id, int damage, int healing, bool defeated)
    {
        if (IsEmpty(target_unit_id))
        {
            return;
        }
        target_results.Add(
            new BattleCommonSkillTargetResult(target_unit_id, damage, healing, defeated)
        );
    }

    public void add_status_effect_ids(
        StringName unit_id,
        Godot.Collections.Array<StringName> status_effect_ids
    )
    {
        if (IsEmpty(unit_id) || status_effect_ids == null || status_effect_ids.Count == 0)
        {
            return;
        }

        if (!status_effect_ids_by_unit_id.TryGetValue(unit_id, out List<StringName> existing))
        {
            existing = new List<StringName>();
            status_effect_ids_by_unit_id[unit_id] = existing;
        }

        foreach (StringName statusId in status_effect_ids)
        {
            if (!IsEmpty(statusId) && !existing.Contains(statusId))
            {
                existing.Add(statusId);
            }
        }
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
