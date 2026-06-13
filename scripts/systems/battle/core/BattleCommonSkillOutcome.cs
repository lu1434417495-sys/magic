using System.Collections.Generic;
using Godot;

internal readonly record struct BattleCommonSkillTargetResult(
    StringName TargetUnitId,
    int Damage,
    int Healing,
    bool Defeated
);

internal class BattleCommonSkillOutcome
{
    public StringName source_unit_id { get; set; } = "";
    public StringName skill_id { get; set; } = "";
    public int total_damage { get; set; }
    public int total_healing { get; set; }
    public Godot.Collections.Array<StringName> defeated_unit_ids { get; set; } = new();
    public Godot.Collections.Array<StringName> changed_unit_ids { get; set; } = new();
    public Godot.Collections.Array<Vector2I> changed_coords { get; set; } = new();
    public Godot.Collections.Array<string> log_lines { get; set; } = new();
    public Godot.Collections.Array<Godot.Collections.Dictionary> report_entries { get; set; } =
        new();
    public List<BattleCommonSkillTargetResult> target_results { get; } = new();
    public Dictionary<StringName, List<StringName>> status_effect_ids_by_unit_id { get; } = new();

    internal void AddChangedUnitId(StringName unit_id)
    {
        if (IsEmpty(unit_id) || changed_unit_ids.Contains(unit_id))
        {
            return;
        }
        changed_unit_ids.Add(unit_id);
    }

    internal void AddChangedCoord(Vector2I coord)
    {
        if (changed_coords.Contains(coord))
        {
            return;
        }
        changed_coords.Add(coord);
    }

    internal void AddDefeatedUnitId(StringName unit_id)
    {
        if (IsEmpty(unit_id) || defeated_unit_ids.Contains(unit_id))
        {
            return;
        }
        defeated_unit_ids.Add(unit_id);
    }

    internal void AddTargetResult(StringName target_unit_id, int damage, int healing, bool defeated)
    {
        if (IsEmpty(target_unit_id))
        {
            return;
        }
        target_results.Add(
            new BattleCommonSkillTargetResult(target_unit_id, damage, healing, defeated)
        );
    }

    internal void AddStatusEffectIds(StringName unit_id, IEnumerable<StringName> status_effect_ids)
    {
        if (IsEmpty(unit_id) || status_effect_ids == null)
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
