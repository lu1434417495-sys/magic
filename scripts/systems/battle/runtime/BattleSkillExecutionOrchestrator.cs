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
    private StringName _scopedCommandSkillUnitId = "";
    private StringName _scopedCommandSkillId = "";
    private int _scopedCommandSkillLevel;
    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private readonly BattleSkillPreviewService _skillPreviewService = new();
    private readonly BattleSkillTargetValidationService _targetValidationService = new();
    private readonly BattleChainDamageService _chainDamageService = new();
    private readonly BattleRandomChainSkillService _randomChainSkillService = new();

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
        _skillPreviewService.Setup(runtime, this, _targetValidationService);
        _targetValidationService.Setup(runtime, this, _randomChainSkillService);
        _chainDamageService.Setup(runtime, this, _skillPreviewService);
        _randomChainSkillService.Setup(runtime, this, _targetValidationService);
    }

    internal void DisposeRuntime()
    {
        _chainDamageService.DisposeRuntime();
        _skillPreviewService.DisposeRuntime();
        _randomChainSkillService.DisposeRuntime();
        _targetValidationService.DisposeRuntime();
        _runtime = null;
    }

    internal void _preview_skill_command(
        BattleUnitReadView active_unit,
        BattleCommand command,
        BattlePreview preview
    ) => _skillPreviewService._preview_skill_command(active_unit, command, preview);

    internal void AppendDamageResultLogLines(
        BattleEventBatch batch,
        string subject_label,
        string target_display_name,
        AttackEffectResolutionResult result
    ) =>
        _skillPreviewService.AppendDamageResultLogLines(
            batch,
            subject_label,
            target_display_name,
            result
        );

    internal GStringNameArray _normalize_target_unit_ids(
        BattleCommand command,
        bool allow_repeat = false
    ) => _targetValidationService._normalize_target_unit_ids(command, allow_repeat);

    internal GStringNameArray _sort_target_unit_ids_for_execution(GStringNameArray target_unit_ids) =>
        _targetValidationService._sort_target_unit_ids_for_execution(target_unit_ids);

    internal BattleUnitSkillTargetAffordance GetUnitSkillTargetAffordance(
        BattleUnitState activeUnit,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant = null,
        bool requireAp = true
    ) =>
        _targetValidationService.GetUnitSkillTargetAffordance(
            activeUnit,
            targetUnit,
            skillDefinition,
            castVariant,
            requireAp
        );

    internal string _get_unit_skill_target_validation_message(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition cast_variant = null
    ) =>
        _targetValidationService._get_unit_skill_target_validation_message(
            active_unit,
            target_unit,
            skillDefinition,
            cast_variant
        );

    internal string _get_unit_skill_target_validation_message(
        BattleUnitReadView active_unit,
        BattleUnitReadView target_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition cast_variant = null
    ) =>
        _targetValidationService._get_unit_skill_target_validation_message(
            active_unit,
            target_unit,
            skillDefinition,
            cast_variant
        );

    internal BattleUnitSkillValidationResult _validate_unit_skill_targets_result(
        BattleUnitState active_unit,
        BattleCommand command,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition cast_variant = null,
        bool requireAp = true
    ) =>
        _targetValidationService._validate_unit_skill_targets_result(
            active_unit,
            command,
            skillDefinition,
            cast_variant,
            requireAp
        );

    internal void _apply_chain_damage_effects(
        BattleUnitState source_unit,
        BattleUnitState primary_target,
        SkillDefinition skillDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        AttackEffectResolutionResult primaryResolution,
        BattleEventBatch batch,
        string skill_subject,
        BattleSpellControlResult spell_control_context = default,
        CombatCastVariantDefinition castVariantDefinition = null
    ) =>
        _chainDamageService._apply_chain_damage_effects(
            source_unit,
            primary_target,
            skillDefinition,
            effectDefinitions,
            primaryResolution,
            batch,
            skill_subject,
            spell_control_context,
            castVariantDefinition
        );

    internal bool _is_within_chain_radius(
        BattleUnitState primary_target,
        BattleUnitState candidate,
        int max_radius
    ) => _chainDamageService._is_within_chain_radius(primary_target, candidate, max_radius);

    internal List<Vector2I> _get_line_coords(Vector2I from, Vector2I to) =>
        _chainDamageService._get_line_coords(from, to);

    internal bool _is_chain_path_clear(BattleUnitState source_unit, BattleUnitState target_unit) =>
        _chainDamageService._is_chain_path_clear(source_unit, target_unit);

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
        BattleForcedMoveContext forced_move_context,
        bool attack_succeeded = false
    )
    {
        return Runtime?.ApplyUnitSkillSpecialEffectsResult(
                active_unit,
                target_unit,
                skill_definition,
                cast_variant,
                effect_definitions ?? Array.Empty<CombatEffectDefinition>(),
                batch,
                forced_move_context,
                attack_succeeded
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
        BattleUnitState killer_unit = null,
        BattleEventBatch batch = null
    )
    {
        Runtime?._collect_defeated_unit_loot(unit_state, killer_unit, batch);
    }

    internal void _clear_defeated_unit(BattleUnitState unit_state, BattleEventBatch batch = null)
    {
        Runtime?._clear_defeated_unit(unit_state, batch);
    }

    internal GVector2IArray _sort_coords(GArray target_coords)
    {
        if (Runtime == null)
            return new GVector2IArray();
        return Runtime._sort_coords(target_coords);
    }

    internal GVector2IArray _sort_coords(GVector2IArray target_coords)
    {
        if (Runtime == null)
            return new GVector2IArray();
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

    private BattleSkillAvailabilityService CreateSkillAvailabilityService()
    {
        BattleRuntimeModule runtime = Runtime;
        return new BattleSkillAvailabilityService(
            runtime?._skillCatalog,
            runtime?._skillDefinitionIndex,
            runtime?._equipmentAbilityBindingIndex,
            runtime?._itemDefIndex
        );
    }

    private int ResolveSkillCommandEntryLevel(
        BattleUnitState activeUnit,
        BattleCommand command,
        StringName skillId
    )
    {
        int fallbackSkillLevel = _get_unit_skill_level(activeUnit, skillId);
        BattleRuntimeModule runtime = Runtime;
        return runtime == null
            ? fallbackSkillLevel
            : CreateSkillAvailabilityService().ResolveSkillCommandEntryLevel(
                runtime._state,
                command,
                BattleSkillAvailabilityConsumer.PreviewExecution,
                runtime.GetBattleWorldStep(),
                fallbackSkillLevel
            );
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
        int commandSkillLevel = ResolveSkillCommandEntryLevel(
            active_unit,
            command,
            skillDefinition.SkillId
        );
        using IDisposable scopedCommandLevel = PushScopedCommandSkillLevel(
            active_unit,
            skillDefinition.SkillId,
            commandSkillLevel
        );
        using IDisposable scopedResolutionLevel = Runtime?._skill_resolution_rules?.PushScopedSkillLevel(
            active_unit,
            skillDefinition.SkillId,
            commandSkillLevel
        );
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
            BattleSkillUseOutcomeSnapshot outcomeSnapshot =
                CaptureSkillUseOutcomeSnapshot(command, allowRepeat);
            bool definitionApplied = _handle_unit_skill_command(
                active_unit,
                command,
                skillDefinition,
                policy?.UnitExecutionCastVariantDefinition,
                policy?.EffectDefinitions,
                batch
            );
            if (definitionApplied || IsEquipmentSkillCommand(command))
            {
                CommitEquipmentSkillUsageIfNeeded(
                    active_unit,
                    command,
                    batch,
                    BuildEquipmentSkillUseOutcome(outcomeSnapshot)
                );
            }
            if (definitionApplied && ShouldGrantSkillMasteryForCommand(command, active_unit, skillDefinition))
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
                CommitEquipmentSkillUsageIfNeeded(active_unit, command, batch);
                if (ShouldGrantSkillMasteryForCommand(command, active_unit, skillDefinition))
                {
                    _grant_skill_mastery_if_needed(active_unit, skillDefinition, batch);
                }
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
            BattleSkillUseOutcomeSnapshot outcomeSnapshot =
                IsEquipmentSkillCommand(command)
                    ? CaptureGroundSkillUseOutcomeSnapshot(
                        active_unit,
                        skillDefinition,
                        policy.GroundCastVariantDefinition,
                        command
                    )
                    : BattleSkillUseOutcomeSnapshot.Empty;
            bool definitionApplied = _handle_ground_skill_command(
                active_unit,
                command,
                skillDefinition,
                policy.GroundCastVariantDefinition,
                batch
            );
            if (definitionApplied || IsEquipmentSkillCommand(command))
            {
                CommitEquipmentSkillUsageIfNeeded(
                    active_unit,
                    command,
                    batch,
                    BuildEquipmentSkillUseOutcome(outcomeSnapshot)
                );
            }
            if (definitionApplied && ShouldGrantSkillMasteryForCommand(command, active_unit, skillDefinition))
            {
                _grant_skill_mastery_if_needed(active_unit, skillDefinition, batch);
            }
            Runtime?._skill_mastery_service.Clear();
            return;
        }

        Runtime?._skill_mastery_service.Clear();
        return;
    }

    private static bool IsEquipmentSkillCommand(BattleCommand command)
    {
        StringName skillEntryId = ProgressionDataUtils.to_string_name(
            command?.skill_entry_id ?? new StringName("")
        );
        return skillEntryId.ToString().StartsWith("equipment_skill:", StringComparison.Ordinal);
    }

    internal bool CommitEquipmentSkillUsageIfNeeded(
        BattleUnitState unit,
        BattleCommand command,
        BattleEventBatch batch = null,
        BattleEquipmentSkillUseOutcome skillOutcome = null
    )
    {
        BattleRuntimeModule runtime = Runtime;
        if (runtime == null || unit == null || command == null)
            return false;
        BattleSkillAvailabilityService service = CreateSkillAvailabilityService();
        BattleSkillAccessResult accessResult = service.ValidateSkillEntryAccess(
            new BattleSkillAvailabilityQuery
            {
                User = unit,
                Consumer = BattleSkillAvailabilityConsumer.PreviewExecution,
                IncludeKnownSkills = false,
                IncludeEquipmentSkills = true,
                WorldStep = runtime.GetBattleWorldStep(),
                BattleState = runtime._state,
            },
            command.skill_entry_id,
            command.skill_id
        );
        if (!accessResult.Allowed)
            return false;

        bool committed = EquipmentAbilityUsageRuntime.TryCommitUsage(
            unit,
            accessResult.Entry,
            runtime.GetBattleWorldStep()
        );
        bool triggered = runtime._equipment_ability_runtime_service?.ResolveGrantedSkillUsed(
            new BattleEquipmentAbilityGrantedSkillUsedContext
            {
                SourceUnit = unit,
                TargetUnit = ResolveCommandPrimaryTargetUnit(runtime._state, command),
                BattleState = runtime._state,
                Batch = batch,
                BindingId = accessResult.Entry.EquipmentBindingId,
                GrantedActionId = accessResult.Entry.EquipmentGrantedActionId,
                SkillId = accessResult.Entry.EntryRef.SkillId,
                SkillEntryId = accessResult.Entry.EntryRef.SkillEntryId,
                SkillOutcome = skillOutcome ?? BattleEquipmentSkillUseOutcome.Empty,
            }
        ) == true;
        if (committed || triggered)
            batch?.AddChangedUnitId(unit.unit_id);
        return committed || triggered;
    }

    private static BattleUnitState ResolveCommandPrimaryTargetUnit(
        BattleState state,
        BattleCommand command
    )
    {
        if (state == null || command == null)
            return null;
        StringName targetUnitId = ProgressionDataUtils.to_string_name(command.target_unit_id);
        if (targetUnitId != "" && state.TryGetUnitTyped(targetUnitId, out BattleUnitState target))
            return target;
        foreach (StringName candidateId in command.TargetUnitIdsTyped ?? Array.Empty<StringName>())
        {
            targetUnitId = ProgressionDataUtils.to_string_name(candidateId);
            if (targetUnitId != "" && state.TryGetUnitTyped(targetUnitId, out target))
                return target;
        }
        return null;
    }

    private static bool ShouldGrantSkillMasteryForCommand(
        BattleCommand command,
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition
    )
    {
        if (!IsEquipmentSkillCommand(command))
        {
            return true;
        }

        StringName skillId = ProgressionDataUtils.to_string_name(
            skillDefinition?.SkillId ?? command?.skill_id ?? new StringName("")
        );
        return UnitHasLearnedActiveSkill(activeUnit, skillId);
    }

    private static bool UnitHasLearnedActiveSkill(BattleUnitState unit, StringName skillId)
    {
        if (unit == null || skillId == "")
        {
            return false;
        }
        if (unit.KnowsActiveSkill(skillId))
        {
            return true;
        }
        return unit.GetKnownSkillLevelTyped(skillId, 0) > 0;
    }

    private IDisposable PushScopedCommandSkillLevel(
        BattleUnitState unitState,
        StringName skillId,
        int skillLevel
    )
    {
        StringName previousUnitId = _scopedCommandSkillUnitId;
        StringName previousSkillId = _scopedCommandSkillId;
        int previousSkillLevel = _scopedCommandSkillLevel;
        _scopedCommandSkillUnitId = ProgressionDataUtils.to_string_name(unitState?.unit_id ?? "");
        _scopedCommandSkillId = ProgressionDataUtils.to_string_name(skillId);
        _scopedCommandSkillLevel = Math.Max(skillLevel, 0);
        return new ScopedCommandSkillLevelScope(
            this,
            previousUnitId,
            previousSkillId,
            previousSkillLevel
        );
    }

    private void RestoreScopedCommandSkillLevel(
        StringName unitId,
        StringName skillId,
        int skillLevel
    )
    {
        _scopedCommandSkillUnitId = ProgressionDataUtils.to_string_name(unitId);
        _scopedCommandSkillId = ProgressionDataUtils.to_string_name(skillId);
        _scopedCommandSkillLevel = Math.Max(skillLevel, 0);
    }

    private sealed class ScopedCommandSkillLevelScope : IDisposable
    {
        private BattleSkillExecutionOrchestrator _owner;
        private readonly StringName _previousUnitId;
        private readonly StringName _previousSkillId;
        private readonly int _previousSkillLevel;

        internal ScopedCommandSkillLevelScope(
            BattleSkillExecutionOrchestrator owner,
            StringName previousUnitId,
            StringName previousSkillId,
            int previousSkillLevel
        )
        {
            _owner = owner;
            _previousUnitId = previousUnitId;
            _previousSkillId = previousSkillId;
            _previousSkillLevel = previousSkillLevel;
        }

        public void Dispose()
        {
            BattleSkillExecutionOrchestrator owner = _owner;
            _owner = null;
            owner?.RestoreScopedCommandSkillLevel(
                _previousUnitId,
                _previousSkillId,
                _previousSkillLevel
            );
        }
    }

    private BattleSkillUseOutcomeSnapshot CaptureSkillUseOutcomeSnapshot(
        BattleCommand command,
        bool allowRepeat
    )
    {
        BattleState state = RtState();
        if (state == null || command == null)
            return BattleSkillUseOutcomeSnapshot.Empty;
        GStringNameArray targetUnitIds = _normalize_target_unit_ids(command, allowRepeat);
        return CaptureSkillUseOutcomeSnapshot(targetUnitIds);
    }

    private BattleSkillUseOutcomeSnapshot CaptureGroundSkillUseOutcomeSnapshot(
        BattleUnitState activeUnit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        BattleCommand command
    )
    {
        BattleState state = RtState();
        if (
            state == null
            || Runtime == null
            || activeUnit == null
            || skillDefinition == null
            || castVariantDefinition == null
            || command == null
        )
        {
            return BattleSkillUseOutcomeSnapshot.Empty;
        }

        BattleGroundSkillValidationResult validation =
            Runtime.ValidateGroundSkillCommandResultTyped(
                activeUnit,
                skillDefinition,
                castVariantDefinition,
                command
            );
        if (!validation.Allowed)
        {
            return BattleSkillUseOutcomeSnapshot.Empty;
        }

        IReadOnlyList<Vector2I> targetCoords = validation.TargetCoords ?? Array.Empty<Vector2I>();
        GroundEffectBarrierClipContext barrierClip = PreviewGroundEffectBarrierClipContext(
            new BattleUnitReadView(activeUnit),
            skillDefinition,
            castVariantDefinition,
            targetCoords
        );
        IReadOnlyList<StringName> targetUnitIds = Runtime.CollectGroundPreviewUnitIdsTyped(
            activeUnit,
            skillDefinition,
            barrierClip.UnitEffectDefinitions,
            barrierClip.UnitEffectCoords
        );
        return CaptureSkillUseOutcomeSnapshot(targetUnitIds);
    }

    private BattleSkillUseOutcomeSnapshot CaptureSkillUseOutcomeSnapshot(
        IEnumerable<StringName> targetUnitIds
    )
    {
        BattleState state = RtState();
        if (state == null)
            return BattleSkillUseOutcomeSnapshot.Empty;
        var snapshots = new Dictionary<StringName, BattleSkillTargetBeforeState>();
        foreach (StringName rawId in targetUnitIds)
        {
            StringName unitId = ProgressionDataUtils.to_string_name(rawId);
            if (unitId == "" || snapshots.ContainsKey(unitId))
                continue;
            if (!state.TryGetUnitTyped(unitId, out BattleUnitState targetUnit) || targetUnit == null)
                continue;
            snapshots[unitId] = new BattleSkillTargetBeforeState(
                targetUnit.GetCurrentHp(),
                targetUnit.IsAlive(),
                targetUnit.GetAnchorCoord()
            );
        }
        return snapshots.Count == 0
            ? BattleSkillUseOutcomeSnapshot.Empty
            : new BattleSkillUseOutcomeSnapshot(snapshots);
    }

    private BattleEquipmentSkillUseOutcome BuildEquipmentSkillUseOutcome(
        BattleSkillUseOutcomeSnapshot snapshot
    )
    {
        if (snapshot == null || snapshot.Targets.Count == 0)
            return BattleEquipmentSkillUseOutcome.Empty;
        BattleState state = RtState();
        if (state == null)
            return BattleEquipmentSkillUseOutcome.Empty;

        var targetUnitIds = new List<StringName>();
        int damagedTargetCount = 0;
        int killedTargetCount = 0;
        int hpDamageDealt = 0;
        int movedTargetCount = 0;
        int unmovedTargetCount = 0;
        foreach (KeyValuePair<StringName, BattleSkillTargetBeforeState> entry in snapshot.Targets)
        {
            StringName unitId = entry.Key;
            if (unitId == "")
                continue;
            targetUnitIds.Add(unitId);
            if (!state.TryGetUnitTyped(unitId, out BattleUnitState targetUnit) || targetUnit == null)
                continue;
            int hpDamage = Math.Max(entry.Value.HpBefore - targetUnit.GetCurrentHp(), 0);
            if (hpDamage > 0)
            {
                damagedTargetCount++;
                hpDamageDealt += hpDamage;
            }
            if (targetUnit.GetAnchorCoord() != entry.Value.CoordBefore)
                movedTargetCount++;
            else
                unmovedTargetCount++;
            if (entry.Value.WasAlive && !targetUnit.IsAlive())
                killedTargetCount++;
        }

        return new BattleEquipmentSkillUseOutcome
        {
            TargetUnitIds = targetUnitIds,
            DamagedTargetCount = damagedTargetCount,
            KilledTargetCount = killedTargetCount,
            HpDamageDealt = hpDamageDealt,
            MovedTargetCount = movedTargetCount,
            UnmovedTargetCount = unmovedTargetCount,
        };
    }

    internal static BattleKillProvenance BuildWeaponAttackKillProvenance(
        BattleUnitState sourceUnit,
        AttackEffectResolutionResult result,
        StringName sourceActionId
    )
    {
        return BattleKillProvenance.FromWeaponAttackResult(
            sourceUnit,
            result,
            sourceActionId
        );
    }

    private static bool ResultIncludesWeaponDamage(AttackEffectResolutionResult result)
    {
        foreach (DamageEventResult damageEvent in result.DamageEvents ?? Array.Empty<DamageEventResult>())
        {
            if (
                damageEvent.AddWeaponDice
                && damageEvent.WeaponDamageDice.Count > 0
                && damageEvent.WeaponDamageDice.Sides > 0
            )
            {
                return true;
            }
        }
        return false;
    }

    private sealed class BattleSkillUseOutcomeSnapshot
    {
        internal static readonly BattleSkillUseOutcomeSnapshot Empty = new(
            new Dictionary<StringName, BattleSkillTargetBeforeState>()
        );

        internal BattleSkillUseOutcomeSnapshot(
            IReadOnlyDictionary<StringName, BattleSkillTargetBeforeState> targets
        )
        {
            Targets = targets ?? new Dictionary<StringName, BattleSkillTargetBeforeState>();
        }

        internal IReadOnlyDictionary<StringName, BattleSkillTargetBeforeState> Targets { get; }
    }

    private readonly struct BattleSkillTargetBeforeState
    {
        internal BattleSkillTargetBeforeState(int hpBefore, bool wasAlive, Vector2I coordBefore)
        {
            HpBefore = Math.Max(hpBefore, 0);
            WasAlive = wasAlive;
            CoordBefore = coordBefore;
        }

        internal int HpBefore { get; }
        internal bool WasAlive { get; }
        internal Vector2I CoordBefore { get; }
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
        if (pending_cast.IsWindup)
        {
            var validationCommand = new BattleCommand
            {
                CommandKind = BattleCommandKind.Skill,
                unit_id = active_unit.unit_id,
                skill_id = pending_cast.SkillId,
                skill_variant_id = pending_cast.VariantId,
                windup_tier = pending_cast.WindupSnapshot?.Tier ?? 0,
            };
            validationCommand.SetTargetUnitIds(pending_cast.TargetUnitIds);
            if (pending_cast.TargetUnitIds.Count > 0)
            {
                validationCommand.target_unit_id = pending_cast.TargetUnitIds[0];
            }
            BattleUnitSkillValidationResult completionValidation =
                _validate_unit_skill_targets_result(
                    active_unit,
                    validationCommand,
                    skillDefinition,
                    null,
                    requireAp: false
                );
            if (!completionValidation.Allowed)
            {
                batch?.AddLogLine(
                    $"{active_unit.display_name} 的 {skillDefinition?.DisplayName ?? pending_cast.SkillId.ToString()} 蓄力完成，但目标已不在有效攻击范围或视线内，攻击落空。"
                );
                return false;
            }
        }
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
            if (pending_cast.WindupSnapshot is BattleWindupSnapshot windupSnapshot)
            {
                effectDefinitions = new List<CombatEffectDefinition>(
                    BattleWindupRules.ApplyWeaponDiceMultiplier(
                        effectDefinitions,
                        windupSnapshot.WeaponDiceMultiplier
                    )
                );
            }
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
            if (!state.TryGetUnitTyped(targetUnitId, out BattleUnitState targetUnit) || !targetUnit.IsAlive())
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
                        batch,
                        castVariantDefinition
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
        GroundEffectBarrierClipContext barrierClip = ResolveGroundEffectBarrierClipContext(
            activeUnit,
            skillDefinition,
            castVariantDefinition,
            targetCoords,
            batch
        );
        BattleGroundUnitEffectsResult unitResult = Runtime.ApplyGroundUnitEffectsResultTyped(
            activeUnit,
            skillDefinition,
            castVariantDefinition,
            barrierClip.UnitEffectDefinitions,
            barrierClip.UnitEffectCoords,
            batch,
            targetCoords,
            barrierClip.VisibleEffectCoords
        );
        BattleGroundTerrainEffectsResult terrainResult =
            Runtime.ApplyGroundTerrainEffectsResultTyped(
                activeUnit,
                skillDefinition,
                barrierClip.TerrainEffectDefinitions,
                barrierClip.TerrainEffectCoords,
                batch
            );
        bool applied =
            barrierClip.BarrierApplied || unitResult.Applied || terrainResult.Applied;
        if (applied)
        {
            batch?.AddLogLine(
                $"{activeUnit.display_name} 使用 {_format_skill_variant_label(skillDefinition, castVariantDefinition)}，影响了 {barrierClip.VisibleEffectCoords.Count} 个地格、{unitResult.AffectedUnitCount} 个单位。"
            );
        }
        return applied;
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

        int mpBeforeCost = active_unit?.GetCurrentMp() ?? 0;
        if (!_consume_skill_costs(active_unit, skillDefinition, castVariantDefinition, batch))
        {
            return false;
        }
        _record_skill_attempt(active_unit, command?.skill_id ?? new StringName(""));
        int spentMp = Math.Max(mpBeforeCost - (active_unit?.GetCurrentMp() ?? 0), 0);
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
        CombatEffectDefinition sourceRetreatEffect =
            BattleSourceRetreatRules.FindEffect(resolvedEffectDefinitions);
        if (
            !CanApplyUnitSkillOrRepeatResultFromDefinitions(resolvedEffectDefinitions)
        )
        {
            return false;
        }
        if (sourceRetreatEffect != null && isRandomChain)
        {
            return false;
        }

        Vector2I sourceRetreatTargetCoord =
            sourceRetreatEffect != null && validation.TargetUnits.Count == 1
                ? validation.TargetUnits[0].GetAnchorCoord()
                : new Vector2I(-1, -1);

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
            return _randomChainSkillService._handle_random_chain_unit_skill_command(
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
                        batch,
                        castVariantDefinition
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
        if (sourceRetreatEffect != null)
        {
            Runtime?._movement_service.ExecuteSourceRetreat(
                active_unit,
                sourceRetreatTargetCoord,
                command.source_retreat_direction,
                sourceRetreatEffect.SourceRetreatDistance,
                batch
            );
            applied = true;
        }
        return applied;
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

        int mpBeforeCost = active_unit?.GetCurrentMp() ?? 0;
        if (!_consume_skill_costs(active_unit, skillDefinition, castVariantDefinition, batch))
        {
            return false;
        }
        int spentMp = Math.Max(mpBeforeCost - (active_unit?.GetCurrentMp() ?? 0), 0);
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
        GroundEffectBarrierClipContext barrierClip = ResolveGroundEffectBarrierClipContext(
            active_unit,
            skillDefinition,
            castVariantDefinition,
            targetCoords,
            batch
        );
        BattleGroundUnitEffectsResult unitResult = Runtime.ApplyGroundUnitEffectsResultTyped(
            active_unit,
            skillDefinition,
            castVariantDefinition,
            barrierClip.UnitEffectDefinitions,
            barrierClip.UnitEffectCoords,
            batch,
            targetCoords,
            barrierClip.VisibleEffectCoords
        );
        BattleGroundTerrainEffectsResult terrainResult =
            Runtime.ApplyGroundTerrainEffectsResultTyped(
                active_unit,
                skillDefinition,
                barrierClip.TerrainEffectDefinitions,
                barrierClip.TerrainEffectCoords,
                batch
            );
        bool applied =
            barrierClip.BarrierApplied || unitResult.Applied || terrainResult.Applied;

        if (applied)
        {
            batch?.AddLogLine(
                $"{active_unit.display_name} 使用 {_format_skill_variant_label(skillDefinition, castVariantDefinition)}，影响了 {barrierClip.VisibleEffectCoords.Count} 个地格、{unitResult.AffectedUnitCount} 个单位。"
            );
        }
        return applied;
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
        BattleEventBatch batch = null,
        bool forceHitAllowCrit = false
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
                skillResolutionRules?.IsForceHitNoCritSkill(skillDefinition, active_unit) == true;
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
                EventBatch = batch,
                ForceHitAllowCrit = forceHitAllowCrit,
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
                        $"{skillDefinition?.DisplayName ?? "技能"}必定命中，且不会触发暴击。",
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
                    .WithBattleState(RtState())
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
                skillDefinition,
                RtState()
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

    internal bool _apply_unit_skill_result(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariantDefinition,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleEventBatch batch,
        BattleSpellControlResult spell_control_context = default,
        bool force_hit_allow_crit = false
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
                    batch,
                    castVariantDefinition
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
        int previousTargetHp = target_unit?.GetCurrentHp() ?? 0;
        UnitSkillEffectResolution effectResolution = _resolve_unit_skill_effect_resolution(
            active_unit,
            target_unit,
            skillDefinition,
            castVariantDefinition,
            effectDefinitions,
            batch,
            force_hit_allow_crit
        );
        AttackEffectResolutionResult damageResult = effectResolution.Result;
        BattleSkillMasteryService skillMasteryService = Runtime?._skill_mastery_service;
        Runtime?._apply_source_bound_weapon_bonus_mastery_grants(
            active_unit,
            target_unit,
            damageResult,
            batch
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
            BattleForcedMoveContext.Empty,
            damageResult.AttackSuccess
        );
        MarkAppliedStatusesForTurnTiming(
            target_unit,
            specialResult.StatusEffectIds
        );
        skillMasteryService?.RecordTargetResult(
            active_unit,
            target_unit,
            skillDefinition,
            damageResult,
            effectDefinitions,
            additionalEffectApplied: shieldResult.Applied || specialResult.Applied
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
                Runtime?._report_formatter.BuildSkillEventEntryPlain(
                    active_unit,
                    target_unit,
                    skillId,
                    BattleReportFormatter.REASON_DOOM_SENTENCE_APPLIED,
                    doomSentenceReportTags
                ) ?? new Dictionary<string, object>(StringComparer.Ordinal)
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
            spell_control_context,
            castVariantDefinition
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
        IReadOnlyList<StringName> terrainEffectIds = damageResult.TerrainEffectIds;
        if (terrainEffectIds.Count != 0)
        {
            BattleGridService gridService = Runtime?.GetGridService();
            foreach (StringName terrainEffectId in terrainEffectIds)
            {
                BattleCellState targetCell = gridService?.GetCellState(
                    RtState(),
                    target_unit.GetAnchorCoord()
                );
                if (
                    targetCell != null
                    && !targetCell.terrain_effect_ids.Contains(terrainEffectId)
                )
                {
                    targetCell.terrain_effect_ids.Add(terrainEffectId);
                    _append_changed_coord(
                        batch,
                        target_unit.GetAnchorCoord()
                    );
                    batch?.AddLogLine(
                        $"{skillSubject} 使 {target_unit.display_name} 所在的地格附加效果 {terrainEffectId}。"
                    );
                }
            }
        }
        int heightDelta = damageResult.HeightDelta;
        Vector2I targetCoord = target_unit.GetAnchorCoord();
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
        if (target_unit?.IsAlive() != true)
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
                new BattleDefeatHandlingOptions(
                    recordEnemyDefeatedAchievement: true,
                    killProvenance: BuildWeaponAttackKillProvenance(
                        active_unit,
                        damageResult,
                        skillId
                    )
                )
            );
        }
        if (active_unit != null && target_unit != null)
        {
            bool causedDefeat = !target_unit.IsAlive();
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
            IReadOnlyList<StringName> removedIds = dispelEvent.RemovedStatusIds;
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
        BattleGridService gridService = Runtime?.GetGridService();
        foreach (
            Vector2I occupiedCoord in unit_state.GetOccupiedCoordsReadViewTyped()
        )
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
