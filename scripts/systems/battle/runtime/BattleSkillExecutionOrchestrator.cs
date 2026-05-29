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
        forced_move_context ??= new GDictionary();
        return Runtime?._apply_unit_skill_special_effects(
                active_unit,
                target_unit,
                skill_def,
                cast_variant,
                effect_defs ?? new GCombatEffectArray(),
                batch,
                forced_move_context
            ) ?? new GDictionary();
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

    public GDictionary _resolve_ground_spell_control_after_cost(
        BattleUnitState active_unit,
        SkillDef skill_def,
        int spent_mp,
        BattleEventBatch batch
    )
    {
        return Runtime?._resolve_ground_spell_control_after_cost(
                active_unit,
                skill_def,
                spent_mp,
                batch
            ) ?? new GDictionary();
    }

    public GDictionary _resolve_unit_spell_control_after_cost(
        BattleUnitState active_unit,
        SkillDef skill_def,
        BattleEventBatch batch
    )
    {
        return Runtime?._resolve_unit_spell_control_after_cost(
            active_unit,
            skill_def,
            batch
        ) ?? new GDictionary();
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
        if (Runtime == null)
            return new GDictionary();
        return Runtime._apply_ground_unit_effects(
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
        if (Runtime == null)
            return new GDictionary();
        return Runtime._apply_ground_terrain_effects(
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
        shield_roll_context ??= new GDictionary();
        if (Runtime == null)
            return new GDictionary();
        return Runtime._apply_unit_shield_effects(
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
        SkillDef skillDef = GdInterop.GetObject(
            SkillDefs(),
            GdInterop.GetStringName(command, "skill_id")
        ) as SkillDef;
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
            GdInterop.GetArray(batch, "log_lines").Add(optionBlockReason);
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
            GdInterop.GetArray(batch, "log_lines").Add(blockReason);
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

        _record_skill_attempt(active_unit, GdInterop.GetStringName(command, "skill_id"));
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
        SkillDef skillDef = GdInterop.GetObject(SkillDefs(), command.skill_id) as SkillDef;
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
        GDictionary validation = _validate_ground_skill_command(
            active_unit,
            skill_def,
            cast_variant,
            command
        );
        if (!GdInterop.GetBool(validation, "allowed", false))
        {
            batch?.log_lines.Add(GdInterop.GetString(validation, "message", "技能或目标无效。"));
            return false;
        }
        GVector2IArray targetCoords = _extract_validated_target_coords(validation);
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
        _record_skill_attempt(active_unit, GdInterop.GetStringName(command, "skill_id"));
        int spentMp = Math.Max(mpBeforeCost - (active_unit?.current_mp ?? 0), 0);
        GDictionary costs = _get_effective_skill_costs(active_unit, skill_def);
        _record_action_issued(
            active_unit,
            BattleCommand.TYPE_SKILL(),
            GdInterop.GetInt(
                costs,
                "ap_cost",
                skill_def?.combat_profile?.ap_cost ?? 0
            )
        );
        _append_changed_unit_id(batch, active_unit?.unit_id ?? new StringName(""));

        GDictionary spellControlContext = _resolve_ground_spell_control_after_cost(
            active_unit,
            skill_def,
            spentMp,
            batch
        );
        if (GdInterop.GetBool(spellControlContext, "skip_effects", false))
        {
            return false;
        }

        GDictionary driftContext =
            Runtime
                ?._magic_backlash_resolver.build_ground_backlash_target_coords(
                    skill_def as SkillDef,
                    targetCoords,
                    Runtime.get_state(),
                    Runtime.get_grid_service(),
                    spellControlContext
                ) ?? new GDictionary { ["target_coords"] = targetCoords };
        GVector2IArray finalTargetCoords = _extract_validated_target_coords(
            new GDictionary
            {
                ["target_coords"] = driftContext.GetValueOrDefault("target_coords", targetCoords),
            }
        );
        if (finalTargetCoords.Count == 0)
        {
            finalTargetCoords = (GVector2IArray)targetCoords.Duplicate();
        }
        if (GdInterop.GetBool(driftContext, "backlash_triggered", false))
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
            spellControlContext,
            driftContext
        );
        MeteorSwarmTargetPlan plan = meteorResolver.BuildTargetPlanTyped(context);
        MeteorSwarmCommitResult result = meteorResolver.ResolveTyped(plan);
        return commitAdapter.commit_meteor_swarm_result(result, batch);
    }

    public GVector2IArray _extract_validated_target_coords(GDictionary validation)
    {
        var targetCoords = new GVector2IArray();
        foreach (Vector2I coord in ToVec2IArray(GdInterop.GetArray(validation, "target_coords")))
        {
            targetCoords.Add(coord);
        }
        return targetCoords;
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
        GDictionary validation = _validate_unit_skill_targets(
            active_unit,
            command,
            skill_def,
            cast_variant
        );
        AiTraceRecorder.exit("preview:unit_skill.validate_targets");
        AiTraceRecorder.enter("preview:unit_skill.copy_validation");
        preview.allowed = GdInterop.GetBool(validation, "allowed", false);
        preview.target_unit_ids.Clear();
        foreach (var targetUnitIdValue in GdInterop.GetArray(validation, "target_unit_ids"))
        {
            preview.target_unit_ids.Add(ProgressionDataUtils.to_string_name(targetUnitIdValue));
        }
        preview.random_chain_candidate_unit_ids.Clear();
        foreach (var candidateUnitIdValue in GdInterop.GetArray(validation, "random_chain_candidate_unit_ids"))
        {
            preview.random_chain_candidate_unit_ids.Add(
                ProgressionDataUtils.to_string_name(candidateUnitIdValue)
            );
        }
        preview.target_coords.Clear();
        foreach (Vector2I previewCoord in ToVec2IArray(GdInterop.GetArray(validation, "preview_coords")))
        {
            preview.target_coords.Add(previewCoord);
        }
        AiTraceRecorder.exit("preview:unit_skill.copy_validation");
        if (preview.allowed)
        {
            GArray targetUnits = GdInterop.GetArray(validation, "target_units");
            AiTraceRecorder.enter("preview:unit_skill.hit_preview");
            preview.hit_preview = _build_unit_skill_hit_preview(
                active_unit,
                targetUnits,
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
            if (targetUnits.Count == 1)
            {
                var targetUnit = targetUnits[0].AsGodotObject() as BattleUnitState;
                if (targetUnit != null)
                {
                    preview.log_lines.Add(
                        $"{active_unit.display_name} 可对 {targetUnit.display_name} 使用 {skillLabel}。"
                    );
                    if (preview.hit_preview.Count != 0)
                    {
                        preview.log_lines.Add(
                            GdInterop.GetString(preview.hit_preview, "summary_text", "")
                        );
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
            if (preview.hit_preview.Count != 0)
            {
                preview.log_lines.Add(
                    GdInterop.GetString(preview.hit_preview, "summary_text", "")
                );
            }
            _append_damage_preview_line(preview);
            AiTraceRecorder.exit("preview:unit_skill.log_lines");
            return;
        }
        preview.log_lines.Add(GdInterop.GetString(validation, "message", "技能或目标无效。"));
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
        GDictionary validation = _validate_ground_skill_command(
            active_unit,
            skill_def,
            cast_variant,
            command
        );
        AiTraceRecorder.exit("preview:ground_skill.validate");
        AiTraceRecorder.enter("preview:ground_skill.preview_coords");
        preview.target_coords.Clear();
        GVector2IArray previewCoords;
        if (validation.ContainsKey("preview_coords"))
        {
            previewCoords = validation["preview_coords"].As<GVector2IArray>();
        }
        else
        {
            previewCoords = _build_ground_effect_coords(
                skill_def,
                GdInterop.GetArray(validation, "target_coords"),
                active_unit != null
                    ? GdInterop.GetVector2I(active_unit, "coord")
                    : new Vector2I(-1, -1),
                active_unit,
                cast_variant
            );
        }
        preview.resolved_anchor_coord = GdInterop.GetVector2I(
            validation,
            "resolved_anchor_coord",
            new Vector2I(-1, -1)
        );
        bool allowed = GdInterop.GetBool(validation, "allowed", false);
        if (allowed && Runtime?._charge_resolver != null)
        {
            CombatEffectDef pathStepAoeEffect = Runtime._charge_resolver
                .get_charge_path_step_aoe_effect_def(cast_variant, skill_def, active_unit);
            if (pathStepAoeEffect != null)
            {
                previewCoords = Runtime._charge_resolver.build_charge_step_aoe_preview_coords(
                    active_unit,
                    GdInterop.GetVector2I(validation, "direction", Vector2I.Zero),
                    GdInterop.GetInt(validation, "distance", 0),
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
            preview.log_lines.Add(GdInterop.GetString(validation, "message", "地面技能目标无效。"));
        }
        AiTraceRecorder.exit("preview:ground_skill.log_lines");
    }

    public GDictionary _build_unit_skill_hit_preview(
        BattleUnitState active_unit,
        GArray target_units,
        SkillDef skill_def,
        CombatCastVariantDef cast_variant
    )
    {
        if (active_unit == null || skill_def == null || target_units.Count != 1)
        {
            return new GDictionary();
        }
        var targetUnit = target_units[0].AsGodotObject() as BattleUnitState;
        if (targetUnit == null)
        {
            return new GDictionary();
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
            return new GDictionary();
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
                return new GDictionary();
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
        Godot.Collections.Array<BattleRepeatAttackStageSpec> stageSpecs =
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
        string damagePreviewText = GdInterop.GetString(damagePreview, "summary_text", "");
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
        string damagePreviewText = GdInterop.GetString(damagePreview, "summary_text", "");
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
            && !string.IsNullOrEmpty(GdInterop.GetString(source_unit, "display_name"))
                ? GdInterop.GetString(source_unit, "display_name")
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
        GDictionary validation = _validate_unit_skill_targets(
            active_unit,
            command,
            skill_def,
            cast_variant
        );
        if (!GdInterop.GetBool(validation, "allowed", false))
        {
            return false;
        }

        GArray targetUnits = GdInterop.GetArray(validation, "target_units");
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        bool isRandomChain =
            combatProfile != null && combatProfile.target_selection_mode == "random_chain";
        if (targetUnits.Count == 0 && !isRandomChain)
        {
            return false;
        }

        if (!_consume_skill_costs(active_unit, skill_def, cast_variant, batch))
        {
            return false;
        }
        GDictionary costs = _get_effective_skill_costs(active_unit, skill_def);
        _record_action_issued(
            active_unit,
            BattleCommand.TYPE_SKILL(),
            GdInterop.GetInt(costs, "ap_cost", combatProfile?.ap_cost ?? 0)
        );
        _append_changed_unit_id(batch, active_unit.unit_id);

        GDictionary spellControlContext = _resolve_unit_spell_control_after_cost(
            active_unit,
            skill_def,
            batch
        );
        if (GdInterop.GetBool(spellControlContext, "skip_effects", false))
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

        foreach (var targetUnitValue in targetUnits)
        {
            var targetUnit = targetUnitValue.AsGodotObject() as BattleUnitState;
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
        GDictionary spell_control_context
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        int maxHitsPerTarget = Math.Max(combatProfile?.max_hits_per_target ?? 0, 1);
        var chainHitCounts = new GDictionary();
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
            GdInterop
                .GetArray(batch, "log_lines")
                .Add(
                    $"{GdInterop.GetString(active_unit, "display_name")} 的{skillLabel}锁定了 {GdInterop.GetString(targetUnit, "display_name")}。"
                );
            StringName targetId = GdInterop.GetStringName(targetUnit, "unit_id");
            chainHitCounts[targetId] = GdInterop.GetInt(chainHitCounts, targetId, 0) + 1;
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
            GdInterop
                .GetArray(batch, "log_lines")
                .Add(
                    $"{GdInterop.GetString(active_unit, "display_name")} 的{skillLabel}执行了 {attemptCount} 次攻击链判定。"
                );
        }
        return applied;
    }

    public GArray _build_random_chain_target_pool(
        BattleUnitState active_unit,
        SkillDef skill_def,
        GDictionary chain_hit_counts,
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
                GdInterop.IsEmpty(candidateId)
                || GdInterop.GetInt(chain_hit_counts, candidateId, 0) >= max_hits_per_target
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
        GDictionary validation = _validate_ground_skill_command(
            active_unit,
            skill_def,
            cast_variant,
            command
        );
        if (!GdInterop.GetBool(validation, "allowed", false))
        {
            return false;
        }

        var targetCoords = new GVector2IArray();
        foreach (Vector2I targetCoord in ToVec2IArray(GdInterop.GetArray(validation, "target_coords")))
        {
            targetCoords.Add(targetCoord);
        }
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
        GDictionary costs = _get_effective_skill_costs(active_unit, skill_def);
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        _record_action_issued(
            active_unit,
            BattleCommand.TYPE_SKILL(),
            GdInterop.GetInt(costs, "ap_cost", combatProfile?.ap_cost ?? 0)
        );
        _append_changed_unit_id(batch, active_unit?.unit_id ?? new StringName(""));
        BattleChargeResolver chargeResolver = Runtime?._charge_resolver;
        if (chargeResolver != null && chargeResolver.is_charge_option(cast_variant))
        {
            return chargeResolver.handle_charge_skill_command(
                active_unit,
                skill_def,
                cast_variant,
                validation,
                batch
            );
        }
        GDictionary spellControlContext = _resolve_ground_spell_control_after_cost(
            active_unit,
            skill_def,
            spentMp,
            batch
        );
        if (GdInterop.GetBool(spellControlContext, "skip_effects", false))
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

        GDictionary driftContext =
            Runtime
                ?._magic_backlash_resolver.build_ground_backlash_target_coords(
                    skill_def as SkillDef,
                    targetCoords,
                    Runtime.get_state(),
                    Runtime.get_grid_service(),
                    spellControlContext
                ) ?? new GDictionary { ["target_coords"] = targetCoords };
        if (GdInterop.GetBool(driftContext, "backlash_triggered", false))
        {
            var driftTargetCoords = new GVector2IArray();
            foreach (Vector2I driftCoord in ToVec2IArray(GdInterop.GetArray(driftContext, "target_coords")))
            {
                driftTargetCoords.Add(driftCoord);
            }
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
                ? GdInterop.GetVector2I(active_unit, "coord")
                : new Vector2I(-1, -1),
            active_unit,
            cast_variant
        );
        GDictionary unitResult = _apply_ground_unit_effects(
            active_unit,
            skill_def,
            _collect_ground_unit_effect_defs(skill_def, cast_variant, active_unit),
            effectCoords,
            batch,
            targetCoords
        );
        GDictionary terrainResult = _apply_ground_terrain_effects(
            active_unit,
            skill_def,
            _collect_ground_terrain_effect_defs(skill_def, cast_variant, active_unit),
            effectCoords,
            batch
        );
        bool applied =
            GdInterop.GetBool(unitResult, "applied", false)
            || GdInterop.GetBool(terrainResult, "applied", false);

        if (applied)
        {
            GdInterop
                .GetArray(batch, "log_lines")
                .Add(
                    $"{GdInterop.GetString(active_unit, "display_name")} 使用 {_format_skill_variant_label(skill_def, cast_variant)}，影响了 {effectCoords.Count} 个地格、{GdInterop.GetInt(unitResult, "affected_unit_count", 0)} 个单位。"
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
        var result = new GDictionary
        {
            ["allowed"] = false,
            ["message"] = "技能或目标无效。",
            ["target_unit_ids"] = new GArray(),
            ["target_units"] = new GArray(),
            ["preview_coords"] = new GArray(),
        };
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
            return result;
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
            return result;
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
                new GDictionary(),
                maxHitsPerTarget
            );
            if (randomChainPool.Count == 0)
            {
                result["message"] = "没有可用的随机连击目标。";
                return result;
            }
            result["allowed"] = true;
            result["message"] = "";
            result["target_unit_ids"] = new GArray();
            result["target_units"] = new GArray();
            var candidateUnitIds = new GStringNameArray();
            foreach (var candidateValue in randomChainPool)
            {
                var candidate = candidateValue.AsGodotObject() as BattleUnitState;
                if (candidate != null)
                {
                    candidateUnitIds.Add(GdInterop.GetStringName(candidate, "unit_id"));
                }
            }
            result["random_chain_candidate_unit_ids"] = candidateUnitIds;
            return result;
        }
        if (targetUnitIds.Count < minTargetCount)
        {
            result["message"] = $"至少需要选择 {minTargetCount} 个单位目标。";
            return result;
        }
        if (targetUnitIds.Count > maxTargetCount)
        {
            result["message"] = $"最多只能选择 {maxTargetCount} 个单位目标。";
            return result;
        }
        if (!_is_multi_unit_skill(skill_def) && targetUnitIds.Count != 1)
        {
            result["message"] = "当前技能只允许选择 1 个单位目标。";
            return result;
        }
        if (combatProfile.selection_order_mode != "manual")
        {
            targetUnitIds = _sort_target_unit_ids_for_execution(targetUnitIds);
        }

        var targetUnits = new GArray();
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
                result["message"] = specialValidationMessage;
                return result;
            }
            if (
                targetUnit == null
                || !_can_skill_target_unit(active_unit, targetUnit, skill_def as SkillDef, true, cast_variant as CombatCastVariantDef)
            )
            {
                result["message"] = "技能目标超出范围或不满足筛选条件。";
                return result;
            }
            targetUnits.Add(targetUnit);
        }

        result["allowed"] = true;
        result["message"] = "";
        result["target_unit_ids"] = targetUnitIds;
        result["target_units"] = targetUnits;
        var emptyTargetCoords = new GVector2IArray();
        GDictionary collectedTargetCoords =
            Runtime?._target_collection_service.collect_combat_profile_target_coords(
                state,
                Runtime.get_grid_service(),
                active_unit != null ? active_unit.coord : new Vector2I(-1, -1),
                combatProfile,
                ToUntypedArray(emptyTargetCoords),
                active_unit,
                targetUnits,
                skillLevel
            ) ?? new GDictionary();
        result["preview_coords"] = _sort_coords(
            GdInterop.GetArray(collectedTargetCoords, "target_coords")
        );
        return result;
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
        var seenIds = new GDictionary();
        StringName singleTargetId = ProgressionDataUtils.to_string_name(
            GdInterop.GetStringName(command, "target_unit_id")
        );
        if (!GdInterop.IsEmpty(singleTargetId))
        {
            seenIds[singleTargetId] = true;
            targetUnitIds.Add(singleTargetId);
        }
        foreach (var targetUnitIdValue in GdInterop.GetArray(command, "target_unit_ids"))
        {
            StringName targetUnitId = ProgressionDataUtils.to_string_name(targetUnitIdValue);
            if (
                GdInterop.IsEmpty(targetUnitId)
                || (!allow_repeat && seenIds.ContainsKey(targetUnitId))
            )
            {
                continue;
            }
            seenIds[targetUnitId] = true;
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
                var unitA = GdInterop.GetObject(units, a) as BattleUnitState;
                var unitB = GdInterop.GetObject(units, b) as BattleUnitState;
                if (unitA == null || unitB == null)
                {
                    return string.CompareOrdinal(a.ToString(), b.ToString());
                }
                Vector2I ca = GdInterop.GetVector2I(unitA, "coord");
                Vector2I cb = GdInterop.GetVector2I(unitB, "coord");
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
        GDictionary costs = _get_effective_skill_costs(active_unit, skill_def);
        if (
            require_ap
            && active_unit.current_ap
                < GdInterop.GetInt(costs, "ap_cost", combatProfile.ap_cost)
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
        BattleSkillResolutionRules skillResolutionRules = Runtime?._skill_resolution_rules;
        BattleDamageResolver damageResolver = Runtime?._damage_resolver;
        if (damageResolver == null)
            return new GDictionary();
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
            GDictionary attackCheck =
                attackPolicy?.build_attack_check(policyContext, 0, 0) ?? new GDictionary();
            var attackContext = new GDictionary
            {
                ["battle_state"] = RtState(),
                ["skill_id"] = skill_def?.skill_id ?? new StringName(""),
            };
            if (skillResolutionRules?.is_force_hit_no_crit_skill(skill_def) == true)
            {
                attackContext["force_hit_no_crit"] = true;
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
            return result;
        }
        if (effect_defs.Count != 0)
        {
            return damageResolver.resolve_effects(
                active_unit,
                target_unit,
                ToUntypedArray(effect_defs),
                new GDictionary { ["skill_id"] = skill_def?.skill_id ?? new StringName("") }
            );
        }
        return damageResolver.resolve_skill(active_unit, target_unit, skill_def);
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
        GDictionary spell_control_context = null
    )
    {
        spell_control_context ??= new GDictionary();
        effect_defs ??= new GCombatEffectArray();
        BattleLayeredBarrierService layeredBarrierService = Runtime?._layered_barrier_service;
        GDictionary barrierResult =
            layeredBarrierService != null
                ? layeredBarrierService.ResolveSkillBarrierInteraction(
                    active_unit,
                    target_unit,
                    skill_def,
                    ToUntypedArray(effect_defs),
                    batch
                )
                : new GDictionary();
        if (GdInterop.GetBool(barrierResult, "blocked", false))
        {
            return GdInterop.GetBool(barrierResult, "applied", false);
        }
        GDictionary result = _resolve_unit_skill_effect_result(
            active_unit,
            target_unit,
            skill_def,
            effect_defs
        );
        BattleSkillMasteryService skillMasteryService = Runtime?._skill_mastery_service;
        skillMasteryService?.RecordTargetResult(
            active_unit,
            target_unit,
            skill_def,
            result,
            ToUntypedArray(effect_defs)
        );
        _flush_last_stand_mastery_records(batch);
        GDictionary guardMasteryGrant =
            skillMasteryService?.BuildGuardMasteryGrantFromIncomingHit(
                active_unit,
                target_unit,
                ToUntypedArray(effect_defs),
                result,
                SkillDefs()
            ) ?? new GDictionary();
        var shieldRollContext = new GDictionary();
        GDictionary shieldResult = _apply_unit_shield_effects(
            active_unit,
            target_unit,
            skill_def,
            effect_defs,
            shieldRollContext
        );
        mark_applied_statuses_for_turn_timing(
            target_unit,
            GdInterop.GetArray(result, "status_effect_ids")
        );
        _append_changed_unit_id(batch, target_unit?.unit_id ?? new StringName(""));
        _append_changed_unit_coords(batch, target_unit);
        append_result_source_status_effects(batch, active_unit, result);
        GDictionary specialResult = _apply_unit_skill_special_effects(
            active_unit,
            target_unit,
            skill_def,
            cast_variant,
            effect_defs,
            batch
        );
        mark_applied_statuses_for_turn_timing(
            target_unit,
            GdInterop.GetArray(specialResult, "status_effect_ids")
        );
        bool applied =
            GdInterop.GetBool(result, "applied", false)
            || GdInterop.GetBool(shieldResult, "applied", false)
            || GdInterop.GetBool(specialResult, "applied", false);
        if (!applied)
        {
            _append_result_report_entry(batch, result);
            foreach (var customLineValue in GdInterop.GetArray(result, "custom_log_lines"))
            {
                string customLine = customLineValue.AsString();
                if (!string.IsNullOrEmpty(customLine))
                {
                    GdInterop.GetArray(batch, "log_lines").Add(customLine);
                }
            }
            foreach (var specialLineValue in GdInterop.GetArray(specialResult, "log_lines"))
            {
                string specialLine = specialLineValue.AsString();
                if (!string.IsNullOrEmpty(specialLine))
                {
                    GdInterop.GetArray(batch, "log_lines").Add(specialLine);
                }
            }
            return false;
        }

        string skillLabel = _format_skill_variant_label(skill_def, cast_variant);
        string skillSubject = _build_skill_log_subject_label(active_unit, skill_def, cast_variant);
        int damage = GdInterop.GetInt(result, "damage", 0);
        int healing = GdInterop.GetInt(result, "healing", 0);
        int movedSteps = GdInterop.GetInt(specialResult, "moved_steps", 0);
        _record_vajra_body_mastery_from_incoming_damage(
            active_unit,
            target_unit,
            skill_def,
            result,
            batch
        );
        if (movedSteps > 0)
        {
            GdInterop
                .GetArray(batch, "log_lines")
                .Add(
                    $"{GdInterop.GetString(active_unit, "display_name")} 使用 {skillLabel}，向更安全位置移动 {movedSteps} 格。"
                );
        }
        append_damage_result_log_lines(
            batch,
            skillSubject,
            GdInterop.GetString(target_unit, "display_name"),
            result
        );
        _apply_equipment_durability_result(target_unit, result, batch);
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
            GdInterop
                .GetArray(batch, "log_lines")
                .Add(
                    $"{skillSubject} 为 {GdInterop.GetString(target_unit, "display_name")} 恢复 {healing} 点生命。"
                );
        }
        if (GdInterop.GetBool(shieldResult, "applied", false))
        {
            GdInterop
                .GetArray(batch, "log_lines")
                .Add(
                    $"{skillSubject} 使 {GdInterop.GetString(target_unit, "display_name")} 的护盾值变为 {GdInterop.GetInt(shieldResult, "current_shield_hp", 0)}。"
                );
        }
        foreach (var statusId in GdInterop.GetArray(result, "status_effect_ids"))
        {
            GdInterop
                .GetArray(batch, "log_lines")
                .Add($"{GdInterop.GetString(target_unit, "display_name")} 获得状态 {statusId}。");
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
        foreach (var customLineValue in GdInterop.GetArray(result, "custom_log_lines"))
        {
            string customLine = customLineValue.AsString();
            if (!string.IsNullOrEmpty(customLine))
            {
                GdInterop.GetArray(batch, "log_lines").Add(customLine);
            }
        }
        foreach (var specialLineValue in GdInterop.GetArray(specialResult, "log_lines"))
        {
            string specialLine = specialLineValue.AsString();
            if (!string.IsNullOrEmpty(specialLine))
            {
                GdInterop.GetArray(batch, "log_lines").Add(specialLine);
            }
        }
        GArray terrainEffectIds = GdInterop.GetArray(result, "terrain_effect_ids");
        if (terrainEffectIds.Count != 0)
        {
            BattleGridService gridService = Runtime?.get_grid_service();
            foreach (var terrainEffectId in terrainEffectIds)
            {
                BattleCellState targetCell = gridService?.get_cell(RtState(), target_unit.coord);
                if (
                    targetCell != null
                    && !GdInterop
                        .GetArray(targetCell, "terrain_effect_ids")
                        .Contains(terrainEffectId)
                )
                {
                    GdInterop.GetArray(targetCell, "terrain_effect_ids").Add(terrainEffectId);
                    _append_changed_coord(batch, target_unit.coord);
                    GdInterop
                        .GetArray(batch, "log_lines")
                        .Add(
                            $"{skillSubject} 使 {GdInterop.GetString(target_unit, "display_name")} 所在的地格附加效果 {terrainEffectId}。"
                        );
                }
            }
        }
        int heightDelta = GdInterop.GetInt(result, "height_delta", 0);
        Vector2I targetCoord = target_unit.coord;
        BattleGridService gridService2 = Runtime?.get_grid_service();
        BattleCellState targetCellBefore = gridService2?.get_cell(RtState(), targetCoord);
        int beforeHeight =
            targetCellBefore != null ? GdInterop.GetInt(targetCellBefore, "current_height") : 0;
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
                    ? GdInterop.GetInt(targetCellAfter, "current_height")
                    : beforeHeight + heightDelta;
            GdInterop
                .GetArray(batch, "log_lines")
                .Add(
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
                $"{GdInterop.GetString(target_unit, "display_name")} 被击倒。",
                new GDictionary { ["record_enemy_defeated_achievement"] = true }
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
        _apply_skill_mastery_grant(target_unit, guardMasteryGrant, batch);
        return true;
    }

    public void _apply_equipment_durability_result(
        BattleUnitState target_unit,
        GDictionary result,
        BattleEventBatch batch
    )
    {
        if (target_unit == null || batch == null)
        {
            return;
        }
        bool destroyedAny = false;
        foreach (GDictionary eventDict in GdInterop.ReadDictionaryItems(
            GdInterop.GetArray(result, "equipment_durability_events")
        ))
        {
            string itemId = GdInterop.GetString(eventDict, "item_id", "");
            if (string.IsNullOrEmpty(itemId))
            {
                itemId = "装备";
            }
            GDictionary saveResult = GdInterop.GetDictionary(eventDict, "save_result");
            if (
                saveResult.Count > 0
                && GdInterop.GetBool(saveResult, "has_save", false)
                && GdInterop.GetBool(saveResult, "success", false)
            )
            {
                GdInterop
                    .GetArray(batch, "log_lines")
                    .Add(
                        $"{GdInterop.GetString(target_unit, "display_name")} 的 {itemId} 抵抗了裂解术。"
                    );
                continue;
            }
            int durabilityLoss = GdInterop.GetInt(eventDict, "durability_loss", 0);
            if (durabilityLoss <= 0)
            {
                continue;
            }
            if (GdInterop.GetBool(eventDict, "destroyed", false))
            {
                destroyedAny = true;
                batch.log_lines.Add($"{target_unit.display_name} 的 {itemId} 被裂解为尘埃。");
            }
            else
            {
                batch.log_lines.Add(
                    $"{target_unit.display_name} 的 {itemId} 被裂解，耐久 {GdInterop.GetInt(eventDict, "durability_before", 0)} -> {GdInterop.GetInt(eventDict, "durability_after", 0)}。"
                );
            }
        }
        if (destroyedAny)
        {
            _refresh_target_after_equipment_destruction(target_unit);
            _append_changed_unit_id(batch, GdInterop.GetStringName(target_unit, "unit_id"));
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
        foreach (GDictionary eventDict in GdInterop.ReadDictionaryItems(
            GdInterop.GetArray(result, "dispel_events")
        ))
        {
            GArray removedIds = GdInterop.GetArray(
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
        if (!GdInterop.IsEmpty(GdInterop.GetStringName(target_unit, "source_member_id")))
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
        target_unit.Set(
            "current_hp",
            Math.Clamp(
                GdInterop.GetInt(target_unit, "current_hp"),
                0,
                Math.Max(snapshot.get_value(AttributeService.HP_MAX_ID()), 1)
            )
        );
        target_unit.Set(
            "current_mp",
            Math.Clamp(
                GdInterop.GetInt(target_unit, "current_mp"),
                0,
                Math.Max(snapshot.get_value(AttributeService.MP_MAX_ID()), 0)
            )
        );
        target_unit.Set(
            "current_stamina",
            Math.Clamp(
                GdInterop.GetInt(target_unit, "current_stamina"),
                0,
                Math.Max(snapshot.get_value(AttributeService.STAMINA_MAX_ID()), 0)
            )
        );
        target_unit.Set(
            "current_aura",
            Math.Clamp(
                GdInterop.GetInt(target_unit, "current_aura"),
                0,
                Math.Max(snapshot.get_value(AttributeService.AURA_MAX_ID()), 0)
            )
        );
        target_unit.Set("is_alive", GdInterop.GetInt(target_unit, "current_hp") > 0);
    }

    public void _apply_chain_damage_effects(
        BattleUnitState source_unit,
        BattleUnitState primary_target,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        GDictionary primary_result,
        BattleEventBatch batch,
        string skill_subject,
        GDictionary spell_control_context = null
    )
    {
        spell_control_context ??= new GDictionary();
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
        if (!GdInterop.GetBool(primary_result, "applied", false))
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
                if (chainTarget == null || !GdInterop.GetBool(chainTarget, "is_alive"))
                {
                    continue;
                }
                GDictionary chainResult = damageResolver?.resolve_effects(
                    source_unit,
                    chainTarget,
                    ToUntypedArray(chainTargetEffects),
                    new GDictionary { ["skill_id"] = skill_def?.skill_id ?? new StringName("") }
                ) ?? new GDictionary();
                skillMasteryService?.RecordTargetResult(
                    source_unit,
                    chainTarget,
                    skill_def,
                    chainResult,
                    ToUntypedArray(chainTargetEffects)
                );
                mark_applied_statuses_for_turn_timing(
                    chainTarget,
                    GdInterop.GetArray(chainResult, "status_effect_ids")
                );
                if (!GdInterop.GetBool(chainResult, "applied", false))
                {
                    continue;
                }

                _append_changed_unit_id(batch, GdInterop.GetStringName(source_unit, "unit_id"));
                _append_changed_unit_id(batch, GdInterop.GetStringName(chainTarget, "unit_id"));
                _append_changed_unit_coords(batch, chainTarget);
                append_result_source_status_effects(batch, source_unit, chainResult);
                append_damage_result_log_lines(
                    batch,
                    $"{skill_subject} 的连锁闪电",
                    GdInterop.GetString(chainTarget, "display_name"),
                    chainResult
                );
                foreach (var statusId in GdInterop.GetArray(chainResult, "status_effect_ids"))
                {
                    GdInterop
                        .GetArray(batch, "log_lines")
                        .Add(
                            $"{GdInterop.GetString(chainTarget, "display_name")} 获得状态 {statusId}。"
                        );
                }

                int chainDamage = GdInterop.GetInt(chainResult, "damage", 0);
                int chainHealing = GdInterop.GetInt(chainResult, "healing", 0);
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
                        $"{GdInterop.GetString(chainTarget, "display_name")} 被击倒。",
                        new GDictionary { ["record_enemy_defeated_achievement"] = true }
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
        GDictionary spell_control_context = null
    )
    {
        spell_control_context ??= new GDictionary();
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
        GDictionary chainParams = _get_effect_params(chain_effect);
        if (maxRadius <= 0)
        {
            return targets;
        }
        bool preventRepeatTarget = GdInterop.GetBool(chainParams, "prevent_repeat_target", true);
        StringName targetFilter = _resolve_effect_target_filter(skill_def, chain_effect);
        if (GdInterop.IsEmpty(targetFilter))
        {
            return targets;
        }

        BattleGridService gridService = Runtime?.get_grid_service();
        var visited = new GDictionary();
        var queue = new List<BattleUnitState>();
        visited[GdInterop.GetStringName(primary_target, "unit_id")] = true;
        queue.Add(primary_target);

        while (queue.Count != 0)
        {
            BattleUnitState current = queue[0];
            queue.RemoveAt(0);

            foreach (var unitValue in GdInterop.GetDictionary(state, "units").Values)
            {
                var candidate = unitValue.AsGodotObject() as BattleUnitState;
                if (candidate == null || !GdInterop.GetBool(candidate, "is_alive"))
                {
                    continue;
                }
                if (
                    preventRepeatTarget
                    && visited.ContainsKey(GdInterop.GetStringName(candidate, "unit_id"))
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

                visited[GdInterop.GetStringName(candidate, "unit_id")] = true;
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
                Vector2I ca = GdInterop.GetVector2I(a, "coord");
                Vector2I cb = GdInterop.GetVector2I(b, "coord");
                if (ca.Y != cb.Y)
                    return ca.Y.CompareTo(cb.Y);
                if (ca.X != cb.X)
                    return ca.X.CompareTo(cb.X);
                return string.CompareOrdinal(
                    GdInterop.GetStringName(a, "unit_id").ToString(),
                    GdInterop.GetStringName(b, "unit_id").ToString()
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
        GDictionary spell_control_context = null
    )
    {
        spell_control_context ??= new GDictionary();
        GDictionary chainParams = _get_effect_params(chain_effect);
        int baseRadius = Math.Max(GdInterop.GetInt(chainParams, "base_chain_radius", 1), 0);
        StringName bonusEffectId = ProgressionDataUtils.to_string_name(
            GdInterop.GetStringName(chainParams, "bonus_terrain_effect_id")
        );
        int radius = baseRadius;
        if (
            !GdInterop.IsEmpty(bonusEffectId)
            && primary_target != null
            && _unit_stands_on_terrain_effect(primary_target, bonusEffectId)
        )
        {
            radius = Math.Max(
                GdInterop.GetInt(chainParams, "wet_chain_radius", baseRadius),
                baseRadius
            );
        }
        if (GdInterop.GetBool(spell_control_context, "backlash_triggered", false))
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
        if (state == null || unit_state == null || GdInterop.IsEmpty(terrain_effect_id))
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
        if (!GdInterop.IsEmpty(effectTargetFilter))
            return effectTargetFilter;
        return skill_def?.combat_profile?.target_team_filter ?? new StringName("");
    }

    public bool _is_unit_valid_for_effect(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        StringName target_team_filter
    )
    {
        bool madnessAnyTeam =
            source_unit != null
            && GdInterop.GetBool(
                GdInterop.GetDictionary(source_unit, "ai_blackboard"),
                "madness_target_any_team",
                false
            );
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
        if (unit_state == null || GdInterop.IsEmpty(skill_id))
        {
            return 0;
        }
        GDictionary knownSkillLevelMap = GdInterop.GetDictionary(
            unit_state,
            "known_skill_level_map"
        );
        if (knownSkillLevelMap.ContainsKey(skill_id))
        {
            return GdInterop.GetInt(knownSkillLevelMap, skill_id, 0);
        }
        if (_runtime != null)
        {
            SkillDef skillDef = SkillDefs().GetValueOrDefault(skill_id).AsGodotObject() as SkillDef;
            if (
                skillDef != null
                && skillDef.max_level == 0
                && GdInterop.IsEmpty(skillDef.dynamic_max_level_stat_id)
            )
            {
                return 0;
            }
        }
        return GdInterop.GetArray(unit_state, "known_active_skill_ids").Contains(skill_id) ? 1 : 0;
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

    private static BattleRuntimeModule ResolveWeakRef(WeakReference<BattleRuntimeModule> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out BattleRuntimeModule target))
            return null;
        return target;
    }
}
