using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedCriticalHitResolver : FixedHitResolver
{
    public FixedCriticalHitResolver()
        : base(NaturalHitRoll) { }

    public override AttackResolutionMetadata resolve_attack_metadata(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        AttackCheckInput attack_check,
        AttackContext attack_context
    )
    {
        return BuildFixedAttackMetadata(
            attack_check,
            attack_context,
            AttackResolutionCriticalHit,
            true,
            true,
            false
        );
    }

    public new GDictionary resolve_spell_control_metadata(
        BattleUnitState source_unit,
        GDictionary attack_context
    )
    {
        return new GDictionary
        {
            ["attack_resolution"] = AttackResolutionCriticalHit,
            ["spell_control_resolution"] = new StringName("critical_success"),
            ["attack_success"] = true,
            ["critical_hit"] = true,
            ["critical_fail"] = false,
            ["ordinary_miss"] = false,
            ["hit_roll"] = NaturalHitRoll,
        };
    }

    public new int _roll_true_random_attack_range(
        int min_value,
        int max_value,
        BattleState battle_state
    )
    {
        if (battle_state != null)
            battle_state.next_attack_roll_nonce();
        return Mathf.Max(min_value, max_value);
    }
}
