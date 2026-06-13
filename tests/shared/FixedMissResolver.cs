using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedMissResolver : BattleHitResolver
{
    private const int NaturalMissRoll = 1;
    private const int NaturalHitRoll = 20;
    private static readonly StringName AttackResolutionMiss = "miss";
    private static readonly StringName RollDispositionNaturalAutoMiss = "natural_1_auto_miss";

    public override AttackResolutionMetadata ResolveAttackMetadata(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        AttackCheckInput attack_check,
        AttackContext attack_context
    )
    {
        int requiredRoll = attack_check.RequiredRoll != 0 ? attack_check.RequiredRoll : NaturalHitRoll;
        return new AttackResolutionMetadata
        {
            AttackResolution = AttackResolutionMiss,
            AttackSuccess = false,
            CriticalHit = false,
            CriticalFail = false,
            OrdinaryMiss = true,
            IsDisadvantage = attack_context?.IsDisadvantage ?? false,
            HiddenLuckAtBirth = 0,
            FaithLuckBonus = 0,
            EffectiveLuck = 0,
            CritLocked = false,
            CritGateDie = 20,
            CritGateRoll = 0,
            HitRoll = NaturalMissRoll,
            FumbleLowEnd = 1,
            CritThreshold = NaturalHitRoll,
            RequiredRoll = requiredRoll,
            DisplayRequiredRoll =
                attack_check.DisplayRequiredRoll != 0
                    ? attack_check.DisplayRequiredRoll
                    : Math.Clamp(requiredRoll, 2, NaturalHitRoll),
            HitRatePercent = 0,
            SuccessRatePercent = 0,
            SkillId = attack_context?.SkillId ?? new StringName(""),
        };
    }

    public override BattleSpellControlMetadata ResolveSpellControlMetadataTyped(
        BattleUnitState source_unit,
        AttackContext attack_context
    )
    {
        return new BattleSpellControlMetadata
        {
            AttackResolution = AttackResolutionMiss,
            SpellControlResolution = "miss",
            AttackSuccess = false,
            CriticalHit = false,
            CriticalFail = false,
            OrdinaryMiss = true,
            HitRoll = NaturalMissRoll,
            EffectiveHitRoll = NaturalMissRoll,
        };
    }

    public override AttackRollResult RollAttackCheck(
        BattleState battle_state,
        AttackCheckInput attack_check
    )
    {
        if (battle_state != null)
            battle_state.NextAttackRollNonce();
        return new AttackRollResult(
            roll: NaturalMissRoll,
            rollDisposition: RollDispositionNaturalAutoMiss,
            success: false,
            resolutionText: "0%（测试固定未命中），d20=1",
            attackRollNonce: battle_state != null ? battle_state.attack_roll_nonce.ToString() : ""
        );
    }

    public override int RollAttackDie(
        int die_size,
        bool is_disadvantage,
        AttackContext attack_context
    )
    {
        return Math.Clamp(NaturalMissRoll, 1, Math.Max(die_size, 1));
    }

    protected override int RollTrueRandomAttackRange(
        int min_value,
        int max_value,
        BattleState battle_state
    )
    {
        if (battle_state != null)
            battle_state.NextAttackRollNonce();
        return Math.Min(min_value, max_value);
    }

    private static int GetInt(GDictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsInt32();
    }

}
