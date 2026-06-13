using Godot;
using GArrayBool = Godot.Collections.Array<bool>;
using GArrayInt = Godot.Collections.Array<int>;
using GArrayStringName = Godot.Collections.Array<Godot.StringName>;
using GDictionary = Godot.Collections.Dictionary;

public partial class StageOutcomeDamageResolver : BattleDamageResolver
{
    public GArrayBool stage_successes = new();
    public GArrayInt stage_damage = new();
    public GArrayStringName target_ids_seen = new();
    public GArrayStringName dead_target_ids_seen = new();
    public GArrayInt hp_before_by_call = new();
    public int call_count = 0;

    internal override GDictionary ResolveAttackEffects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        Godot.Collections.Array effect_defs,
        AttackCheckInput attack_check,
        AttackContext attack_context = null
    )
    {
        bool success = call_count < stage_successes.Count && stage_successes[call_count];
        int damage = call_count < stage_damage.Count ? stage_damage[call_count] : 0;
        call_count += 1;

        if (target_unit != null)
        {
            target_ids_seen.Add(target_unit.unit_id);
            hp_before_by_call.Add(target_unit.current_hp);
            if (!target_unit.is_alive)
            {
                dead_target_ids_seen.Add(target_unit.unit_id);
            }
        }

        if (!success)
        {
            return new GDictionary
            {
                ["attack_success"] = false,
                ["attack_resolution"] = new StringName("miss"),
                ["hit_roll"] = 1,
                ["applied"] = false,
                ["damage"] = 0,
                ["healing"] = 0,
                ["status_effect_ids"] = new GArrayStringName(),
                ["source_status_effect_ids"] = new GArrayStringName(),
            };
        }

        if (target_unit != null && damage > 0)
        {
            target_unit.current_hp = Mathf.Max(target_unit.current_hp - damage, 0);
            if (target_unit.current_hp <= 0)
            {
                target_unit.is_alive = false;
            }
        }

        return new GDictionary
        {
            ["attack_success"] = true,
            ["attack_resolution"] = new StringName("hit"),
            ["hit_roll"] = 10,
            ["applied"] = true,
            ["damage"] = damage,
            ["healing"] = 0,
            ["status_effect_ids"] = new GArrayStringName(),
            ["source_status_effect_ids"] = new GArrayStringName(),
        };
    }
}
