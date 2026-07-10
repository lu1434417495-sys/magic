using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedHitResolver : BattleHitResolver
{
    protected const int NaturalMissRoll = 1;
    protected const int NaturalHitRoll = 20;
    protected static readonly StringName AttackResolutionHit = "hit";
    protected static readonly StringName AttackResolutionCriticalHit = "critical_hit";
    protected static readonly StringName AttackResolutionCriticalFail = "critical_fail";
    protected static readonly StringName RollDispositionThresholdHit = "threshold_hit";

    public int fixed_roll = 10;

    public FixedHitResolver() { }

    public FixedHitResolver(int pFixedRoll)
    {
        fixed_roll = Math.Clamp(pFixedRoll, NaturalMissRoll, NaturalHitRoll);
    }

    public override AttackResolutionMetadata ResolveAttackMetadata(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        AttackCheckInput attack_check,
        AttackContext attack_context
    )
    {
        bool forcedCritical =
            attack_check.ForceCriticalOnHit
            && !attack_check.ForceHitNoCrit
            && !attack_check.CritLocked
            && attack_context?.ForceHitNoCrit != true;
        return BuildFixedAttackMetadata(
            attack_check,
            attack_context,
            forcedCritical ? AttackResolutionCriticalHit : AttackResolutionHit,
            true,
            forcedCritical,
            false
        );
    }

    public override BattleSpellControlMetadata ResolveSpellControlMetadataTyped(
        BattleUnitState source_unit,
        AttackContext attack_context
    )
    {
        StringName attackResolution = AttackResolutionHit;
        StringName spellControlResolution = "normal";
        bool attackSuccess = true;
        bool criticalHit = false;
        bool criticalFail = false;
        if (fixed_roll <= NaturalMissRoll)
        {
            attackResolution = AttackResolutionCriticalFail;
            spellControlResolution = "critical_fail";
            attackSuccess = false;
            criticalFail = true;
        }
        else if (fixed_roll >= NaturalHitRoll)
        {
            attackResolution = AttackResolutionCriticalHit;
            spellControlResolution = "critical_success";
            criticalHit = true;
        }
        return new BattleSpellControlMetadata
        {
            AttackResolution = attackResolution,
            SpellControlResolution = spellControlResolution,
            AttackSuccess = attackSuccess,
            CriticalHit = criticalHit,
            CriticalFail = criticalFail,
            OrdinaryMiss = false,
            HitRoll = fixed_roll,
            EffectiveHitRoll = fixed_roll,
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
            roll: fixed_roll,
            rollDisposition: RollDispositionThresholdHit,
            success: true,
            resolutionText: $"100%（测试固定命中），d20={fixed_roll}",
            attackRollNonce: battle_state != null ? battle_state.attack_roll_nonce.ToString() : ""
        );
    }

    public override AttackRollResult RollHitRate(BattleState battle_state, int hit_rate_percent)
    {
        return RollAttackCheck(
            battle_state,
            new AttackCheckInput(
                requiredRoll: fixed_roll,
                displayRequiredRoll: fixed_roll,
                naturalOneAutoMiss: false,
                naturalTwentyAutoHit: false
            )
        );
    }

    public override int RollAttackDie(
        int die_size,
        bool is_disadvantage,
        AttackContext attack_context
    )
    {
        return Math.Clamp(fixed_roll, 1, Math.Max(die_size, 1));
    }

    protected override int RollTrueRandomAttackRange(
        int min_value,
        int max_value,
        BattleState battle_state
    )
    {
        if (battle_state != null)
            battle_state.NextAttackRollNonce();
        return Math.Clamp(
            fixed_roll,
            Math.Min(min_value, max_value),
            Math.Max(min_value, max_value)
        );
    }

    protected AttackResolutionMetadata BuildFixedAttackMetadata(
        AttackCheckInput attackCheck,
        AttackContext attackContext,
        StringName attackResolution,
        bool attackSuccess,
        bool criticalHit,
        bool ordinaryMiss
    )
    {
        int requiredRoll = attackCheck.RequiredRoll != 0 ? attackCheck.RequiredRoll : fixed_roll;
        return new AttackResolutionMetadata
        {
            AttackResolution = attackResolution,
            AttackSuccess = attackSuccess,
            CriticalHit = criticalHit,
            CriticalFail = false,
            OrdinaryMiss = ordinaryMiss,
            IsDisadvantage = attackContext?.IsDisadvantage ?? false,
            HiddenLuckAtBirth = 0,
            FaithLuckBonus = 0,
            EffectiveLuck = 0,
            CritLocked = attackCheck.CritLocked || (attackContext?.ForceHitNoCrit ?? false),
            CritGateDie = 20,
            CritGateRoll = 0,
            HitRoll = fixed_roll,
            FumbleLowEnd = 1,
            CritThreshold = NaturalHitRoll,
            RequiredRoll = requiredRoll,
            DisplayRequiredRoll =
                attackCheck.DisplayRequiredRoll != 0
                    ? attackCheck.DisplayRequiredRoll
                    : Math.Clamp(requiredRoll, 2, NaturalHitRoll),
            HitRatePercent = attackSuccess ? 100 : 0,
            SuccessRatePercent = attackSuccess ? 100 : 0,
            SkillId = attackContext?.SkillId ?? new StringName(""),
        };
    }

    protected static int GetInt(GDictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsInt32();
    }

}
