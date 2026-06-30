using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// 翻译自 battle_skill_execution_orchestrator.gd（2026-05-26，技能执行编排器 C# 迁移）。
// runtime 强耦合：执行编排器只直连 BattleRuntimeModule，不走 Godot 动态调用。
internal sealed partial class BattleSkillExecutionOrchestrator
{
    private static readonly StringName STATUS_GUARDING = "guarding";
    private readonly record struct ChainDamageParameters(
        int BaseRadius,
        StringName BonusTerrainEffectId,
        int WetChainRadius,
        bool PreventRepeatTarget
    )
    {
        public static ChainDamageParameters FromEffect(CombatEffectDefinition effectDefinition)
        {
            int baseRadius = Math.Max(
                effectDefinition?.GetIntParamTyped("base_chain_radius", 1) ?? 1,
                0
            );
            return new ChainDamageParameters(
                baseRadius,
                effectDefinition?.GetStringNameParamTyped("bonus_terrain_effect_id")
                    ?? new StringName(""),
                Math.Max(
                    effectDefinition?.GetIntParamTyped("wet_chain_radius", baseRadius) ?? baseRadius,
                    baseRadius
                ),
                effectDefinition?.PreventRepeatTarget ?? true
            );
        }
    }

    private WeakReference<BattleRuntimeModule> _runtimeRef;

    internal BattleSkillExecutionOrchestrator()
    {
    }

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    private BattleRuntimeModule Runtime => _runtime;

    internal void Setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
    }

    internal void DisposeRuntime()
    {
        _runtime = null;
    }

    // ============================================================
    // 委托 _runtime 的薄包装
    // ============================================================

    internal void append_result_report_entry(
        BattleEventBatch batch,
        AttackEffectResolutionResult result
    )
    {
        Runtime?.AppendResultReportEntry(batch, result);
    }

    internal void MarkAppliedStatusesForTurnTiming(
        BattleUnitState target_unit,
        GArray status_effect_ids
    )
    {
        Runtime?.MarkAppliedStatusesForTurnTiming(target_unit, status_effect_ids);
    }

    internal void MarkAppliedStatusesForTurnTiming(
        BattleUnitState target_unit,
        GStringNameArray status_effect_ids
    )
    {
        Runtime?.MarkAppliedStatusesForTurnTiming(target_unit, status_effect_ids);
    }

    internal void MarkAppliedStatusesForTurnTiming(
        BattleUnitState target_unit,
        IReadOnlyList<StringName> status_effect_ids
    )
    {
        var typedStatusIds = new GStringNameArray();
        foreach (StringName statusId in status_effect_ids ?? Array.Empty<StringName>())
        {
            typedStatusIds.Add(statusId);
        }
        MarkAppliedStatusesForTurnTiming(target_unit, typedStatusIds);
    }

    internal void append_result_source_status_effects(
        BattleEventBatch batch,
        BattleUnitState source_unit,
        AttackEffectResolutionResult result
    )
    {
        Runtime?.AppendResultSourceStatusEffects(batch, source_unit, result);
    }

    internal void _record_action_issued(
        BattleUnitState unit_state,
        StringName command_type,
        int ap_cost = 0
    )
    {
        Runtime?._record_action_issued(unit_state, command_type, ap_cost);
    }

    internal void _record_skill_attempt(BattleUnitState unit_state, StringName skill_id)
    {
        Runtime?._record_skill_attempt(unit_state, skill_id);
    }

    internal void _record_effect_metrics(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        int damage,
        int healing,
        int kill_count
    )
    {
        Runtime?._record_effect_metrics(source_unit, target_unit, damage, healing, kill_count);
    }

    internal void _record_unit_defeated(BattleUnitState unit_state)
    {
        Runtime?._record_unit_defeated(unit_state);
    }

    internal void _apply_on_kill_gain_resources_effects(
        BattleUnitState source_unit,
        BattleUnitState defeated_unit,
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch
    )
    {
        Runtime?._apply_on_kill_gain_resources_effects(
            source_unit,
            defeated_unit,
            skillDefinition,
            effectDefinitions ?? Array.Empty<CombatEffectDefinition>(),
            batch
        );
    }

    internal BattleSpecialSkillResult ApplyUnitSkillSpecialEffectsResult(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skill_definition,
        CombatCastVariantDefinition cast_variant,
        IEnumerable<CombatEffectDefinition> effect_definitions,
        BattleEventBatch batch,
        BattleForcedMoveContext forced_move_context
    )
    {
        return Runtime?.ApplyUnitSkillSpecialEffectsResult(
                active_unit,
                target_unit,
                skill_definition,
                cast_variant,
                effect_definitions ?? Array.Empty<CombatEffectDefinition>(),
                batch,
                forced_move_context
            ) ?? BattleSpecialSkillResult.Empty();
    }

    internal bool _is_doom_shift_skill(StringName skill_id)
    {
        return Runtime?._is_doom_shift_skill(skill_id) == true;
    }

    internal bool _is_black_crown_seal_skill(StringName skill_id)
    {
        return Runtime?._is_black_crown_seal_skill(skill_id) == true;
    }

    internal bool _is_crown_break_target_eligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        return Runtime?._is_crown_break_target_eligible(active_unit, target_unit) == true;
    }

    internal bool _is_crown_break_skill(StringName skill_id)
    {
        return Runtime?._is_crown_break_skill(skill_id) == true;
    }

    internal bool _is_doom_sentence_target_eligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        return Runtime?._is_doom_sentence_target_eligible(active_unit, target_unit) == true;
    }

    internal bool _is_black_crown_seal_target_eligible(
        BattleUnitState active_unit,
        BattleUnitState target_unit
    )
    {
        return Runtime?._is_black_crown_seal_target_eligible(active_unit, target_unit) == true;
    }

    internal bool _is_doom_sentence_skill(StringName skill_id)
    {
        return Runtime?._is_doom_sentence_skill(skill_id) == true;
    }

    internal void RecordVajraBodyMasteryFromIncomingDamageTyped(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        AttackEffectResolutionResult result,
        BattleEventBatch batch = null
    )
    {
        Runtime?.RecordVajraBodyMasteryFromIncomingDamageTyped(
            sourceUnit,
            targetUnit,
            skillDefinition,
            result,
            batch
        );
    }

    internal BattleSpellControlResult _resolve_ground_spell_control_after_cost_result(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        int spent_mp,
        BattleEventBatch batch
    )
    {
        return Runtime?._resolve_ground_spell_control_after_cost_result(
                active_unit,
                skillDefinition,
                spent_mp,
                batch
            ) ?? BattleSpellControlResult.None();
    }

    internal BattleSpellControlResult _resolve_unit_spell_control_after_cost_result(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        BattleEventBatch batch
    )
    {
        return Runtime?._resolve_unit_spell_control_after_cost_result(
            active_unit,
            skillDefinition,
            batch
        ) ?? BattleSpellControlResult.None();
    }

    internal BattleShieldApplyResult _apply_unit_shield_effects_result(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        SkillDefinition skill_definition,
        IEnumerable<CombatEffectDefinition> effect_definitions,
        Dictionary<long, int> shield_roll_context = null
    )
    {
        shield_roll_context ??= new Dictionary<long, int>();
        if (Runtime == null)
            return new BattleShieldApplyResult(false, 0, 0, -1, new StringName(""));
        return Runtime.ApplyUnitShieldEffectsResult(
            source_unit,
            target_unit,
            skill_definition,
            effect_definitions ?? Array.Empty<CombatEffectDefinition>(),
            shield_roll_context
        );
    }

    internal void _grant_skill_mastery_if_needed(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        BattleEventBatch batch
    )
    {
        if (Runtime == null)
            return;
        Runtime._grant_skill_mastery_if_needed(active_unit, skillDefinition, batch);
    }

    internal void _apply_skill_mastery_grant(
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

    internal void _flush_last_stand_mastery_records(BattleEventBatch batch)
    {
        if (Runtime == null)
            return;
        Runtime._flush_last_stand_mastery_records(batch);
    }

    internal void _append_changed_coord(BattleEventBatch batch, Vector2I coord)
    {
        Runtime?._append_changed_coord(batch, coord);
    }

    internal void _append_changed_unit_id(BattleEventBatch batch, StringName unit_id)
    {
        Runtime?._append_changed_unit_id(batch, unit_id);
    }

    internal void _append_changed_unit_coords(BattleEventBatch batch, BattleUnitState unit_state)
    {
        Runtime?._append_changed_unit_coords(batch, unit_state);
    }

    internal void _collect_defeated_unit_loot(
        BattleUnitState unit_state,
        BattleUnitState killer_unit = null
    )
    {
        Runtime?._collect_defeated_unit_loot(unit_state, killer_unit);
    }

    internal void _clear_defeated_unit(BattleUnitState unit_state, BattleEventBatch batch = null)
    {
        Runtime?._clear_defeated_unit(unit_state, batch);
    }

    internal GVector2IArray _sort_coords(GArray target_coords)
    {
        if (Runtime == null)
            return new Vector2IList().ToGodotArray();
        return Runtime._sort_coords(target_coords);
    }

    internal GVector2IArray _sort_coords(GVector2IArray target_coords)
    {
        if (Runtime == null)
            return new Vector2IList().ToGodotArray();
        return Runtime._sort_coords(target_coords);
    }

    internal string _get_skill_command_block_reason(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        if (Runtime == null)
            return "";
        return Runtime._get_skill_command_block_reason(
            active_unit,
            skillDefinition,
            castVariant
        );
    }

    internal string _get_skill_command_block_reason(
        BattleUnitReadView active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        if (Runtime == null)
            return "";
        return Runtime._get_skill_command_block_reason(
            active_unit,
            skillDefinition,
            castVariant
        );
    }

    internal bool _consume_skill_costs(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant = null,
        BattleEventBatch batch = null
    )
    {
        return Runtime?._consume_skill_costs(
            active_unit,
            skillDefinition,
            castVariant,
            batch
        ) == true;
    }

    internal CombatSkillResourceCosts _get_effective_skill_resource_costs(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition
    )
    {
        return Runtime?._get_effective_skill_resource_costs(active_unit, skillDefinition)
            ?? CombatSkillResourceCosts.Zero;
    }

    internal CombatSkillResourceCosts _get_effective_skill_resource_costs(
        BattleUnitReadView active_unit,
        SkillDefinition skillDefinition
    )
    {
        return Runtime?._skill_turn_resolver?.GetEffectiveSkillResourceCosts(
                active_unit,
                skillDefinition
            ) ?? CombatSkillResourceCosts.Zero;
    }

    internal int _get_effective_skill_range(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition
    )
    {
        return Runtime?._get_effective_skill_range(active_unit, skillDefinition) ?? 0;
    }

    internal int _get_effective_skill_range(
        BattleUnitReadView active_unit,
        SkillDefinition skillDefinition
    )
    {
        return BattleRangeService.GetEffectiveSkillRange(active_unit, skillDefinition);
    }

    // ============================================================
    // 主流程
    // ============================================================

    internal void _handle_skill_command(
        BattleUnitState active_unit,
        BattleCommand command,
        BattleEventBatch batch
    )
    {
        SkillDefinition skillDefinition = Runtime?.GetSkillDefinitionTyped(command.skill_id);
        if (skillDefinition?.CombatProfile == null)
        {
            return;
        }
        bool allowRepeat = skillDefinition.CombatProfile.AllowRepeatTarget;
        BattleSkillResolutionPolicy policy = Runtime?._skill_resolution_rules
            ?.BuildSkillResolutionPolicy(
                skillDefinition,
                active_unit,
                command != null ? command.skill_variant_id : new StringName(""),
                _normalize_target_unit_ids(command, allowRepeat)
            );
        bool routesToUnitTargeting = policy?.RoutesToUnitTargeting == true;
        string optionBlockReason = policy?.OptionErrorMessage ?? "技能或目标无效。";
        if (!string.IsNullOrEmpty(optionBlockReason))
        {
            batch?.AddLogLine(optionBlockReason);
            return;
        }
        string blockReason = _get_skill_command_block_reason(
            active_unit,
            skillDefinition,
            policy?.CommandCastVariantDefinition
        );
        if (!string.IsNullOrEmpty(blockReason))
        {
            batch?.AddLogLine(blockReason);
            return;
        }

        if (
            Runtime?._casting_time_service.TryHandleCastingSkillStart(
                active_unit,
                command,
                skillDefinition,
                policy?.UnitExecutionCastVariantDefinition,
                policy?.GroundCastVariantDefinition,
                routesToUnitTargeting,
                batch
            ) == true
        )
        {
            Runtime?._skill_mastery_service.Clear();
            return;
        }

        bool isMeteorSwarm =
            skillDefinition.CombatProfile.SpecialResolutionProfileId
            == new StringName("meteor_swarm");
        bool routesToGroundTargeting = !routesToUnitTargeting
            && policy?.GroundCastVariantDefinition != null;
        if (CanHandleUnitSkillCommandFromDefinitions(skillDefinition, policy))
        {
            _record_skill_attempt(active_unit, command?.skill_id ?? new StringName(""));
            Runtime?._skill_mastery_service.Clear();
            bool definitionApplied = _handle_unit_skill_command(
                active_unit,
                command,
                skillDefinition,
                policy?.UnitExecutionCastVariantDefinition,
                policy?.EffectDefinitions,
                batch
            );
            if (definitionApplied)
            {
                _grant_skill_mastery_if_needed(active_unit, skillDefinition, batch);
            }
            Runtime?._skill_mastery_service.Clear();
            return;
        }

        if (isMeteorSwarm)
        {
            BattleSpecialProfileGateResult gateResult =
                Runtime._special_profile_gate != null
                    ? Runtime._special_profile_gate.CanExecuteSkill(
                        skillDefinition,
                        command,
                        active_unit,
                        Runtime._state
                    )
                    : null;
            if (gateResult == null || !gateResult.Allowed)
            {
                Runtime._append_special_profile_gate_block(batch, gateResult);
                return;
            }
            Runtime._skill_mastery_service.Clear();
            bool meteorApplied = _handle_meteor_swarm_skill_command(
                active_unit,
                command,
                skillDefinition,
                policy?.GroundCastVariantDefinition,
                batch
            );
            if (meteorApplied)
            {
                _grant_skill_mastery_if_needed(active_unit, skillDefinition, batch);
            }
            Runtime._skill_mastery_service.Clear();
            return;
        }

        if (!routesToUnitTargeting && policy?.GroundCastVariantDefinition != null)
        {
            if (
                Runtime?._casting_time_service.TryHandleCastingSkillStart(
                    active_unit,
                    command,
                    skillDefinition,
                    null,
                    policy.GroundCastVariantDefinition,
                    routesToUnitTargeting,
                    batch
                ) == true
            )
            {
                Runtime?._skill_mastery_service.Clear();
                return;
            }
            _record_skill_attempt(active_unit, command?.skill_id ?? new StringName(""));
            Runtime?._skill_mastery_service.Clear();
            bool definitionApplied = _handle_ground_skill_command(
                active_unit,
                command,
                skillDefinition,
                policy.GroundCastVariantDefinition,
                batch
            );
            if (definitionApplied)
            {
                _grant_skill_mastery_if_needed(active_unit, skillDefinition, batch);
            }
            Runtime?._skill_mastery_service.Clear();
            return;
        }

        Runtime?._skill_mastery_service.Clear();
        return;
    }

    internal bool ResolvePendingCast(
        BattleUnitState active_unit,
        BattlePendingCastState pending_cast,
        BattleEventBatch batch
    )
    {
        if (active_unit == null || pending_cast == null || Runtime == null)
        {
            return false;
        }
        SkillDefinition skillDefinition = Runtime.GetSkillDefinitionTyped(pending_cast.SkillId);
        CombatCastVariantDefinition castVariantDefinition =
            pending_cast.TargetMode == BattleTargetMode.Ground
                ? Runtime?._skill_resolution_rules?.ResolveGroundCastVariantDefinition(
                    skillDefinition,
                    active_unit,
                    pending_cast.VariantId
                )
                : Runtime?._skill_resolution_rules?.ResolveUnitCastVariantDefinition(
                    skillDefinition,
                    active_unit,
                    pending_cast.VariantId
                );
        BattleSpellControlResult spellControlContext = BattleSpellControlResult.None(
            pending_cast.SpellControlMetadata ?? BattleSpellControlMetadata.Empty()
        );
        if (
            pending_cast.TargetMode != BattleTargetMode.Ground
            && CanApplyPendingUnitCastFromDefinitions(skillDefinition, castVariantDefinition, active_unit)
        )
        {
            Runtime._skill_mastery_service.Clear();
            List<CombatEffectDefinition> effectDefinitions = CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariantDefinition,
                active_unit
            );
            bool definitionApplied = ResolvePendingUnitCast(
                active_unit,
                pending_cast,
                skillDefinition,
                castVariantDefinition,
                effectDefinitions,
                batch,
                spellControlContext
            );
            if (definitionApplied)
            {
                _grant_skill_mastery_if_needed(active_unit, skillDefinition, batch);
            }
            Runtime._skill_mastery_service.Clear();
            return definitionApplied;
        }
        if (
            pending_cast.TargetMode == BattleTargetMode.Ground
            && skillDefinition?.CombatProfile != null
            && castVariantDefinition != null
        )
        {
            Runtime._skill_mastery_service.Clear();
            bool definitionApplied = ResolvePendingGroundCast(
                active_unit,
                pending_cast,
                skillDefinition,
                castVariantDefinition,
                batch
            );
            if (definitionApplied)
            {
                _grant_skill_mastery_if_needed(active_unit, skillDefinition, batch);
            }
            Runtime._skill_mastery_service.Clear();
            return definitionApplied;
        }

        Runtime._skill_mastery_service.Clear();
        return false;
    }

    private bool ResolvePendingUnitCast(
        BattleUnitState activeUnit,
        BattlePendingCastState pendingCast,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch,
        BattleSpellControlResult spellControlContext
    )
    {
        BattleState state = RtState();
        if (state == null)
        {
            return false;
        }
        BattleRepeatAttackResolver repeatAttackResolver = Runtime?._repeat_attack_resolver;
        CombatEffectDefinition repeatAttackEffect =
            repeatAttackResolver?.get_repeat_attack_effect_def(effectDefinitions);
        bool applied = false;
        foreach (StringName targetUnitId in pendingCast.TargetUnitIds)
        {
            if (!state.TryGetUnitTyped(targetUnitId, out BattleUnitState targetUnit) || !targetUnit.is_alive)
            {
                continue;
            }
            if (repeatAttackEffect != null)
            {
                if (
                    repeatAttackResolver != null
                    && repeatAttackResolver.ApplyRepeatAttackSkillResult(
                        activeUnit,
                        targetUnit,
                        skillDefinition,
                        effectDefinitions,
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
                    activeUnit,
                    targetUnit,
                    skillDefinition,
                    castVariantDefinition,
                    effectDefinitions,
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

    private bool ResolvePendingGroundCast(
        BattleUnitState activeUnit,
        BattlePendingCastState pendingCast,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleEventBatch batch
    )
    {
        IReadOnlyList<Vector2I> targetCoords = pendingCast.TargetCoords;
        if (targetCoords == null || targetCoords.Count == 0)
        {
            return false;
        }
        if (
            Runtime?.ApplyGroundPrecastSpecialEffectsTyped(
                activeUnit,
                skillDefinition,
                castVariantDefinition,
                targetCoords,
                batch
            ) != true
        )
        {
            return false;
        }
        IReadOnlyList<Vector2I> effectCoords = Runtime.BuildGroundEffectCoordsTyped(
            skillDefinition,
            targetCoords,
            activeUnit != null ? activeUnit.coord : new Vector2I(-1, -1),
            activeUnit,
            castVariantDefinition
        );
        BattleGroundUnitEffectsResult unitResult = Runtime.ApplyGroundUnitEffectsResultTyped(
            activeUnit,
            skillDefinition,
            castVariantDefinition,
            Runtime.CollectGroundUnitEffectDefinitionsTyped(
                skillDefinition,
                castVariantDefinition,
                activeUnit
            ),
            effectCoords,
            batch,
            targetCoords
        );
        BattleGroundTerrainEffectsResult terrainResult =
            Runtime.ApplyGroundTerrainEffectsResultTyped(
                activeUnit,
                skillDefinition,
                Runtime.CollectGroundTerrainEffectDefinitionsTyped(
                    skillDefinition,
                    castVariantDefinition,
                    activeUnit
                ),
                effectCoords,
                batch
            );
        bool applied = unitResult.Applied || terrainResult.Applied;
        if (applied)
        {
            batch?.AddLogLine(
                $"{activeUnit.display_name} 使用 {_format_skill_variant_label(skillDefinition, castVariantDefinition)}，影响了 {effectCoords.Count} 个地格、{unitResult.AffectedUnitCount} 个单位。"
            );
        }
        return applied;
    }

    internal void _preview_skill_command(
        BattleUnitReadView active_unit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        AiTraceRecorder.Enter("preview:skill.orchestrator");
        _preview_skill_command_impl(active_unit, command, preview);
        AiTraceRecorder.Exit("preview:skill.orchestrator");
    }

    internal void _preview_skill_command_impl(
        BattleUnitReadView active_unit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        SkillDefinition skillDefinition = Runtime?.GetSkillDefinitionTyped(command.skill_id);
        if (skillDefinition?.CombatProfile == null)
        {
            preview.AddLogLine("技能或目标无效。");
            return;
        }
        var runtime = _runtime as BattleRuntimeModule;
        bool isMeteorSwarm =
            skillDefinition.CombatProfile.SpecialResolutionProfileId
            == new StringName("meteor_swarm");
        if (runtime != null && isMeteorSwarm)
        {
            AiTraceRecorder.Enter("preview:skill.meteor_gate");
            BattleSpecialProfileGateResult gateResult = runtime._special_profile_gate != null
                ? runtime._special_profile_gate.PreviewSkill(
                    skillDefinition,
                    command,
                    active_unit,
                    runtime._state
                )
                : null;
            AiTraceRecorder.Exit("preview:skill.meteor_gate");
            preview.special_profile_gate_result = gateResult;
            if (gateResult == null || !gateResult.Allowed)
            {
                if (
                    gateResult != null
                    && !string.IsNullOrEmpty(gateResult.PlayerMessage)
                )
                {
                    preview.AddLogLine(gateResult.PlayerMessage);
                }
                else
                {
                    preview.AddLogLine("该禁咒配置未通过校验，暂时无法施放。");
                }
                return;
            }
            string blockReason = _get_skill_command_block_reason(active_unit, skillDefinition, null);
            if (!string.IsNullOrEmpty(blockReason))
            {
                preview.AddLogLine(blockReason);
                return;
            }
            if (runtime._meteor_swarm_resolver != null)
            {
                runtime._meteor_swarm_resolver.PopulatePreview(
                    active_unit,
                    command,
                    skillDefinition,
                    preview
                );
                return;
            }
            preview.allowed = false;
            preview.AddLogLine("该禁咒结算尚未接入。");
            return;
        }
        AiTraceRecorder.Enter("preview:skill.resolve_options");
        bool allowRepeat = skillDefinition.CombatProfile.AllowRepeatTarget;
        BattleSkillResolutionPolicy policy = Runtime?._skill_resolution_rules
            ?.BuildSkillResolutionPolicy(
                skillDefinition,
                active_unit,
                command != null ? command.skill_variant_id : new StringName(""),
                _normalize_target_unit_ids(command, allowRepeat)
            );
        bool routesToUnitTargeting = policy?.RoutesToUnitTargeting == true;
        AiTraceRecorder.Exit("preview:skill.resolve_options");
        AiTraceRecorder.Enter("preview:skill.option_block");
        string optionBlockReason = policy?.OptionErrorMessage ?? "技能或目标无效。";
        AiTraceRecorder.Exit("preview:skill.option_block");
        if (!string.IsNullOrEmpty(optionBlockReason))
        {
            preview.AddLogLine(optionBlockReason);
            return;
        }

        if (routesToUnitTargeting)
        {
            _preview_unit_skill_command(
                active_unit,
                command,
                skillDefinition,
                policy?.UnitExecutionCastVariantDefinition,
                preview
            );
            return;
        }

        bool routesToGroundTargeting = !routesToUnitTargeting
            && policy?.GroundCastVariantDefinition != null;
        if (routesToGroundTargeting)
        {
            _preview_ground_skill_command(
                active_unit,
                command,
                skillDefinition,
                policy.GroundCastVariantDefinition,
                preview
            );
            return;
        }

        preview.AddLogLine("技能或目标无效。");
    }

    internal bool _handle_meteor_swarm_skill_command(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleEventBatch batch
    )
    {
        BattleMeteorSwarmResolver meteorResolver = Runtime?._meteor_swarm_resolver;
        BattleSkillOutcomeCommitter outcomeCommitter = Runtime?._skill_outcome_committer;
        if (_runtime == null || meteorResolver == null || outcomeCommitter == null)
        {
            batch?.AddLogLine("该禁咒结算尚未接入。");
            return false;
        }
        BattleGroundSkillValidationResult validation =
            Runtime?.ValidateGroundSkillCommandResultTyped(
                active_unit,
                skillDefinition,
                castVariantDefinition,
                command
            ) ?? BattleGroundSkillValidationResult.Denied("地面技能目标无效。");
        if (!validation.Allowed)
        {
            batch?.AddLogLine(
                string.IsNullOrEmpty(validation.Message) ? "技能或目标无效。" : validation.Message
            );
            return false;
        }
        IReadOnlyList<Vector2I> targetCoords = validation.TargetCoords ?? Array.Empty<Vector2I>();
        if (targetCoords.Count == 0)
        {
            batch?.AddLogLine("技能或目标无效。");
            return false;
        }

        int mpBeforeCost = active_unit?.current_mp ?? 0;
        if (!_consume_skill_costs(active_unit, skillDefinition, castVariantDefinition, batch))
        {
            return false;
        }
        _record_skill_attempt(active_unit, command?.skill_id ?? new StringName(""));
        int spentMp = Math.Max(mpBeforeCost - (active_unit?.current_mp ?? 0), 0);
        CombatSkillResourceCosts costs = _get_effective_skill_resource_costs(
            active_unit,
            skillDefinition
        );
        _record_action_issued(
            active_unit,
            BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            costs.ApCost
        );
        _append_changed_unit_id(batch, active_unit?.unit_id ?? new StringName(""));

        BattleSpellControlResult spellControlContext = _resolve_ground_spell_control_after_cost_result(
            active_unit,
            skillDefinition,
            spentMp,
            batch
        );
        if (spellControlContext.SkipEffects)
        {
            return false;
        }

        BattleGroundBacklashTargetResult driftContext =
            Runtime
                ?._magic_backlash_resolver.BuildGroundBacklashTargetCoordsResult(
                    skillDefinition,
                    targetCoords,
                    Runtime.GetState(),
                    Runtime.GetGridService(),
                    spellControlContext
                ) ?? BattleGroundBacklashTargetResult.None(targetCoords);
        IReadOnlyList<Vector2I> finalTargetCoords =
            driftContext.TargetCoords ?? Array.Empty<Vector2I>();
        if (finalTargetCoords.Count == 0)
        {
            finalTargetCoords = targetCoords;
        }
        if (driftContext.BacklashTriggered)
        {
            Runtime?._magic_backlash_resolver.AppendGroundBacklashLog(
                active_unit,
                skillDefinition,
                driftContext,
                batch as BattleEventBatch
            );
        }

        MeteorSwarmCastContext context = meteorResolver.BuildCastContextTyped(
            active_unit,
            command,
            skillDefinition,
            castVariantDefinition,
            targetCoords[0],
            finalTargetCoords[0],
            spellControlContext,
            driftContext
        );
        MeteorSwarmTargetPlan plan = meteorResolver.BuildTargetPlanTyped(context);
        MeteorSwarmCommitResult result = meteorResolver.ResolveTyped(plan);
        return outcomeCommitter.CommitMeteorSwarmResult(result, batch);
    }

    internal void _preview_unit_skill_command(
        BattleUnitReadView active_unit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattlePreview preview
    )
    {
        AiTraceRecorder.Enter("preview:unit_skill");
        _preview_unit_skill_command_impl(
            active_unit,
            command,
            preview,
            skillDefinition,
            castVariantDefinition
        );
        AiTraceRecorder.Exit("preview:unit_skill");
    }

    internal void _preview_unit_skill_command_impl(
        BattleUnitReadView active_unit,
        BattleCommand command,
        BattlePreview preview,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        if (preview == null)
        {
            return;
        }
        preview.ClearSaveBranchPreview();
        castVariantDefinition ??= Runtime?._skill_resolution_rules
            ?.ResolveUnitCastVariantDefinition(
                skillDefinition,
                active_unit,
                command != null ? command.skill_variant_id : new StringName("")
            );
        string blockReason = _get_skill_command_block_reason(
            active_unit,
            skillDefinition,
            castVariantDefinition
        );
        if (!string.IsNullOrEmpty(blockReason))
        {
            preview.AddLogLine(blockReason);
            return;
        }

        AiTraceRecorder.Enter("preview:unit_skill.validate_targets");
        BattleUnitSkillPreviewValidationResult validation = _validate_unit_skill_preview_targets_result(
            active_unit,
            command,
            skillDefinition,
            castVariantDefinition
        );
        AiTraceRecorder.Exit("preview:unit_skill.validate_targets");
        AiTraceRecorder.Enter("preview:unit_skill.copy_validation");
        preview.allowed = validation.Allowed;
        preview.SetTargetUnitIds(validation.TargetUnitIds);
        preview.SetRandomChainCandidateUnitIds(validation.RandomChainCandidateUnitIds);
        preview.ClearTargetCoords();
        foreach (Vector2I previewCoord in validation.PreviewCoords)
        {
            preview.AddTargetCoord(previewCoord);
        }
        AiTraceRecorder.Exit("preview:unit_skill.copy_validation");
        if (preview.allowed)
        {
            AiTraceRecorder.Enter("preview:unit_skill.hit_preview");
            preview.hit_preview = _build_unit_skill_hit_preview(
                active_unit,
                validation.TargetUnits,
                skillDefinition,
                castVariantDefinition
            );
            AiTraceRecorder.Exit("preview:unit_skill.hit_preview");
            AiTraceRecorder.Enter("preview:unit_skill.damage_preview");
            preview.SetDamagePreview(
                BuildUnitSkillDamagePreviewTyped(
                    active_unit,
                    skillDefinition,
                    castVariantDefinition
                )
            );
            preview.SetSaveBranchPreview(
                BuildUnitSkillSaveBranchPreview(
                    active_unit,
                    validation.TargetUnits,
                    skillDefinition,
                    castVariantDefinition
                )
            );
            AiTraceRecorder.Exit("preview:unit_skill.damage_preview");
            AiTraceRecorder.Enter("preview:unit_skill.log_lines");
            string skillLabel = _format_skill_variant_label(skillDefinition, castVariantDefinition);
            if (validation.TargetUnits.Count == 1)
            {
                BattleUnitReadView targetUnit = validation.TargetUnits[0];
                if (targetUnit.IsValid)
                {
                    preview.AddLogLine(
                        $"{active_unit.DisplayName} 可对 {targetUnit.DisplayName} 使用 {skillLabel}。"
                    );
                    if (preview.hit_preview != null && !preview.hit_preview.IsEmpty)
                    {
                        preview.AddLogLine(preview.hit_preview.SummaryText);
                    }
                    _append_damage_preview_line(preview);
                    AiTraceRecorder.Exit("preview:unit_skill.log_lines");
                    return;
                }
            }
            if (
                skillDefinition?.CombatProfile != null
                && skillDefinition.CombatProfile.TargetSelectionModeKind
                    == BattleTargetSelectionMode.RandomChain
            )
            {
                preview.AddLogLine(
                    $"{active_unit.DisplayName} 可用 {skillLabel} 从 {preview.RandomChainCandidateUnitIdsTyped.Count} 个候选单位中随机连击。"
                );
                _append_damage_preview_line(preview);
                AiTraceRecorder.Exit("preview:unit_skill.log_lines");
                return;
            }
            preview.AddLogLine(
                $"{active_unit.DisplayName} 可对 {preview.TargetUnitIdsTyped.Count} 个单位使用 {skillLabel}。"
            );
            if (preview.hit_preview != null && !preview.hit_preview.IsEmpty)
            {
                preview.AddLogLine(preview.hit_preview.SummaryText);
            }
            _append_damage_preview_line(preview);
            AiTraceRecorder.Exit("preview:unit_skill.log_lines");
            return;
        }
        preview.AddLogLine(
            string.IsNullOrEmpty(validation.Message) ? "技能或目标无效。" : validation.Message
        );
    }

    internal void _preview_ground_skill_command(
        BattleUnitReadView active_unit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattlePreview preview
    )
    {
        AiTraceRecorder.Enter("preview:ground_skill");
        _preview_ground_skill_command_impl(
            active_unit,
            command,
            preview,
            skillDefinition,
            castVariantDefinition
        );
        AiTraceRecorder.Exit("preview:ground_skill");
    }

    internal void _preview_ground_skill_command_impl(
        BattleUnitReadView active_unit,
        BattleCommand command,
        BattlePreview preview,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition
    )
    {
        if (preview == null)
        {
            return;
        }
        preview.ClearSaveBranchPreview();
        castVariantDefinition ??= Runtime?._skill_resolution_rules
            ?.ResolveGroundCastVariantDefinition(
                skillDefinition,
                active_unit,
                command != null ? command.skill_variant_id : new StringName("")
            );
        string blockReason = _get_skill_command_block_reason(
            active_unit,
            skillDefinition,
            castVariantDefinition
        );
        if (!string.IsNullOrEmpty(blockReason))
        {
            preview.AddLogLine(blockReason);
            return;
        }
        AiTraceRecorder.Enter("preview:ground_skill.validate");
        BattleGroundSkillValidationResult validation =
            Runtime?.ValidateGroundSkillCommandResultTyped(
                active_unit,
                skillDefinition,
                castVariantDefinition,
                command
            ) ?? BattleGroundSkillValidationResult.Denied("地面技能目标无效。");
        AiTraceRecorder.Exit("preview:ground_skill.validate");
        AiTraceRecorder.Enter("preview:ground_skill.preview_coords");
        preview.ClearTargetCoords();
        IReadOnlyList<Vector2I> previewCoords;
        if (validation.HasPreviewCoords)
        {
            previewCoords = validation.PreviewCoords;
        }
        else
        {
            Vector2I sourceCoord = active_unit.IsValid
                ? active_unit.Coord
                : new Vector2I(-1, -1);
            IReadOnlyList<Vector2I> builtCoords =
                Runtime?.BuildGroundEffectCoordsTyped(
                    skillDefinition,
                    validation.TargetCoords,
                    sourceCoord,
                    active_unit,
                    castVariantDefinition
                ) ?? Array.Empty<Vector2I>();
            previewCoords = builtCoords;
        }
        preview.resolved_anchor_coord = validation.ResolvedAnchorCoord;
        bool allowed = validation.Allowed;
        if (allowed && Runtime?._charge_resolver != null)
        {
            CombatEffectDefinition pathStepAoeEffect = Runtime._charge_resolver
                .GetChargePathStepAoeEffectDefinition(
                    castVariantDefinition,
                    skillDefinition,
                    active_unit
                );
            if (pathStepAoeEffect != null)
            {
                previewCoords = Runtime._charge_resolver.BuildChargeStepAoePreviewCoords(
                    active_unit,
                    validation.Direction,
                    validation.Distance,
                    pathStepAoeEffect
                );
            }
        }
        foreach (Vector2I targetCoord in previewCoords)
        {
            preview.AddTargetCoord(targetCoord);
        }
        AiTraceRecorder.Exit("preview:ground_skill.preview_coords");
        AiTraceRecorder.Enter("preview:ground_skill.collect_unit_ids");
        IReadOnlyList<StringName> previewUnitIds =
            Runtime?.CollectGroundPreviewUnitIdsTyped(
                active_unit,
                skillDefinition,
                Runtime.CollectGroundUnitEffectDefinitionsTyped(
                    skillDefinition,
                    castVariantDefinition,
                    active_unit
                ),
                preview.TargetCoordsTyped
            ) ?? Array.Empty<StringName>();
        preview.SetTargetUnitIds(previewUnitIds);
        AiTraceRecorder.Exit("preview:ground_skill.collect_unit_ids");
        if (allowed && Runtime?._charge_resolver != null)
        {
            AiTraceRecorder.Enter("preview:ground_skill.path_step_aoe");
            CombatEffectDefinition pathStepAoeEffect = Runtime._charge_resolver
                .GetChargePathStepAoeEffectDefinition(
                    castVariantDefinition,
                    skillDefinition,
                    active_unit
                );
            if (pathStepAoeEffect != null)
            {
                StringName pathStepTargetFilter =
                    Runtime?._skill_resolution_rules?.ResolveEffectTargetFilter(
                        skillDefinition,
                        pathStepAoeEffect
                    ) ?? new StringName("");
                foreach (
                    BattleUnitReadView targetUnit in CollectUnitsInCoordsReadView(
                        preview.TargetCoordsTyped
                    )
                )
                {
                    if (!_is_unit_valid_for_effect(active_unit, targetUnit, pathStepTargetFilter))
                    {
                        continue;
                    }
                    if (preview.ContainsTargetUnitId(targetUnit.UnitId))
                    {
                        continue;
                    }
                    preview.AddTargetUnitId(targetUnit.UnitId);
                }
            }
            AiTraceRecorder.Exit("preview:ground_skill.path_step_aoe");
        }
        preview.allowed = allowed;
        if (preview.allowed)
        {
            preview.SetSaveBranchPreview(
                BuildGroundSkillGradedSaveExecutePreview(
                    active_unit,
                    skillDefinition,
                    castVariantDefinition,
                    preview.TargetUnitIdsTyped
                )
            );
        }
        AiTraceRecorder.Enter("preview:ground_skill.log_lines");
        if (preview.allowed)
        {
            preview.AddLogLine(
                $"{active_unit.DisplayName} 可使用 {_format_skill_variant_label(skillDefinition, castVariantDefinition)}，预计影响 {preview.TargetCoordsTyped.Count} 个地格、{preview.TargetUnitIdsTyped.Count} 个单位。"
            );
        }
        else
        {
            preview.AddLogLine(
                string.IsNullOrEmpty(validation.Message)
                    ? "地面技能目标无效。"
                    : validation.Message
            );
        }
        AiTraceRecorder.Exit("preview:ground_skill.log_lines");
    }

    private BattleSaveBranchPreviewData BuildGroundSkillGradedSaveExecutePreview(
        BattleUnitReadView activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        IReadOnlyList<StringName> targetUnitIds
    )
    {
        if (
            !activeUnit.IsValid
            || skillDefinition == null
            || targetUnitIds == null
            || targetUnitIds.Count == 0
        )
        {
            return null;
        }

        CombatEffectDefinition effectDefinition = FindFirstValidGradedSaveExecuteEffect(
            Runtime?._skill_resolution_rules?.CollectGroundUnitEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            ) ?? new List<CombatEffectDefinition>(),
            out BattleGradedSaveExecutionProfile profile
        );
        if (effectDefinition == null)
        {
            return null;
        }

        BattleState state = RtState();
        if (
            state == null
            || !state.TryGetUnitTyped(activeUnit.UnitId, out BattleUnitState sourceUnit)
        )
        {
            return null;
        }

        StringName targetFilter =
            Runtime?._skill_resolution_rules?.ResolveEffectTargetFilter(
                skillDefinition,
                effectDefinition
            ) ?? new StringName("");
        int targetCount = 0;
        int enemyTargetCount = 0;
        int friendlyTargetCount = 0;
        int affectedUnitCount = 0;
        int enemyAffectedCount = 0;
        int friendlyAffectedCount = 0;
        int immuneCount = 0;
        int enemyExecuteRiskCount = 0;
        int friendlyExecuteRiskCount = 0;
        int failureExecuteRiskCount = 0;
        int criticalFailureExecuteRiskCount = 0;
        int criticalSuccessExpectedCount = 0;
        int criticalSuccessExpectedBasisPoints = 0;
        int successAftershockExpectedBasisPoints = 0;
        int failureExpectedBasisPoints = 0;
        int criticalFailureExpectedBasisPoints = 0;

        foreach (StringName targetUnitId in targetUnitIds)
        {
            if (
                StringNameIsEmpty(targetUnitId)
                || !state.TryGetUnitTyped(targetUnitId, out BattleUnitState targetUnit)
                || !_is_unit_valid_for_effect(sourceUnit, targetUnit, targetFilter)
            )
            {
                continue;
            }

            bool friendly = targetUnit.faction_id == sourceUnit.faction_id;
            targetCount++;
            if (friendly)
            {
                friendlyTargetCount++;
            }
            else
            {
                enemyTargetCount++;
            }

            BattleGradedSaveGradeDistribution distribution =
                BattleGradedSaveExecutionRules.EstimateGradeDistribution(
                    sourceUnit,
                    targetUnit,
                    effectDefinition,
                    BattleSaveContext.ForSkill(skillDefinition.SkillId)
                );
            if (distribution.ImmuneBasisPoints > 0)
            {
                immuneCount++;
                continue;
            }

            affectedUnitCount++;
            if (friendly)
            {
                friendlyAffectedCount++;
            }
            else
            {
                enemyAffectedCount++;
            }

            if (distribution.CriticalSuccessBasisPoints > 0)
            {
                criticalSuccessExpectedCount++;
                criticalSuccessExpectedBasisPoints += distribution.CriticalSuccessBasisPoints;
            }
            successAftershockExpectedBasisPoints += distribution.SuccessBasisPoints;
            failureExpectedBasisPoints += distribution.FailureBasisPoints;
            criticalFailureExpectedBasisPoints += distribution.CriticalFailureBasisPoints;

            int targetMaxHp = GetUnitMaxHp(targetUnit);
            bool failureExecuteRisk =
                distribution.FailureBasisPoints > 0
                && targetUnit.current_hp
                    <= BattleGradedSaveExecutionRules.ResolveFailureExecuteThreshold(
                        profile,
                        targetMaxHp
                    );
            bool criticalFailureExecuteRisk =
                distribution.CriticalFailureBasisPoints > 0
                && targetUnit.current_hp
                    <= BattleGradedSaveExecutionRules.ResolveCriticalFailureExecuteThreshold(
                        profile,
                        targetMaxHp
                    );
            if (failureExecuteRisk)
            {
                failureExecuteRiskCount++;
            }
            if (criticalFailureExecuteRisk)
            {
                criticalFailureExecuteRiskCount++;
            }
            if (failureExecuteRisk || criticalFailureExecuteRisk)
            {
                if (friendly)
                {
                    friendlyExecuteRiskCount++;
                }
                else
                {
                    enemyExecuteRiskCount++;
                }
            }
        }

        if (targetCount == 0)
        {
            return null;
        }

        return new BattleSaveBranchPreviewData
        {
            Kind = new StringName("graded_save_execute"),
            SaveTag = effectDefinition.SaveTag,
            SaveAbility = effectDefinition.SaveAbility,
            SummaryText = BuildGroundGradedSaveExecuteSummaryText(
                enemyTargetCount,
                friendlyAffectedCount,
                friendlyExecuteRiskCount,
                enemyExecuteRiskCount,
                immuneCount,
                failureExecuteRiskCount,
                criticalFailureExecuteRiskCount,
                successAftershockExpectedBasisPoints
            ),
            ResidualValues = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["profile_id"] = profile.ProfileId,
                ["target_count"] = targetCount,
                ["enemy_target_count"] = enemyTargetCount,
                ["friendly_target_count"] = friendlyTargetCount,
                ["affected_unit_count"] = affectedUnitCount,
                ["enemy_affected_count"] = enemyAffectedCount,
                ["friendly_affected_count"] = friendlyAffectedCount,
                ["friendly_execute_risk_count"] = friendlyExecuteRiskCount,
                ["enemy_execute_risk_count"] = enemyExecuteRiskCount,
                ["immune_count"] = immuneCount,
                ["critical_success_expected_count"] = criticalSuccessExpectedCount,
                ["critical_success_expected_basis_points"] =
                    criticalSuccessExpectedBasisPoints,
                ["success_aftershock_expected_basis_points"] =
                    successAftershockExpectedBasisPoints,
                ["failure_expected_basis_points"] = failureExpectedBasisPoints,
                ["critical_failure_expected_basis_points"] =
                    criticalFailureExpectedBasisPoints,
                ["failure_execute_risk_count"] = failureExecuteRiskCount,
                ["critical_failure_execute_risk_count"] = criticalFailureExecuteRiskCount,
            },
        };
    }

    private static CombatEffectDefinition FindFirstValidGradedSaveExecuteEffect(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        out BattleGradedSaveExecutionProfile profile
    )
    {
        profile = default;
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition?.EffectKind == BattleEffectKind.GradedSaveExecute
                && BattleGradedSaveExecutionRules.TryReadPhantasmalKillProfile(
                    effectDefinition,
                    out profile,
                    out _
                )
            )
            {
                return effectDefinition;
            }
        }
        return null;
    }

    private static string BuildGroundGradedSaveExecuteSummaryText(
        int enemyTargetCount,
        int friendlyAffectedCount,
        int friendlyExecuteRiskCount,
        int enemyExecuteRiskCount,
        int immuneCount,
        int failureExecuteRiskCount,
        int criticalFailureExecuteRiskCount,
        int successAftershockExpectedBasisPoints
    )
    {
        var parts = new List<string>
        {
            $"怪影杀戮：敌方 {enemyTargetCount}，友军 {friendlyAffectedCount}",
            $"处决风险 敌方 {enemyExecuteRiskCount} / 友军 {friendlyExecuteRiskCount}",
            $"分支风险 失败处决 {failureExecuteRiskCount} / 大失败处决 {criticalFailureExecuteRiskCount}",
        };
        if (successAftershockExpectedBasisPoints > 0)
        {
            parts.Add($"成功余悸期望 {successAftershockExpectedBasisPoints}bp");
        }
        if (immuneCount > 0)
        {
            parts.Add($"免疫/无效 {immuneCount}");
        }
        if (friendlyAffectedCount > 0 || friendlyExecuteRiskCount > 0)
        {
            parts.Add("友军误伤风险");
        }
        return string.Join(" · ", parts);
    }

    internal void AppendDamageResultLogLines(
        BattleEventBatch batch,
        string subject_label,
        string target_display_name,
        AttackEffectResolutionResult result
    )
    {
        Runtime?._report_formatter.AppendDamageResultLogLines(
            batch,
            subject_label,
            target_display_name,
            result
        );
    }

    internal BattleDamagePreviewRangeService.SkillDamagePreview? BuildUnitSkillDamagePreviewTyped(
        BattleUnitReadView active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        if (!active_unit.IsValid || skillDefinition == null)
        {
            return null;
        }
        List<CombatEffectDefinition> effectDefinitions = Runtime?._skill_resolution_rules
            ?.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                active_unit
            ) ?? new List<CombatEffectDefinition>();
        return BattleDamagePreviewRangeService.BuildSkillDamagePreview(
            active_unit,
            effectDefinitions
        );
    }

    private BattleSaveBranchPreviewData BuildUnitSkillSaveBranchPreview(
        BattleUnitReadView activeUnit,
        IReadOnlyList<BattleUnitReadView> targetUnits,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        if (!activeUnit.IsValid || skillDefinition == null || targetUnits == null || targetUnits.Count != 1)
        {
            return null;
        }

        BattleUnitReadView targetUnit = targetUnits[0];
        if (!targetUnit.IsValid)
        {
            return null;
        }

        var lookup = FindSingleExecuteEffect(
            Runtime?._skill_resolution_rules?.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            ) ?? new List<CombatEffectDefinition>()
        );
        if (lookup.Effect == null || !string.IsNullOrEmpty(lookup.ErrorMessage))
        {
            return null;
        }

        BattleExecutePlan plan = BattleExecutionRules.BuildExecutePlan(
            activeUnit,
            targetUnit,
            BattleExecutionRuleParams.FromEffect(lookup.Effect, skillDefinition.SkillId)
        );
        if (!plan.CanExecute)
        {
            return null;
        }

        BattleState state = RtState();
        if (
            state == null
            || !state.TryGetUnitTyped(activeUnit.UnitId, out BattleUnitState sourceState)
            || !state.TryGetUnitTyped(targetUnit.UnitId, out BattleUnitState targetState)
        )
        {
            return null;
        }

        BattleSaveProbabilityResult probability =
            BattleSaveResolver.EstimateSaveSuccessProbabilityResult(
                sourceState,
                targetState,
                lookup.Effect,
                BattleSaveContext.ForSkill(skillDefinition.SkillId)
            );
        int saveSuccessBps = Mathf.Clamp(probability.SuccessProbabilityBasisPoints, 0, 10000);
        int hitChanceBps = probability.HasSave ? Mathf.Clamp(10000 - saveSuccessBps, 0, 10000) : 10000;
        string successBranchText = plan.SoulFractureParams.HasValue ? "灵魂裂解" : "抵抗";
        string summaryText =
            $"命中率 {FormatBasisPointPercent(hitChanceBps)} · 豁免失败：死亡律令 · 豁免成功：{successBranchText}";

        return new BattleSaveBranchPreviewData
        {
            Kind = new StringName("execute"),
            Branch = plan.Branch,
            SaveTag = probability.SaveTag,
            SaveAbility = probability.Ability,
            SaveDc = probability.Dc,
            SaveAdvantageState = probability.AdvantageState,
            SaveSuccessChanceBasisPoints = saveSuccessBps,
            HitChanceBasisPoints = hitChanceBps,
            Threshold = plan.Threshold,
            CurrentHp = plan.CurrentHp,
            MaxHp = plan.MaxHp,
            FailureBranchText = "死亡律令",
            SuccessBranchText = successBranchText,
            SummaryText = summaryText,
        };
    }

    private static string FormatBasisPointPercent(int basisPoints)
    {
        int clamped = Mathf.Clamp(basisPoints, 0, 10000);
        if (clamped % 100 == 0)
        {
            return $"{clamped / 100}%";
        }
        return $"{clamped / 100.0f:0.#}%";
    }

    internal string _build_skill_log_subject_label(
        BattleUnitState source_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition = null
    )
    {
        string actorLabel =
            source_unit != null
            && !string.IsNullOrEmpty(source_unit.display_name)
                ? source_unit.display_name
                : "未知单位";
        string skillLabel = _format_skill_variant_label(skillDefinition, castVariantDefinition);
        if (string.IsNullOrEmpty(skillLabel) && skillDefinition != null)
        {
            skillLabel = skillDefinition.DisplayName;
        }
        if (string.IsNullOrEmpty(skillLabel))
        {
            skillLabel = "技能";
        }
        return $"{actorLabel} 使用 {skillLabel}";
    }

    internal bool _handle_unit_skill_command(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch
    )
    {
        BattleUnitSkillValidationResult validation = _validate_unit_skill_targets_result(
            active_unit,
            command,
            skillDefinition,
            castVariantDefinition
        );
        bool isRandomChain = IsRandomChainSkill(skillDefinition);
        if (!validation.Allowed || (!isRandomChain && validation.TargetUnits.Count == 0))
        {
            return false;
        }

        IReadOnlyList<CombatEffectDefinition> resolvedEffectDefinitions =
            effectDefinitions
            ?? CollectUnitSkillEffectDefinitions(skillDefinition, castVariantDefinition, active_unit);
        if (
            !CanApplyUnitSkillOrRepeatResultFromDefinitions(resolvedEffectDefinitions)
        )
        {
            return false;
        }

        if (!_consume_skill_costs(active_unit, skillDefinition, castVariantDefinition, batch))
        {
            return false;
        }
        CombatSkillResourceCosts costs = _get_effective_skill_resource_costs(
            active_unit,
            skillDefinition
        );
        _record_action_issued(
            active_unit,
            BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            costs.ApCost
        );
        _append_changed_unit_id(batch, active_unit.unit_id);

        BattleSpellControlResult spellControlContext = _resolve_unit_spell_control_after_cost_result(
            active_unit,
            skillDefinition,
            batch
        );
        if (spellControlContext.SkipEffects)
        {
            return true;
        }

        BattleRepeatAttackResolver repeatAttackResolver = Runtime?._repeat_attack_resolver;
        CombatEffectDefinition repeatAttackEffect =
            repeatAttackResolver?.get_repeat_attack_effect_def(resolvedEffectDefinitions);
        if (isRandomChain)
        {
            return _handle_random_chain_unit_skill_command(
                active_unit,
                skillDefinition,
                castVariantDefinition,
                batch,
                resolvedEffectDefinitions,
                repeatAttackEffect,
                spellControlContext
            );
        }
        bool applied = false;
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
                    && repeatAttackResolver.ApplyRepeatAttackSkillResult(
                        active_unit,
                        targetUnit,
                        skillDefinition,
                        resolvedEffectDefinitions,
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
                    skillDefinition,
                    castVariantDefinition,
                    resolvedEffectDefinitions,
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

    internal bool _handle_random_chain_unit_skill_command(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleEventBatch batch,
        IReadOnlyList<CombatEffectDefinition> effect_definitions,
        CombatEffectDefinition repeat_attack_effect,
        BattleSpellControlResult spell_control_context
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        int maxHitsPerTarget = Math.Max(combatProfile?.MaxHitsPerTarget ?? 0, 1);
        var chainHitCounts = new Dictionary<StringName, int>();
        bool applied = false;
        int attemptCount = 0;
        int maxAttempts = Math.Max(
            (Runtime?._state?.UnitCount ?? 0) * maxHitsPerTarget,
            1
        );
        string skillLabel = _format_skill_variant_label(skillDefinition, castVariantDefinition);
        BattleRepeatAttackResolver repeatAttackResolver = Runtime?._repeat_attack_resolver;
        while (attemptCount < maxAttempts)
        {
            List<BattleUnitState> chainPool = BuildRandomChainTargetPool(
                active_unit,
                skillDefinition,
                castVariantDefinition,
                chainHitCounts,
                maxHitsPerTarget
            );
            if (chainPool.Count == 0)
            {
                break;
            }
            ShuffleRandomChainPool(chainPool);
            BattleUnitState targetUnit = chainPool[0];
            if (targetUnit == null)
            {
                break;
            }
            batch?.AddLogLine(
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
                    && repeatAttackResolver.ApplyRepeatAttackSkillResult(
                        active_unit,
                        targetUnit,
                        skillDefinition,
                        effect_definitions,
                        repeat_attack_effect,
                        batch
                    );
            }
            else
            {
                stageApplied = _apply_unit_skill_result(
                    active_unit,
                    targetUnit,
                    skillDefinition,
                    castVariantDefinition,
                    effect_definitions,
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
            batch?.AddLogLine(
                $"{active_unit.display_name} 的{skillLabel}执行了 {attemptCount} 次攻击链判定。"
            );
        }
        return applied;
    }

    private List<BattleUnitState> BuildRandomChainTargetPool(
        BattleUnitState active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        IReadOnlyDictionary<StringName, int> chain_hit_counts,
        int max_hits_per_target
    )
    {
        var chainPool = new List<BattleUnitState>();
        BattleState state = RtState();
        if (state == null)
        {
            return chainPool;
        }
        foreach (BattleUnitState candidate in state.GetUnitsTyped())
        {
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
            if (!_can_skill_target_unit(active_unit, candidate, skillDefinition, false, castVariant))
            {
                continue;
            }
            chainPool.Add(candidate);
        }
        return chainPool;
    }

    private List<BattleUnitReadView> BuildRandomChainTargetPool(
        BattleUnitReadView active_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        int max_hits_per_target
    )
    {
        var chainPool = new List<BattleUnitReadView>();
        BattleState state = RtState();
        if (state == null || !active_unit.IsValid)
        {
            return chainPool;
        }
        foreach (BattleUnitReadView candidate in state.AsReadView().AliveUnits())
        {
            if (
                !candidate.IsValid
                || candidate.UnitId == active_unit.UnitId
                || StringNameIsEmpty(candidate.UnitId)
            )
            {
                continue;
            }
            if (max_hits_per_target <= 0)
            {
                continue;
            }
            if (!_can_skill_target_unit(active_unit, candidate, skillDefinition, false, castVariant))
            {
                continue;
            }
            chainPool.Add(candidate);
        }
        return chainPool;
    }

    internal void _shuffle_random_chain_pool(GArray chain_pool)
    {
        if (chain_pool.Count <= 1)
        {
            return;
        }
        for (int index = chain_pool.Count - 1; index > 0; index--)
        {
            int swapIndex = TrueRandomSeedService.RandiRange(0, index);
            if (swapIndex == index)
            {
                continue;
            }
            var temp = chain_pool[index];
            chain_pool[index] = chain_pool[swapIndex];
            chain_pool[swapIndex] = temp;
        }
    }

    private static void ShuffleRandomChainPool(List<BattleUnitState> chainPool)
    {
        if (chainPool == null || chainPool.Count <= 1)
        {
            return;
        }
        for (int index = chainPool.Count - 1; index > 0; index--)
        {
            int swapIndex = TrueRandomSeedService.RandiRange(0, index);
            if (swapIndex == index)
            {
                continue;
            }
            BattleUnitState temp = chainPool[index];
            chainPool[index] = chainPool[swapIndex];
            chainPool[swapIndex] = temp;
        }
    }

    internal bool _handle_ground_skill_command(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleEventBatch batch
    )
    {
        BattleGroundSkillValidationResult validation =
            Runtime?.ValidateGroundSkillCommandResultTyped(
                active_unit,
                skillDefinition,
                castVariantDefinition,
                command
            ) ?? BattleGroundSkillValidationResult.Denied("地面技能目标无效。");
        if (!validation.Allowed)
        {
            return false;
        }

        IReadOnlyList<Vector2I> targetCoords = validation.TargetCoords ?? Array.Empty<Vector2I>();
        string precastValidationMessage =
            Runtime?.GetGroundSpecialEffectValidationMessageTyped(
                active_unit,
                skillDefinition,
                castVariantDefinition,
                targetCoords
            ) ?? "";
        if (!string.IsNullOrEmpty(precastValidationMessage))
        {
            batch?.AddLogLine(precastValidationMessage);
            return false;
        }

        int mpBeforeCost = active_unit?.current_mp ?? 0;
        if (!_consume_skill_costs(active_unit, skillDefinition, castVariantDefinition, batch))
        {
            return false;
        }
        int spentMp = Math.Max(mpBeforeCost - (active_unit?.current_mp ?? 0), 0);
        CombatSkillResourceCosts costs = _get_effective_skill_resource_costs(
            active_unit,
            skillDefinition
        );
        _record_action_issued(
            active_unit,
            BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            costs.ApCost
        );
        _append_changed_unit_id(batch, active_unit?.unit_id ?? new StringName(""));
        BattleChargeResolver chargeResolver = Runtime?._charge_resolver;
        if (chargeResolver != null && chargeResolver.IsChargeOption(castVariantDefinition))
        {
            return chargeResolver.handle_charge_skill_command_result(
                active_unit,
                skillDefinition,
                castVariantDefinition,
                validation,
                batch
            );
        }
        BattleSpellControlResult spellControlContext =
            _resolve_ground_spell_control_after_cost_result(
                active_unit,
                skillDefinition,
                spentMp,
                batch
            );
        if (spellControlContext.SkipEffects)
        {
            return false;
        }
        if (
            Runtime?.ApplyGroundPrecastSpecialEffectsTyped(
                active_unit,
                skillDefinition,
                castVariantDefinition,
                targetCoords,
                batch
            ) != true
        )
        {
            return false;
        }

        BattleGroundBacklashTargetResult driftContext =
            Runtime
                ?._magic_backlash_resolver.BuildGroundBacklashTargetCoordsResult(
                    skillDefinition,
                    targetCoords,
                    Runtime.GetState(),
                    Runtime.GetGridService(),
                    spellControlContext
                ) ?? BattleGroundBacklashTargetResult.None(targetCoords);
        if (driftContext.BacklashTriggered)
        {
            if (driftContext.TargetCoords.Count != 0)
            {
                targetCoords = driftContext.TargetCoords;
            }
            Runtime?._magic_backlash_resolver.AppendGroundBacklashLog(
                active_unit,
                skillDefinition,
                driftContext,
                batch
            );
        }
        IReadOnlyList<Vector2I> effectCoords = Runtime.BuildGroundEffectCoordsTyped(
            skillDefinition,
            targetCoords,
            active_unit != null ? active_unit.coord : new Vector2I(-1, -1),
            active_unit,
            castVariantDefinition
        );
        BattleGroundUnitEffectsResult unitResult = Runtime.ApplyGroundUnitEffectsResultTyped(
            active_unit,
            skillDefinition,
            castVariantDefinition,
            Runtime.CollectGroundUnitEffectDefinitionsTyped(
                skillDefinition,
                castVariantDefinition,
                active_unit
            ),
            effectCoords,
            batch,
            targetCoords
        );
        BattleGroundTerrainEffectsResult terrainResult =
            Runtime.ApplyGroundTerrainEffectsResultTyped(
                active_unit,
                skillDefinition,
                Runtime.CollectGroundTerrainEffectDefinitionsTyped(
                    skillDefinition,
                    castVariantDefinition,
                    active_unit
                ),
                effectCoords,
                batch
            );
        bool applied = unitResult.Applied || terrainResult.Applied;

        if (applied)
        {
            batch?.AddLogLine(
                $"{active_unit.display_name} 使用 {_format_skill_variant_label(skillDefinition, castVariantDefinition)}，影响了 {effectCoords.Count} 个地格、{unitResult.AffectedUnitCount} 个单位。"
            );
        }
        return applied;
    }

    internal BattleUnitSkillValidationResult _validate_unit_skill_targets_result(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition cast_variant = null
    )
    {
        BattleState state = RtState();
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            state == null
            || active_unit == null
            || command == null
            || skillDefinition == null
            || combatProfile == null
        )
        {
            return BattleUnitSkillValidationResult.Denied("技能或目标无效。");
        }

        bool allowRepeat = combatProfile.AllowRepeatTarget;
        GStringNameArray targetUnitIds = _normalize_target_unit_ids(command, allowRepeat);
        int skillLevel = _get_unit_skill_level(
            active_unit,
            skillDefinition.SkillId
        );
        int minTargetCount = 1;
        int maxTargetCount = 1;
        if (_is_multi_unit_skill(skillDefinition))
        {
            minTargetCount = Math.Max(combatProfile.MinTargetCount, 1);
            maxTargetCount = Math.Max(
                combatProfile.GetEffectiveMaxTargetCount(skillLevel),
                minTargetCount
            );
        }
        bool isRandomChain =
            combatProfile.TargetSelectionModeKind == BattleTargetSelectionMode.RandomChain;
        if (targetUnitIds.Count == 0 && !isRandomChain)
        {
            return BattleUnitSkillValidationResult.Denied("技能或目标无效。");
        }
        if (isRandomChain)
        {
            int maxHitsPerTarget = Math.Max(
                combatProfile.MaxHitsPerTarget,
                1
            );
            List<BattleUnitState> randomChainPool = BuildRandomChainTargetPool(
                active_unit,
                skillDefinition,
                cast_variant,
                new Dictionary<StringName, int>(),
                maxHitsPerTarget
            );
            if (randomChainPool.Count == 0)
            {
                return BattleUnitSkillValidationResult.Denied("没有可用的随机连击目标。");
            }
            var candidateUnitIds = new List<StringName>();
            foreach (BattleUnitState candidate in randomChainPool)
            {
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
        if (!_is_multi_unit_skill(skillDefinition) && targetUnitIds.Count != 1)
        {
            return BattleUnitSkillValidationResult.Denied("当前技能只允许选择 1 个单位目标。");
        }
        if (combatProfile.SelectionOrderModeKind != BattleTargetSelectionOrderMode.Manual)
        {
            targetUnitIds = _sort_target_unit_ids_for_execution(targetUnitIds);
        }

        var targetUnits = new List<BattleUnitState>();
        foreach (StringName targetUnitId in targetUnitIds)
        {
            state.TryGetUnitTyped(targetUnitId, out BattleUnitState targetUnit);
            string specialValidationMessage = _get_unit_skill_target_validation_message(
                active_unit,
                targetUnit,
                skillDefinition,
                cast_variant
            );
            if (!string.IsNullOrEmpty(specialValidationMessage))
            {
                return BattleUnitSkillValidationResult.Denied(specialValidationMessage);
            }
            if (
                targetUnit == null
                || !_can_skill_target_unit(
                    active_unit,
                    targetUnit,
                    skillDefinition,
                    true,
                    cast_variant
                )
            )
            {
                return BattleUnitSkillValidationResult.Denied("技能目标超出范围或不满足筛选条件。");
            }
            targetUnits.Add(targetUnit);
        }

        IReadOnlyList<Vector2I> emptyTargetCoords = Array.Empty<Vector2I>();
        BattleTargetCollectionResult collectedTargetCoords =
            Runtime?._target_collection_service.CollectCombatProfileTargetCoords(
                state,
                Runtime.GetGridService(),
                active_unit != null ? active_unit.coord : new Vector2I(-1, -1),
                combatProfile,
                emptyTargetCoords,
                active_unit,
                targetUnits,
                skillLevel
            ) ?? BattleTargetCollectionResult.UnhandledResult(emptyTargetCoords);
        List<Vector2I> previewCoords = SortCoordsTyped(collectedTargetCoords.TargetCoords);
        return BattleUnitSkillValidationResult.AllowedResult(
            ToStringNameList(targetUnitIds),
            targetUnits,
            null,
            previewCoords
        );
    }

    internal BattleUnitSkillPreviewValidationResult _validate_unit_skill_preview_targets_result(
        BattleUnitReadView active_unit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition cast_variant = null
    )
    {
        BattleState state = RtState();
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            state == null
            || !active_unit.IsValid
            || command == null
            || skillDefinition == null
            || combatProfile == null
        )
        {
            return BattleUnitSkillPreviewValidationResult.Denied("技能或目标无效。");
        }

        bool allowRepeat = combatProfile.AllowRepeatTarget;
        GStringNameArray targetUnitIds = _normalize_target_unit_ids(command, allowRepeat);
        int skillLevel = active_unit.GetKnownSkillLevel(skillDefinition.SkillId);
        int minTargetCount = 1;
        int maxTargetCount = 1;
        if (_is_multi_unit_skill(skillDefinition))
        {
            minTargetCount = Math.Max(combatProfile.MinTargetCount, 1);
            maxTargetCount = Math.Max(
                combatProfile.GetEffectiveMaxTargetCount(skillLevel),
                minTargetCount
            );
        }
        bool isRandomChain =
            combatProfile.TargetSelectionModeKind == BattleTargetSelectionMode.RandomChain;
        if (targetUnitIds.Count == 0 && !isRandomChain)
        {
            return BattleUnitSkillPreviewValidationResult.Denied("技能或目标无效。");
        }
        if (isRandomChain)
        {
            int maxHitsPerTarget = Math.Max(
                combatProfile.MaxHitsPerTarget,
                1
            );
            List<BattleUnitReadView> randomChainPool = BuildRandomChainTargetPool(
                active_unit,
                skillDefinition,
                cast_variant,
                maxHitsPerTarget
            );
            if (randomChainPool.Count == 0)
            {
                return BattleUnitSkillPreviewValidationResult.Denied("没有可用的随机连击目标。");
            }
            var candidateUnitIds = new List<StringName>();
            foreach (BattleUnitReadView candidate in randomChainPool)
            {
                if (candidate.IsValid)
                {
                    candidateUnitIds.Add(candidate.UnitId);
                }
            }
            return BattleUnitSkillPreviewValidationResult.AllowedResult(
                System.Array.Empty<StringName>(),
                System.Array.Empty<BattleUnitReadView>(),
                candidateUnitIds
            );
        }
        if (targetUnitIds.Count < minTargetCount)
        {
            return BattleUnitSkillPreviewValidationResult.Denied($"至少需要选择 {minTargetCount} 个单位目标。");
        }
        if (targetUnitIds.Count > maxTargetCount)
        {
            return BattleUnitSkillPreviewValidationResult.Denied($"最多只能选择 {maxTargetCount} 个单位目标。");
        }
        if (!_is_multi_unit_skill(skillDefinition) && targetUnitIds.Count != 1)
        {
            return BattleUnitSkillPreviewValidationResult.Denied("当前技能只允许选择 1 个单位目标。");
        }
        if (combatProfile.SelectionOrderModeKind != BattleTargetSelectionOrderMode.Manual)
        {
            targetUnitIds = _sort_target_unit_ids_for_execution(targetUnitIds);
        }

        BattleStateReadView stateView = state.AsReadView();
        var targetUnits = new List<BattleUnitReadView>();
        foreach (StringName targetUnitId in targetUnitIds)
        {
            BattleUnitReadView targetUnit = stateView.GetUnit(targetUnitId);
            string specialValidationMessage = _get_unit_skill_target_validation_message(
                active_unit,
                targetUnit,
                skillDefinition,
                cast_variant
            );
            if (!string.IsNullOrEmpty(specialValidationMessage))
            {
                return BattleUnitSkillPreviewValidationResult.Denied(specialValidationMessage);
            }
            if (
                !targetUnit.IsValid
                || !_can_skill_target_unit(
                    active_unit,
                    targetUnit,
                    skillDefinition,
                    true,
                    cast_variant
                )
            )
            {
                return BattleUnitSkillPreviewValidationResult.Denied("技能目标超出范围或不满足筛选条件。");
            }
            targetUnits.Add(targetUnit);
        }

        IReadOnlyList<Vector2I> emptyTargetCoords = Array.Empty<Vector2I>();
        BattleTargetCollectionResult collectedTargetCoords =
            Runtime?._target_collection_service.CollectCombatProfileTargetCoords(
                state,
                Runtime.GetGridService(),
                active_unit.Coord,
                combatProfile,
                emptyTargetCoords,
                active_unit,
                targetUnits,
                skillLevel
            ) ?? BattleTargetCollectionResult.UnhandledResult(emptyTargetCoords);
        List<Vector2I> previewCoords = SortCoordsTyped(collectedTargetCoords.TargetCoords);
        return BattleUnitSkillPreviewValidationResult.AllowedResult(
            ToStringNameList(targetUnitIds),
            targetUnits,
            null,
            previewCoords
        );
    }

    internal GStringNameArray _normalize_target_unit_ids(
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
        foreach (StringName targetUnitIdValue in command.TargetUnitIdsTyped)
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

    internal GStringNameArray _sort_target_unit_ids_for_execution(GStringNameArray target_unit_ids)
    {
        BattleState state = RtState();
        if (state == null)
        {
            return (GStringNameArray)target_unit_ids.Duplicate();
        }
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

    internal bool _is_multi_unit_skill(SkillDefinition skillDefinition)
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        return combatProfile != null
            && combatProfile.TargetSelectionModeKind == BattleTargetSelectionMode.MultiUnit;
    }

    internal bool _can_skill_target_unit(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skillDefinition,
        bool require_ap = true,
        CombatCastVariantDefinition cast_variant = null
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            active_unit == null
            || target_unit == null
            || skillDefinition == null
            || combatProfile == null
        )
        {
            return false;
        }
        CombatSkillResourceCosts costs = _get_effective_skill_resource_costs(
            active_unit,
            skillDefinition
        );
        if (
            require_ap
            && active_unit.current_ap < costs.ApCost
        )
        {
            return false;
        }
        if (
            !_is_unit_valid_for_effect(
                active_unit,
                target_unit,
                combatProfile.TargetTeamFilter
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
                    skillDefinition,
                    cast_variant
                )
            )
        )
        {
            return false;
        }
        active_unit.RefreshFootprint();
        target_unit.RefreshFootprint();
        return Runtime?.GetGridService().GetDistanceBetweenUnits(active_unit, target_unit)
            <= _get_effective_skill_range(active_unit, skillDefinition);
    }

    internal bool _can_skill_target_unit(
        BattleUnitReadView active_unit,
        BattleUnitReadView target_unit,
        SkillDefinition skillDefinition,
        bool require_ap = true,
        CombatCastVariantDefinition cast_variant = null
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            !active_unit.IsValid
            || !target_unit.IsValid
            || skillDefinition == null
            || combatProfile == null
        )
        {
            return false;
        }
        CombatSkillResourceCosts costs = _get_effective_skill_resource_costs(
            active_unit,
            skillDefinition
        );
        if (require_ap && active_unit.CurrentAp < costs.ApCost)
        {
            return false;
        }
        if (
            !_is_unit_valid_for_effect(
                active_unit,
                target_unit,
                combatProfile.TargetTeamFilter
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
                    skillDefinition,
                    cast_variant
                )
            )
        )
        {
            return false;
        }
        return Runtime?.GetGridService().GetDistanceBetweenUnits(active_unit, target_unit)
            <= _get_effective_skill_range(active_unit, skillDefinition);
    }

    internal BattleUnitSkillTargetAffordance GetUnitSkillTargetAffordance(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant = null,
        bool requireAp = true
    )
    {
        bool allowed = _can_skill_target_unit(
            activeUnit,
            targetUnit,
            skillDefinition,
            requireAp,
            castVariant
        );
        if (allowed)
        {
            return BattleUnitSkillTargetAffordance.AllowedResult();
        }
        string reason = _get_unit_skill_target_validation_message(
            activeUnit,
            targetUnit,
            skillDefinition,
            castVariant
        );
        return BattleUnitSkillTargetAffordance.Denied(
            string.IsNullOrEmpty(reason) ? "技能目标超出范围或不满足筛选条件。" : reason
        );
    }

    internal UnitSkillEffectResolution ResolveUnitSkillEffectResult(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        CombatCastVariantDefinition castVariantDefinition = null
    )
    {
        return _resolve_unit_skill_effect_resolution(
            active_unit,
            target_unit,
            skillDefinition,
            castVariantDefinition,
            effectDefinitions,
            null
        );
    }

    private UnitSkillEffectResolution _resolve_unit_skill_effect_resolution(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch = null
    )
    {
        BattleSkillResolutionRules skillResolutionRules = Runtime?._skill_resolution_rules;
        BattleDamageResolver damageResolver = Runtime?._damage_resolver;
        if (damageResolver == null)
            return UnitSkillEffectResolution.FromResult(
                BattleDamageResolver.BuildEmptyResolutionResult(
                    skillDefinition?.SkillId ?? new StringName("")
                )
            );
        IReadOnlyList<CombatEffectDefinition> runtimeEffectDefinitions =
            effectDefinitions as IReadOnlyList<CombatEffectDefinition>
            ?? (effectDefinitions != null
                ? new List<CombatEffectDefinition>(effectDefinitions)
                : Array.Empty<CombatEffectDefinition>());
        if (
            skillResolutionRules?.ShouldResolveUnitSkillAsFateAttack(
                active_unit,
                target_unit,
                skillDefinition,
                effectDefinitions
            ) == true
        )
        {
            bool forceHitNoCrit =
                skillResolutionRules?.IsForceHitNoCritSkill(skillDefinition) == true;
            BattleAttackCheckPolicyService attackPolicy = Runtime?.GetAttackCheckPolicyService();
            BattleAttackCheckPolicyContext policyContext =
                attackPolicy?.BuildSkillDefinitionAttackContext(
                    RtState(),
                    active_unit,
                    target_unit,
                    skillDefinition,
                    new StringName("skill_attack_check"),
                    new StringName("execute"),
                    forceHitNoCrit
                );
            AttackCheckInput attackCheck =
                attackPolicy != null
                    ? attackPolicy.BuildAttackCheck(policyContext, 0, 0)
                    : new AttackCheckInput(invalid: true);
            if (forceHitNoCrit)
            {
                attackCheck = NormalizeForceHitNoCritAttackCheck(attackCheck);
            }
            var attackContext = new AttackContext
            {
                BattleState = RtState(),
                SkillId = skillDefinition?.SkillId ?? new StringName(""),
            };
            if (forceHitNoCrit)
            {
                attackContext.ForceHitNoCrit = true;
            }
            AttackEffectResolutionResult result = damageResolver.ResolveAttackEffects(
                active_unit,
                target_unit,
                runtimeEffectDefinitions,
                attackCheck,
                attackContext
            );
            if (forceHitNoCrit)
            {
                return UnitSkillEffectResolution.FromResult(
                    result,
                    new[]
                    {
                        "黑契推进压低了命运摆幅：这次攻击必定命中，且不会触发暴击。",
                    }
                );
            }
            return UnitSkillEffectResolution.FromResult(result);
        }
        if (runtimeEffectDefinitions.Count != 0)
        {
            AttackEffectResolutionResult result = damageResolver.ResolveEffects(
                active_unit,
                target_unit,
                runtimeEffectDefinitions,
                DamageResolutionContext
                    .ForSkill(skillDefinition?.SkillId ?? new StringName(""))
                    .WithSourceSkillLevel(
                        Math.Max(
                            _get_unit_skill_level(
                                active_unit,
                                skillDefinition?.SkillId ?? new StringName("")
                            ),
                            1
                        )
                    )
                    .WithDamageApplicationHookContext(
                        batch,
                        Runtime?.CurrentEffectOriginForContingency
                            ?? BattleEffectOrigin.PlayerCommand()
                    )
            );
            return UnitSkillEffectResolution.FromResult(result);
        }
        return UnitSkillEffectResolution.FromResult(
            damageResolver.ResolveSkillResult(
                active_unit,
                target_unit,
                skillDefinition
            )
        );
    }

    private static AttackCheckInput NormalizeForceHitNoCritAttackCheck(AttackCheckInput source)
    {
        return new AttackCheckInput(
            attackerBaseAttackBonus: source.AttackerBaseAttackBonus,
            attackerAttackBonus: source.AttackerAttackBonus,
            attackerBab: source.AttackerBab,
            targetArmorClass: source.TargetArmorClass,
            skillAttackBonus: source.SkillAttackBonus,
            lockedSkillHitBonus: source.LockedSkillHitBonus,
            situationalAttackBonus: source.SituationalAttackBonus,
            situationalAttackPenalty: source.SituationalAttackPenalty,
            requiredRoll: source.RequiredRoll,
            displayRequiredRoll: source.DisplayRequiredRoll,
            hitRatePercent: source.HitRatePercent,
            successRatePercent: source.SuccessRatePercent,
            baseHitRatePercent: source.BaseHitRatePercent,
            naturalOneAutoMiss: source.NaturalOneAutoMiss,
            naturalTwentyAutoHit: source.NaturalTwentyAutoHit,
            critThreshold: source.CritThreshold,
            fumbleLowEnd: source.FumbleLowEnd,
            critLocked: true,
            critGateDie: source.CritGateDie,
            forceHitNoCrit: true,
            skillId: source.SkillId,
            followUpAttackPenalty: source.FollowUpAttackPenalty,
            exponentialPenalty: source.ExponentialPenalty,
            isDisadvantage: source.IsDisadvantage,
            invalid: source.Invalid,
            errorId: source.ErrorId,
            errorMessage: source.ErrorMessage,
            previewText: source.PreviewText
        );
    }

    private bool _apply_unit_skill_result(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch,
        BattleSpellControlResult spell_control_context = default
    )
    {
        effectDefinitions ??= Array.Empty<CombatEffectDefinition>();
        BattleLayeredBarrierService layeredBarrierService = Runtime?._layered_barrier_service;
        BattleBarrierInteractionResult barrierResult =
            layeredBarrierService != null
                ? layeredBarrierService.ResolveSkillBarrierInteractionResult(
                    active_unit,
                    target_unit,
                    skillDefinition,
                    effectDefinitions,
                    batch
                )
                : new BattleBarrierInteractionResult(false, false);
        if (barrierResult.Blocked)
        {
            return barrierResult.Applied;
        }
        StringName sourceEventId = Runtime?.AllocateContingencySourceEventId("unit_spell") ?? "";
        Runtime?.EmitContingencySpellAffected(
            active_unit,
            target_unit,
            new[] { target_unit?.unit_id ?? new StringName("") },
            sourceEventId
        );
        int previousTargetHp = target_unit?.current_hp ?? 0;
        UnitSkillEffectResolution effectResolution = _resolve_unit_skill_effect_resolution(
            active_unit,
            target_unit,
            skillDefinition,
            castVariantDefinition,
            effectDefinitions,
            batch
        );
        AttackEffectResolutionResult damageResult = effectResolution.Result;
        BattleSkillMasteryService skillMasteryService = Runtime?._skill_mastery_service;
        skillMasteryService?.RecordTargetResult(
            active_unit,
            target_unit,
            skillDefinition,
            damageResult,
            effectDefinitions
        );
        _flush_last_stand_mastery_records(batch);
        BattleSkillMasteryGrant guardMasteryGrant =
            skillMasteryService?.BuildGuardMasteryGrantFromIncomingHitTyped(
                active_unit,
                target_unit,
                effectDefinitions,
                damageResult,
                Runtime?.GetSkillDefinitionIndexTyped()
            );
        var shieldRollContext = new Dictionary<long, int>();
        BattleShieldApplyResult shieldResult = _apply_unit_shield_effects_result(
            active_unit,
            target_unit,
            skillDefinition,
            effectDefinitions,
            shieldRollContext
        );
        MarkAppliedStatusesForTurnTiming(
            target_unit,
            damageResult.StatusEffectIds
        );
        _append_changed_unit_id(batch, target_unit?.unit_id ?? new StringName(""));
        _append_changed_unit_coords(batch, target_unit);
        append_result_source_status_effects(batch, active_unit, damageResult);
        BattleSpecialSkillResult specialResult = ApplyUnitSkillSpecialEffectsResult(
            active_unit,
            target_unit,
            skillDefinition,
            castVariantDefinition,
            effectDefinitions,
            batch,
            BattleForcedMoveContext.Empty
        );
        MarkAppliedStatusesForTurnTiming(
            target_unit,
            specialResult.StatusEffectIds
        );
        var appliedStatusIds = new List<StringName>();
        if (damageResult.StatusEffectIds != null)
        {
            foreach (StringName statusId in damageResult.StatusEffectIds)
                if (!StringNameIsEmpty(statusId) && !appliedStatusIds.Contains(statusId))
                    appliedStatusIds.Add(statusId);
        }
        foreach (StringName statusId in specialResult.StatusEffectIds ?? Array.Empty<StringName>())
            if (!StringNameIsEmpty(statusId) && !appliedStatusIds.Contains(statusId))
                appliedStatusIds.Add(statusId);
        Runtime?.EmitContingencyHpAndStatusHooks(
            active_unit,
            target_unit,
            previousTargetHp,
            appliedStatusIds,
            sourceEventId
        );
        bool applied =
            damageResult.Applied
            || shieldResult.Applied
            || specialResult.Applied;
        if (!applied)
        {
            append_result_report_entry(batch, damageResult);
            foreach (string customLine in effectResolution.CustomLogLines)
            {
                if (!string.IsNullOrEmpty(customLine))
                {
                    batch?.AddLogLine(customLine);
                }
            }
            foreach (string specialLine in specialResult.LogLines)
            {
                if (!string.IsNullOrEmpty(specialLine))
                {
                    batch?.AddLogLine(specialLine);
                }
            }
            return false;
        }

        string skillLabel = _format_skill_variant_label(skillDefinition, castVariantDefinition);
        string actorLabel =
            active_unit != null && !string.IsNullOrEmpty(active_unit.display_name)
                ? active_unit.display_name
                : "未知单位";
        string skillSubject = $"{actorLabel} 使用 {skillLabel}";
        int damage = damageResult.Damage;
        int healing = damageResult.Healing;
        int movedSteps = specialResult.MovedSteps;
        RecordVajraBodyMasteryFromIncomingDamageTyped(
            active_unit,
            target_unit,
            skillDefinition,
            damageResult,
            batch
        );
        if (movedSteps > 0)
        {
            batch?.AddLogLine(
                $"{active_unit.display_name} 使用 {skillLabel}，向更安全位置移动 {movedSteps} 格。"
            );
        }
        AppendDamageResultLogLines(
            batch,
            skillSubject,
            target_unit?.display_name ?? "",
            damageResult
        );
        _apply_equipment_durability_result(target_unit, damageResult, batch);
        append_result_report_entry(batch, damageResult);
        StringName skillId = skillDefinition?.SkillId ?? new StringName("");
        if (_is_doom_sentence_skill(skillId))
        {
            var doomSentenceReportTags = new GStringNameArray
            {
                BattleReportFormatter.TAG_DOOM_SENTENCE,
            };
            Runtime?._append_report_entry_to_batch(
                batch,
                Runtime?._report_formatter.BuildSkillEventEntry(
                    active_unit,
                    target_unit,
                    skillId,
                    BattleReportFormatter.REASON_DOOM_SENTENCE_APPLIED,
                    doomSentenceReportTags
                ) ?? new GDictionary()
            );
        }
        if (healing > 0)
        {
            batch?.AddLogLine(
                $"{skillSubject} 为 {target_unit.display_name} 恢复 {healing} 点生命。"
            );
        }
        if (shieldResult.Applied)
        {
            batch?.AddLogLine(
                $"{skillSubject} 使 {target_unit.display_name} 的护盾值变为 {shieldResult.CurrentShieldHp}。"
            );
        }
        foreach (StringName statusId in damageResult.StatusEffectIds)
        {
            batch?.AddLogLine($"{target_unit.display_name} 获得状态 {statusId}。");
        }
        _append_dispel_result_log_lines(batch, skillSubject, target_unit, damageResult);
        _apply_chain_damage_effects(
            active_unit,
            target_unit,
            skillDefinition,
            effectDefinitions,
            damageResult,
            batch,
            skillSubject,
            spell_control_context
        );
        foreach (string customLine in effectResolution.CustomLogLines)
        {
            if (!string.IsNullOrEmpty(customLine))
            {
                batch?.AddLogLine(customLine);
            }
        }
        foreach (string specialLine in specialResult.LogLines)
        {
            if (!string.IsNullOrEmpty(specialLine))
            {
                batch?.AddLogLine(specialLine);
            }
        }
        GStringNameArray terrainEffectIds = damageResult.TerrainEffectIds;
        if (terrainEffectIds.Count != 0)
        {
            BattleGridService gridService = Runtime?.GetGridService();
            foreach (StringName terrainEffectId in terrainEffectIds)
            {
                BattleCellState targetCell = gridService?.GetCellState(RtState(), target_unit.coord);
                if (
                    targetCell != null
                    && !targetCell.terrain_effect_ids.Contains(terrainEffectId)
                )
                {
                    targetCell.terrain_effect_ids.Add(terrainEffectId);
                    _append_changed_coord(batch, target_unit.coord);
                    batch?.AddLogLine(
                        $"{skillSubject} 使 {target_unit.display_name} 所在的地格附加效果 {terrainEffectId}。"
                    );
                }
            }
        }
        int heightDelta = damageResult.HeightDelta;
        Vector2I targetCoord = target_unit.coord;
        BattleGridService gridService2 = Runtime?.GetGridService();
        BattleCellState targetCellBefore = gridService2?.GetCellState(RtState(), targetCoord);
        int beforeHeight = targetCellBefore?.current_height ?? 0;
        if (
            heightDelta != 0
            && gridService2 != null
            && gridService2.ApplyHeightDelta(RtState(), targetCoord, heightDelta)
        )
        {
            _append_changed_coord(batch, targetCoord);
            BattleCellState targetCellAfter = gridService2.GetCellState(RtState(), targetCoord);
            int afterHeight =
                targetCellAfter != null
                    ? targetCellAfter.current_height
                    : beforeHeight + heightDelta;
            batch?.AddLogLine(
                $"{skillSubject} 使 ({targetCoord.X}, {targetCoord.Y}) 的高度由 {beforeHeight} 变为 {afterHeight}。"
            );
        }
        if (target_unit?.is_alive != true)
        {
            _apply_on_kill_gain_resources_effects(
                active_unit,
                target_unit,
                skillDefinition,
                effectDefinitions,
                batch
            );
            Runtime?.HandleUnitDefeatedByRuntimeEffect(
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
            Runtime?._battle_rating_system.RecordContributionFromUnits(
                active_unit,
                target_unit,
                damage,
                healing,
                causedDefeat,
                new StringName("skill"),
                skillId
            );
        }
        ApplySkillMasteryGrantTyped(target_unit, guardMasteryGrant, batch);
        return true;
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
                batch.AddLogLine($"{target_unit.display_name} 的 {itemId} 抵抗了裂解术。");
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
                batch.AddLogLine($"{target_unit.display_name} 的 {itemId} 被裂解为尘埃。");
            }
            else
            {
                batch.AddLogLine(
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

    internal void _append_dispel_result_log_lines(
        BattleEventBatch batch,
        string skill_subject,
        BattleUnitState target_unit,
        AttackEffectResolutionResult result
    )
    {
        if (batch == null || target_unit == null)
        {
            return;
        }
        foreach (DispelEventResult dispelEvent in result.DispelEvents ?? Array.Empty<DispelEventResult>())
        {
            GStringNameArray removedIds = dispelEvent.RemovedStatusIds;
            if (removedIds == null || removedIds.Count == 0)
            {
                continue;
            }
            var labels = new List<string>();
            foreach (StringName statusId in removedIds)
            {
                if (!StringNameIsEmpty(statusId))
                {
                    labels.Add(statusId.ToString());
                }
            }
            batch.AddLogLine(
                $"{skill_subject} 解除 {target_unit.display_name} 身上的 {string.Join("、", labels)}。"
            );
        }
    }

    internal void _refresh_target_after_equipment_destruction(BattleUnitState target_unit)
    {
        BattleUnitFactory unitFactory = Runtime?._unit_factory;
        if (target_unit == null || Runtime == null || unitFactory == null)
        {
            return;
        }
        if (!StringNameIsEmpty(target_unit.source_member_id))
        {
            unitFactory.RefreshEquipmentProjection(target_unit);
        }
        _clamp_target_resources_after_equipment_projection(target_unit);
    }

    internal void _clamp_target_resources_after_equipment_projection(BattleUnitState target_unit)
    {
        AttributeSnapshot snapshot = target_unit?.attribute_snapshot;
        if (target_unit == null || snapshot == null)
        {
            return;
        }
        target_unit.SetCurrentHpClamped(
            target_unit.current_hp,
            Math.Max(snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax)), 1)
        );
        target_unit.SetCurrentMp(Math.Clamp(
            target_unit.current_mp,
            0,
            Math.Max(snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.MpMax)), 0)
        ));
        target_unit.SetCurrentStamina(Math.Clamp(
            target_unit.current_stamina,
            0,
            Math.Max(snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax)), 0)
        ));
        target_unit.SetCurrentAura(Math.Clamp(
            target_unit.current_aura,
            0,
            Math.Max(snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax)), 0)
        ));
    }

    internal void _apply_chain_damage_effects(
        BattleUnitState source_unit,
        BattleUnitState primary_target,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        AttackEffectResolutionResult primaryResolution,
        BattleEventBatch batch,
        string skill_subject,
        BattleSpellControlResult spell_control_context = default
    )
    {
        if (!primaryResolution.Applied)
        {
            return;
        }
        BattleDamageResolver damageResolver = Runtime?._damage_resolver;
        BattleSkillMasteryService skillMasteryService = Runtime?._skill_mastery_service;
        BattleRatingSystem ratingSystem = Runtime?._battle_rating_system;
        foreach (CombatEffectDefinition chainEffect in effectDefinitions)
        {
            if (chainEffect == null || chainEffect.EffectKind != BattleEffectKind.ChainDamage)
            {
                continue;
            }
            List<CombatEffectDefinition> chainTargetEffects = BuildChainTargetEffectDefinitions(
                effectDefinitions,
                chainEffect
            );
            if (chainTargetEffects.Count == 0)
            {
                continue;
            }
            List<BattleUnitState> chainTargets = CollectChainDamageTargets(
                source_unit,
                primary_target,
                skillDefinition,
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
            foreach (BattleUnitState chainTarget in chainTargets)
            {
                if (chainTarget == null || !chainTarget.is_alive)
                {
                    continue;
                }
                AttackEffectResolutionResult chainResolution =
                    damageResolver?.ResolveEffects(
                        source_unit,
                        chainTarget,
                        chainTargetEffects,
                        DamageResolutionContext
                            .ForSkill(skillDefinition?.SkillId ?? new StringName(""))
                            .WithSourceSkillLevel(
                                Math.Max(
                                    source_unit.GetKnownSkillLevelTyped(
                                        skillDefinition?.SkillId ?? new StringName(""),
                                        fallback: 1
                                    ),
                                    1
                                )
                            )
                    ) ?? new AttackEffectResolutionResult
                    {
                        AttackCheck = new AttackCheckInput(
                            skillId: skillDefinition?.SkillId ?? new StringName("")
                        ),
                    };
                skillMasteryService?.RecordTargetResult(
                    source_unit,
                    chainTarget,
                    skillDefinition,
                    chainResolution
                );
                MarkAppliedStatusesForTurnTiming(
                    chainTarget,
                    chainResolution.StatusEffectIds
                );
                if (!chainResolution.Applied)
                {
                    continue;
                }

                _append_changed_unit_id(batch, source_unit.unit_id);
                _append_changed_unit_id(batch, chainTarget.unit_id);
                _append_changed_unit_coords(batch, chainTarget);
                append_result_source_status_effects(batch, source_unit, chainResolution);
                AppendDamageResultLogLines(
                    batch,
                    $"{skill_subject} 的连锁闪电",
                    chainTarget.display_name,
                    chainResolution
                );
                foreach (StringName statusId in chainResolution.StatusEffectIds)
                {
                    batch.AddLogLine($"{chainTarget.display_name} 获得状态 {statusId}。");
                }

                int chainDamage = chainResolution.Damage;
                int chainHealing = chainResolution.Healing;
                totalDamage += chainDamage;
                totalHealing += chainHealing;
                if (!chainTarget.is_alive)
                {
                    totalKillCount += 1;
                    Runtime?._apply_on_kill_gain_resources_effects(
                        source_unit,
                        chainTarget,
                        skillDefinition,
                        chainTargetEffects,
                        batch
                    );
                    Runtime?.HandleUnitDefeatedByRuntimeEffect(
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
                ratingSystem?.RecordContributionFromUnits(
                    source_unit,
                    chainTarget,
                    chainDamage,
                    chainHealing,
                    causedChainDefeat,
                    new StringName("skill"),
                    skillDefinition?.SkillId ?? new StringName("")
                );
            }
        }
    }

    private static List<CombatEffectDefinition> BuildChainTargetEffectDefinitions(
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        CombatEffectDefinition chainEffect
    )
    {
        var chainTargetEffects = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition == null
                || effectDefinition == chainEffect
                || effectDefinition.EffectKind == BattleEffectKind.ChainDamage
            )
            {
                continue;
            }
            chainTargetEffects.Add(effectDefinition);
        }
        return chainTargetEffects;
    }

    private List<BattleUnitState> CollectChainDamageTargets(
        BattleUnitState source_unit,
        BattleUnitState primary_target,
        SkillDefinition skillDefinition,
        CombatEffectDefinition chainEffect,
        BattleSpellControlResult spell_control_context = default
    )
    {
        var targets = new List<BattleUnitState>();
        BattleState state = RtState();
        if (state == null || source_unit == null || primary_target == null || chainEffect == null)
        {
            return targets;
        }

        int maxRadius = _resolve_chain_damage_radius(
            primary_target,
            chainEffect,
            spell_control_context
        );
        if (maxRadius <= 0)
        {
            return targets;
        }
        bool preventRepeatTarget = ChainDamageParameters
            .FromEffect(chainEffect)
            .PreventRepeatTarget;
        StringName targetFilter = ResolveEffectTargetFilter(skillDefinition, chainEffect);
        if (StringNameIsEmpty(targetFilter))
        {
            return targets;
        }

        BattleGridService gridService = Runtime?.GetGridService();
        var visited = new HashSet<StringName>();
        var queue = new List<BattleUnitState>();
        visited.Add(primary_target.unit_id);
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
                    && visited.Contains(candidate.unit_id)
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

                visited.Add(candidate.unit_id);
                targets.Add(candidate);
                queue.Add(candidate);
            }
        }

        targets.Sort(
            (a, b) =>
            {
                int distanceA = gridService?.GetDistanceBetweenUnits(primary_target, a) ?? 0;
                int distanceB = gridService?.GetDistanceBetweenUnits(primary_target, b) ?? 0;
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
        return targets;
    }

    private int _resolve_chain_damage_radius(
        BattleUnitState primary_target,
        CombatEffectDefinition chainEffect,
        BattleSpellControlResult spell_control_context = default
    )
    {
        ChainDamageParameters chainParams = ChainDamageParameters.FromEffect(chainEffect);
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

    internal bool _unit_stands_on_terrain_effect(
        BattleUnitState unit_state,
        StringName terrain_effect_id
    )
    {
        BattleState state = RtState();
        if (state == null || unit_state == null || StringNameIsEmpty(terrain_effect_id))
        {
            return false;
        }
        unit_state.RefreshFootprint();
        BattleGridService gridService = Runtime?.GetGridService();
        foreach (Vector2I occupiedCoord in unit_state.occupied_coords)
        {
            BattleCellState cell = gridService?.GetCellState(state, occupiedCoord);
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

    internal bool _is_within_chain_radius(
        BattleUnitState primary_target,
        BattleUnitState candidate,
        int max_radius
    )
    {
        if (primary_target == null || candidate == null || max_radius <= 0)
        {
            return false;
        }
        primary_target.RefreshFootprint();
        candidate.RefreshFootprint();
        BattleGridService gridService = Runtime?.GetGridService();
        foreach (Vector2I primaryCoord in primary_target.occupied_coords)
        {
            foreach (Vector2I candidateCoord in candidate.occupied_coords)
            {
                if (gridService != null && gridService.GetDistance(primaryCoord, candidateCoord) <= max_radius)
                {
                    return true;
                }
            }
        }
        return false;
    }

    internal List<Vector2I> _get_line_coords(Vector2I from, Vector2I to)
    {
        var coords = new List<Vector2I>();
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

    internal bool _is_chain_path_clear(BattleUnitState source_unit, BattleUnitState target_unit)
    {
        BattleState state = RtState();
        BattleGridService gridService = Runtime?.GetGridService();
        if (state == null || source_unit == null || target_unit == null || gridService == null)
        {
            return false;
        }
        source_unit.RefreshFootprint();
        target_unit.RefreshFootprint();
        foreach (Vector2I sourceCoord in source_unit.occupied_coords)
        {
            BattleCellState sourceCell = gridService.GetCellState(state, sourceCoord);
            if (sourceCell == null)
            {
                continue;
            }
            int sourceHeight = sourceCell.current_height;
            foreach (Vector2I targetCoord in target_unit.occupied_coords)
            {
                foreach (Vector2I midCoord in _get_line_coords(sourceCoord, targetCoord))
                {
                    BattleCellState midCell = gridService.GetCellState(state, midCoord);
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

    internal string _get_unit_skill_target_validation_message(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition cast_variant = null
    )
    {
        string bodySizeOverrideMessage = _get_body_size_category_override_validation_message(
            active_unit,
            target_unit,
            skillDefinition,
            cast_variant
        );
        if (!string.IsNullOrEmpty(bodySizeOverrideMessage))
        {
            return bodySizeOverrideMessage;
        }
        string executeMessage = GetExecuteTargetValidationMessage(
            active_unit,
            target_unit,
            skillDefinition,
            cast_variant
        );
        if (!string.IsNullOrEmpty(executeMessage))
        {
            return executeMessage;
        }
        if (!BattleTemporalStatusService.CanTargetTimeStasis(target_unit, skillDefinition))
        {
            return "目标处于时间静滞，只有时间系解控技能能够作用。";
        }
        StringName skillId = skillDefinition?.SkillId ?? new StringName("");
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
            if (target_unit.unit_id == active_unit.unit_id)
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

    internal string _get_unit_skill_target_validation_message(
        BattleUnitReadView active_unit,
        BattleUnitReadView target_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition cast_variant = null
    )
    {
        string bodySizeOverrideMessage = _get_body_size_category_override_validation_message(
            active_unit,
            target_unit,
            skillDefinition,
            cast_variant
        );
        if (!string.IsNullOrEmpty(bodySizeOverrideMessage))
        {
            return bodySizeOverrideMessage;
        }
        string executeMessage = GetExecuteTargetValidationMessage(
            active_unit,
            target_unit,
            skillDefinition,
            cast_variant
        );
        if (!string.IsNullOrEmpty(executeMessage))
        {
            return executeMessage;
        }
        if (
            target_unit.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_STASIS)
            && !BattleTemporalStatusService.IsTemporalReleaseSkill(skillDefinition)
        )
        {
            return "目标处于时间静滞，只有时间系解控技能能够作用。";
        }
        StringName skillId = skillDefinition?.SkillId ?? new StringName("");
        if (_is_black_crown_seal_skill(skillId))
        {
            if (
                !_is_unit_valid_for_effect(active_unit, target_unit, BattleTypedNames.TargetFilterEnemy)
                || !target_unit.IsBossTarget
            )
            {
                return "黑冠封印只能对 boss 施放。";
            }
        }
        if (_is_doom_shift_skill(skillId))
        {
            if (!target_unit.IsValid || !active_unit.IsValid)
            {
                return "断命换位的目标无效。";
            }
            if (target_unit.UnitId == active_unit.UnitId)
            {
                return "断命换位不能以自己为目标。";
            }
        }
        if (_is_crown_break_skill(skillId))
        {
            if (
                !_is_unit_valid_for_effect(active_unit, target_unit, BattleTypedNames.TargetFilterEnemy)
                || !target_unit.HasStatusEffect("black_star_brand_elite")
            )
            {
                return "折冠只能对已被黑星烙印的 elite / boss 施放。";
            }
        }
        if (_is_doom_sentence_skill(skillId))
        {
            if (
                !_is_unit_valid_for_effect(active_unit, target_unit, BattleTypedNames.TargetFilterEnemy)
                || !target_unit.IsEliteOrBossTarget
            )
            {
                return "厄命宣判只能对 elite / boss 施放。";
            }
        }
        return "";
    }

    private string GetExecuteTargetValidationMessage(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        var lookup = FindSingleExecuteEffect(
            Runtime?._skill_resolution_rules?.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            ) ?? new List<CombatEffectDefinition>()
        );
        if (!string.IsNullOrEmpty(lookup.ErrorMessage))
        {
            return lookup.ErrorMessage;
        }
        if (lookup.Effect == null)
        {
            return "";
        }
        if (targetUnit == null)
        {
            return "律令死亡目标无效。";
        }
        if (!targetUnit.is_alive)
        {
            return "";
        }
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            combatProfile == null
            || !_is_unit_valid_for_effect(activeUnit, targetUnit, combatProfile.TargetTeamFilter)
        )
        {
            return "";
        }
        BattleExecutePlan plan = BattleExecutionRules.BuildExecutePlan(
            activeUnit,
            targetUnit,
            BattleExecutionRuleParams.FromEffect(lookup.Effect, skillDefinition.SkillId)
        );
        return plan.CanExecute
            ? ""
            : $"{targetUnit.display_name} 当前生命高于律令死亡阈值。";
    }

    private string GetExecuteTargetValidationMessage(
        BattleUnitReadView activeUnit,
        BattleUnitReadView targetUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        var lookup = FindSingleExecuteEffect(
            Runtime?._skill_resolution_rules?.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                activeUnit
            ) ?? new List<CombatEffectDefinition>()
        );
        if (!string.IsNullOrEmpty(lookup.ErrorMessage))
        {
            return lookup.ErrorMessage;
        }
        if (lookup.Effect == null)
        {
            return "";
        }
        if (!targetUnit.IsValid)
        {
            return "律令死亡目标无效。";
        }
        if (!targetUnit.IsAlive)
        {
            return "";
        }
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            combatProfile == null
            || !_is_unit_valid_for_effect(activeUnit, targetUnit, combatProfile.TargetTeamFilter)
        )
        {
            return "";
        }
        BattleExecutePlan plan = BattleExecutionRules.BuildExecutePlan(
            activeUnit,
            targetUnit,
            BattleExecutionRuleParams.FromEffect(lookup.Effect, skillDefinition.SkillId)
        );
        return plan.CanExecute
            ? ""
            : $"{targetUnit.DisplayName} 当前生命高于律令死亡阈值。";
    }

    private (CombatEffectDefinition Effect, string ErrorMessage) FindSingleExecuteEffect(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        CombatEffectDefinition found = null;
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition?.EffectKind != BattleEffectKind.Execute)
            {
                continue;
            }
            if (found != null)
            {
                return (null, "律令死亡效果配置无效。");
            }
            found = effectDefinition;
        }
        return (found, "");
    }

    internal string _get_body_size_category_override_validation_message(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition cast_variant = null
    )
    {
        BattleState state = RtState();
        if (state == null || target_unit == null || skillDefinition == null)
        {
            return "";
        }
        BattleGridService gridService = Runtime?.GetGridService();
        if (gridService == null)
        {
            return "";
        }
        foreach (
            CombatEffectDefinition effectDefinition in CollectUnitSkillEffectDefinitions(
                skillDefinition,
                cast_variant,
                active_unit
            )
        )
        {
            if (
                effectDefinition == null
                || effectDefinition.EffectKind != BattleEffectKind.BodySizeCategoryOverride
            )
            {
                continue;
            }
            StringName targetCategory = effectDefinition.BodySizeCategory;
            if (!BodySizeContentRules.IsValidBodySizeCategory(targetCategory))
            {
                continue;
            }
            Vector2I targetFootprint = BodySizeContentRules.GetFootprintForCategory(targetCategory);
            if (
                !gridService
                    .CanPlaceFootprint(
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

    internal string _get_body_size_category_override_validation_message(
        BattleUnitReadView active_unit,
        BattleUnitReadView target_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition cast_variant = null
    )
    {
        BattleState state = RtState();
        if (state == null || !target_unit.IsValid || skillDefinition == null)
        {
            return "";
        }
        BattleGridService gridService = Runtime?.GetGridService();
        if (gridService == null)
        {
            return "";
        }
        foreach (
            CombatEffectDefinition effectDefinition in CollectUnitSkillEffectDefinitions(
                skillDefinition,
                cast_variant,
                active_unit
            )
        )
        {
            if (
                effectDefinition == null
                || effectDefinition.EffectKind != BattleEffectKind.BodySizeCategoryOverride
            )
            {
                continue;
            }
            StringName targetCategory = effectDefinition.BodySizeCategory;
            if (!BodySizeContentRules.IsValidBodySizeCategory(targetCategory))
            {
                continue;
            }
            Vector2I targetFootprint = BodySizeContentRules.GetFootprintForCategory(targetCategory);
            if (
                !gridService
                    .CanPlaceFootprint(
                        state,
                        target_unit.Coord,
                        targetFootprint,
                        target_unit.UnitId,
                        target_unit
                    )
            )
            {
                return $"{target_unit.DisplayName} 周围空间不足，无法改变体型。";
            }
        }
        return "";
    }

    internal readonly struct UnitSkillEffectResolution
    {
        internal readonly AttackEffectResolutionResult Result;
        internal readonly IReadOnlyList<string> CustomLogLines;

        private UnitSkillEffectResolution(
            AttackEffectResolutionResult result,
            IReadOnlyList<string> customLogLines
        )
        {
            Result = result;
            CustomLogLines = customLogLines ?? Array.Empty<string>();
        }

        internal static UnitSkillEffectResolution FromResult(
            AttackEffectResolutionResult result,
            IReadOnlyList<string> customLogLines = null
        )
        {
            return new UnitSkillEffectResolution(result, customLogLines);
        }
    }

    private static BattleRuntimeModule ResolveWeakRef(WeakReference<BattleRuntimeModule> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out BattleRuntimeModule target))
            return null;
        return target;
    }
}
