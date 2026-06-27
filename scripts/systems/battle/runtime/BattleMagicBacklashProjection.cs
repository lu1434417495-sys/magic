using Godot;

internal static class BattleMagicBacklashProjection
{
    internal static Godot.Collections.Dictionary Project(BattleSpellControlMetadata metadata)
    {
        if (metadata == null || !metadata.HasResolutionMetadata)
            return new Godot.Collections.Dictionary();

        return new Godot.Collections.Dictionary
        {
            ["attack_resolution"] = metadata.AttackResolution,
            ["spell_control_resolution"] = metadata.SpellControlResolution,
            ["attack_success"] = metadata.AttackSuccess,
            ["critical_hit"] = metadata.CriticalHit,
            ["critical_fail"] = metadata.CriticalFail,
            ["ordinary_miss"] = metadata.OrdinaryMiss,
            ["is_disadvantage"] = metadata.IsDisadvantage,
            ["hidden_luck_at_birth"] = metadata.HiddenLuckAtBirth,
            ["faith_luck_bonus"] = metadata.FaithLuckBonus,
            ["effective_luck"] = metadata.EffectiveLuck,
            ["crit_locked"] = metadata.CritLocked,
            ["crit_gate_die"] = metadata.CritGateDie,
            ["crit_gate_roll"] = metadata.CritGateRoll,
            ["hit_roll"] = metadata.HitRoll,
            ["fumble_low_end"] = metadata.FumbleLowEnd,
            ["crit_threshold"] = metadata.CritThreshold,
            ["locked_skill_hit_bonus"] = metadata.LockedSkillHitBonus,
            ["effective_hit_roll"] = metadata.EffectiveHitRoll,
            ["reverse_fate_downgraded"] = metadata.ReverseFateDowngraded,
            ["trait_trigger_results"] = new Godot.Collections.Array(),
        };
    }

    internal static Godot.Collections.Dictionary Project(BattleSpellControlResult result) =>
        new()
        {
            ["skip_effects"] = result.SkipEffects,
            ["backlash_triggered"] = result.BacklashTriggered,
            ["fumble_protected"] = result.FumbleProtected,
            ["mp_refund"] = result.MpRefund,
            ["extra_mp_drained"] = result.ExtraMpDrained,
            ["spell_control"] = Project(result.SpellControl),
        };

    internal static Godot.Collections.Dictionary Project(BattleGroundBacklashTargetResult result) =>
        new()
        {
            ["target_coords"] = ToVector2IArray(result.TargetCoords),
            ["backlash_triggered"] = result.BacklashTriggered,
            ["original_target_coord"] = result.OriginalTargetCoord,
            ["resolved_target_coord"] = result.ResolvedTargetCoord,
            ["offset_delta"] = result.OffsetDelta,
            ["backlash_offset_fallback"] = result.BacklashOffsetFallback,
        };

    private static Godot.Collections.Array<Vector2I> ToVector2IArray(
        System.Collections.Generic.IReadOnlyList<Vector2I> coords
    ) => new Vector2IList(coords).ToGodotArray();
}
