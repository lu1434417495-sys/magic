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

    public override GDictionary resolve_spell_control_metadata(
        BattleUnitState source_unit,
        AttackContext attack_context
    ) => resolve_spell_control_metadata_typed(source_unit, attack_context).ToDictionary();

    public override BattleSpellControlMetadata resolve_spell_control_metadata_typed(
        BattleUnitState source_unit,
        AttackContext attack_context
    )
    {
        return new BattleSpellControlMetadata
        {
            AttackResolution = AttackResolutionCriticalHit,
            SpellControlResolution = "critical_success",
            AttackSuccess = true,
            CriticalHit = true,
            CriticalFail = false,
            OrdinaryMiss = false,
            HitRoll = NaturalHitRoll,
            EffectiveHitRoll = NaturalHitRoll,
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
