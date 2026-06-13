using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedCriticalHitResolver : FixedHitResolver
{
    public FixedCriticalHitResolver()
        : base(NaturalHitRoll) { }

    public override AttackResolutionMetadata ResolveAttackMetadata(
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

    public override BattleSpellControlMetadata ResolveSpellControlMetadataTyped(
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

    protected override int RollTrueRandomAttackRange(
        int min_value,
        int max_value,
        BattleState battle_state
    )
    {
        if (battle_state != null)
            battle_state.NextAttackRollNonce();
        return Mathf.Max(min_value, max_value);
    }
}
