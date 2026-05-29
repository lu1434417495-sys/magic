using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class SpySequenceDamageResolver : BattleDamageResolver
{
    public int sequence_preview_call_count = 0;

    public override GDictionary preview_damage_sequence(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        Godot.Collections.Array effect_defs,
        GDictionary damage_context = null,
        GDictionary options = null
    )
    {
        sequence_preview_call_count += 1;
        return new GDictionary
        {
            ["is_empty"] = false,
            ["applied"] = true,
            ["hp_damage"] = 7,
            ["post_save_damage"] = 7,
            ["incoming_budget_damage"] = 7,
            ["shield_absorbed"] = 0,
            ["shield_broken"] = false,
            ["stable_lethal"] = false,
            ["lethal_probability_basis_points"] = 0,
            ["save_estimates"] = new GArray(),
            ["damage_events"] = new GArray(),
            ["branches"] = new GArray(),
            ["diagnostics"] = new GArray(),
        };
    }
}
