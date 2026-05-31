using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GCombatEffectArray = Godot.Collections.Array<CombatEffectDef>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// 翻译自 battle_skill_execution_orchestrator.gd（2026-05-26，技能执行编排器 C# 迁移）。
// runtime 强耦合：执行编排器只直连 BattleRuntimeModule，不走 Godot 动态调用。
[GlobalClass]
public partial class BattleSkillExecutionOrchestrator : RefCounted
{
    private static readonly StringName BODY_SIZE_CATEGORY_OVERRIDE_EFFECT_TYPE =
        "body_size_category_override";
    private static readonly StringName CHAIN_DAMAGE_EFFECT_TYPE = "chain_damage";
    private static readonly StringName EQUIPMENT_DURABILITY_DAMAGE_EFFECT_TYPE =
        "equipment_durability_damage";
    private static readonly StringName STATUS_GUARDING = "guarding";
    private readonly record struct ChainDamageParameters(
        int BaseRadius,
        StringName BonusTerrainEffectId,
        int WetChainRadius,
        bool PreventRepeatTarget
    )
    {
        public static ChainDamageParameters FromEffect(CombatEffectDef effectDef)
        {
            GDictionary parameters = effectDef?.@params ?? new GDictionary();
            int baseRadius = Math.Max(DictInt(parameters, "base_chain_radius", 1), 0);
            return new ChainDamageParameters(
                baseRadius,
                ProgressionDataUtils.to_string_name(
                    DictStringName(parameters, "bonus_terrain_effect_id")
                ),
                Math.Max(DictInt(parameters, "wet_chain_radius", baseRadius), baseRadius),
                effectDef?.prevent_repeat_target ?? true
            );
        }
    }

    private WeakReference<BattleRuntimeModule> _runtimeRef;

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    private BattleRuntimeModule Runtime => _runtime;

    public void setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
    }

    public void dispose()
    {
        _runtime = null;
    }

    // ============================================================
    // 委托 _runtime 的薄包装
    // ============================================================

    public void _append_result_report_entry(BattleEventBatch batch, GDictionary result)
    {
        Runtime?._append_result_report_entry(batch, result);
    }

    public void _append_report_entry_to_batch(BattleEventBatch batch, GDictionary report_entry)
    {
        Runtime?._append_report_entry_to_batch(batch, report_entry);
    }

    public void mark_applied_statuses_for_turn_timing(
        BattleUnitState target_unit,
        GArray status_effect_ids
    )
    {
        Runtime?.mark_applied_statuses_for_turn_timing(target_unit, status_effect_ids ?? new GArray());
    }

    public void append_result_source_status_effects(
        BattleEventBatch batch,
        BattleUnitState source_unit,
        GDictionary result
    )
    {
        Runtime?.append_result_source_status_effects(batch, source_unit, result);
    }

    internal void append_result_source_status_effects(
        BattleEventBatch batch,
        BattleUnitState source_unit,
        AttackEffectResolutionResult result
    )
    {
        Runtime?.append_result_source_status_effects(batch, source_unit, result);
    }

    public void _record_action_issued(
        BattleUnitState unit_state,
        StringName command_type,
        int ap_cost = 0
    )
    {
        Runtime?._record_action_issued(unit_state, command_type, ap_cost);
    }

    public void _record_skill_attempt(BattleUnitState unit_state, StringName skill_id)
    {
        Runtime?._record_skill_attempt(unit_state, skill_id);
    }

    public void _record_effect_metrics(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        int damage,
        int healing,
        int kill_count
    )
    {
        Runtime?._record_effect_metrics(source_unit, target_unit, damage, healing, kill_count);
    }

    public void _record_unit_defeated(BattleUnitState unit_state)
    {
        Runtime?._record_unit_defeated(unit_state);
    }

    public void _apply_on_kill_gain_resources_effects(
        BattleUnitState source_unit,
        BattleUnitState defeated_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        BattleEventBatch batch
    )
    {
        Runtime?._apply_on_kill_gain_resources_effects(
            source_unit,
            defeated_unit,
            skill_def,
            effect_defs ?? new GCombatEffectArray(),
            batch
        );
    }

    public GDictionary _apply_unit_skill_special_effects(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        GCombatEffectArray effect_defs,
        BattleEventBatch batch,
        GDictionary forced_move_context = null
    )
    {
        return _apply_unit_skill_special_effects_result(
                active_unit,
                target_unit,
                skill_def,
                cast_variant,
                effect_defs,
                batch,
                forced_move_context
            )
            .ToDictionary();
    }

    public BattleSpecialSkillResult _apply_unit_skill_special_effects_result(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        GCombatEffectArray effect_defs,
        BattleEventBatch batch,
        GDictionary forced_move_context = null
    )
    {
        forced_move_context ??= new GDictionary();
        return Runtime?.ApplyUnitSkillSpecialEffectsResult(
                active_unit,
                target_unit,
                skill_def,
                cast_variant,
                effect_defs ?? new GCombatEffectArray(),
                batch,
                forced_move_context
            ) ?? BattleSpecialSkillResult.Empty();
    }

    public bool _is_doom_shift_skill(StringName skill_id)
    {
        return Runtime?._is_doom_shift_skill(skill_id) == true;
    }

    public bool _is_black_crown_seal_skill(StringName skill_id)
    {
        return Runtime?._is_black_crown_seal_skill(skill_id) == true;
    }

    public bool _is_crown_break_target_eligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        return Runtime?._is_crown_break_target_eligible(active_unit, target_unit) == true;
    }

    public bool _is_crown_break_skill(StringName skill_id)
    {
        return Runtime?._is_crown_break_skill(skill_id) == true;
    }

    public bool _is_doom_sentence_target_eligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        return Runtime?._is_doom_sentence_target_eligible(active_unit, target_unit) == true;
    }

    public bool _is_black_crown_seal_target_eligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        return Runtime?._is_black_crown_seal_target_eligible(active_unit, target_unit) == true;
    }

    public bool _is_doom_sentence_skill(StringName skill_id)
    {
        return Runtime?._is_doom_sentence_skill(skill_id) == true;
    }

    public void _record_vajra_body_mastery_from_incoming_damage(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GDictionary result,
        BattleEventBatch batch = null
    )
    {
        Runtime?._record_vajra_body_mastery_from_incoming_damage(
            source_unit,
            target_unit,
            skill_def,
            result,
            batch
        );
    }

    internal void RecordVajraBodyMasteryFromIncomingDamageTyped(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDef skillDef,
        AttackEffectResolutionResult result,
        BattleEventBatch batch = null
    )
    {
        Runtime?.RecordVajraBodyMasteryFromIncomingDamageTyped(
            sourceUnit,
            targetUnit,
            skillDef,
            result,
            batch
        );
    }

    public GDictionary _resolve_ground_spell_control_after_cost(
        BattleUnitState active_unit,
        SkillDef skill_def,
        int spent_mp,
        BattleEventBatch batch
    )
    {
        return _resolve_ground_spell_control_after_cost_result(
            active_unit,
            skill_def,
            spent_mp,
            batch
        ).ToDictionary();
    }

    public BattleSpellControlResult _resolve_ground_spell_control_after_cost_result(
        BattleUnitState active_unit,
        SkillDef skill_def,
        int spent_mp,
        BattleEventBatch batch
    )
    {
        return Runtime?._resolve_ground_spell_control_after_cost_result(
                active_unit,
                skill_def,
                spent_mp,
                batch
            ) ?? BattleSpellControlResult.None();
    }

    public GDictionary _resolve_unit_spell_control_after_cost(
        BattleUnitState active_unit,
        SkillDef skill_def,
        BattleEventBatch batch
    )
    {
        return _resolve_unit_spell_control_after_cost_result(
            active_unit,
            skill_def,
            batch
        ).ToDictionary();
    }

    public BattleSpellControlResult _resolve_unit_spell_control_after_cost_result(
        BattleUnitState active_unit,
        SkillDef skill_def,
        BattleEventBatch batch
    )
    {
        return Runtime?._resolve_unit_spell_control_after_cost_result(
            active_unit,
            skill_def,
            batch
        ) ?? BattleSpellControlResult.None();
    }

    public bool _apply_ground_precast_special_effects(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        GVector2IArray target_coords,
        BattleEventBatch batch
    )
    {
        return Runtime?._apply_ground_precast_special_effects(
            active_unit,
            skill_def,
            cast_variant,
            target_coords,
            batch
        ) == true;
    }

    public GVector2IArray _build_ground_effect_coords(
        SkillDef skill_def,
        GArray target_coords,
        Vector2I source_coord,
        BattleUnitState active_unit = null,
        CombatCastVariantDef cast_variant = null
    )
    {
        if (Runtime == null)
            return new GVector2IArray();
        return Runtime._build_ground_effect_coords(
            skill_def,
            target_coords,
            source_coord,
            active_unit,
            cast_variant
        );
    }

    public GCombatEffectArray _collect_ground_unit_effect_defs(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitState active_unit = null
    )
    {
        if (Runtime == null)
            return new GCombatEffectArray();
        return Runtime._collect_ground_unit_effect_defs(
            skill_def,
            cast_variant,
            active_unit
        );
    }

    public GCombatEffectArray _collect_ground_terrain_effect_defs(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitState active_unit = null
    )
    {
        if (Runtime == null)
            return new GCombatEffectArray();
        return Runtime._collect_ground_terrain_effect_defs(
            skill_def,
            cast_variant,
            active_unit
        );
    }

    public GStringNameArray _collect_ground_preview_unit_ids(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        GVector2IArray effect_coords
    )
    {
        if (Runtime == null)
            return new GStringNameArray();
        return Runtime._collect_ground_preview_unit_ids(
            source_unit,
            skill_def,
            effect_defs ?? new GCombatEffectArray(),
            effect_coords
        );
    }

    public GDictionary _apply_ground_unit_effects(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        GVector2IArray effect_coords,
        BattleEventBatch batch,
        GVector2IArray target_coords = null
    )
    {
        target_coords ??= new GVector2IArray();
        return _apply_ground_unit_effects_result(
            source_unit,
            skill_def,
            effect_defs ?? new GCombatEffectArray(),
            effect_coords,
            batch,
            target_coords
        ).ToDictionary();
    }

    public BattleGroundUnitEffectsResult _apply_ground_unit_effects_result(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        GVector2IArray effect_coords,
        BattleEventBatch batch,
        GVector2IArray target_coords = null
    )
    {
        target_coords ??= new GVector2IArray();
        if (Runtime == null)
            return new BattleGroundUnitEffectsResult(false, 0, 0, 0, 0);
        return Runtime._apply_ground_unit_effects_result(
            source_unit,
            skill_def,
            effect_defs ?? new GCombatEffectArray(),
            effect_coords,
            batch,
            target_coords
        );
    }

    public GDictionary _apply_ground_terrain_effects(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        GVector2IArray effect_coords,
        BattleEventBatch batch
    )
    {
        return _apply_ground_terrain_effects_result(
            source_unit,
            skill_def,
            effect_defs ?? new GCombatEffectArray(),
            effect_coords,
            batch
        ).ToDictionary();
    }

    public BattleGroundTerrainEffectsResult _apply_ground_terrain_effects_result(
        BattleUnitState source_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        GVector2IArray effect_coords,
        BattleEventBatch batch
    )
    {
        if (Runtime == null)
            return new BattleGroundTerrainEffectsResult(false);
        return Runtime._apply_ground_terrain_effects_result(
            source_unit,
            skill_def,
            effect_defs ?? new GCombatEffectArray(),
            effect_coords,
            batch
        );
    }

    public GDictionary _apply_unit_shield_effects(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        GDictionary shield_roll_context = null
    )
    {
        return _apply_unit_shield_effects_result(
                source_unit,
                target_unit,
                skill_def,
                effect_defs,
                shield_roll_context
            )
            .ToDictionary();
    }

    public BattleShieldApplyResult _apply_unit_shield_effects_result(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        GDictionary shield_roll_context = null
    )
    {
        shield_roll_context ??= new GDictionary();
        if (Runtime == null)
            return new BattleShieldApplyResult(false, 0, 0, -1, new StringName(""));
        return Runtime.ApplyUnitShieldEffectsResult(
            source_unit,
            target_unit,
            skill_def,
            effect_defs ?? new GCombatEffectArray(),
            shield_roll_context
        );
    }

    public void _grant_skill_mastery_if_needed(
        BattleUnitState active_unit,
        SkillDef skill_def,
        BattleEventBatch batch
    )
    {
        if (Runtime == null)
            return;
        Runtime._grant_skill_mastery_if_needed(active_unit, skill_def, batch);
    }

    public void _apply_skill_mastery_grant(
        BattleUnitState unit_state,
        GDictionary grant,
        BattleEventBatch batch
    )
    {
        if (Runtime == null)
            return;
        Runtime._apply_skill_mastery_grant(unit_state, grant, batch);
    }

    internal void ApplySkillMasteryGrantTyped(
        BattleUnitState unit_state,
        BattleSkillMasteryGrant grant,
        BattleEventBatch batch
    )
    {
        if (Runtime == null)
            return;
        Runtime.ApplySkillMasteryGrantTyped(unit_state, grant, batch);
    }

    public void _flush_last_stand_mastery_records(BattleEventBatch batch)
    {
        if (Runtime == null)
            return;
        Runtime._flush_last_stand_mastery_records(batch);
    }

    public GDictionary _validate_ground_skill_command(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleCommand command
    )
    {
        if (Runtime == null)
            return new GDictionary();
        return Runtime._validate_ground_skill_command(
            active_unit,
            skill_def,
            cast_variant,
            command
        );
    }

    public BattleGroundSkillValidationResult _validate_ground_skill_command_result(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleCommand command
    )
    {
        return Runtime?._validate_ground_skill_command_result(
            active_unit,
            skill_def,
            cast_variant,
            command
        ) ?? BattleGroundSkillValidationResult.Denied("地面技能目标无效。");
    }

    public string _get_ground_special_effect_validation_message(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        GVector2IArray target_coords
    )
    {
        return Runtime?._get_ground_special_effect_validation_message(
            active_unit,
            skill_def,
            cast_variant,
            target_coords
        ) ?? "";
    }

    public void _append_changed_coord(BattleEventBatch batch, Vector2I coord)
    {
        Runtime?._append_changed_coord(batch, coord);
    }

    public void _append_changed_unit_id(BattleEventBatch batch, StringName unit_id)
    {
        Runtime?._append_changed_unit_id(batch, unit_id);
    }

    public void _append_changed_unit_coords(BattleEventBatch batch, BattleUnitState unit_state)
    {
        Runtime?._append_changed_unit_coords(batch, unit_state);
    }

    public void _collect_defeated_unit_loot(
        BattleUnitState unit_state,
        BattleUnitState killer_unit = null
    )
    {
        Runtime?._collect_defeated_unit_loot(unit_state, killer_unit);
    }

    public void _clear_defeated_unit(BattleUnitState unit_state, BattleEventBatch batch = null)
    {
        Runtime?._clear_defeated_unit(unit_state, batch);
    }

    public GVector2IArray _sort_coords(GArray target_coords)
    {
        if (Runtime == null)
            return new GVector2IArray();
        return Runtime._sort_coords(target_coords);
    }

    public string _get_skill_command_block_reason(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        if (Runtime == null)
            return "";
        return Runtime._get_skill_command_block_reason(
            active_unit,
            skill_def,
            cast_variant
        );
    }

    public bool _consume_skill_costs(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant = null,
        BattleEventBatch batch = null
    )
    {
        return Runtime?._consume_skill_costs(
            active_unit,
            skill_def,
            cast_variant,
            batch
        ) == true;
    }

    public GDictionary _get_effective_skill_costs(
        BattleUnitState active_unit,
        SkillDef skill_def
    )
    {
        return Runtime?._get_effective_skill_costs(active_unit, skill_def)
            ?? new GDictionary();
    }

    public CombatSkillResourceCosts _get_effective_skill_resource_costs(
        BattleUnitState active_unit,
        SkillDef skill_def
    )
    {
        return Runtime?._get_effective_skill_resource_costs(active_unit, skill_def)
            ?? CombatSkillResourceCosts.Zero;
    }

    public int _get_effective_skill_range(BattleUnitState active_unit, SkillDef skill_def)
    {
        return Runtime?._get_effective_skill_range(active_unit, skill_def) ?? 0;
    }

    // ============================================================
    // 主流程
    // ============================================================

    public void _handle_skill_command(
        BattleUnitState active_unit,
        BattleCommand command,
        BattleEventBatch batch
    )
    {
        SkillDef skillDef = Runtime?.get_skill_def_typed(command.skill_id);
        if (skillDef == null || skillDef.combat_profile == null)
        {
            return;
        }
        CombatCastVariantDef unitCastVariant =
            _resolve_unit_cast_variant(skillDef, active_unit, command);
        CombatCastVariantDef groundCastVariant = _resolve_ground_cast_variant(
            skillDef,
            active_unit,
            command
        ) as CombatCastVariantDef;
        bool routesToUnitTargeting = _should_route_skill_command_to_unit_targeting(
            skillDef,
            command
        );
        string optionBlockReason = _get_skill_variant_command_block_reason(
            skillDef,
            active_unit,
            command,
            routesToUnitTargeting
        );
        if (!string.IsNullOrEmpty(optionBlockReason))
        {
            batch?.log_lines.Add(optionBlockReason);
            return;
        }
        CombatCastVariantDef commandCastVariant = _resolve_command_route_cast_variant(
            skillDef,
            active_unit,
            command,
            routesToUnitTargeting
        );
        CombatCastVariantDef unitExecutionCastVariant = routesToUnitTargeting
            ? commandCastVariant
            : unitCastVariant;
        string blockReason = _get_skill_command_block_reason(
            active_unit,
            skillDef,
            commandCastVariant
        );
        if (!string.IsNullOrEmpty(blockReason))
        {
            batch?.log_lines.Add(blockReason);
            return;
        }
        if (Runtime?._has_special_profile(skillDef, new StringName("meteor_swarm")) == true)
        {
            BattleSpecialProfileGateResult gateResult =
                Runtime._special_profile_gate != null
                    ? Runtime._special_profile_gate.can_execute_skill(
                        skillDef,
                        command,
                        active_unit,
                        Runtime._state
                    )
                    : null;
            if (gateResult == null || !gateResult.allowed)
            {
                Runtime._append_special_profile_gate_block(batch, gateResult);
                return;
            }
            Runtime._skill_mastery_service.clear();
            bool meteorApplied = _handle_meteor_swarm_skill_command(
                active_unit,
                command,
                skillDef,
                groundCastVariant,
                batch
            );
            if (meteorApplied)
            {
                _grant_skill_mastery_if_needed(active_unit, skillDef, batch);
            }
            Runtime._skill_mastery_service.clear();
            return;
        }

        _record_skill_attempt(active_unit, command?.skill_id ?? new StringName(""));
        Runtime?._skill_mastery_service.clear();
        bool applied = false;
        if (routesToUnitTargeting)
        {
            applied = _handle_unit_skill_command(
                active_unit,
                command,
                skillDef,
                unitExecutionCastVariant,
                batch
            );
        }
        else
        {
            if (groundCastVariant != null)
            {
                applied = _handle_ground_skill_command(
                    active_unit,
                    command,
                    skillDef,
                    groundCastVariant,
                    batch
                );
            }
            else
            {
                applied = _handle_unit_skill_command(active_unit, command, skillDef, null, batch);
            }
        }
        if (applied)
        {
            _grant_skill_mastery_if_needed(active_unit, skillDef, batch);
        }
        Runtime?._skill_mastery_service.clear();
    }

    public void _preview_skill_command(
        BattleUnitState active_unit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        AiTraceRecorder.enter("preview:skill.orchestrator");
        _preview_skill_command_impl(active_unit, command, preview);
        AiTraceRecorder.exit("preview:skill.orchestrator");
    }

    public void _preview_skill_command_impl(
        BattleUnitState active_unit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        SkillDef skillDef = Runtime?.get_skill_def_typed(command.skill_id);
        if (skillDef?.combat_profile == null)
        {
            preview.log_lines.Add("技能或目标无效。");
            return;
        }
        var runtime = _runtime as BattleRuntimeModule;
        if (runtime != null && runtime._has_special_profile(skillDef, new StringName("meteor_swarm")))
        {
            AiTraceRecorder.enter("preview:skill.meteor_gate");
            BattleSpecialProfileGateResult gateResult = runtime._special_profile_gate != null
                ? runtime._special_profile_gate.preview_skill(
                    skillDef,
                    command,
                    active_unit,
                    runtime._state
                )
                : null;
            AiTraceRecorder.exit("preview:skill.meteor_gate");
            preview.special_profile_gate_result = gateResult;
            if (gateResult == null || !gateResult.allowed)
            {
                if (
                    gateResult != null
                    && !string.IsNullOrEmpty(gateResult.player_message)
                )
                {
                    preview.log_lines.Add(gateResult.player_message);
                }
                else
                {
                    preview.log_lines.Add("该禁咒配置未通过校验，暂时无法施放。");
                }
                return;
            }
            string blockReason = _get_skill_command_block_reason(active_unit, skillDef, null);
            if (!string.IsNullOrEmpty(blockReason))
            {
                preview.log_lines.Add(blockReason);
                return;
            }
            if (runtime._meteor_swarm_resolver != null)
            {
                runtime._meteor_swarm_resolver.populate_preview(active_unit, command, skillDef, preview);
                return;
            }
            preview.allowed = false;
            preview.log_lines.Add("该禁咒结算尚未接入。");
            return;
        }
        AiTraceRecorder.enter("preview:skill.resolve_options");
        CombatCastVariantDef unitCastVariant =
            _resolve_unit_cast_variant(skillDef, active_unit, command) as CombatCastVariantDef;
        CombatCastVariantDef groundCastVariant = _resolve_ground_cast_variant(
            skillDef,
            active_unit,
            command
        ) as CombatCastVariantDef;
        bool routesToUnitTargeting = _should_route_skill_command_to_unit_targeting(
            skillDef,
            command
        );
        AiTraceRecorder.exit("preview:skill.resolve_options");
        AiTraceRecorder.enter("preview:skill.option_block");
        string optionBlockReason = _get_skill_variant_command_block_reason(
            skillDef,
            active_unit,
            command,
            routesToUnitTargeting
        );
        AiTraceRecorder.exit("preview:skill.option_block");
        if (!string.IsNullOrEmpty(optionBlockReason))
        {
            preview.log_lines.Add(optionBlockReason);
            return;
        }
        AiTraceRecorder.enter("preview:skill.route_option");
        CombatCastVariantDef unitExecutionCastVariant = routesToUnitTargeting
            ? _resolve_command_route_cast_variant(
                skillDef,
                active_unit,
                command,
                routesToUnitTargeting
            ) as CombatCastVariantDef
            : unitCastVariant;
        AiTraceRecorder.exit("preview:skill.route_option");

        if (routesToUnitTargeting)
        {
            _preview_unit_skill_command(
                active_unit,
                command,
                skillDef,
                unitExecutionCastVariant,
                preview
            );
            return;
        }
        if (groundCastVariant != null)
        {
            _preview_ground_skill_command(
                active_unit,
                command,
                skillDef,
                groundCastVariant,
                preview
            );
            return;
        }
        _preview_unit_skill_command(active_unit, command, skillDef, null, preview);
    }

    public bool _handle_meteor_swarm_skill_command(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleEventBatch batch
    )
    {
        BattleMeteorSwarmResolver meteorResolver = Runtime?._meteor_swarm_resolver;
        BattleSpecialProfileCommitAdapter commitAdapter = Runtime?._special_profile_commit_adapter;
        if (_runtime == null || meteorResolver == null || commitAdapter == null)
        {
            batch?.log_lines.Add("该禁咒结算尚未接入。");
            return false;
        }
        BattleGroundSkillValidationResult validation = _validate_ground_skill_command_result(
            active_unit,
            skill_def,
            cast_variant,
            command
        );
        if (!validation.Allowed)
        {
            batch?.log_lines.Add(
                string.IsNullOrEmpty(validation.Message) ? "技能或目标无效。" : validation.Message
            );
            return false;
        }
        GVector2IArray targetCoords = validation.TargetCoordsArray();
        if (targetCoords.Count == 0)
        {
            batch?.log_lines.Add("技能或目标无效。");
            return false;
        }

        int mpBeforeCost = active_unit?.current_mp ?? 0;
        if (!_consume_skill_costs(active_unit, skill_def, cast_variant, batch))
        {
            return false;
        }
        _record_skill_attempt(active_unit, command?.skill_id ?? new StringName(""));
        int spentMp = Math.Max(mpBeforeCost - (active_unit?.current_mp ?? 0), 0);
        CombatSkillResourceCosts costs = _get_effective_skill_resource_costs(
            active_unit,
            skill_def
        );
        _record_action_issued(
            active_unit,
            BattleCommand.TYPE_SKILL(),
            costs.ApCost
        );
        _append_changed_unit_id(batch, active_unit?.unit_id ?? new StringName(""));

        BattleSpellControlResult spellControlContext = _resolve_ground_spell_control_after_cost_result(
            active_unit,
            skill_def,
            spentMp,
            batch
        );
        if (spellControlContext.SkipEffects)
        {
            return false;
        }

        BattleGroundBacklashTargetResult driftContext =
            Runtime
                ?._magic_backlash_resolver.build_ground_backlash_target_coords_result(
                    skill_def as SkillDef,
                    targetCoords,
                    Runtime.get_state(),
                    Runtime.get_grid_service(),
                    spellControlContext
                ) ?? BattleGroundBacklashTargetResult.None(ToVector2IList(targetCoords));
        GVector2IArray finalTargetCoords = driftContext.TargetCoordsArray();
        if (finalTargetCoords.Count == 0)
        {
            finalTargetCoords = (GVector2IArray)targetCoords.Duplicate();
        }
        if (driftContext.BacklashTriggered)
        {
            Runtime?._magic_backlash_resolver.append_ground_backlash_log(
                active_unit,
                skill_def as SkillDef,
                driftContext,
                batch as BattleEventBatch
            );
        }

        MeteorSwarmCastContext context = meteorResolver.BuildCastContextTyped(
            active_unit,
            command,
            skill_def,
            cast_variant,
            targetCoords[0],
            finalTargetCoords[0],
            spellControlContext.ToDictionary(),
            driftContext.ToDictionary()
        );
        MeteorSwarmTargetPlan plan = meteorResolver.BuildTargetPlanTyped(context);
        MeteorSwarmCommitResult result = meteorResolver.ResolveTyped(plan);
        return commitAdapter.commit_meteor_swarm_result(result, batch);
    }

    public void _preview_unit_skill_command(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattlePreview preview
    )
    {
        AiTraceRecorder.enter("preview:unit_skill");
        _preview_unit_skill_command_impl(active_unit, command, skill_def, cast_variant, preview);
        AiTraceRecorder.exit("preview:unit_skill");
    }

    public void _preview_unit_skill_command_impl(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattlePreview preview
    )
    {
        if (preview == null)
        {
            return;
        }
        string blockReason = _get_skill_command_block_reason(active_unit, skill_def, cast_variant);
        if (!string.IsNullOrEmpty(blockReason))
        {
            preview.log_lines.Add(blockReason);
            return;
        }

        AiTraceRecorder.enter("preview:unit_skill.validate_targets");
        BattleUnitSkillValidationResult validation = _validate_unit_skill_targets_result(
            active_unit,
            command,
            skill_def,
            cast_variant
        );
        AiTraceRecorder.exit("preview:unit_skill.validate_targets");
        AiTraceRecorder.enter("preview:unit_skill.copy_validation");
        preview.allowed = validation.Allowed;
        preview.target_unit_ids.Clear();
        foreach (StringName targetUnitId in validation.TargetUnitIds)
        {
            preview.target_unit_ids.Add(targetUnitId);
        }
        preview.random_chain_candidate_unit_ids.Clear();
        foreach (StringName candidateUnitId in validation.RandomChainCandidateUnitIds)
        {
            preview.random_chain_candidate_unit_ids.Add(candidateUnitId);
        }
        preview.target_coords.Clear();
        foreach (Vector2I previewCoord in validation.PreviewCoords)
        {
            preview.target_coords.Add(previewCoord);
        }
        AiTraceRecorder.exit("preview:unit_skill.copy_validation");
        if (preview.allowed)
        {
            AiTraceRecorder.enter("preview:unit_skill.hit_preview");
            preview.hit_preview = _build_unit_skill_hit_preview(
                active_unit,
                validation.TargetUnits,
                skill_def,
                cast_variant
            );
            AiTraceRecorder.exit("preview:unit_skill.hit_preview");
            AiTraceRecorder.enter("preview:unit_skill.damage_preview");
            preview.damage_preview = _build_unit_skill_damage_preview(
                active_unit,
                skill_def,
                cast_variant
            );
            AiTraceRecorder.exit("preview:unit_skill.damage_preview");
            AiTraceRecorder.enter("preview:unit_skill.log_lines");
            string skillLabel = _format_skill_variant_label(skill_def, cast_variant);
            if (validation.TargetUnits.Count == 1)
            {
                BattleUnitState targetUnit = validation.TargetUnits[0];
                if (targetUnit != null)
                {
                    preview.log_lines.Add(
                        $"{active_unit.display_name} 可对 {targetUnit.display_name} 使用 {skillLabel}。"
                    );
                    if (preview.hit_preview != null && !preview.hit_preview.IsEmpty)
                    {
                        preview.log_lines.Add(preview.hit_preview.SummaryText);
                    }
                    _append_damage_preview_line(preview);
                    AiTraceRecorder.exit("preview:unit_skill.log_lines");
                    return;
                }
            }
            if (
                skill_def?.combat_profile != null
                && skill_def.combat_profile.target_selection_mode == "random_chain"
            )
            {
                preview.log_lines.Add(
                    $"{active_unit.display_name} 可用 {skillLabel} 从 {preview.random_chain_candidate_unit_ids.Count} 个候选单位中随机连击。"
                );
                _append_damage_preview_line(preview);
                AiTraceRecorder.exit("preview:unit_skill.log_lines");
                return;
            }
            preview.log_lines.Add(
                $"{active_unit.display_name} 可对 {preview.target_unit_ids.Count} 个单位使用 {skillLabel}。"
            );
            if (preview.hit_preview != null && !preview.hit_preview.IsEmpty)
            {
                preview.log_lines.Add(preview.hit_preview.SummaryText);
            }
            _append_damage_preview_line(preview);
            AiTraceRecorder.exit("preview:unit_skill.log_lines");
            return;
        }
        preview.log_lines.Add(
            string.IsNullOrEmpty(validation.Message) ? "技能或目标无效。" : validation.Message
        );
    }

    public void _preview_ground_skill_command(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattlePreview preview
    )
    {
        AiTraceRecorder.enter("preview:ground_skill");
        _preview_ground_skill_command_impl(active_unit, command, skill_def, cast_variant, preview);
        AiTraceRecorder.exit("preview:ground_skill");
    }

    public void _preview_ground_skill_command_impl(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattlePreview preview
    )
    {
        if (preview == null)
        {
            return;
        }
        string blockReason = _get_skill_command_block_reason(active_unit, skill_def, cast_variant);
        if (!string.IsNullOrEmpty(blockReason))
        {
            preview.log_lines.Add(blockReason);
            return;
        }
        AiTraceRecorder.enter("preview:ground_skill.validate");
        BattleGroundSkillValidationResult validation = _validate_ground_skill_command_result(
            active_unit,
            skill_def,
            cast_variant,
            command
        );
        AiTraceRecorder.exit("preview:ground_skill.validate");
        AiTraceRecorder.enter("preview:ground_skill.preview_coords");
        preview.target_coords.Clear();
        GVector2IArray previewCoords;
        if (validation.HasPreviewCoords)
        {
            previewCoords = validation.PreviewCoordsArray();
        }
        else
        {
            previewCoords = _build_ground_effect_coords(
                skill_def,
                ToUntypedArray(validation.TargetCoordsArray()),
                active_unit != null
                    ? active_unit.coord
                    : new Vector2I(-1, -1),
                active_unit,
                cast_variant
            );
        }
        preview.resolved_anchor_coord = validation.ResolvedAnchorCoord;
        bool allowed = validation.Allowed;
        if (allowed && Runtime?._charge_resolver != null)
        {
            CombatEffectDef pathStepAoeEffect = Runtime._charge_resolver
                .get_charge_path_step_aoe_effect_def(cast_variant, skill_def, active_unit);
            if (pathStepAoeEffect != null)
            {
                previewCoords = Runtime._charge_resolver.build_charge_step_aoe_preview_coords(
                    active_unit,
                    validation.Direction,
                    validation.Distance,
                    pathStepAoeEffect
                );
            }
        }
        foreach (Vector2I targetCoord in previewCoords)
        {
            preview.target_coords.Add(targetCoord);
        }
        AiTraceRecorder.exit("preview:ground_skill.preview_coords");
        AiTraceRecorder.enter("preview:ground_skill.collect_unit_ids");
        preview.target_unit_ids = _collect_ground_preview_unit_ids(
            active_unit,
            skill_def,
            _collect_ground_unit_effect_defs(skill_def, cast_variant, active_unit),
            preview.target_coords
        );
        AiTraceRecorder.exit("preview:ground_skill.collect_unit_ids");
        if (allowed && Runtime?._charge_resolver != null)
        {
            AiTraceRecorder.enter("preview:ground_skill.path_step_aoe");
            CombatEffectDef pathStepAoeEffect = Runtime._charge_resolver
                .get_charge_path_step_aoe_effect_def(cast_variant, skill_def, active_unit);
            if (pathStepAoeEffect != null)
            {
                StringName pathStepTargetFilter = _resolve_effect_target_filter(
                    skill_def,
                    pathStepAoeEffect
                );
                foreach (
                    BattleUnitState targetUnit in _collect_units_in_coords(
                        preview.target_coords
                    )
                )
                {
                    if (!_is_unit_valid_for_effect(active_unit, targetUnit, pathStepTargetFilter))
                    {
                        continue;
                    }
                    if (preview.target_unit_ids.Contains(targetUnit.unit_id))
                    {
                        continue;
                    }
                    preview.target_unit_ids.Add(targetUnit.unit_id);
                }
            }
            AiTraceRecorder.exit("preview:ground_skill.path_step_aoe");
        }
        preview.allowed = allowed;
        AiTraceRecorder.enter("preview:ground_skill.log_lines");
        if (preview.allowed)
        {
            preview.log_lines.Add(
                $"{active_unit.display_name} 可使用 {_format_skill_variant_label(skill_def, cast_variant)}，预计影响 {preview.target_coords.Count} 个地格、{preview.target_unit_ids.Count} 个单位。"
            );
        }
        else
        {
            preview.log_lines.Add(
                string.IsNullOrEmpty(validation.Message)
                    ? "地面技能目标无效。"
                    : validation.Message
            );
        }
        AiTraceRecorder.exit("preview:ground_skill.log_lines");
    }

    public AttackPreviewData _build_unit_skill_hit_preview(
        BattleUnitState active_unit,
        GArray target_units,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        return BuildUnitSkillHitPreview(
            active_unit,
            ToUnitList(target_units),
            skill_def,
            cast_variant
        );
    }

    private AttackPreviewData _build_unit_skill_hit_preview(
        BattleUnitState active_unit,
        IReadOnlyList<BattleUnitState> target_units,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        return BuildUnitSkillHitPreview(active_unit, target_units, skill_def, cast_variant);
    }

    private AttackPreviewData BuildUnitSkillHitPreview(
        BattleUnitState active_unit,
        IReadOnlyList<BattleUnitState> target_units,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        if (active_unit == null || skill_def == null || target_units == null || target_units.Count != 1)
        {
            return null;
        }
        BattleUnitState targetUnit = target_units[0];
        if (targetUnit == null)
        {
            return null;
        }
        GCombatEffectArray effectDefs = _collect_unit_skill_effect_defs(
            skill_def,
            cast_variant,
            active_unit
        );
        BattleRepeatAttackResolver repeatAttackResolver = Runtime?._repeat_attack_resolver;
        CombatEffectDef repeatAttackEffect =
            repeatAttackResolver?.get_repeat_attack_effect_def(effectDefs);
        BattleAttackCheckPolicyService attackPolicy = Runtime?.get_attack_check_policy_service();
        BattleSkillResolutionRules skillResolutionRules = Runtime?._skill_resolution_rules;
        if (attackPolicy == null || skillResolutionRules == null)
        {
            return null;
        }
        if (repeatAttackEffect == null)
        {
            if (
                !skillResolutionRules.should_resolve_unit_skill_as_fate_attack(
                        active_unit,
                        targetUnit,
                        skill_def,
                        effectDefs
                    )
            )
            {
                return null;
            }
            BattleAttackCheckPolicyContext attackContext = attackPolicy.build_attack_context(
                Runtime?._state,
                active_unit,
                targetUnit,
                skill_def,
                new StringName("skill_attack_preview"),
                new StringName("hud_preview"),
                skillResolutionRules.is_force_hit_no_crit_skill(skill_def)
            );
            return attackPolicy.build_attack_preview(attackContext);
        }
        List<BattleRepeatAttackStageSpec> stageSpecs =
            BattleRepeatAttackResolver.build_stage_specs_from_repeat_attack_effect(
            active_unit,
            skill_def,
            repeatAttackEffect,
            -1,
            true
        );
        BattleAttackCheckPolicyContext repeatContext = attackPolicy.build_repeat_attack_stage_context(
            Runtime?._state,
            active_unit,
            targetUnit,
            skill_def,
            default,
            new StringName("repeat_attack_preview"),
            new StringName("hud_preview")
        );
        return attackPolicy.build_repeat_attack_preview(repeatContext, stageSpecs);
    }

    public GDictionary _build_unit_skill_damage_preview(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        if (active_unit == null || skill_def == null)
        {
            return new GDictionary();
        }
        GCombatEffectArray effectDefs = _collect_unit_skill_effect_defs(
            skill_def,
            cast_variant,
            active_unit
        );
        return BattleDamagePreviewRangeService.build_skill_damage_preview(
            active_unit,
            ToUntypedArray(effectDefs)
        );
    }

    public void _append_damage_preview_line(BattlePreview preview)
    {
        GDictionary damagePreview = preview?.damage_preview;
        if (preview == null || damagePreview == null || damagePreview.Count == 0)
        {
            return;
        }
        string damagePreviewText = DictString(damagePreview, "summary_text");
        if (string.IsNullOrEmpty(damagePreviewText))
        {
            return;
        }
        preview.log_lines.Add(damagePreviewText);
    }

    public GDictionary summarize_damage_result(GDictionary result)
    {
        return Runtime?._report_formatter.summarize_damage_result(result) ?? new GDictionary();
    }

    public string build_damage_absorb_reason_text(GDictionary summary)
    {
        return Runtime?._report_formatter.build_damage_absorb_reason_text(summary) ?? "";
    }

    public void append_damage_result_log_lines(
        BattleEventBatch batch,
        string subject_label,
        string target_display_name,
        GDictionary result
    )
    {
        Runtime?._report_formatter.append_damage_result_log_lines(
            batch,
            subject_label,
            target_display_name,
            result
        );
    }

    internal void append_damage_result_log_lines(
        BattleEventBatch batch,
        string subject_label,
        string target_display_name,
        AttackEffectResolutionResult result
    )
    {
        Runtime?._report_formatter.append_damage_result_log_lines(
            batch,
            subject_label,
            target_display_name,
            result
        );
    }

    public GStringArray _build_unit_skill_resolution_preview_lines(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        var lines = new GStringArray();
        if (active_unit == null || target_unit == null || skill_def == null)
        {
            return lines;
        }
        GDictionary damagePreview = _build_unit_skill_damage_preview(
            active_unit,
            skill_def,
            cast_variant
        );
        string damagePreviewText = DictString(damagePreview, "summary_text");
        if (!string.IsNullOrEmpty(damagePreviewText))
        {
            lines.Add(damagePreviewText);
        }
        return lines;
    }

    public string _build_skill_log_subject_label(
        BattleUnitState source_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant = null
    )
    {
        string actorLabel =
            source_unit != null
            && !string.IsNullOrEmpty(source_unit.display_name)
                ? source_unit.display_name
                : "未知单位";
        string skillLabel = _format_skill_variant_label(skill_def, cast_variant);
        if (string.IsNullOrEmpty(skillLabel) && skill_def != null)
        {
            skillLabel = skill_def.display_name;
        }
        if (string.IsNullOrEmpty(skillLabel))
        {
            skillLabel = "技能";
        }
        return $"{actorLabel} 使用 {skillLabel}";
    }

    public bool _handle_unit_skill_command(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleEventBatch batch
    )
    {
        BattleUnitSkillValidationResult validation = _validate_unit_skill_targets_result(
            active_unit,
            command,
            skill_def,
            cast_variant
        );
        if (!validation.Allowed)
        {
            return false;
        }

        CombatSkillDef combatProfile = skill_def?.combat_profile;
        bool isRandomChain =
            combatProfile != null && combatProfile.target_selection_mode == "random_chain";
        if (validation.TargetUnits.Count == 0 && !isRandomChain)
        {
            return false;
        }

        if (!_consume_skill_costs(active_unit, skill_def, cast_variant, batch))
        {
            return false;
        }
        CombatSkillResourceCosts costs = _get_effective_skill_resource_costs(
            active_unit,
            skill_def
        );
        _record_action_issued(
            active_unit,
            BattleCommand.TYPE_SKILL(),
            costs.ApCost
        );
        _append_changed_unit_id(batch, active_unit.unit_id);

        BattleSpellControlResult spellControlContext = _resolve_unit_spell_control_after_cost_result(
            active_unit,
            skill_def,
            batch
        );
        if (spellControlContext.SkipEffects)
        {
            return true;
        }

        bool applied = false;
        GCombatEffectArray effectDefs = _collect_unit_skill_effect_defs(
            skill_def,
            cast_variant,
            active_unit
        );
        BattleRepeatAttackResolver repeatAttackResolver = Runtime?._repeat_attack_resolver;
        CombatEffectDef repeatAttackEffect = repeatAttackResolver?.get_repeat_attack_effect_def(
            effectDefs
        );
        if (isRandomChain)
        {
            return _handle_random_chain_unit_skill_command(
                active_unit,
                skill_def,
                cast_variant,
                batch,
                effectDefs,
                repeatAttackEffect,
                spellControlContext
            );
        }

        foreach (BattleUnitState targetUnit in validation.TargetUnits)
        {
            if (targetUnit == null)
            {
                continue;
            }
            if (repeatAttackEffect != null)
            {
                if (
                    repeatAttackResolver != null
                    && repeatAttackResolver.apply_repeat_attack_skill_result(
                        active_unit,
                        targetUnit,
                        skill_def,
                        effectDefs,
                        repeatAttackEffect,
                        batch
                    )
                )
                {
                    applied = true;
                }
                continue;
            }
            if (
                _apply_unit_skill_result(
                    active_unit,
                    targetUnit,
                    skill_def,
                    cast_variant,
                    effectDefs,
                    batch,
                    spellControlContext
                )
            )
            {
                applied = true;
            }
        }
        return applied;
    }

    public bool _handle_random_chain_unit_skill_command(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleEventBatch batch,
        GCombatEffectArray effect_defs,
        CombatEffectDef repeat_attack_effect,
        BattleSpellControlResult spell_control_context
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        int maxHitsPerTarget = Math.Max(combatProfile?.max_hits_per_target ?? 0, 1);
        var chainHitCounts = new Dictionary<StringName, int>();
        bool applied = false;
        int attemptCount = 0;
        int maxAttempts = Math.Max(
            (Runtime?._state?.units.Count ?? 0) * maxHitsPerTarget,
            1
        );
        string skillLabel = _format_skill_variant_label(skill_def, cast_variant);
        BattleRepeatAttackResolver repeatAttackResolver = Runtime?._repeat_attack_resolver;
        while (attemptCount < maxAttempts)
        {
            GArray chainPool = _build_random_chain_target_pool(
                active_unit,
                skill_def,
                chainHitCounts,
                maxHitsPerTarget
            );
            if (chainPool.Count == 0)
            {
                break;
            }
            _shuffle_random_chain_pool(chainPool);
            var targetUnit = chainPool[0].AsGodotObject() as BattleUnitState;
            if (targetUnit == null)
            {
                break;
            }
            batch?.log_lines.Add(
                $"{active_unit.display_name} 的{skillLabel}锁定了 {targetUnit.display_name}。"
            );
            StringName targetId = targetUnit.unit_id;
            chainHitCounts.TryGetValue(targetId, out int targetHitCount);
            chainHitCounts[targetId] = targetHitCount + 1;
            attemptCount += 1;
            bool stageApplied;
            if (repeat_attack_effect != null)
            {
                stageApplied =
                    repeatAttackResolver != null
                    && repeatAttackResolver.apply_repeat_attack_skill_result(
                        active_unit,
                        targetUnit,
                        skill_def,
                        effect_defs,
                        repeat_attack_effect,
                        batch
                    );
            }
            else
            {
                stageApplied = _apply_unit_skill_result(
                    active_unit,
                    targetUnit,
                    skill_def,
                    cast_variant,
                    effect_defs,
                    batch,
                    spell_control_context
                );
            }
            if (stageApplied)
            {
                applied = true;
            }
            else
            {
                break;
            }
        }
        if (attemptCount > 0)
        {
            batch?.log_lines.Add(
                $"{active_unit.display_name} 的{skillLabel}执行了 {attemptCount} 次攻击链判定。"
            );
        }
        return applied;
    }

    public GArray _build_random_chain_target_pool(
        BattleUnitState active_unit,
        SkillDef skill_def,
        IReadOnlyDictionary<StringName, int> chain_hit_counts,
        int max_hits_per_target
    )
    {
        var chainPool = new GArray();
        BattleState state = RtState();
        if (state == null)
        {
            return chainPool;
        }
        foreach (var unitValue in state.units.Values)
        {
            var candidate = unitValue.AsGodotObject() as BattleUnitState;
            if (
                candidate == null
                || candidate == active_unit
                || !candidate.is_alive
            )
            {
                continue;
            }
            StringName candidateId = ProgressionDataUtils.to_string_name(
                candidate.unit_id
            );
            if (
                StringNameIsEmpty(candidateId)
                || (
                    chain_hit_counts != null
                    && chain_hit_counts.TryGetValue(candidateId, out int hitCount)
                    && hitCount >= max_hits_per_target
                )
            )
            {
                continue;
            }
            if (!_can_skill_target_unit(active_unit, candidate, skill_def, false))
            {
                continue;
            }
            chainPool.Add(candidate);
        }
        return chainPool;
    }

    public void _shuffle_random_chain_pool(GArray chain_pool)
    {
        if (chain_pool.Count <= 1)
        {
            return;
        }
        for (int index = chain_pool.Count - 1; index > 0; index--)
        {
            int swapIndex = TrueRandomSeedService.randi_range(0, index);
            if (swapIndex == index)
            {
                continue;
            }
            var temp = chain_pool[index];
            chain_pool[index] = chain_pool[swapIndex];
            chain_pool[swapIndex] = temp;
        }
    }

    public bool _handle_ground_skill_command(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleEventBatch batch
    )
    {
        BattleGroundSkillValidationResult validation = _validate_ground_skill_command_result(
            active_unit,
            skill_def,
            cast_variant,
            command
        );
        if (!validation.Allowed)
        {
            return false;
        }

        var targetCoords = validation.TargetCoordsArray();
        string precastValidationMessage = _get_ground_special_effect_validation_message(
            active_unit,
            skill_def,
            cast_variant,
            targetCoords
        );
        if (!string.IsNullOrEmpty(precastValidationMessage))
        {
            batch?.log_lines.Add(precastValidationMessage);
            return false;
        }

        int mpBeforeCost = active_unit?.current_mp ?? 0;
        if (!_consume_skill_costs(active_unit, skill_def, null, batch))
        {
            return false;
        }
        int spentMp = Math.Max(mpBeforeCost - (active_unit?.current_mp ?? 0), 0);
        CombatSkillResourceCosts costs = _get_effective_skill_resource_costs(
            active_unit,
            skill_def
        );
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        _record_action_issued(
            active_unit,
            BattleCommand.TYPE_SKILL(),
            costs.ApCost
        );
        _append_changed_unit_id(batch, active_unit?.unit_id ?? new StringName(""));
        BattleChargeResolver chargeResolver = Runtime?._charge_resolver;
        if (chargeResolver != null && chargeResolver.is_charge_option(cast_variant))
        {
            return chargeResolver.handle_charge_skill_command_result(
                active_unit,
                skill_def,
                cast_variant,
                validation,
                batch
            );
        }
        BattleSpellControlResult spellControlContext = _resolve_ground_spell_control_after_cost_result(
            active_unit,
            skill_def,
            spentMp,
            batch
        );
        if (spellControlContext.SkipEffects)
        {
            return false;
        }
        if (
            !_apply_ground_precast_special_effects(
                active_unit,
                skill_def,
                cast_variant,
                targetCoords,
                batch
            )
        )
        {
            return false;
        }

        BattleGroundBacklashTargetResult driftContext =
            Runtime
                ?._magic_backlash_resolver.build_ground_backlash_target_coords_result(
                    skill_def as SkillDef,
                    targetCoords,
                    Runtime.get_state(),
                    Runtime.get_grid_service(),
                    spellControlContext
                ) ?? BattleGroundBacklashTargetResult.None(ToVector2IList(targetCoords));
        if (driftContext.BacklashTriggered)
        {
            GVector2IArray driftTargetCoords = driftContext.TargetCoordsArray();
            if (driftTargetCoords.Count != 0)
            {
                targetCoords = driftTargetCoords;
            }
            Runtime?._magic_backlash_resolver.append_ground_backlash_log(
                active_unit,
                skill_def as SkillDef,
                driftContext,
                batch as BattleEventBatch
            );
        }
        GVector2IArray effectCoords = _build_ground_effect_coords(
                skill_def,
                ToUntypedArray(targetCoords),
                active_unit != null
                ? active_unit.coord
                : new Vector2I(-1, -1),
            active_unit,
            cast_variant
        );
        BattleGroundUnitEffectsResult unitResult = _apply_ground_unit_effects_result(
            active_unit,
            skill_def,
            _collect_ground_unit_effect_defs(skill_def, cast_variant, active_unit),
            effectCoords,
            batch,
            targetCoords
        );
        BattleGroundTerrainEffectsResult terrainResult = _apply_ground_terrain_effects_result(
            active_unit,
            skill_def,
            _collect_ground_terrain_effect_defs(skill_def, cast_variant, active_unit),
            effectCoords,
            batch
        );
        bool applied = unitResult.Applied || terrainResult.Applied;

        if (applied)
        {
            batch?.log_lines.Add(
                $"{active_unit.display_name} 使用 {_format_skill_variant_label(skill_def, cast_variant)}，影响了 {effectCoords.Count} 个地格、{unitResult.AffectedUnitCount} 个单位。"
            );
        }
        return applied;
    }

    public bool _should_route_skill_command_to_unit_targeting(
        SkillDef skill_def,
        BattleCommand command
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        bool allowRepeat =
            combatProfile != null && combatProfile.allow_repeat_target;
        return Runtime?._skill_resolution_rules?.should_route_skill_command_to_unit_targeting(
            skill_def,
            _normalize_target_unit_ids(command, allowRepeat)
        ) == true;
    }

    public GDictionary _validate_unit_skill_targets(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant = null
    )
    {
        return _validate_unit_skill_targets_result(
            active_unit,
            command,
            skill_def,
            cast_variant
        ).ToDictionary();
    }

    public BattleUnitSkillValidationResult _validate_unit_skill_targets_result(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant = null
    )
    {
        BattleState state = RtState();
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (
            state == null
            || active_unit == null
            || command == null
            || skill_def == null
            || combatProfile == null
        )
        {
            return BattleUnitSkillValidationResult.Denied("技能或目标无效。");
        }

        bool allowRepeat = combatProfile.allow_repeat_target;
        GStringNameArray targetUnitIds = _normalize_target_unit_ids(command, allowRepeat);
        int skillLevel = _get_unit_skill_level(
            active_unit,
            skill_def.skill_id
        );
        int minTargetCount = 1;
        int maxTargetCount = 1;
        if (_is_multi_unit_skill(skill_def))
        {
            minTargetCount = Math.Max(combatProfile.min_target_count, 1);
            maxTargetCount = Math.Max(
                combatProfile.get_effective_max_target_count(skillLevel),
                minTargetCount
            );
        }
        bool isRandomChain =
            combatProfile.target_selection_mode == "random_chain";
        if (targetUnitIds.Count == 0 && !isRandomChain)
        {
            return BattleUnitSkillValidationResult.Denied("技能或目标无效。");
        }
        if (isRandomChain)
        {
            int maxHitsPerTarget = Math.Max(
                combatProfile.max_hits_per_target,
                1
            );
            GArray randomChainPool = _build_random_chain_target_pool(
                active_unit,
                skill_def,
                new Dictionary<StringName, int>(),
                maxHitsPerTarget
            );
            if (randomChainPool.Count == 0)
            {
                return BattleUnitSkillValidationResult.Denied("没有可用的随机连击目标。");
            }
            var candidateUnitIds = new List<StringName>();
            foreach (var candidateValue in randomChainPool)
            {
                var candidate = candidateValue.AsGodotObject() as BattleUnitState;
                if (candidate != null)
                {
                    candidateUnitIds.Add(candidate.unit_id);
                }
            }
            return BattleUnitSkillValidationResult.AllowedResult(
                System.Array.Empty<StringName>(),
                System.Array.Empty<BattleUnitState>(),
                candidateUnitIds
            );
        }
        if (targetUnitIds.Count < minTargetCount)
        {
            return BattleUnitSkillValidationResult.Denied($"至少需要选择 {minTargetCount} 个单位目标。");
        }
        if (targetUnitIds.Count > maxTargetCount)
        {
            return BattleUnitSkillValidationResult.Denied($"最多只能选择 {maxTargetCount} 个单位目标。");
        }
        if (!_is_multi_unit_skill(skill_def) && targetUnitIds.Count != 1)
        {
            return BattleUnitSkillValidationResult.Denied("当前技能只允许选择 1 个单位目标。");
        }
        if (combatProfile.selection_order_mode != "manual")
        {
            targetUnitIds = _sort_target_unit_ids_for_execution(targetUnitIds);
        }

        var targetUnits = new List<BattleUnitState>();
        foreach (StringName targetUnitId in targetUnitIds)
        {
            var targetUnit =
                state.units.GetValueOrDefault(targetUnitId).AsGodotObject() as BattleUnitState;
            string specialValidationMessage = _get_unit_skill_target_validation_message(
                active_unit,
                targetUnit,
                skill_def,
                cast_variant
            );
            if (!string.IsNullOrEmpty(specialValidationMessage))
            {
                return BattleUnitSkillValidationResult.Denied(specialValidationMessage);
            }
            if (
                targetUnit == null
                || !_can_skill_target_unit(active_unit, targetUnit, skill_def as SkillDef, true, cast_variant as CombatCastVariantDef)
            )
            {
                return BattleUnitSkillValidationResult.Denied("技能目标超出范围或不满足筛选条件。");
            }
            targetUnits.Add(targetUnit);
        }

        var emptyTargetCoords = new GVector2IArray();
        BattleTargetCollectionResult collectedTargetCoords =
            Runtime?._target_collection_service.CollectCombatProfileTargetCoords(
                state,
                Runtime.get_grid_service(),
                active_unit != null ? active_unit.coord : new Vector2I(-1, -1),
                combatProfile,
                emptyTargetCoords,
                active_unit,
                targetUnits,
                skillLevel
            ) ?? BattleTargetCollectionResult.UnhandledResult(emptyTargetCoords);
        GVector2IArray previewCoords = _sort_coords(ToUntypedArray(collectedTargetCoords.ToGodotCoords()));
        return BattleUnitSkillValidationResult.AllowedResult(
            ToStringNameList(targetUnitIds),
            targetUnits,
            null,
            ToVector2IList(previewCoords)
        );
    }

    public GStringNameArray _normalize_target_unit_ids(
        BattleCommand command,
        bool allow_repeat = false
    )
    {
        var targetUnitIds = new GStringNameArray();
        if (command == null)
        {
            return targetUnitIds;
        }
        var seenIds = new HashSet<StringName>();
        StringName singleTargetId = ProgressionDataUtils.to_string_name(
            command.target_unit_id
        );
        if (!StringNameIsEmpty(singleTargetId))
        {
            seenIds.Add(singleTargetId);
            targetUnitIds.Add(singleTargetId);
        }
        foreach (StringName targetUnitIdValue in command.target_unit_ids)
        {
            StringName targetUnitId = ProgressionDataUtils.to_string_name(targetUnitIdValue);
            if (
                StringNameIsEmpty(targetUnitId)
                || (!allow_repeat && seenIds.Contains(targetUnitId))
            )
            {
                continue;
            }
            seenIds.Add(targetUnitId);
            targetUnitIds.Add(targetUnitId);
        }
        return targetUnitIds;
    }

    public GStringNameArray _sort_target_unit_ids_for_execution(GStringNameArray target_unit_ids)
    {
        BattleState state = RtState();
        if (state == null)
        {
            return (GStringNameArray)target_unit_ids.Duplicate();
        }
        GDictionary units = state.units;
        var ids = new List<StringName>();
        foreach (StringName id in target_unit_ids)
        {
            ids.Add(id);
        }
        ids.Sort(
            (a, b) =>
            {
                state.TryGetUnitTyped(a, out BattleUnitState unitA);
                state.TryGetUnitTyped(b, out BattleUnitState unitB);
                if (unitA == null || unitB == null)
                {
                    return string.CompareOrdinal(a.ToString(), b.ToString());
                }
                Vector2I ca = unitA.coord;
                Vector2I cb = unitB.coord;
                if (ca.Y != cb.Y)
                    return ca.Y.CompareTo(cb.Y);
                if (ca.X != cb.X)
                    return ca.X.CompareTo(cb.X);
                return string.CompareOrdinal(a.ToString(), b.ToString());
            }
        );
        var sorted = new GStringNameArray();
        foreach (StringName id in ids)
        {
            sorted.Add(id);
        }
        return sorted;
    }

    public bool _is_multi_unit_skill(SkillDef skill_def)
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        return combatProfile != null && combatProfile.target_selection_mode == "multi_unit";
    }

    public bool _can_skill_target_unit(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        bool require_ap = true,
        CombatCastVariantDef cast_variant = null
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (
            active_unit == null
            || target_unit == null
            || skill_def == null
            || combatProfile == null
        )
        {
            return false;
        }
        CombatSkillResourceCosts costs = _get_effective_skill_resource_costs(
            active_unit,
            skill_def
        );
        if (
            require_ap
            && active_unit.current_ap
                < costs.ApCost
        )
        {
            return false;
        }
        if (
            !_is_unit_valid_for_effect(
                active_unit,
                target_unit,
                combatProfile.target_team_filter
            )
        )
        {
            return false;
        }
        if (
            !string.IsNullOrEmpty(
                _get_unit_skill_target_validation_message(
                    active_unit,
                    target_unit,
                    skill_def,
                    cast_variant
                )
            )
        )
        {
            return false;
        }
        active_unit.refresh_footprint();
        target_unit.refresh_footprint();
        return Runtime?.get_grid_service().get_distance_between_units(active_unit, target_unit)
            <= _get_effective_skill_range(active_unit, skill_def);
    }

    public GDictionary _resolve_unit_skill_effect_result(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs
    )
    {
        return _resolve_unit_skill_effect_resolution(
            active_unit,
            target_unit,
            skill_def,
            effect_defs
        ).Payload;
    }

    private UnitSkillEffectResolution _resolve_unit_skill_effect_resolution(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs
    )
    {
        BattleSkillResolutionRules skillResolutionRules = Runtime?._skill_resolution_rules;
        BattleDamageResolver damageResolver = Runtime?._damage_resolver;
        if (damageResolver == null)
            return UnitSkillEffectResolution.FromPayload(
                new GDictionary(),
                new AttackCheckInput(skillId: skill_def?.skill_id ?? new StringName(""))
            );
        effect_defs ??= new GCombatEffectArray();
        if (
            _should_resolve_unit_skill_as_fate_attack(
                active_unit,
                target_unit,
                skill_def,
                effect_defs
            )
        )
        {
            BattleAttackCheckPolicyService attackPolicy = Runtime?.get_attack_check_policy_service();
            BattleAttackCheckPolicyContext policyContext = attackPolicy?.build_attack_context(
                RtState(),
                active_unit,
                target_unit,
                skill_def,
                new StringName("skill_attack_check"),
                new StringName("execute"),
                false
            );
            AttackCheckInput attackCheck =
                attackPolicy != null
                    ? attackPolicy.build_attack_check(policyContext, 0, 0)
                    : new AttackCheckInput(invalid: true);
            var attackContext = new AttackContext
            {
                BattleState = RtState(),
                SkillId = skill_def?.skill_id ?? new StringName(""),
            };
            if (skillResolutionRules?.is_force_hit_no_crit_skill(skill_def) == true)
            {
                attackContext.ForceHitNoCrit = true;
            }
            GDictionary result = damageResolver.resolve_attack_effects(
                active_unit,
                target_unit,
                ToUntypedArray(effect_defs),
                attackCheck,
                attackContext
            );
            if (skillResolutionRules?.is_force_hit_no_crit_skill(skill_def) == true)
            {
                result["custom_log_lines"] = new GArray
                {
                    "黑契推进压低了命运摆幅：这次攻击必定命中，且不会触发暴击。",
                };
            }
            return UnitSkillEffectResolution.FromPayload(result, attackCheck);
        }
        if (effect_defs.Count != 0)
        {
            GDictionary result = damageResolver.resolve_effects(
                active_unit,
                target_unit,
                ToUntypedArray(effect_defs),
                new GDictionary { ["skill_id"] = skill_def?.skill_id ?? new StringName("") }
            );
            return UnitSkillEffectResolution.FromPayload(
                result,
                new AttackCheckInput(skillId: skill_def?.skill_id ?? new StringName(""))
            );
        }
        return UnitSkillEffectResolution.FromPayload(
            damageResolver.resolve_skill(active_unit, target_unit, skill_def),
            new AttackCheckInput(skillId: skill_def?.skill_id ?? new StringName(""))
        );
    }

    public bool _should_resolve_unit_skill_as_fate_attack(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs
    )
    {
        return Runtime?._skill_resolution_rules?.should_resolve_unit_skill_as_fate_attack(
            active_unit,
            target_unit,
            skill_def,
            effect_defs ?? new GCombatEffectArray()
        ) == true;
    }

    public bool _apply_unit_skill_result(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        GCombatEffectArray effect_defs,
        BattleEventBatch batch,
        BattleSpellControlResult spell_control_context = default
    )
    {
        effect_defs ??= new GCombatEffectArray();
        BattleLayeredBarrierService layeredBarrierService = Runtime?._layered_barrier_service;
        BattleBarrierInteractionResult barrierResult =
            layeredBarrierService != null
                ? layeredBarrierService.ResolveSkillBarrierInteractionResult(
                    active_unit,
                    target_unit,
                    skill_def,
                    ToUntypedArray(effect_defs),
                    batch
                )
                : new BattleBarrierInteractionResult(false, false);
        if (barrierResult.Blocked)
        {
            return barrierResult.Applied;
        }
        UnitSkillEffectResolution effectResolution = _resolve_unit_skill_effect_resolution(
            active_unit,
            target_unit,
            skill_def,
            effect_defs
        );
        GDictionary result = effectResolution.Payload;
        AttackEffectResolutionResult damageResult = effectResolution.Result;
        BattleSkillMasteryService skillMasteryService = Runtime?._skill_mastery_service;
        skillMasteryService?.RecordTargetResult(
            active_unit,
            target_unit,
            skill_def,
            damageResult,
            effect_defs
        );
        _flush_last_stand_mastery_records(batch);
        BattleSkillMasteryGrant guardMasteryGrant =
            skillMasteryService?.BuildGuardMasteryGrantFromIncomingHitTyped(
                active_unit,
                target_unit,
                effect_defs,
                damageResult,
                SkillDefs()
            );
        var shieldRollContext = new GDictionary();
        BattleShieldApplyResult shieldResult = _apply_unit_shield_effects_result(
            active_unit,
            target_unit,
            skill_def,
            effect_defs,
            shieldRollContext
        );
        mark_applied_statuses_for_turn_timing(
            target_unit,
            ToUntypedStringNameArray(damageResult.StatusEffectIds)
        );
        _append_changed_unit_id(batch, target_unit?.unit_id ?? new StringName(""));
        _append_changed_unit_coords(batch, target_unit);
        append_result_source_status_effects(batch, active_unit, damageResult);
        BattleSpecialSkillResult specialResult = _apply_unit_skill_special_effects_result(
            active_unit,
            target_unit,
            skill_def,
            cast_variant,
            effect_defs,
            batch
        );
        mark_applied_statuses_for_turn_timing(
            target_unit,
            ToUntypedStringNameArray(specialResult.StatusEffectIds)
        );
        bool applied =
            damageResult.Applied
            || shieldResult.Applied
            || specialResult.Applied;
        if (!applied)
        {
            _append_result_report_entry(batch, result);
            foreach (var customLineValue in DictArray(result, "custom_log_lines"))
            {
                string customLine = customLineValue.AsString();
                if (!string.IsNullOrEmpty(customLine))
                {
                    batch?.log_lines.Add(customLine);
                }
            }
            foreach (string specialLine in specialResult.LogLines)
            {
                if (!string.IsNullOrEmpty(specialLine))
                {
                    batch?.log_lines.Add(specialLine);
                }
            }
            return false;
        }

        string skillLabel = _format_skill_variant_label(skill_def, cast_variant);
        string skillSubject = _build_skill_log_subject_label(active_unit, skill_def, cast_variant);
        int damage = damageResult.Damage;
        int healing = damageResult.Healing;
        int movedSteps = specialResult.MovedSteps;
        RecordVajraBodyMasteryFromIncomingDamageTyped(
            active_unit,
            target_unit,
            skill_def,
            damageResult,
            batch
        );
        if (movedSteps > 0)
        {
            batch?.log_lines.Add(
                $"{active_unit.display_name} 使用 {skillLabel}，向更安全位置移动 {movedSteps} 格。"
            );
        }
        append_damage_result_log_lines(
            batch,
            skillSubject,
            target_unit?.display_name ?? "",
            damageResult
        );
        _apply_equipment_durability_result(target_unit, damageResult, batch);
        _append_result_report_entry(batch, result);
        if (_is_doom_sentence_skill(skill_def?.skill_id ?? new StringName("")))
        {
            var doomSentenceReportTags = new GStringNameArray
            {
                BattleReportFormatter.TAG_DOOM_SENTENCE,
            };
            _append_report_entry_to_batch(
                batch,
                Runtime?._report_formatter.build_skill_event_entry(
                    active_unit,
                    target_unit,
                    skill_def?.skill_id ?? new StringName(""),
                    BattleReportFormatter.REASON_DOOM_SENTENCE_APPLIED,
                    doomSentenceReportTags
                ) ?? new GDictionary()
            );
        }
        if (healing > 0)
        {
            batch?.log_lines.Add(
                $"{skillSubject} 为 {target_unit.display_name} 恢复 {healing} 点生命。"
            );
        }
        if (shieldResult.Applied)
        {
            batch?.log_lines.Add(
                $"{skillSubject} 使 {target_unit.display_name} 的护盾值变为 {shieldResult.CurrentShieldHp}。"
            );
        }
        foreach (StringName statusId in damageResult.StatusEffectIds)
        {
            batch?.log_lines.Add($"{target_unit.display_name} 获得状态 {statusId}。");
        }
        _append_dispel_result_log_lines(batch, skillSubject, target_unit, result);
        _apply_chain_damage_effects(
            active_unit,
            target_unit,
            skill_def,
            effect_defs,
            result,
            batch,
            skillSubject,
            spell_control_context
        );
        foreach (var customLineValue in DictArray(result, "custom_log_lines"))
        {
            string customLine = customLineValue.AsString();
            if (!string.IsNullOrEmpty(customLine))
            {
                batch?.log_lines.Add(customLine);
            }
        }
        foreach (string specialLine in specialResult.LogLines)
        {
            if (!string.IsNullOrEmpty(specialLine))
            {
                batch?.log_lines.Add(specialLine);
            }
        }
        GStringNameArray terrainEffectIds = damageResult.TerrainEffectIds;
        if (terrainEffectIds.Count != 0)
        {
            BattleGridService gridService = Runtime?.get_grid_service();
            foreach (StringName terrainEffectId in terrainEffectIds)
            {
                BattleCellState targetCell = gridService?.get_cell(RtState(), target_unit.coord);
                if (
                    targetCell != null
                    && !targetCell.terrain_effect_ids.Contains(terrainEffectId)
                )
                {
                    targetCell.terrain_effect_ids.Add(terrainEffectId);
                    _append_changed_coord(batch, target_unit.coord);
                    batch?.log_lines.Add(
                        $"{skillSubject} 使 {target_unit.display_name} 所在的地格附加效果 {terrainEffectId}。"
                    );
                }
            }
        }
        int heightDelta = damageResult.HeightDelta;
        Vector2I targetCoord = target_unit.coord;
        BattleGridService gridService2 = Runtime?.get_grid_service();
        BattleCellState targetCellBefore = gridService2?.get_cell(RtState(), targetCoord);
        int beforeHeight = targetCellBefore?.current_height ?? 0;
        if (
            heightDelta != 0
            && gridService2 != null
            && gridService2.apply_height_delta(RtState(), targetCoord, heightDelta)
        )
        {
            _append_changed_coord(batch, targetCoord);
            BattleCellState targetCellAfter = gridService2.get_cell(RtState(), targetCoord);
            int afterHeight =
                targetCellAfter != null
                    ? targetCellAfter.current_height
                    : beforeHeight + heightDelta;
            batch?.log_lines.Add(
                $"{skillSubject} 使 ({targetCoord.X}, {targetCoord.Y}) 的高度由 {beforeHeight} 变为 {afterHeight}。"
            );
        }
        if (target_unit?.is_alive != true)
        {
            _apply_on_kill_gain_resources_effects(
                active_unit,
                target_unit,
                skill_def as SkillDef,
                effect_defs,
                batch
            );
            Runtime?.handle_unit_defeated_by_runtime_effect(
                target_unit,
                active_unit,
                batch,
                $"{target_unit.display_name} 被击倒。",
                new BattleDefeatHandlingOptions(recordEnemyDefeatedAchievement: true)
            );
        }
        if (active_unit != null && target_unit != null)
        {
            bool causedDefeat = !target_unit.is_alive;
            _record_effect_metrics(active_unit, target_unit, damage, healing, causedDefeat ? 1 : 0);
            Runtime?._battle_rating_system.record_contribution_from_units(
                active_unit,
                target_unit,
                damage,
                healing,
                causedDefeat,
                new StringName("skill"),
                skill_def?.skill_id ?? new StringName("")
            );
        }
        ApplySkillMasteryGrantTyped(target_unit, guardMasteryGrant, batch);
        return true;
    }

    public void _apply_equipment_durability_result(
        BattleUnitState target_unit,
        GDictionary result,
        BattleEventBatch batch
    )
    {
        _apply_equipment_durability_result(
            target_unit,
            AttackEffectResolutionResultReader.ReadLegacyResolverResult(
                result,
                new AttackCheckInput()
            ),
            batch
        );
    }

    internal void _apply_equipment_durability_result(
        BattleUnitState target_unit,
        AttackEffectResolutionResult result,
        BattleEventBatch batch
    )
    {
        if (target_unit == null || batch == null)
        {
            return;
        }
        bool destroyedAny = false;
        foreach (
            EquipmentDurabilityEventResult eventResult in result.EquipmentDurabilityEvents
                ?? Array.Empty<EquipmentDurabilityEventResult>()
        )
        {
            string itemId = eventResult.ItemId ?? "";
            if (string.IsNullOrEmpty(itemId))
            {
                itemId = "装备";
            }
            if (eventResult.SaveResult.HasSave && eventResult.SaveResult.Success)
            {
                batch.log_lines.Add($"{target_unit.display_name} 的 {itemId} 抵抗了裂解术。");
                continue;
            }
            int durabilityLoss = eventResult.DurabilityLoss;
            if (durabilityLoss <= 0)
            {
                continue;
            }
            if (eventResult.Destroyed)
            {
                destroyedAny = true;
                batch.log_lines.Add($"{target_unit.display_name} 的 {itemId} 被裂解为尘埃。");
            }
            else
            {
                batch.log_lines.Add(
                    $"{target_unit.display_name} 的 {itemId} 被裂解，耐久 {eventResult.DurabilityBefore} -> {eventResult.DurabilityAfter}。"
                );
            }
        }
        if (destroyedAny)
        {
            _refresh_target_after_equipment_destruction(target_unit);
            _append_changed_unit_id(batch, target_unit.unit_id);
            _append_changed_unit_coords(batch, target_unit);
        }
    }

    public void _append_dispel_result_log_lines(
        BattleEventBatch batch,
        string skill_subject,
        BattleUnitState target_unit,
        GDictionary result
    )
    {
        if (batch == null || target_unit == null)
        {
            return;
        }
        foreach (GDictionary eventDict in ReadDictionaryItems(DictArray(result, "dispel_events")))
        {
            GArray removedIds = DictArray(
                eventDict,
                "removed_status_ids"
            );
            if (removedIds.Count == 0)
            {
                continue;
            }
            var labels = new List<string>();
            foreach (var statusIdValue in removedIds)
            {
                labels.Add(ProgressionDataUtils.to_string_name(statusIdValue).ToString());
            }
            batch.log_lines.Add(
                $"{skill_subject} 解除 {target_unit.display_name} 身上的 {string.Join("、", labels)}。"
            );
        }
    }

    public void _refresh_target_after_equipment_destruction(BattleUnitState target_unit)
    {
        BattleUnitFactory unitFactory = Runtime?._unit_factory;
        if (target_unit == null || Runtime == null || unitFactory == null)
        {
            return;
        }
        if (!StringNameIsEmpty(target_unit.source_member_id))
        {
            unitFactory.refresh_equipment_projection(target_unit);
        }
        _clamp_target_resources_after_equipment_projection(target_unit);
    }

    public void _clamp_target_resources_after_equipment_projection(BattleUnitState target_unit)
    {
        AttributeSnapshot snapshot = target_unit?.attribute_snapshot;
        if (target_unit == null || snapshot == null)
        {
            return;
        }
        target_unit.current_hp = Math.Clamp(
            target_unit.current_hp,
            0,
            Math.Max(snapshot.get_value(AttributeService.HP_MAX_ID()), 1)
        );
        target_unit.current_mp = Math.Clamp(
            target_unit.current_mp,
            0,
            Math.Max(snapshot.get_value(AttributeService.MP_MAX_ID()), 0)
        );
        target_unit.current_stamina = Math.Clamp(
            target_unit.current_stamina,
            0,
            Math.Max(snapshot.get_value(AttributeService.STAMINA_MAX_ID()), 0)
        );
        target_unit.current_aura = Math.Clamp(
            target_unit.current_aura,
            0,
            Math.Max(snapshot.get_value(AttributeService.AURA_MAX_ID()), 0)
        );
        target_unit.is_alive = target_unit.current_hp > 0;
    }

    public void _apply_chain_damage_effects(
        BattleUnitState source_unit,
        BattleUnitState primary_target,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        GDictionary primary_result,
        BattleEventBatch batch,
        string skill_subject,
        BattleSpellControlResult spell_control_context = default
    )
    {
        BattleState state = RtState();
        if (
            state == null
            || source_unit == null
            || primary_target == null
            || skill_def == null
            || batch == null
        )
        {
            return;
        }
        AttackEffectResolutionResult primaryResolution =
            AttackEffectResolutionResultReader.ReadLegacyResolverResult(
                primary_result,
                new AttackCheckInput(skillId: skill_def?.skill_id ?? new StringName(""))
            );
        if (!primaryResolution.Applied)
        {
            return;
        }
        effect_defs ??= new GCombatEffectArray();
        GCombatEffectArray chainEffects = _collect_chain_damage_effect_defs(effect_defs);
        if (chainEffects.Count == 0)
        {
            return;
        }

        BattleDamageResolver damageResolver = Runtime?._damage_resolver;
        BattleSkillMasteryService skillMasteryService = Runtime?._skill_mastery_service;
        BattleRatingSystem ratingSystem = Runtime?._battle_rating_system;
        foreach (CombatEffectDef chainEffect in chainEffects)
        {
            GCombatEffectArray chainTargetEffects = _build_chain_target_effect_defs(
                effect_defs,
                chainEffect
            );
            if (chainTargetEffects.Count == 0)
            {
                continue;
            }
            GArray chainTargets = _collect_chain_damage_targets(
                source_unit,
                primary_target,
                skill_def,
                chainEffect,
                spell_control_context
            );
            if (chainTargets.Count == 0)
            {
                continue;
            }

            int totalDamage = 0;
            int totalHealing = 0;
            int totalKillCount = 0;
            foreach (var chainTargetValue in chainTargets)
            {
                var chainTarget = chainTargetValue.AsGodotObject() as BattleUnitState;
                if (chainTarget == null || !chainTarget.is_alive)
                {
                    continue;
                }
                GDictionary chainResult = damageResolver?.resolve_effects(
                    source_unit,
                    chainTarget,
                    ToUntypedArray(chainTargetEffects),
                    new GDictionary { ["skill_id"] = skill_def?.skill_id ?? new StringName("") }
                ) ?? new GDictionary();
                AttackEffectResolutionResult chainResolution =
                    AttackEffectResolutionResultReader.ReadLegacyResolverResult(
                        chainResult,
                        new AttackCheckInput(skillId: skill_def?.skill_id ?? new StringName(""))
                    );
                skillMasteryService?.RecordTargetResult(
                    source_unit,
                    chainTarget,
                    skill_def,
                    chainResolution,
                    chainTargetEffects
                );
                mark_applied_statuses_for_turn_timing(
                    chainTarget,
                    ToUntypedStringNameArray(chainResolution.StatusEffectIds)
                );
                if (!chainResolution.Applied)
                {
                    continue;
                }

                _append_changed_unit_id(batch, source_unit.unit_id);
                _append_changed_unit_id(batch, chainTarget.unit_id);
                _append_changed_unit_coords(batch, chainTarget);
                append_result_source_status_effects(batch, source_unit, chainResolution);
                append_damage_result_log_lines(
                    batch,
                    $"{skill_subject} 的连锁闪电",
                    chainTarget.display_name,
                    chainResolution
                );
                foreach (StringName statusId in chainResolution.StatusEffectIds)
                {
                    batch.log_lines.Add($"{chainTarget.display_name} 获得状态 {statusId}。");
                }

                int chainDamage = chainResolution.Damage;
                int chainHealing = chainResolution.Healing;
                totalDamage += chainDamage;
                totalHealing += chainHealing;
                if (!chainTarget.is_alive)
                {
                    totalKillCount += 1;
                    _apply_on_kill_gain_resources_effects(
                        source_unit,
                        chainTarget,
                        skill_def,
                        chainTargetEffects,
                        batch
                    );
                    Runtime?.handle_unit_defeated_by_runtime_effect(
                        chainTarget,
                        source_unit,
                        batch,
                        $"{chainTarget.display_name} 被击倒。",
                        new BattleDefeatHandlingOptions(recordEnemyDefeatedAchievement: true)
                    );
                }
                bool causedChainDefeat = !chainTarget.is_alive;
                _record_effect_metrics(
                    source_unit,
                    chainTarget,
                    chainDamage,
                    chainHealing,
                    causedChainDefeat ? 1 : 0
                );
                ratingSystem?.record_contribution_from_units(
                    source_unit,
                    chainTarget,
                    chainDamage,
                    chainHealing,
                    causedChainDefeat,
                    new StringName("skill"),
                    skill_def?.skill_id ?? new StringName("")
                );
            }
        }
    }

    public GCombatEffectArray _collect_chain_damage_effect_defs(GCombatEffectArray effect_defs)
    {
        var chainEffects = new GCombatEffectArray();
        foreach (CombatEffectDef effectDef in effect_defs ?? new GCombatEffectArray())
        {
            if (effectDef != null && effectDef.effect_type == CHAIN_DAMAGE_EFFECT_TYPE)
            {
                chainEffects.Add(effectDef);
            }
        }
        return chainEffects;
    }

    public GDictionary _get_effect_params(CombatEffectDef effect_def)
    {
        if (effect_def == null)
        {
            return new GDictionary();
        }
        return effect_def.@params ?? new GDictionary();
    }

    public GCombatEffectArray _build_chain_target_effect_defs(
        GCombatEffectArray effect_defs,
        CombatEffectDef chain_effect
    )
    {
        var chainTargetEffects = new GCombatEffectArray();
        foreach (CombatEffectDef effectDef in effect_defs ?? new GCombatEffectArray())
        {
            if (effectDef == null || effectDef.effect_type == CHAIN_DAMAGE_EFFECT_TYPE)
            {
                continue;
            }
            CombatEffectDef runtimeEffect = effectDef.duplicate_for_runtime();
            if (runtimeEffect == null)
            {
                continue;
            }
            chainTargetEffects.Add(runtimeEffect);
        }
        return chainTargetEffects;
    }

    public GArray _collect_chain_damage_targets(
        BattleUnitState source_unit,
        BattleUnitState primary_target,
        SkillDef skill_def,
        CombatEffectDef chain_effect,
        BattleSpellControlResult spell_control_context = default
    )
    {
        var targets = new GArray();
        BattleState state = RtState();
        if (state == null || source_unit == null || primary_target == null || chain_effect == null)
        {
            return targets;
        }

        int maxRadius = _resolve_chain_damage_radius(
            primary_target,
            chain_effect,
            spell_control_context
        );
        if (maxRadius <= 0)
        {
            return targets;
        }
        bool preventRepeatTarget = ChainDamageParameters
            .FromEffect(chain_effect)
            .PreventRepeatTarget;
        StringName targetFilter = _resolve_effect_target_filter(skill_def, chain_effect);
        if (StringNameIsEmpty(targetFilter))
        {
            return targets;
        }

        BattleGridService gridService = Runtime?.get_grid_service();
        var visited = new GDictionary();
        var queue = new List<BattleUnitState>();
        visited[primary_target.unit_id] = true;
        queue.Add(primary_target);

        while (queue.Count != 0)
        {
            BattleUnitState current = queue[0];
            queue.RemoveAt(0);

            foreach (BattleUnitState candidate in state.GetUnitsTyped())
            {
                if (candidate == null || !candidate.is_alive)
                {
                    continue;
                }
                if (
                    preventRepeatTarget
                    && visited.ContainsKey(candidate.unit_id)
                )
                {
                    continue;
                }
                if (!_is_unit_valid_for_effect(source_unit, candidate, targetFilter))
                {
                    continue;
                }
                if (!_is_within_chain_radius(primary_target, candidate, maxRadius))
                {
                    continue;
                }
                if (!_is_chain_path_clear(current, candidate))
                {
                    continue;
                }

                visited[candidate.unit_id] = true;
                targets.Add(candidate);
                queue.Add(candidate);
            }
        }

        var targetList = new List<BattleUnitState>();
        foreach (var t in targets)
        {
            targetList.Add(t.AsGodotObject() as BattleUnitState);
        }
        targetList.Sort(
            (a, b) =>
            {
                int distanceA = gridService?.get_distance_between_units(primary_target, a) ?? 0;
                int distanceB = gridService?.get_distance_between_units(primary_target, b) ?? 0;
                if (distanceA != distanceB)
                    return distanceA.CompareTo(distanceB);
                Vector2I ca = a?.coord ?? Vector2I.Zero;
                Vector2I cb = b?.coord ?? Vector2I.Zero;
                if (ca.Y != cb.Y)
                    return ca.Y.CompareTo(cb.Y);
                if (ca.X != cb.X)
                    return ca.X.CompareTo(cb.X);
                return string.CompareOrdinal(
                    (a?.unit_id ?? new StringName("")).ToString(),
                    (b?.unit_id ?? new StringName("")).ToString()
                );
            }
        );
        var sortedTargets = new GArray();
        foreach (BattleUnitState t in targetList)
        {
            sortedTargets.Add(t);
        }
        return sortedTargets;
    }

    public int _resolve_chain_damage_radius(
        BattleUnitState primary_target,
        CombatEffectDef chain_effect,
        BattleSpellControlResult spell_control_context = default
    )
    {
        ChainDamageParameters chainParams = ChainDamageParameters.FromEffect(chain_effect);
        int baseRadius = chainParams.BaseRadius;
        StringName bonusEffectId = chainParams.BonusTerrainEffectId;
        int radius = baseRadius;
        if (
            !StringNameIsEmpty(bonusEffectId)
            && primary_target != null
            && _unit_stands_on_terrain_effect(primary_target, bonusEffectId)
        )
        {
            radius = chainParams.WetChainRadius;
        }
        if (spell_control_context.BacklashTriggered)
        {
            radius += 1;
        }
        return radius;
    }

    public bool _unit_stands_on_terrain_effect(
        BattleUnitState unit_state,
        StringName terrain_effect_id
    )
    {
        BattleState state = RtState();
        if (state == null || unit_state == null || StringNameIsEmpty(terrain_effect_id))
        {
            return false;
        }
        unit_state.refresh_footprint();
        BattleGridService gridService = Runtime?.get_grid_service();
        foreach (Vector2I occupiedCoord in unit_state.occupied_coords)
        {
            BattleCellState cell = gridService?.get_cell(state, occupiedCoord);
            if (cell == null)
            {
                continue;
            }
            if (cell.terrain_effect_ids.Contains(terrain_effect_id))
            {
                return true;
            }
            foreach (BattleTerrainEffectState effectState in cell.timed_terrain_effects)
            {
                if (effectState != null && effectState.effect_id == terrain_effect_id)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public bool _is_within_chain_radius(
        BattleUnitState primary_target,
        BattleUnitState candidate,
        int max_radius
    )
    {
        if (primary_target == null || candidate == null || max_radius <= 0)
        {
            return false;
        }
        primary_target.refresh_footprint();
        candidate.refresh_footprint();
        BattleGridService gridService = Runtime?.get_grid_service();
        foreach (Vector2I primaryCoord in primary_target.occupied_coords)
        {
            foreach (Vector2I candidateCoord in candidate.occupied_coords)
            {
                if (gridService != null && gridService.get_distance(primaryCoord, candidateCoord) <= max_radius)
                {
                    return true;
                }
            }
        }
        return false;
    }

    public GVector2IArray _get_line_coords(Vector2I from, Vector2I to)
    {
        var coords = new GVector2IArray();
        int dx = Math.Abs(to.X - from.X);
        int dy = Math.Abs(to.Y - from.Y);
        int sx = from.X < to.X ? 1 : -1;
        int sy = from.Y < to.Y ? 1 : -1;
        int err = dx - dy;
        int x = from.X;
        int y = from.Y;
        while (x != to.X || y != to.Y)
        {
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
            if (x == to.X && y == to.Y)
            {
                break;
            }
            coords.Add(new Vector2I(x, y));
        }
        return coords;
    }

    public bool _is_chain_path_clear(BattleUnitState source_unit, BattleUnitState target_unit)
    {
        BattleState state = RtState();
        BattleGridService gridService = Runtime?.get_grid_service();
        if (state == null || source_unit == null || target_unit == null || gridService == null)
        {
            return false;
        }
        source_unit.refresh_footprint();
        target_unit.refresh_footprint();
        foreach (Vector2I sourceCoord in source_unit.occupied_coords)
        {
            BattleCellState sourceCell = gridService.get_cell(state, sourceCoord);
            if (sourceCell == null)
            {
                continue;
            }
            int sourceHeight = sourceCell.current_height;
            foreach (Vector2I targetCoord in target_unit.occupied_coords)
            {
                foreach (Vector2I midCoord in _get_line_coords(sourceCoord, targetCoord))
                {
                    BattleCellState midCell = gridService.get_cell(state, midCoord);
                    if (midCell == null)
                    {
                        continue;
                    }
                    if (Math.Abs(midCell.current_height - sourceHeight) > 1)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    public string _get_unit_skill_target_validation_message(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant = null
    )
    {
        string bodySizeOverrideMessage = _get_body_size_category_override_validation_message(
            active_unit,
            target_unit,
            skill_def,
            cast_variant
        );
        if (!string.IsNullOrEmpty(bodySizeOverrideMessage))
        {
            return bodySizeOverrideMessage;
        }
        StringName skillId = skill_def?.skill_id ?? new StringName("");
        if (
            _is_black_crown_seal_skill(skillId)
            && !_is_black_crown_seal_target_eligible(active_unit, target_unit)
        )
        {
            return "黑冠封印只能对 boss 施放。";
        }
        if (_is_doom_shift_skill(skillId))
        {
            if (target_unit == null || active_unit == null)
            {
                return "断命换位的目标无效。";
            }
            if (
                target_unit.unit_id == active_unit.unit_id
            )
            {
                return "断命换位不能以自己为目标。";
            }
        }
        if (
            _is_crown_break_skill(skillId)
            && !_is_crown_break_target_eligible(active_unit, target_unit)
        )
        {
            return "折冠只能对已被黑星烙印的 elite / boss 施放。";
        }
        if (
            _is_doom_sentence_skill(skillId)
            && !_is_doom_sentence_target_eligible(active_unit, target_unit)
        )
        {
            return "厄命宣判只能对 elite / boss 施放。";
        }
        return "";
    }

    public string _get_body_size_category_override_validation_message(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant = null
    )
    {
        BattleState state = RtState();
        if (state == null || target_unit == null || skill_def == null)
        {
            return "";
        }
        BattleGridService gridService = Runtime?.get_grid_service();
        if (gridService == null)
        {
            return "";
        }
        foreach (
            CombatEffectDef effectDef in _collect_unit_skill_effect_defs(
                skill_def,
                cast_variant,
                active_unit
            )
        )
        {
            if (
                effectDef == null
                || effectDef.effect_type != BODY_SIZE_CATEGORY_OVERRIDE_EFFECT_TYPE
            )
            {
                continue;
            }
            StringName targetCategory = ProgressionDataUtils.to_string_name(
                effectDef.body_size_category
            );
            if (!BodySizeRules.is_valid_body_size_category(targetCategory))
            {
                continue;
            }
            Vector2I targetFootprint = BodySizeRules.get_footprint_for_category(targetCategory);
            if (
                !gridService
                    .can_place_footprint(
                        state,
                        target_unit.coord,
                        targetFootprint,
                        target_unit.unit_id,
                        target_unit
                    )
            )
            {
                return $"{target_unit.display_name} 周围空间不足，无法改变体型。";
            }
        }
        return "";
    }

    public bool _skill_grants_guarding(SkillDef skill_def)
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (skill_def == null || combatProfile == null)
        {
            return false;
        }
        foreach (CombatEffectDef effectDef in _collect_unit_skill_effect_defs(skill_def, null))
        {
            if (effectDef == null)
            {
                continue;
            }
            StringName effectType = effectDef.effect_type;
            if (
                (effectType == "status" || effectType == "apply_status")
                && effectDef.status_id == STATUS_GUARDING
            )
            {
                return true;
            }
        }
        foreach (CombatCastVariantDef castVariant in combatProfile.cast_variants)
        {
            if (castVariant == null)
            {
                continue;
            }
            foreach (CombatEffectDef effectDef in castVariant.effect_defs)
            {
                if (effectDef == null)
                {
                    continue;
                }
                StringName effectType = effectDef.effect_type;
                if (
                    (effectType == "status" || effectType == "apply_status")
                    && effectDef.status_id == STATUS_GUARDING
                )
                {
                    return true;
                }
            }
        }
        return false;
    }

    public GCombatEffectArray _collect_unit_skill_effect_defs(
        SkillDef skill_def,
        CombatCastVariantDef cast_variant,
        BattleUnitState active_unit = null
    )
    {
        return Runtime?._skill_resolution_rules != null
            ? Runtime._skill_resolution_rules.collect_unit_skill_effect_defs(
                skill_def,
                cast_variant,
                active_unit
            )
            : new GCombatEffectArray();
    }

    public GArray _collect_units_in_coords(GVector2IArray effect_coords)
    {
        var units = new GArray();
        HashSet<StringName> seenUnitIds = new();
        BattleGridService gridService = Runtime?._grid_service;
        foreach (Vector2I effectCoord in effect_coords)
        {
            BattleUnitState targetUnit = gridService?.get_unit_at_coord(
                Runtime?._state,
                effectCoord
            );
            if (
                targetUnit == null
                || !targetUnit.is_alive
                || seenUnitIds.Contains(targetUnit.unit_id)
            )
            {
                continue;
            }
            seenUnitIds.Add(targetUnit.unit_id);
            units.Add(targetUnit);
        }
        return units;
    }

    public bool _is_unit_effect(CombatEffectDef effect_def)
    {
        if (effect_def == null)
        {
            return false;
        }
        StringName effectType = effect_def.effect_type;
        return effectType == "damage"
            || effectType == EQUIPMENT_DURABILITY_DAMAGE_EFFECT_TYPE
            || effectType == "dispel_magic"
            || effectType == "heal"
            || effectType == "shield"
            || effectType == "layered_barrier"
            || effectType == "status"
            || effectType == "apply_status"
            || effectType == "forced_move"
            || effectType == "execute";
    }

    public bool _is_terrain_effect(CombatEffectDef effect_def)
    {
        if (effect_def == null)
        {
            return false;
        }
        StringName effectType = effect_def.effect_type;
        return effectType == "terrain"
            || effectType == "terrain_replace"
            || effectType == "terrain_replace_to"
            || effectType == "height"
            || effectType == "height_delta"
            || effectType == "terrain_effect";
    }

    public StringName _resolve_effect_target_filter(SkillDef skill_def, CombatEffectDef effect_def)
    {
        StringName effectTargetFilter = effect_def?.effect_target_team_filter ?? new StringName("");
        if (!StringNameIsEmpty(effectTargetFilter))
            return effectTargetFilter;
        return skill_def?.combat_profile?.target_team_filter ?? new StringName("");
    }

    public bool _is_unit_valid_for_effect(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        StringName target_team_filter
    )
    {
        bool madnessAnyTeam = source_unit?.ai_blackboard?.madness_target_any_team == true;
        return BattleTargetTeamRules.is_unit_valid_for_filter(
            source_unit,
            target_unit,
            target_team_filter,
            new BattleTargetTeamRules.TargetFilterOptions(MadnessTargetAnyTeam: madnessAnyTeam)
        );
    }

    public CombatCastVariantDef _resolve_ground_cast_variant(
        SkillDef skill_def,
        BattleUnitState active_unit,
        BattleCommand command
    )
    {
        return Runtime?._skill_resolution_rules?.resolve_ground_cast_variant(
            skill_def,
            active_unit,
            command != null ? command.skill_variant_id : new StringName("")
        );
    }

    public CombatCastVariantDef _resolve_unit_cast_variant(
        SkillDef skill_def,
        BattleUnitState active_unit,
        BattleCommand command
    )
    {
        return Runtime?._skill_resolution_rules?.resolve_unit_cast_variant(
            skill_def,
            active_unit,
            command != null ? command.skill_variant_id : new StringName("")
        );
    }

    public CombatCastVariantDef _resolve_command_route_cast_variant(
        SkillDef skill_def,
        BattleUnitState active_unit,
        BattleCommand command,
        bool routes_to_unit_targeting
    )
    {
        return Runtime?._skill_resolution_rules?.resolve_command_route_cast_variant(
            skill_def,
            active_unit,
            command != null ? command.skill_variant_id : new StringName(""),
            routes_to_unit_targeting
        );
    }

    public StringName _get_cast_variant_target_mode(SkillDef skill_def, CombatCastVariantDef cast_variant)
    {
        return Runtime?._skill_resolution_rules?.get_cast_variant_target_mode(
            skill_def,
            cast_variant
        ) ?? new StringName("");
    }

    public string _get_skill_variant_command_block_reason(
        SkillDef skill_def,
        BattleUnitState active_unit,
        BattleCommand command,
        bool routes_to_unit_targeting
    )
    {
        BattleSkillResolutionRules skillResolutionRules = Runtime?._skill_resolution_rules;
        if (_runtime == null || skillResolutionRules == null)
        {
            return "";
        }
        return skillResolutionRules.get_skill_variant_command_error_message(
            skill_def,
            active_unit,
            command != null ? command.skill_variant_id : new StringName(""),
            routes_to_unit_targeting
        );
    }

    public CombatCastVariantDef _build_implicit_ground_cast_variant(SkillDef skill_def)
    {
        CombatCastVariantDef castVariant = new CombatCastVariantDef();
        castVariant.variant_id = new StringName("");
        castVariant.display_name = "";
        castVariant.target_mode = new StringName("ground");
        castVariant.footprint_pattern = new StringName("single");
        castVariant.required_coord_count = 1;
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (skill_def != null && combatProfile != null)
        {
            castVariant.effect_defs = new Godot.Collections.Array<CombatEffectDef>();
            foreach (CombatEffectDef def in combatProfile.effect_defs)
            {
                if (def != null)
                {
                    castVariant.effect_defs.Add(def);
                }
            }
        }
        return castVariant;
    }

    public int _get_unit_skill_level(BattleUnitState unit_state, StringName skill_id)
    {
        if (unit_state == null || StringNameIsEmpty(skill_id))
        {
            return 0;
        }
        GDictionary knownSkillLevelMap = unit_state.known_skill_level_map ?? new GDictionary();
        if (knownSkillLevelMap.ContainsKey(skill_id))
        {
            return DictInt(knownSkillLevelMap, skill_id, 0);
        }
        if (_runtime != null)
        {
            SkillDef skillDef = Runtime?.get_skill_def_typed(skill_id);
            if (
                skillDef != null
                && skillDef.max_level == 0
                && StringNameIsEmpty(skillDef.dynamic_max_level_stat_id)
            )
            {
                return 0;
            }
        }
        return unit_state.known_active_skill_ids.Contains(skill_id) ? 1 : 0;
    }

    public string _format_skill_variant_label(SkillDef skill_def, CombatCastVariantDef cast_variant)
    {
        if (skill_def == null)
        {
            return "";
        }
        if (
            cast_variant == null
            || string.IsNullOrEmpty(cast_variant.display_name)
        )
        {
            return skill_def.display_name;
        }
        return $"{skill_def.display_name}·{cast_variant.display_name}";
    }

    private static GVector2IArray ToVec2IArray(GArray src)
    {
        var result = new GVector2IArray();
        foreach (var v in src)
        {
            result.Add(v.AsVector2I());
        }
        return result;
    }

    private static GArray ToUntypedArray(GVector2IArray src)
    {
        var result = new GArray();
        foreach (Vector2I v in src)
        {
            result.Add(v);
        }
        return result;
    }

    private static GArray ToUntypedArray(IReadOnlyList<BattleUnitState> src)
    {
        var result = new GArray();
        if (src == null)
        {
            return result;
        }
        foreach (BattleUnitState unit in src)
        {
            if (unit != null)
            {
                result.Add(unit);
            }
        }
        return result;
    }

    private static GArray ToUntypedStringNameArray(IReadOnlyList<StringName> src)
    {
        var result = new GArray();
        if (src == null)
        {
            return result;
        }
        foreach (StringName value in src)
        {
            result.Add(value);
        }
        return result;
    }

    private static GArray ToUntypedArray(GCombatEffectArray src)
    {
        var result = new GArray();
        if (src == null)
        {
            return result;
        }
        foreach (CombatEffectDef effectDef in src)
        {
            if (effectDef != null)
            {
                result.Add(effectDef);
            }
        }
        return result;
    }

    private static List<StringName> ToStringNameList(GStringNameArray src)
    {
        var result = new List<StringName>();
        if (src == null)
        {
            return result;
        }
        foreach (StringName id in src)
        {
            result.Add(id);
        }
        return result;
    }

    private static List<Vector2I> ToVector2IList(GArray src)
    {
        var result = new List<Vector2I>();
        if (src == null)
        {
            return result;
        }
        foreach (var value in src)
        {
            if (value.VariantType == Variant.Type.Vector2I)
            {
                result.Add(value.AsVector2I());
            }
        }
        return result;
    }

    private static List<Vector2I> ToVector2IList(GVector2IArray src)
    {
        var result = new List<Vector2I>();
        if (src == null)
        {
            return result;
        }
        foreach (Vector2I coord in src)
        {
            result.Add(coord);
        }
        return result;
    }

    private static List<BattleUnitState> ToUnitList(GArray src)
    {
        var result = new List<BattleUnitState>();
        if (src == null)
        {
            return result;
        }
        foreach (var value in src)
        {
            var unit = value.AsGodotObject() as BattleUnitState;
            if (unit != null)
            {
                result.Add(unit);
            }
        }
        return result;
    }

    private static Godot.Collections.Array<CombatEffectDef> ToCombatEffectDefArray(GArray src)
    {
        var result = new Godot.Collections.Array<CombatEffectDef>();
        if (src == null)
        {
            return result;
        }
        foreach (var value in src)
        {
            var effectDef = value.AsGodotObject() as CombatEffectDef;
            if (effectDef != null)
            {
                result.Add(effectDef);
            }
        }
        return result;
    }

    private BattleState RtState()
    {
        return Runtime?._state;
    }

    private GDictionary SkillDefs()
    {
        return Runtime?._skill_defs ?? new GDictionary();
    }

    private static GArray DictArray(GDictionary source, object key)
    {
        if (!TryGetDictionaryValue(source, key, out Variant value))
        {
            return new GArray();
        }
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static int DictInt(GDictionary source, object key, int fallback = 0)
    {
        if (!TryGetDictionaryValue(source, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static string DictString(GDictionary source, object key, string fallback = "")
    {
        if (!TryGetDictionaryValue(source, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType == Variant.Type.String
            || value.VariantType == Variant.Type.StringName
            ? value.AsString()
            : fallback;
    }

    private static StringName DictStringName(
        GDictionary source,
        object key,
        StringName fallback = default
    )
    {
        if (!TryGetDictionaryValue(source, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType == Variant.Type.StringName
            ? value.AsStringName()
            : value.VariantType == Variant.Type.String
                ? new StringName(value.AsString())
                : fallback;
    }

    private static IEnumerable<GDictionary> ReadDictionaryItems(GArray values)
    {
        foreach (Variant value in values ?? new GArray())
        {
            if (value.VariantType == Variant.Type.Dictionary)
            {
                yield return value.AsGodotDictionary();
            }
        }
    }

    private static bool TryGetDictionaryValue(
        GDictionary dictionary,
        object key,
        out Variant value
    )
    {
        if (dictionary == null || key == null)
        {
            value = default;
            return false;
        }
        Variant variantKey = key switch
        {
            Variant valueKey => valueKey,
            string stringKey => stringKey,
            StringName stringNameKey => stringNameKey,
            int intKey => intKey,
            long longKey => longKey,
            _ => default,
        };
        if (dictionary.ContainsKey(variantKey))
        {
            value = dictionary[variantKey];
            return true;
        }
        if (variantKey.VariantType == Variant.Type.String)
        {
            StringName stringNameKey = new(variantKey.AsString());
            if (dictionary.ContainsKey(stringNameKey))
            {
                value = dictionary[stringNameKey];
                return true;
            }
        }
        else if (variantKey.VariantType == Variant.Type.StringName)
        {
            string stringKey = variantKey.AsStringName().ToString();
            if (dictionary.ContainsKey(stringKey))
            {
                value = dictionary[stringKey];
                return true;
            }
        }
        value = default;
        return false;
    }

    private static bool StringNameIsEmpty(StringName value)
    {
        return value == null || value.ToString().Length == 0;
    }

    private readonly struct UnitSkillEffectResolution
    {
        internal readonly GDictionary Payload;
        internal readonly AttackEffectResolutionResult Result;

        private UnitSkillEffectResolution(GDictionary payload, AttackEffectResolutionResult result)
        {
            Payload = payload ?? new GDictionary();
            Result = result;
        }

        internal static UnitSkillEffectResolution FromPayload(
            GDictionary payload,
            AttackCheckInput attackCheck
        )
        {
            payload ??= new GDictionary();
            return new UnitSkillEffectResolution(
                payload,
                AttackEffectResolutionResultReader.ReadLegacyResolverResult(payload, attackCheck)
            );
        }
    }

    private static BattleRuntimeModule ResolveWeakRef(WeakReference<BattleRuntimeModule> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out BattleRuntimeModule target))
            return null;
        return target;
    }
}
