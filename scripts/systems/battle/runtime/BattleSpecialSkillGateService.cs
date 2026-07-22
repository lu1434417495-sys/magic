using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GBattleUnitArray = System.Collections.Generic.List<BattleUnitState>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

internal sealed class BattleSpecialSkillGateService : BattleRuntimeModuleBorrower
{

    internal void _apply_on_kill_gain_resources_effects(
        BattleUnitState source_unit,
        BattleUnitState defeated_unit,
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch
    )
    {
        _runtime._ensure_sidecars_ready();
        _runtime._special_skill_resolver.ApplyOnKillGainResourcesEffects(
            source_unit,
            defeated_unit,
            skillDefinition,
            effectDefinitions ?? Array.Empty<CombatEffectDefinition>(),
            batch
        );
    }

    public BattleSpecialSkillResult ApplyUnitSkillSpecialEffectsResult(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skill_definition,
        CombatCastVariantDefinition cast_variant,
        IEnumerable<CombatEffectDefinition> effect_definitions,
        BattleEventBatch batch,
        BattleForcedMoveContext forced_move_context = default
    )
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.ApplyUnitSkillSpecialEffectsResult(
            active_unit,
            target_unit,
            skill_definition,
            cast_variant,
            effect_definitions ?? Array.Empty<CombatEffectDefinition>(),
            batch,
            forced_move_context
        );
    }

    internal void _set_runtime_status_effect(
        BattleUnitState unit_state,
        StringName status_id,
        int duration_tu,
        StringName source_unit_id = default,
        int power = 1,
        GDictionary @params = null,
        StringName source_profile_id = default,
        StringName source_layer_id = default,
        StringName source_skill_id = default,
        int? source_skill_level = null,
        int self_save_dc = 0,
        StringName self_save_ability = default,
        StringName self_save_tag = default,
        int? self_save_roll_override = null,
        int save_bonus = 0,
        int control_save_bonus = 0,
        int range_bonus = 0,
        int passive_reduction = 0,
        int content_dr = 0,
        int guard_block = 0,
        IReadOnlyList<StringName> save_advantage_tags = null,
        IReadOnlyList<StringName> save_disadvantage_tags = null,
        IReadOnlyList<StringName> save_immunity_tags = null,
        int? heal_multiplier_percent = null,
        int? shield_gain_multiplier_percent = null,
        double? incoming_damage_multiplier = null,
        double? outgoing_damage_multiplier = null,
        StringName damage_tag = default,
        IReadOnlyList<StringName> damage_tags = null,
        StringName damage_category = default,
        int attack_roll_penalty = -1,
        bool undispellable = false,
        bool dispellable_magic = false,
        bool dispellable_harmful_magic = false,
        bool dispellable_beneficial_magic = false,
        StringName mitigation_tier = default,
        StringName dr_bypass_tag = default,
        bool counts_as_debuff_override = false,
        bool counts_as_debuff = false,
        bool forced_move_immune = false,
        bool lock_counterattack = false,
        bool lock_guard = false,
        bool lock_dodge_bonus = false,
        bool lock_crit = false,
        int main_skill_lock_other_debuff_count = 0,
        StringName stack_behavior = default,
        int stack_limit = 0,
        StringName body_size_category_override = default,
        StringName previous_body_size_category = default
    )
    {
        _runtime._ensure_sidecars_ready();
        _runtime._special_skill_resolver.SetRuntimeStatusEffect(
            unit_state,
            status_id,
            duration_tu,
            source_unit_id,
            power,
            @params,
            source_profile_id,
            source_layer_id,
            source_skill_id,
            source_skill_level,
            self_save_dc,
            self_save_ability,
            self_save_tag,
            self_save_roll_override,
            save_bonus,
            control_save_bonus,
            range_bonus,
            passive_reduction,
            content_dr,
            guard_block,
            save_advantage_tags,
            save_disadvantage_tags,
            save_immunity_tags,
            heal_multiplier_percent,
            shield_gain_multiplier_percent,
            incoming_damage_multiplier,
            outgoing_damage_multiplier,
            damage_tag,
            damage_tags,
            damage_category,
            attack_roll_penalty,
            undispellable,
            dispellable_magic,
            dispellable_harmful_magic,
            dispellable_beneficial_magic,
            mitigation_tier,
            dr_bypass_tag,
            counts_as_debuff_override,
            counts_as_debuff,
            forced_move_immune,
            lock_counterattack,
            lock_guard,
            lock_dodge_bonus,
            lock_crit,
            main_skill_lock_other_debuff_count,
            stack_behavior,
            stack_limit,
            body_size_category_override,
            previous_body_size_category
        );
    }

    internal void _set_runtime_debuff_status_effect(
        BattleUnitState unit_state,
        StringName status_id,
        int duration_tu,
        StringName source_unit_id = default,
        int power = 1,
        GDictionary @params = null
    )
    {
        _runtime._ensure_sidecars_ready();
        _runtime._special_skill_resolver.SetRuntimeDebuffStatusEffect(
            unit_state,
            status_id,
            duration_tu,
            source_unit_id,
            power,
            @params
        );
    }

    internal void _set_runtime_body_size_override_status_effect(
        BattleUnitState unit_state,
        StringName status_id,
        int duration_tu,
        StringName source_unit_id = default,
        int power = 1,
        GDictionary @params = null,
        StringName body_size_category_override = default,
        StringName previous_body_size_category = default
    )
    {
        _runtime._ensure_sidecars_ready();
        _runtime._special_skill_resolver.SetRuntimeBodySizeOverrideStatusEffect(
            unit_state,
            status_id,
            duration_tu,
            source_unit_id,
            power,
            @params,
            body_size_category_override,
            previous_body_size_category
        );
    }

    internal void _set_runtime_source_status_effect(
        BattleUnitState unit_state,
        StringName status_id,
        int duration_tu,
        StringName source_unit_id,
        int power = 1,
        GDictionary @params = null
    )
    {
        _runtime._ensure_sidecars_ready();
        _runtime._special_skill_resolver.SetRuntimeSourceStatusEffect(
            unit_state,
            status_id,
            duration_tu,
            source_unit_id,
            power,
            @params
        );
    }

    internal void _set_runtime_barrier_status_effect(
        BattleUnitState unit_state,
        StringName status_id,
        StringName source_unit_id,
        StringName source_profile_id,
        StringName source_layer_id,
        int self_save_dc,
        StringName self_save_ability,
        StringName self_save_tag,
        int power = 1,
        GDictionary @params = null
    )
    {
        _runtime._ensure_sidecars_ready();
        _runtime._special_skill_resolver.SetRuntimeBarrierStatusEffect(
            unit_state,
            status_id,
            source_unit_id,
            source_profile_id,
            source_layer_id,
            self_save_dc,
            self_save_ability,
            self_save_tag,
            power,
            @params
        );
    }

    internal void _clear_black_star_brand_statuses(BattleUnitState unit_state)
    {
        _runtime._ensure_sidecars_ready();
        _runtime._special_skill_resolver.ClearBlackStarBrandStatuses(unit_state);
    }

    internal bool _is_black_star_brand_elite_target(BattleUnitState unit_state)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.IsBlackStarBrandEliteTarget(unit_state);
    }

    internal bool _is_elite_or_boss_target(BattleUnitState unit_state)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.IsEliteOrBossTarget(unit_state);
    }

    internal bool _is_boss_target(BattleUnitState unit_state)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.IsBossTarget(unit_state);
    }

    internal bool _is_black_star_brand_skill(StringName skill_id)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.IsBlackStarBrandSkill(skill_id);
    }

    internal bool _is_black_contract_push_skill(StringName skill_id)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.IsBlackContractPushSkill(skill_id);
    }

    internal bool _is_doom_shift_skill(StringName skill_id)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.IsDoomShiftSkill(skill_id);
    }

    internal bool _is_black_crown_seal_skill(StringName skill_id)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.IsBlackCrownSealSkill(skill_id);
    }

    internal void _clear_crown_break_seal_statuses(BattleUnitState unit_state)
    {
        _runtime._ensure_sidecars_ready();
        _runtime._special_skill_resolver.ClearCrownBreakSealStatuses(unit_state);
    }

    internal bool _is_crown_break_target_eligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.IsCrownBreakTargetEligible(active_unit, target_unit);
    }

    internal bool _is_crown_break_skill(StringName skill_id)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.IsCrownBreakSkill(skill_id);
    }

    internal bool _is_doom_sentence_target_eligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.IsDoomSentenceTargetEligible(active_unit, target_unit);
    }

    internal bool _is_black_crown_seal_target_eligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.IsBlackCrownSealTargetEligible(
            active_unit,
            target_unit
        );
    }

    internal bool _is_doom_sentence_skill(StringName skill_id)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.IsDoomSentenceSkill(skill_id);
    }

    internal bool _blocks_enemy_forced_move(BattleUnitState source_unit, BattleUnitState target_unit)
    {
        _runtime._ensure_sidecars_ready();
        return _runtime._special_skill_resolver.BlocksEnemyForcedMove(source_unit, target_unit);
    }

    internal void RecordVajraBodyMasteryFromIncomingDamageTyped(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        AttackEffectResolutionResult result,
        BattleEventBatch batch = null
    )
    {
        _runtime._ensure_sidecars_ready();
        _runtime._special_skill_resolver.RecordVajraBodyMasteryFromIncomingDamageTyped(
            sourceUnit,
            targetUnit,
            skillDefinition,
            result,
            batch
        );
    }

    internal string _get_black_contract_push_variant_block_reason(
        BattleUnitState active_unit,
        CombatCastVariantDefinition castVariant
    )
    {
        BattleSkillCastBlockReasonKind blockReason =
            _runtime._skill_turn_resolver.GetBlackContractPushVariantBlockReason(
            active_unit,
            castVariant
        );
        return _runtime._skill_turn_resolver.FormatSkillCastBlockReason(
            active_unit,
            null,
            blockReason,
            castVariant
        );
    }

    internal bool _consume_black_contract_push_cast(
        BattleUnitState active_unit,
        CombatCastVariantDefinition castVariant,
        BattleEventBatch batch = null
    ) => _runtime._skill_turn_resolver.ConsumeBlackContractPushCast(active_unit, castVariant, batch);
}
