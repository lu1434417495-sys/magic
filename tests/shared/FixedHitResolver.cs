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
            AttackResolutionHit,
            true,
            false,
            false
        );
    }

    public new GDictionary resolve_spell_control_metadata(
        BattleUnitState source_unit,
        GDictionary attack_context
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
        return new GDictionary
        {
            ["attack_resolution"] = attackResolution,
            ["spell_control_resolution"] = spellControlResolution,
            ["attack_success"] = attackSuccess,
            ["critical_hit"] = criticalHit,
            ["critical_fail"] = criticalFail,
            ["ordinary_miss"] = false,
            ["hit_roll"] = fixed_roll,
        };
    }

    public override AttackRollResult roll_attack_check(
        BattleState battle_state,
        AttackCheckInput attack_check
    )
    {
        if (battle_state != null)
            battle_state.next_attack_roll_nonce();
        return new AttackRollResult(
            roll: fixed_roll,
            rollDisposition: RollDispositionThresholdHit,
            success: true,
            resolutionText: $"100%（测试固定命中），d20={fixed_roll}",
            attackRollNonce: battle_state != null ? battle_state.attack_roll_nonce.ToString() : ""
        );
    }

    public override AttackRollResult roll_hit_rate(BattleState battle_state, int hit_rate_percent)
    {
        return roll_attack_check(
            battle_state,
            new AttackCheckInput(
                requiredRoll: fixed_roll,
                displayRequiredRoll: fixed_roll,
                naturalOneAutoMiss: false,
                naturalTwentyAutoHit: false
            )
        );
    }

    public override int roll_attack_die(
        int die_size,
        bool is_disadvantage,
        GDictionary attack_context
    )
    {
        return Math.Clamp(fixed_roll, 1, Math.Max(die_size, 1));
    }

    public override int roll_attack_die(
        int die_size,
        bool is_disadvantage,
        AttackContext attack_context
    )
    {
        return Math.Clamp(fixed_roll, 1, Math.Max(die_size, 1));
    }

    public new int _roll_true_random_attack_range(
        int min_value,
        int max_value,
        BattleState battle_state
    )
    {
        if (battle_state != null)
            battle_state.next_attack_roll_nonce();
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
            CritLocked = attackContext?.ForceHitNoCrit ?? false,
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

    protected static bool GetBool(GDictionary dictionary, string key, bool fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsBool();
    }
}
